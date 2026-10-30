using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Tracking;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Không có dbo.Orders / dbo.Order_Tracking — dùng cho test không liên quan hành trình hệ thống cũ.</summary>
internal sealed class NoOrderTracking : IOrderTracking
{
    public static readonly NoOrderTracking Instance = new();
    public Task<IReadOnlyDictionary<string, OrderTrack>> FindAsync(IReadOnlyCollection<string> orderNumbers, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, OrderTrack>>(new Dictionary<string, OrderTrack>());
}

/// <summary>Hành trình hệ thống cũ (dbo.Orders + dbo.Order_Tracking) như trang tracking cũ hiện.</summary>
public class OrderTrackingTests
{
    private static readonly DateTime Today = new(2026, 10, 30);

    [Fact]
    public void Tach_su_kien_tu_JSON_cua_hang_ngay_gio_rieng_va_dia_diem_long_nhau()
    {
        const string json = """
            {"meta":{"code":200},"data":{"tracking_number":"1234","origin_info":{"trackinfo":[
              {"date":"2026-10-22","time":"11:35","description":"Clearance delay","location":{"city":"GUANGXI","country":"CHINA"}},
              {"date":"2026-10-21","time":"19:42","description":"Departed Facility in Ha Noi","location":"Ha Noi - VN"},
              {"date":"2026-10-22","time":"05:51","description":"Arrived at Sort Facility Wuxu","location":"GUANGXI, CHINA"}
            ]}}}
            """;
        var events = OrderTrackingData.Parse(json);
        Assert.Equal(["Clearance delay", "Arrived at Sort Facility Wuxu", "Departed Facility in Ha Noi"], events.Select(e => e.Description));
        Assert.Equal(new DateTime(2026, 10, 22, 11, 35, 0), events[0].Time);
        Assert.Equal("GUANGXI, CHINA", events[0].Location);
    }

    [Theory]
    [InlineData("""[{"Checkpoint_Time":"2026-10-20T21:11:00+07:00","Message":"Departed Facility in Ho Chi Minh","Location":"Ho Chi Minh - VN"}]""", "Departed Facility in Ho Chi Minh")]
    [InlineData("""{"shipments":[{"events":[{"timestamp":"2026-10-20T21:11:00","status":{"description":"Shipment picked up"},"location":{"address":{"addressLocality":"HO CHI MINH"}}}]}]}""", "Shipment picked up")]
    public void Nhieu_dang_JSON_hang_khac_nhau(string json, string description)
    {
        var e = Assert.Single(OrderTrackingData.Parse(json));
        Assert.Equal((description, new DateTime(2026, 10, 20, 21, 11, 0)), (e.Description, e.Time));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""[{"time":"11:35","description":"Chỉ có giờ, không có ngày"}]""")]
    public void Du_lieu_khong_doc_duoc_thi_khong_co_su_kien(string? json) => Assert.Empty(OrderTrackingData.Parse(json));

    [Fact]
    public void Khong_tach_duoc_JSON_thi_dung_su_kien_cuoi_LastEvent()
    {
        var track = OrderTrackingData.ToTrack("6178081", new OrderTrackingRow
        {
            TrackingData = "{}", StatusString = "transit",
            LastEventDate = new DateTime(2026, 10, 22, 11, 35, 0), LastEventContent = " Clearance delay ", LastEventLocation = "GUANGXI, CHINA"
        });
        var e = Assert.Single(track.Events);
        Assert.Equal(("Clearance delay", "GUANGXI, CHINA"), (e.Description, e.Location));
        Assert.False(track.Signed);
        Assert.True(OrderTrackingData.IsDelivered("delivered"));
        Assert.False(OrderTrackingData.IsDelivered("undelivered"));
        Assert.False(OrderTrackingData.IsDelivered("out for delivery"));
    }

    [Fact]
    public void Don_da_khai_thac_co_ngay_gui_va_hanh_trinh_cua_Order_Tracking()
    {
        // Như bill 6178081: MaVanDon chưa có ngày gửi; Orders.OrderDate = 20/10, Order_Tracking có hành trình hãng.
        var row = new LegacyOrder { OrderNumber = 6178081, CreateDate = new DateTime(2026, 10, 19), ConsigneeCity = "KUNSHAN", ConsigneeCountry = "China" };
        var track = new HubEzTrack("6178081", [new HubEzEvent(new DateTime(2026, 10, 22, 11, 35, 0), "Clearance delay", "GUANGXI, CHINA", null)], false);
        var legacy = new OrderTrack("6178081", new DateTime(2026, 10, 20), track, null, false);

        var r = LegacyTrackingMapper.ToResult("6178081", row, Today, legacy: legacy);

        Assert.Equal(("fly", "20/10/2026"), (r.Status, r.ShipDate));
        Assert.Equal(["Clearance delay", "Đã tạo vận đơn"], r.Events!.Select(e => e.Title));
        Assert.Equal("22/10/2026 11:35", r.Events![0].Time);
        Assert.Equal("ok", LegacyTrackingMapper.ToResult("6178081", row, Today, legacy: legacy with { Delivered = true }).Status);
    }

    [Fact]
    public void Hanh_trinh_chuyen_tuyen_Order_Tracking_NoiDong_ngay_cong_SortOrder_gio_cot_Time()
    {
        // Như trang cũ bill 6178081: gửi 20/10, mẫu Carriers_NoiDong SortOrder 0 / 1 / 2.
        var events = OrderTrackingData.Domestic(
        [
            new DomesticEventRow { OrderNumber = "6178081", EventDate = new DateTime(2026, 10, 20), Time = "21:11", Status = "Departed Facility in Ho Chi Minh", Location = "Ho Chi Minh - VN" },
            new DomesticEventRow { OrderNumber = "6178081", EventDate = new DateTime(2026, 10, 22), Time = "11:35", Status = "Clearance delay", Location = "GUANGXI, CHINA" },
            new DomesticEventRow { OrderNumber = "6178081", EventDate = new DateTime(2026, 10, 21), Time = "19:42", Status = " Departed Facility in Ha Noi ", Location = "Ha Noi - VN" },
            new DomesticEventRow { OrderNumber = "6178081", EventDate = new DateTime(2026, 10, 21), Time = null, Status = "" }
        ]);
        Assert.Equal(["Clearance delay", "Departed Facility in Ha Noi", "Departed Facility in Ho Chi Minh"], events.Select(e => e.Description));
        Assert.Equal((new DateTime(2026, 10, 22, 11, 35, 0), "GUANGXI, CHINA"), (events[0].Time, events[0].Location));
    }

    [Fact]
    public void Bill_chi_co_o_Orders_van_tra_duoc()
    {
        var legacy = new OrderTrack("6178081", new DateTime(2026, 10, 20), null, null, false, 201008, "KUNSHAN, China", "DHL");
        var r = LegacyTrackingMapper.FromOrder("6178081", legacy);
        Assert.Equal((true, "fly", "KUNSHAN, China", "20/10/2026"), (r.Found, r.Status, r.Destination, r.ShipDate));
        Assert.Empty(r.Events!);
    }

    [Fact]
    public void Hanh_trinh_HUB_EZ_co_su_kien_uu_tien_hon_Order_Tracking()
    {
        var hub = new HubEzTrack("H1", [new HubEzEvent(Today, "HUB-EZ", null, null)], false);
        var empty = new HubEzTrack("H1", [], false);
        var legacy = new OrderTrack("1", Today, new HubEzTrack("1", [new HubEzEvent(Today, "Cũ", null, null)], false), null, false);
        Assert.Same(hub, LegacyTrackingMapper.Carrier(hub, legacy));
        Assert.Same(legacy.Track, LegacyTrackingMapper.Carrier(empty, legacy));
        Assert.Same(empty, LegacyTrackingMapper.Carrier(empty, null));
    }
}
