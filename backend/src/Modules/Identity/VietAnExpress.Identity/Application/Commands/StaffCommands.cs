using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Application.Dtos;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

// Tài khoản con của nhân viên — chỉ tài khoản chính (admin) của khách quản lý; controller đã chặn bằng quyền account.staff,
// handler kiểm tra thêm StaffId == null để tài khoản con không bao giờ tự cấp quyền.

internal sealed record ListStaffQuery : IRequest<Result<IReadOnlyList<StaffAccountDto>>>;

internal sealed record ListAssignablePermissionsQuery : IRequest<Result<IReadOnlyList<AssignablePermissionDto>>>;

internal sealed record CreateStaffCommand(
    string UserName, string Password, string FullName, string? Email, string? Phone, IReadOnlyList<string> Permissions,
    string? Phone2 = null, string? Email2 = null)
    : IRequest<Result<StaffAccountDto>>;

internal sealed record UpdateStaffCommand(
    long Id, string FullName, string? Email, string? Phone, IReadOnlyList<string> Permissions, bool Active,
    string? Phone2 = null, string? Email2 = null)
    : IRequest<Result<StaffAccountDto>>;

internal sealed record ResetStaffPasswordCommand(long Id, string NewPassword) : IRequest<Result>;

internal sealed record DeleteStaffCommand(long Id) : IRequest<Result>;

internal sealed class StaffHandlers(IdentityDbContext db, SessionService sessions, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<ListStaffQuery, Result<IReadOnlyList<StaffAccountDto>>>,
    IRequestHandler<ListAssignablePermissionsQuery, Result<IReadOnlyList<AssignablePermissionDto>>>,
    IRequestHandler<CreateStaffCommand, Result<StaffAccountDto>>,
    IRequestHandler<UpdateStaffCommand, Result<StaffAccountDto>>,
    IRequestHandler<ResetStaffPasswordCommand, Result>,
    IRequestHandler<DeleteStaffCommand, Result>
{
    /// <summary>Giới hạn số tài khoản con / khách — tránh tạo tràn lan.</summary>
    public const int MaxPerCustomer = 100;

    private DateTime Now => VietnamTime.Now(clock);

    public async Task<Result<IReadOnlyList<StaffAccountDto>>> Handle(ListStaffQuery q, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var staff = await db.StaffAccounts.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .OrderBy(s => s.UserName)
            .ToListAsync(ct);
        return staff.Select(ToDto).ToList();
    }

    public Task<Result<IReadOnlyList<AssignablePermissionDto>>> Handle(ListAssignablePermissionsQuery q, CancellationToken ct)
    {
        Result<IReadOnlyList<AssignablePermissionDto>> result = user.MainAccountCustomerId() is null
            ? IdentityErrors.AdminOnly
            : sessions.AssignablePermissions.Select(p => new AssignablePermissionDto(p.Code, p.Description)).ToList();
        return Task.FromResult(result);
    }

    public async Task<Result<StaffAccountDto>> Handle(CreateStaffCommand cmd, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var permissions = WithoutStaffAlways(cmd.Permissions);
        if (!PermissionsAllowed(permissions)) return IdentityErrors.UnknownPermission;

        var userName = cmd.UserName.Trim();
        // Bảng cũ không có index duy nhất cho tên đăng nhập: kiểm tra + thêm trong cùng 1 khóa để 2 người tạo cùng lúc
        // (cùng tên, hoặc cùng lúc vượt giới hạn 100 tài khoản) phải chờ nhau thay vì cùng lọt qua.
        return await db.WithAppLockAsync<Result<StaffAccountDto>>("portal:staff-account", async () =>
        {
            if (await UserNameTakenAsync(userName, ct)) return IdentityErrors.UserNameTaken;
            if (await db.StaffAccounts.CountAsync(s => s.CustomerId == customerId, ct) >= MaxPerCustomer)
                return IdentityErrors.TooManyStaff;

            // Mật khẩu lưu như hệ thống cũ (cột Mo_Ta dùng chung) — giống dbo.TCustomer.Login_Password.
            var staff = new StaffAccount(customerId, userName, cmd.Password, cmd.FullName, cmd.Email, cmd.Phone, permissions, Now);
            staff.SetSecondContact(cmd.Phone2, cmd.Email2);
            db.StaffAccounts.Add(staff);
            await db.SaveChangesAsync(ct);
            return ToDto(staff);
        }, ct);
    }

    public async Task<Result<StaffAccountDto>> Handle(UpdateStaffCommand cmd, CancellationToken ct)
    {
        var found = await FindAsync(cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        var permissions = WithoutStaffAlways(cmd.Permissions);
        if (!PermissionsAllowed(permissions)) return IdentityErrors.UnknownPermission;
        var staff = found.Value;

        // Khóa tài khoản: phiên đang mở hết hiệu lực khi access token hết hạn (refresh bị từ chối).
        staff.Update(cmd.FullName, cmd.Email, cmd.Phone, permissions, cmd.Active);
        staff.SetSecondContact(cmd.Phone2, cmd.Email2);
        await db.SaveChangesAsync(ct);
        return ToDto(staff);
    }

    public async Task<Result> Handle(ResetStaffPasswordCommand cmd, CancellationToken ct)
    {
        var found = await FindAsync(cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        var staff = found.Value;
        // Đổi mật khẩu → dấu mật khẩu trong refresh token đổi → nhân viên phải đăng nhập lại.
        staff.SetPassword(cmd.NewPassword);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(DeleteStaffCommand cmd, CancellationToken ct)
    {
        var found = await FindAsync(cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        // Giữ dòng người liên hệ (hệ thống cũ còn dùng), chỉ gỡ đăng nhập — phiên đang mở hết hạn khi làm mới.
        found.Value.RemoveLogin();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Tài khoản con của khách đang đăng nhập: chỉ tài khoản chính được thao tác (AdminOnly), không thấy thì StaffNotFound.</summary>
    private async Task<Result<StaffAccount>> FindAsync(long id, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var staff = await db.StaffAccounts.FirstOrDefaultAsync(s => s.Id == id && s.CustomerId == customerId, ct);
        return staff is null ? IdentityErrors.StaffNotFound : staff;
    }

    /// <summary>Tên đăng nhập không được trùng tài khoản con khác hay tài khoản chính của bất kỳ khách nào.</summary>
    private async Task<bool> UserNameTakenAsync(string userName, CancellationToken ct) =>
        await db.StaffAccounts.AnyAsync(s => s.UserName == userName, ct)
        || await db.Logins.AnyAsync(l => l.UserName == userName, ct);

    private bool PermissionsAllowed(IEnumerable<string> permissions) => permissions.All(sessions.IsAssignable);

    /// <summary>Quyền luôn có (vd hỗ trợ & sự cố) không cần lưu — bỏ qua nếu client gửi kèm thay vì báo quyền không hợp lệ.</summary>
    private static List<string> WithoutStaffAlways(IEnumerable<string> permissions) =>
        permissions.Where(p => !IdentityPermissions.StaffAlways.Contains(p.Trim())).ToList();

    private StaffAccountDto ToDto(StaffAccount s) => new(
        s.Id, s.UserName, s.FullName, s.Email, s.Phone, sessions.GrantedPermissions(s), s.IsActive, s.CreateDate, s.Phone2, s.Email2);
}
