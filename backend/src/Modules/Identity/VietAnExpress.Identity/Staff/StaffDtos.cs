
namespace VietAnExpress.Identity.Staff;

/// <summary>Tài khoản con của nhân viên — khớp <c>StaffAccount</c> của frontend (web/src/features/staff/types.ts).</summary>
internal sealed record StaffAccountDto(
    long Id,
    string UserName,
    string FullName,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Permissions,
    bool Active,
    DateTime CreatedAt,
    string? Phone2,
    string? Email2);

/// <summary>1 quyền admin có thể cấp cho tài khoản con.</summary>
internal sealed record AssignablePermissionDto(string Code, string Description);
