using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;

namespace VietAnExpress.Identity.Auth;

/// <summary>
/// Tạo phiên đăng nhập (access + refresh token) cho tài khoản chính của khách (dbo.TCustomer — admin) hoặc tài khoản con (nhân viên).
/// Quyền khai báo trong code (các module, vai trò "customer") — admin nhận tất cả; tài khoản con nhận mọi quyền
/// trừ <see cref="IdentityPermissions.AdminOnly"/> (không phân quyền riêng từng người).
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
    private IReadOnlyList<string>? _staffPermissions;

    /// <summary>Quyền của tài khoản chính (admin).</summary>
    public IReadOnlyList<string> Permissions => _permissions ??= _customerPermissions.Select(p => p.Code).ToList();

    /// <summary>Quyền của tài khoản chính: mọi quyền của khách, trừ MyTracking khi CMS chưa cho phép (TCustomer.ViewLinkTracking ≠ 1).</summary>
    public IReadOnlyList<string> MainPermissions(CustomerLogin login) =>
        login.MyTrackingAllowed ? Permissions : Permissions.Where(p => p != IdentityPermissions.MyTracking).ToList();

    /// <summary>Quyền của tài khoản con: mọi quyền của khách trừ quyền chỉ tài khoản chính có.</summary>
    public IReadOnlyList<string> StaffPermissions =>
        _staffPermissions ??= Permissions.Where(p => !IdentityPermissions.AdminOnly.Contains(p)).ToList();

    /// <summary>Null nếu tài khoản không còn đăng nhập được (hồ sơ khách không còn / chưa có mật khẩu).</summary>
    /// <param name="authTime">Gia hạn phiên: lần nhập mật khẩu gốc (giữ giới hạn Jwt:MaxSessionDays); null = vừa nhập mật khẩu.</param>
    public async Task<AuthSession?> StartAsync(CustomerLogin login, bool persistent, CancellationToken ct, DateTimeOffset? authTime = null)
    {
        if (!login.HasPassword) return null;
        var user = await BuildUserAsync(login, ct);
        if (user is null) return null;

        var access = tokens.CreateAccessToken(login.CustomerId, null, user.UserName, AdminRoles, user.Permissions);
        var refresh = tokens.CreateRefreshToken(login.CustomerId, null, tokens.PasswordStamp(login.CustomerId, login.Password!), persistent, authTime);
        return new AuthSession(user, access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, persistent);
    }

    /// <summary>Null nếu tài khoản con đã bị khóa hoặc hồ sơ khách cha không còn.</summary>
    public async Task<AuthSession?> StartAsync(StaffAccount staff, bool persistent, CancellationToken ct, DateTimeOffset? authTime = null)
    {
        var user = await BuildUserAsync(staff, ct);
        if (user is null) return null;

        var access = tokens.CreateAccessToken(staff.CustomerId, staff.Id, user.UserName, StaffRoles, user.Permissions);
        var refresh = tokens.CreateRefreshToken(staff.CustomerId, staff.Id,
            tokens.PasswordStamp(staff.CustomerId, staff.Password, staff.Id), persistent, authTime);
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
            AvatarUrl: null, DefaultBranch: null, AdminRoles, MainPermissions(login),
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
            AvatarUrl: null, DefaultBranch: null, StaffRoles, StaffPermissions,
            Phone: staff.Phone, Address: customer.Address, TaxCode: customer.TaxCode, IsAdmin: false);
    }
}
