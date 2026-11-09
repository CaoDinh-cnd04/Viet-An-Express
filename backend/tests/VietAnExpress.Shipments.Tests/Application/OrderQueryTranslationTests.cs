using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class OrderQueryTranslationTests
{
    private static ShipmentsDbContext CreateDb() => new(new DbContextOptionsBuilder<ShipmentsDbContext>()
        .UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=True;Encrypt=True")
        .Options);

    [Fact]
    public void Summary_is_a_single_SQL_aggregate_with_customer_filter()
    {
        using var db = CreateDb();
        var sql = OrderSummaryQuery.Build(db.LegacyOrders.Where(o => o.CustomerId == 201008)).ToQueryString();
        Assert.Contains("COUNT", sql);
        Assert.Contains("SUM", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("[CustomerID]", sql);
        Assert.DoesNotContain("[ConsigneeName]", sql);
    }

    [Fact]
    public void Tim_kiem_so_nhi_phan_chu_in_hoa_chi_dong_co_dau_moi_so_bo_dau()
    {
        using var db = CreateDb();
        var sql = OrderListFilter.Apply(db.LegacyOrders.Where(o => o.CustomerId == 201008),
            new GetOrdersQuery("Uyen", "cnee", null, "DOC", null, null, null, null, 1, 20, null, null)).ToQueryString();
        Assert.Contains("UPPER([m].[ConsigneeName]) COLLATE Latin1_General_100_BIN2 LIKE", sql);
        Assert.Contains("COLLATE Latin1_General_100_CI_AI LIKE", sql);
        Assert.Contains("UPPER([m].[Ten_hang]) COLLATE Latin1_General_100_BIN2 LIKE N'%DOCUMENT%'", sql);
        Assert.DoesNotContain("[ConsigneeAddress1]", sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..]); // chỉ ô "Tên người nhận"
    }

    [Fact]
    public void Tim_so_bill_chi_so_nhi_phan_khong_so_bo_dau()
    {
        using var db = CreateDb();
        var sql = OrderListFilter.Apply(db.LegacyOrders.Where(o => o.CustomerId == 201008),
            new GetOrdersQuery("6010", "all", null, null, null, null, null, null, 1, 20, null, null)).ToQueryString();
        // Từ khóa không có chữ cái: so bỏ dấu cho ra y như so nhị phân → không dùng collation bỏ dấu (đắt) ở cột nào.
        Assert.DoesNotContain("CI_AI", sql);
        Assert.Contains("BIN2", sql);
    }

    [Fact]
    public void List_projects_receiver_fields_without_sender_or_financial_columns()
    {
        using var db = CreateDb();
        var sql = db.LegacyOrders.OrderByDescending(o => o.Id).Take(20)
            .Select(LegacyOrderProjections.List).ToQueryString();
        Assert.Contains("TOP", sql);
        Assert.Contains("[ConsigneeEmail]", sql);
        Assert.DoesNotContain("[SenderEmail]", sql);
        Assert.DoesNotContain("[Gia_Tri_Hang]", sql);
    }

    [Fact]
    public void Tracking_does_not_read_personal_contact_or_address_columns()
    {
        using var db = CreateDb();
        var sql = db.LegacyOrders.Select(LegacyOrderProjections.Tracking).ToQueryString();
        Assert.Contains("[POD]", sql);
        Assert.DoesNotContain("[ConsigneeEmail]", sql);
        Assert.DoesNotContain("[ConsigneePhone]", sql);
        Assert.DoesNotContain("[ConsigneeName]", sql);
        Assert.DoesNotContain("[SenderEmail]", sql);
    }
}
