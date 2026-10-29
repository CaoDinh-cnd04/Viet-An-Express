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
        var sql = OrderSummaryQuery.Build(db.LegacyOrders.Where(o => o.CustomerId == 201008),
            new DateTime(2026, 10, 3)).ToQueryString();
        Assert.Contains("COUNT", sql);
        Assert.Contains("SUM", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("[CustomerID]", sql);
        Assert.DoesNotContain("[ConsigneeName]", sql);
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
