using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.OrderData;

namespace VietAnExpress.Shipments.Orders;

internal static class OrderErrors
{
    public static Error NotFound(string bill) => Error.NotFound("ORDER_NOT_FOUND", $"Không tìm thấy đơn {bill}");
    public static readonly Error DraftNotFound = Error.NotFound("DRAFT_NOT_FOUND", "Không tìm thấy đơn nháp");
    public static readonly Error DraftChanged = Error.Conflict("DRAFT_CHANGED",
        "Đơn này vừa được in hoặc xóa ở nơi khác — vui lòng tải lại danh sách");
    public static readonly Error DraftNotReady =
        Error.BusinessRule("DRAFT_NOT_READY", "Đơn chưa khai đủ thông tin — mở đơn, hoàn tất rồi bấm In");
    public static readonly Error TermsNotAccepted =
        Error.BusinessRule("TERMS_NOT_ACCEPTED", "Vui lòng đọc và đồng ý Điều khoản dịch vụ trước khi in bill");
    public static readonly Error InvalidPayload =
        Error.BusinessRule("DRAFT_INVALID_PAYLOAD", "Dữ liệu đơn nháp không đọc được — vui lòng mở đơn và lưu lại");
    public static readonly Error CustomerNotFound = Error.BusinessRule("CUSTOMER_NOT_FOUND",
        "Không tìm thấy hồ sơ khách hàng — vui lòng liên hệ nhân viên kinh doanh Việt An");
    public static readonly Error CustomerRequired = Error.Forbidden("ORDER_CUSTOMER_REQUIRED",
        "Vui lòng đăng nhập bằng tài khoản khách hàng để tạo đơn");
}

/// <summary>
/// Phạm vi đơn trong dbo.MaVanDon: <paramref name="CustomerId"/> của khách; <paramref name="StaffId"/> có giá trị thì
/// chỉ lấy đơn tài khoản con đó tạo (MaVanDon.Customer_Staff_ID).
/// Nhân viên Việt An xem đơn khách mình phụ trách: <paramref name="CustomerIds"/> = các khách đó, <paramref name="AllCustomers"/> = mọi khách.
/// </summary>
internal sealed record OrderScope(long CustomerId, long? StaffId, IReadOnlyList<long>? CustomerIds = null, bool AllCustomers = false)
{
    /// <summary>Đơn của nhiều khách (trang "Đơn khách phụ trách") — dòng cần kèm mã / tên khách.</summary>
    public bool IsManaged => AllCustomers || CustomerIds is not null;
}

/// <summary>
/// Phạm vi đơn: khách chỉ thấy / ghi đơn có CustomerID = mã khách của mình (dbo.TCustomer.CustomerID).
/// Tài khoản con chỉ thấy đơn và nháp mình tạo; tài khoản chính thấy hết.
/// </summary>
internal sealed class OrderAccess(ICurrentUser user, ICustomersApi customers)
{
    /// <summary>Chưa đăng nhập bằng tài khoản khách → không thấy đơn nào (Id không tồn tại).</summary>
    private const long NoCustomer = -1;

    public bool IsCustomer => user.CustomerId is not null;

    /// <summary>Tài khoản con đang đăng nhập (ghi vào MaVanDon.Customer_Staff_ID khi tạo đơn); null = tài khoản chính.</summary>
    public long? CreatorStaffId => user.StaffId;

    public Task<OrderScope> ScopeAsync(CancellationToken ct) =>
        Task.FromResult(new OrderScope(user.CustomerId ?? NoCustomer, user.RestrictedStaffId()));

    /// <param name="managed">true = đơn mọi khách nhân viên Việt An đang đăng nhập phụ trách (không phụ thuộc khách đang chọn).</param>
    public Task<OrderScope> ScopeAsync(bool managed, CancellationToken ct) => managed ? ManagedScopeAsync(ct) : ScopeAsync(ct);

    /// <summary>
    /// Phạm vi như CMS / trang chọn khách: vai trò Administrator = mọi khách, còn lại khách có TCustomer.Staff_ID = USERS.ID.
    /// Không phải nhân viên Việt An / nhân viên đã khóa → không thấy đơn nào.
    /// </summary>
    private async Task<OrderScope> ManagedScopeAsync(CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId || await customers.GetVietAnUserAsync(agentId, ct) is not { } agent)
            return new OrderScope(NoCustomer, null);
        return agent.ViewAllCustomers
            ? new OrderScope(NoCustomer, null, AllCustomers: true)
            : new OrderScope(NoCustomer, null, await customers.GetCustomerIdsOfManagerAsync(agent.Id, ct));
    }

    /// <summary>
    /// Gắn mã / tên khách vào dòng đơn khi xem đơn của nhiều khách (<see cref="OrderScope.IsManaged"/>); phạm vi 1 khách thì giữ nguyên.
    /// Khách không còn hồ sơ → tên ghi trên đơn (MaVanDon.CustomerName).
    /// </summary>
    public async Task<IReadOnlyList<OrderDto>> WithCustomersAsync(
        OrderScope scope, IReadOnlyList<(LegacyOrder Order, OrderDto Dto)> rows, CancellationToken ct)
    {
        if (!scope.IsManaged || rows.Count == 0) return [.. rows.Select(r => r.Dto)];
        var ids = rows.Select(r => r.Order.CustomerId).OfType<long>().Distinct().ToList();
        var found = (await customers.GetByIdsAsync(ids, ct)).ToDictionary(c => c.Id);
        var agents = await customers.GetVietAnUserNamesAsync([.. rows.Select(r => r.Order.CreateUser ?? 0).Where(id => id > 0).Distinct()], ct)
            ?? new Dictionary<int, string>();
        var subs = await customers.GetContactLoginNamesAsync([.. rows.Select(r => r.Order.CustomerStaffId ?? 0).Where(id => id > 0).Distinct()], ct)
            ?? new Dictionary<long, string>();
        return [.. rows.Select(r => r.Dto with
        {
            Customer = CustomerOf(r.Order, found) is { } c
                ? c with { CreatedBy = CreatorOf(r.Order, agents, subs, found), SenderName = r.Order.SenderName?.Trim() is { Length: > 0 } s ? s : null }
                : null
        })];
    }

    /// <summary>
    /// Tên đăng nhập người tạo đơn (cột "Người tạo" của trang Đơn khách phụ trách). Ưu tiên: nhân viên Việt An tạo giúp (CreateUser &gt; 0) →
    /// tài khoản con (Customer_Staff_ID &gt; 0) → tài khoản chính của khách. Không tra được → null. Hàm thuần — có test.
    /// </summary>
    internal static string? CreatorOf(LegacyOrder o, IReadOnlyDictionary<int, string> agents, IReadOnlyDictionary<long, string> subs,
        IReadOnlyDictionary<long, CustomerSummary> customersById)
    {
        if (o.CreateUser is > 0) return agents.GetValueOrDefault(o.CreateUser.Value);
        if (o.CustomerStaffId is > 0) return subs.GetValueOrDefault(o.CustomerStaffId.Value);
        return o.CustomerId is { } id && customersById.GetValueOrDefault(id)?.LoginUserName is { Length: > 0 } login ? login.Trim() : null;
    }

    /// <summary>Bảng kê nhiều khách: CustomerID → "mã — tên công ty". Phạm vi 1 khách → null (bảng kê không có cột khách).</summary>
    public async Task<IReadOnlyDictionary<long, string>?> CustomerLabelsAsync(OrderScope scope, IEnumerable<LegacyOrder> orders, CancellationToken ct)
    {
        if (!scope.IsManaged) return null;
        var ids = orders.Select(o => o.CustomerId).OfType<long>().Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, string>();
        return (await customers.GetByIdsAsync(ids, ct)).ToDictionary(c => c.Id, c => $"{c.Code} — {c.CompanyName}");
    }

    private static OrderCustomerDto? CustomerOf(LegacyOrder o, IReadOnlyDictionary<long, CustomerSummary> found)
    {
        if (o.CustomerId is not { } id) return null;
        var c = found.GetValueOrDefault(id);
        return new OrderCustomerDto(id.ToString(System.Globalization.CultureInfo.InvariantCulture), c?.Code ?? "", c?.CompanyName ?? o.CustomerName?.Trim() ?? "");
    }

    /// <summary>Số khách tối đa khớp 1 từ khóa "kh".</summary>
    private const int CustomerMatchLimit = 200;

    /// <summary>
    /// Thẻ tìm "kh" / "Tất cả" (trang đơn khách phụ trách): từ khóa → CustomerID khớp mã / tên / tên hóa đơn / người liên hệ / SĐT / tên đăng nhập
    /// (dbo.TCustomer qua module Customers). Phạm vi 1 khách ("Đơn hàng của tôi") → rỗng (không truy vấn).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<long>>> CustomerMatchesAsync(OrderScope scope, GetOrdersQuery q, CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<long>>();
        if (!scope.IsManaged) return result;
        foreach (var term in OrderListFilter.SearchTags(q.Q, q.SearchField).Where(t => t.Field is OrderListFilter.CustomerField or "all").Select(t => t.Value).Distinct())
            result[term] = [.. (await customers.SearchCustomersAsync(term, null, CustomerMatchLimit, ct)).Select(c => c.Id)];
        return result;
    }

    public IQueryable<LegacyOrder> Apply(IQueryable<LegacyOrder> query, OrderScope scope)
    {
        if (scope.AllCustomers) return query.Where(o => o.CustomerId != null && o.CustomerId > 0);
        if (scope.CustomerIds is { } ids) return query.Where(o => o.CustomerId != null && ids.Contains(o.CustomerId.Value));
        query = query.Where(o => o.CustomerId == scope.CustomerId);
        return scope.StaffId is { } staffId
            ? query.Where(o => o.CustomerStaffId == staffId)
            : query;
    }

    /// <summary>
    /// Ghi người tạo vào MaVanDon.Customer_Staff_ID (cột hệ thống cũ = người liên hệ / nhân viên của khách,
    /// dbo.TCustomer_NguoiLienLac.ID): tài khoản con → ID của mình, tài khoản chính → 0 như hệ thống cũ.
    /// </summary>
    /// <remarks>
    /// Nhân viên Việt An làm thay khách: thêm MaVanDon.CreateUser / ModifyUser = USERS.ID (khách tự làm = 0 như hệ thống cũ) —
    /// khách thấy nhãn "Việt An tạo giúp".
    /// </remarks>
    public void StampCreator(LegacyOrder order)
    {
        order.CustomerStaffId = CreatorStaffId ?? 0;
        order.CreateUser = order.ModifyUser = VietAnUserId;
    }

    /// <summary>Người sửa / in gần nhất: MaVanDon.ModifyUser = USERS.ID khi nhân viên Việt An làm thay, 0 = khách.</summary>
    public void StampModifier(LegacyOrder order) => order.ModifyUser = VietAnUserId;

    private int VietAnUserId => user.VietAnUserId ?? 0;

    /// <summary>Khách ghi đơn vào dbo.MaVanDon — kèm tên công ty, người liên hệ để điền người gửi.</summary>
    public async Task<Result<LegacyCustomerRef>> WriterAsync(CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return Result.Failure<LegacyCustomerRef>(OrderErrors.CustomerRequired);
        var customer = await customers.GetByIdAsync(customerId, ct);
        if (customer is null) return Result.Failure<LegacyCustomerRef>(OrderErrors.CustomerNotFound);
        return new LegacyCustomerRef(customer.Id, customer.CompanyName, customer.ContactName, customer.Phone, customer.Email, customer.IsNew);
    }

    /// <summary>
    /// Khách mới (nút "Tạo khách mới" của nhân viên Việt An): người gửi của đơn vừa lưu → hồ sơ dbo.TCustomer, để CS hoàn thiện ở CMS.
    /// Khách đã được CS hoàn thiện (hết đánh dấu khách mới) thì module Customers bỏ qua.
    /// </summary>
    public async Task FillNewCustomerAsync(LegacyCustomerRef customer, OrderPayload payload, CancellationToken ct)
    {
        if (!customer.IsNew) return;
        var s = payload.Shipper;
        await customers.FillNewCustomerAsync(customer.LegacyCustomerId,
            new NewCustomerInfo(s.Company, s.Contact, s.Tel, s.Address, s.TaxId, s.Email), VietAnUserId, ct);
    }
}

