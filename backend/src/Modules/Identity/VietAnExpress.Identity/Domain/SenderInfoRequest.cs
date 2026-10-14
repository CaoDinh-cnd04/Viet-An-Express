namespace VietAnExpress.Identity.Domain;

/// <summary>Trạng thái yêu cầu sửa thông tin người gửi — khớp frontend (<c>features/sender-info/types.ts</c>).</summary>
internal static class SenderInfoRequestStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    /// <summary>Khách tự hủy khi còn chờ duyệt.</summary>
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Pending, Approved, Rejected, Cancelled };

    public const int MaxLength = 10;
}

/// <summary>Bộ thông tin người gửi (hồ sơ công ty ở dbo.TCustomer) — trước và sau khi sửa.</summary>
internal sealed record SenderInfo(string CompanyName, string? ContactName, string? Phone, string? Email, string? Address, string? TaxCode)
{
    public const int CompanyNameMaxLength = 250;
    public const int ContactNameMaxLength = 150;
    public const int PhoneMaxLength = 50;
    public const int EmailMaxLength = 150;
    public const int AddressMaxLength = 250;
    public const int TaxCodeMaxLength = 20;
}

/// <summary>
/// Yêu cầu sửa thông tin người gửi của khách (dbo.YeuCauSuaNguoiGui) — quản trị Việt An duyệt ở /admin;
/// duyệt xong mới ghi vào dbo.TCustomer. Lưu cả giá trị cũ để admin so sánh và tra lại lịch sử.
/// </summary>
internal sealed class SenderInfoRequest
{
    public const int NoteMaxLength = 500;
    public const int ReasonMaxLength = 500;

    private SenderInfoRequest() { } // EF Core

    public static SenderInfoRequest Create(long customerId, string customerCode, string userName, SenderInfo current, SenderInfo proposed,
        string? note, DateTime now) => new()
    {
        CustomerId = customerId,
        CustomerCode = customerCode,
        SenderUserName = userName,
        OldCompanyName = current.CompanyName,
        OldContactName = current.ContactName,
        OldPhone = current.Phone,
        OldEmail = current.Email,
        OldAddress = current.Address,
        OldTaxCode = current.TaxCode,
        NewCompanyName = proposed.CompanyName,
        NewContactName = proposed.ContactName,
        NewPhone = proposed.Phone,
        NewEmail = proposed.Email,
        NewAddress = proposed.Address,
        NewTaxCode = proposed.TaxCode,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        Status = SenderInfoRequestStatus.Pending,
        CreatedAt = now
    };

    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public string CustomerCode { get; private set; } = "";
    public string SenderUserName { get; private set; } = "";

    public string OldCompanyName { get; private set; } = "";
    public string? OldContactName { get; private set; }
    public string? OldPhone { get; private set; }
    public string? OldEmail { get; private set; }
    public string? OldAddress { get; private set; }
    public string? OldTaxCode { get; private set; }

    public string NewCompanyName { get; private set; } = "";
    public string? NewContactName { get; private set; }
    public string? NewPhone { get; private set; }
    public string? NewEmail { get; private set; }
    public string? NewAddress { get; private set; }
    public string? NewTaxCode { get; private set; }

    /// <summary>Ghi chú của khách cho admin (vd lý do đổi).</summary>
    public string? Note { get; private set; }
    public string Status { get; private set; } = SenderInfoRequestStatus.Pending;
    public string? RejectReason { get; private set; }
    public string? ReviewedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    /// <summary>Lúc duyệt / từ chối / khách hủy.</summary>
    public DateTime? ReviewedAt { get; private set; }

    public SenderInfo Old => new(OldCompanyName, OldContactName, OldPhone, OldEmail, OldAddress, OldTaxCode);
    public SenderInfo New => new(NewCompanyName, NewContactName, NewPhone, NewEmail, NewAddress, NewTaxCode);
    public bool IsPending => Status == SenderInfoRequestStatus.Pending;

    public void Approve(string reviewer, DateTime now) => Close(SenderInfoRequestStatus.Approved, reviewer, null, now);

    public void Reject(string reviewer, string reason, DateTime now) => Close(SenderInfoRequestStatus.Rejected, reviewer, reason.Trim(), now);

    public void Cancel(DateTime now) => Close(SenderInfoRequestStatus.Cancelled, null, null, now);

    private void Close(string status, string? reviewer, string? reason, DateTime now)
    {
        if (!IsPending) throw new InvalidOperationException($"Yêu cầu {Id} đã đóng ({Status}).");
        Status = status;
        ReviewedBy = reviewer;
        RejectReason = reason;
        ReviewedAt = now;
    }
}
