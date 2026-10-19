using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application.Dtos;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application;

internal static class IdentityErrors
{
    public static readonly Error NotLoggedIn = Error.Unauthorized("NOT_LOGGED_IN", "Vui lòng đăng nhập");
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("INVALID_CREDENTIALS", "Sai tên đăng nhập hoặc mật khẩu");
    public static readonly Error SessionExpired =
        Error.Unauthorized("SESSION_EXPIRED", "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
    public static readonly Error WrongCurrentPassword =
        Error.Validation("WRONG_CURRENT_PASSWORD", "Mật khẩu hiện tại không đúng");
    public static readonly Error StaffDisabled =
        Error.Unauthorized("ACCOUNT_DISABLED", "Tài khoản đã bị khóa, vui lòng liên hệ quản trị viên của công ty");
    public static readonly Error AdminOnly =
        Error.Forbidden("ADMIN_ONLY", "Chỉ tài khoản chính (quản trị) của công ty được thực hiện thao tác này");
    public static readonly Error TooManyStaff =
        Error.BusinessRule("TOO_MANY_STAFF", "Đã đạt số tài khoản nhân viên tối đa (100)");
    public static readonly Error StaffPasswordManagedByAdmin =
        Error.Forbidden("STAFF_PASSWORD_ADMIN_ONLY", "Tài khoản nhân viên không tự đổi mật khẩu được — vui lòng liên hệ quản trị viên tài khoản công ty để đặt lại");
    public static readonly Error StaffNotFound =
        Error.NotFound("STAFF_NOT_FOUND", "Không tìm thấy tài khoản nhân viên");
    public static readonly Error UserNameTaken =
        Error.Conflict("USERNAME_TAKEN", "Tên đăng nhập đã được sử dụng, vui lòng chọn tên khác");
    public static readonly Error UnknownPermission =
        Error.Validation("UNKNOWN_PERMISSION", "Có quyền không hợp lệ hoặc không được cấp cho nhân viên");
    public static readonly Error SlugTaken =
        Error.Conflict("SLUG_TAKEN", "Đường dẫn MyTracking đã có người dùng, vui lòng chọn đường dẫn khác");
    public static readonly Error MyTrackingChanged =
        Error.Conflict("MYTRACKING_CHANGED", "Trang MyTracking vừa được lưu ở nơi khác — vui lòng tải lại trang rồi lưu lại");
    public static readonly Error InvalidTrackingConfig =
        Error.Validation("INVALID_MYTRACKING_CONFIG", "Cấu hình MyTracking không hợp lệ hoặc quá lớn (tối đa 2 MB)");
    public static readonly Error MyTrackingNotFound =
        Error.NotFound("MYTRACKING_NOT_FOUND", "Trang tra cứu không tồn tại hoặc chưa được xuất bản");
}

/// <summary>
/// Tạo phiên đăng nhập (access + refresh token) cho tài khoản chính của khách (dbo.TCustomer — admin) hoặc tài khoản con (nhân viên).
/// Quyền khai báo trong code (các module, vai trò "customer") — admin nhận tất cả; tài khoản con chỉ nhận các quyền admin đã chọn
/// (trong số <see cref="AssignablePermissions"/>).
/// </summary>
internal sealed class SessionService(ITokenService tokens, ICustomersApi customers, IEnumerable<IPermissionProvider> permissionProviders)
{
    private static readonly string[] AdminRoles = [SystemRoles.Customer];
    private static readonly string[] StaffRoles = [SystemRoles.CustomerStaff];

    private readonly IReadOnlyList<PermissionDefinition> _customerPermissions = permissionProviders
        .SelectMany(p => p.GetPermissions())
        .Where(p => p.DefaultRoles.Contains(SystemRoles.Customer))
        .DistinctBy(p => p.Code)
        .OrderBy(p => p.Code, StringComparer.Ordinal)
        .ToList();

    // Tính 1 lần cho mỗi instance (danh sách quyền cố định trong code).
    private IReadOnlyList<string>? _permissions;
    private IReadOnlyList<PermissionDefinition>? _assignable;
    private HashSet<string>? _assignableCodes;

    /// <summary>Quyền của tài khoản chính (admin).</summary>
    public IReadOnlyList<string> Permissions => _permissions ??= _customerPermissions.Select(p => p.Code).ToList();

    /// <summary>Quyền admin chọn được cho tài khoản con — mọi quyền của khách trừ <see cref="IdentityPermissions.AdminOnly"/> và quyền tài khoản con luôn có.</summary>
    public IReadOnlyList<PermissionDefinition> AssignablePermissions =>
        _assignable ??= _customerPermissions
            .Where(p => !IdentityPermissions.AdminOnly.Contains(p.Code) && !IdentityPermissions.StaffAlways.Contains(p.Code)).ToList();

    public bool IsAssignable(string code) =>
        (_assignableCodes ??= AssignablePermissions.Select(p => p.Code).ToHashSet(StringComparer.Ordinal)).Contains(code.Trim());

    /// <summary>Quyền tài khoản chính đã chọn cho tài khoản con (quyền đã lưu ∩ quyền chọn được) — trang quản lý nhân viên hiện và gửi lại đúng danh sách này.</summary>
    public IReadOnlyList<string> GrantedPermissions(StaffAccount staff)
    {
        var granted = staff.PermissionList.ToHashSet(StringComparer.Ordinal);
        return AssignablePermissions.Select(p => p.Code).Where(granted.Contains).ToList();
    }

    /// <summary>Quyền thực tế của tài khoản con khi đăng nhập: quyền đã chọn + quyền luôn có (vd gửi góp ý).</summary>
    public IReadOnlyList<string> StaffPermissions(StaffAccount staff) =>
        GrantedPermissions(staff)
            .Concat(_customerPermissions.Select(p => p.Code).Where(IdentityPermissions.StaffAlways.Contains))
            .ToList();

    /// <summary>Null nếu tài khoản không còn đăng nhập được (hồ sơ khách không còn / chưa có mật khẩu).</summary>
    /// <param name="sessionId">Làm mới phiên → giữ mã phiên cũ; null → phiên mới (đăng nhập).</param>
    public async Task<AuthSession?> StartAsync(CustomerLogin login, bool persistent, CancellationToken ct, string? sessionId = null)
    {
        if (!login.HasPassword) return null;
        var user = await BuildUserAsync(login, ct);
        if (user is null) return null;

        var sid = sessionId ?? JwtTokenService.NewSessionId();
        var access = tokens.CreateAccessToken(login.CustomerId, null, user.UserName, AdminRoles, user.Permissions, sid);
        var refresh = tokens.CreateRefreshToken(login.CustomerId, null, tokens.PasswordStamp(login.CustomerId, login.Password!), persistent, sid);
        return new AuthSession(user, access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, persistent);
    }

    /// <summary>Null nếu tài khoản con đã bị khóa hoặc hồ sơ khách cha không còn.</summary>
    public async Task<AuthSession?> StartAsync(StaffAccount staff, bool persistent, CancellationToken ct, string? sessionId = null)
    {
        var user = await BuildUserAsync(staff, ct);
        if (user is null) return null;

        var sid = sessionId ?? JwtTokenService.NewSessionId();
        var access = tokens.CreateAccessToken(staff.CustomerId, staff.Id, user.UserName, StaffRoles, user.Permissions, sid);
        var refresh = tokens.CreateRefreshToken(staff.CustomerId, staff.Id,
            tokens.PasswordStamp(staff.CustomerId, staff.Password, staff.Id), persistent, sid);
        return new AuthSession(user, access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, persistent);
    }

    public async Task<SessionUserDto?> BuildUserAsync(CustomerLogin login, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(login.CustomerId, ct);
        if (customer is null) return null;

        var userName = string.IsNullOrWhiteSpace(login.UserName) ? customer.Code : login.UserName.Trim();
        return new SessionUserDto(
            login.CustomerId, userName, customer.ContactName ?? customer.CompanyName, AccountTypes.Customer,
            customer.Code, customer.CompanyName, customer.ContactName, customer.Email,
            AvatarUrl: null, DefaultBranch: null, AdminRoles, Permissions,
            Phone: customer.Phone, Address: customer.Address, TaxCode: customer.TaxCode, IsAdmin: true);
    }

    /// <summary>
    /// Tài khoản con: tên công ty, địa chỉ lấy hàng, MST lấy của khách cha (đơn thuộc công ty);
    /// người liên hệ, điện thoại, email là của nhân viên — form tạo đơn điền sẵn người gửi theo đúng người đang tạo đơn.
    /// Nhân viên chưa có điện thoại / email thì để trống (không lấy của công ty), người tạo đơn tự nhập.
    /// </summary>
    public async Task<SessionUserDto?> BuildUserAsync(StaffAccount staff, CancellationToken ct)
    {
        if (!staff.IsActive) return null;
        var customer = await customers.GetByIdAsync(staff.CustomerId, ct);
        if (customer is null) return null;

        return new SessionUserDto(
            staff.CustomerId, staff.UserName, staff.FullName, AccountTypes.Staff,
            customer.Code, customer.CompanyName, staff.FullName, staff.Email,
            AvatarUrl: null, DefaultBranch: null, StaffRoles, StaffPermissions(staff),
            Phone: staff.Phone, Address: customer.Address, TaxCode: customer.TaxCode, IsAdmin: false);
    }
}
