namespace VietAnExpress.Identity.Domain;

/// <summary>Trạng thái sự cố — khớp frontend (<c>features/troubles/types.ts</c>).</summary>
internal static class TroubleStatus
{
    public const string New = "new";
    public const string Doing = "doing";
    /// <summary>Chờ khách phản hồi.</summary>
    public const string WaitCustomer = "waitc";
    public const string Done = "done";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { New, Doing, WaitCustomer, Done };

    public static string Label(string status) => status switch
    {
        New => "Mới",
        Doing => "Đang xử lý",
        WaitCustomer => "Chờ khách phản hồi",
        Done => "Đã xử lý",
        _ => status
    };

    public const int MaxLength = 10;
}

/// <summary>Mức độ — khớp frontend.</summary>
internal static class TroublePriority
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { "low", "mid", "high" };
    public const int MaxLength = 10;
}

/// <summary>Sự cố khách báo cho 1 vận đơn (dbo.SuCo) — quản trị Việt An xử lý ở trang /admin.</summary>
internal sealed class Trouble
{
    public const int BillMaxLength = 50;
    public const int NameMaxLength = 250;
    public const int CountryMaxLength = 100;
    public const int TypeMaxLength = 100;
    public const int DescriptionMaxLength = 4000;
    public const int RequesterMaxLength = 150;
    public const int ContactMaxLength = 200;

    private Trouble() { } // EF Core

    public static Trouble Create(long customerId, string customerCode, string companyName, long? staffId, string userName,
        string bill, string? consignee, string? country, string type, string priority, string description, string requester,
        string contact, DateTime now) => new()
    {
        CustomerId = customerId,
        CustomerCode = customerCode,
        CompanyName = companyName,
        StaffId = staffId,
        SenderUserName = userName,
        Bill = bill,
        ConsigneeName = consignee,
        Country = country,
        Type = type,
        Priority = priority,
        Description = description,
        Requester = requester,
        Contact = contact,
        Status = TroubleStatus.New,
        CreatedAt = now,
        UpdatedAt = now
    };

    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public string CustomerCode { get; private set; } = "";
    public string CompanyName { get; private set; } = "";
    /// <summary>Tài khoản con báo (null = tài khoản chính).</summary>
    public long? StaffId { get; private set; }
    public string SenderUserName { get; private set; } = "";
    public string Bill { get; private set; } = "";
    public string? ConsigneeName { get; private set; }
    public string? Country { get; private set; }
    public string Type { get; private set; } = "";
    public string Priority { get; private set; } = "mid";
    public string Description { get; private set; } = "";
    public string Requester { get; private set; } = "";
    public string Contact { get; private set; } = "";
    public string Status { get; private set; } = TroubleStatus.New;
    public DateTime CreatedAt { get; private set; }
    /// <summary>Lần trao đổi / đổi trạng thái gần nhất — sắp xếp danh sách.</summary>
    public DateTime UpdatedAt { get; private set; }
    public List<TroubleMessage> Messages { get; private set; } = [];

    /// <summary>Mã hiển thị (vd TRB-1024).</summary>
    public string Code => FormatCode(Id);

    public static string FormatCode(long id) => $"TRB-{id}";

    /// <summary>"TRB-1024" hoặc "1024" → 1024.</summary>
    public static long? ParseCode(string? code)
    {
        var v = code?.Trim() ?? "";
        if (v.StartsWith("TRB-", StringComparison.OrdinalIgnoreCase)) v = v[4..];
        return long.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    /// <summary>Khách gửi thêm thông tin: đang chờ khách thì chuyển lại "Đang xử lý"; khách đóng thì "Đã xử lý".</summary>
    public void CustomerReply(string author, string message, bool close, DateTime now)
    {
        var next = close ? TroubleStatus.Done : Status == TroubleStatus.WaitCustomer ? TroubleStatus.Doing : Status;
        Messages.Add(new TroubleMessage(false, author, message, next != Status ? next : null, now));
        Status = next;
        UpdatedAt = now;
    }

    /// <summary>Việt An trả lời và / hoặc đổi trạng thái. Trả về true nếu có thay đổi.</summary>
    public bool VietAnReply(string author, string? message, string? status, DateTime now)
    {
        var next = status ?? Status;
        var changed = next != Status;
        if (string.IsNullOrWhiteSpace(message) && !changed) return false;
        Messages.Add(new TroubleMessage(true, author, message?.Trim() ?? "", changed ? next : null, now));
        Status = next;
        UpdatedAt = now;
        return true;
    }
}

/// <summary>1 lượt trao đổi của sự cố (dbo.SuCo_TraoDoi): khách hoặc Việt An, kèm trạng thái mới nếu có đổi.</summary>
internal sealed class TroubleMessage
{
    public const int MessageMaxLength = 4000;

    private TroubleMessage() { } // EF Core

    public TroubleMessage(bool fromVietAn, string author, string message, string? newStatus, DateTime now)
    {
        FromVietAn = fromVietAn;
        Author = author;
        Message = message;
        NewStatus = newStatus;
        CreatedAt = now;
    }

    public long Id { get; private set; }
    public long TroubleId { get; private set; }
    public bool FromVietAn { get; private set; }
    public string Author { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string? NewStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
