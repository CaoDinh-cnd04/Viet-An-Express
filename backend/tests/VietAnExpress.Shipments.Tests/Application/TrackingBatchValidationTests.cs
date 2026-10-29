using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Tracking;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class TrackingBatchValidationTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Tracking_bounds_the_number_of_unique_bills_before_querying(int count, bool valid)
    {
        var query = new TrackShipmentsQuery(Enumerable.Range(1, count).Select(i => $"BILL-{i}").ToArray());
        Assert.Equal(valid, new TrackShipmentsQueryValidator().Validate(query).IsValid);
    }

    [Fact]
    public void Duplicate_and_blank_bills_do_not_consume_the_batch_limit()
    {
        var query = new TrackShipmentsQuery(Enumerable.Repeat(" bill-1 ", 11).Concat(["BILL-1", " "]).ToArray());
        Assert.True(new TrackShipmentsQueryValidator().Validate(query).IsValid);
        Assert.Equal("BILL-1", Assert.Single(query.NormalizedBills));
    }
}
