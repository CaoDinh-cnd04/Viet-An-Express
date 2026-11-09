using VietAnExpress.Shipments.Services;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Danh mục dịch vụ → hub đọc từ dbo.TGroupBaseCode / dbo.TBaseCode của hệ thống cũ.</summary>
public class ServiceHubRulesTests
{
    [Fact]
    public void Gom_hang_theo_thu_tu_danh_muc_hub_theo_SortOrder_so_giu_dung_chu()
    {
        ServiceHubRow[] rows =
        [
            new(5, "Chuyên tuyến", 3, "AU_Toll (T.Phẩm/M.Phẩm)", "10", "CT-Au_Toll (TP/MP)"),
            new(5, "Chuyên tuyến", 3, "AU_AuPost", "2", " CT AU_AuPost "),
            new(5, "Chuyên tuyến", 3, "Singapore", null, ""),
            new(2, "DHL", 1, "SIN VIP", "1", null),
            new(2, "DHL", 1, "sin vip", "9", null),          // trùng tên (khác hoa thường) → bỏ
            new(7, "Aramex", null, "Dubai", "1", "Aramex-Dxb"), // không có SortOrder → cuối
            new(9, "  ", 2, "X", "1", null),                  // hãng trống tên → bỏ
            new(8, "Fedex", 2, null, null, null)               // hãng chưa có hub
        ];

        var result = ServiceHubRules.Build(rows);

        Assert.Equal(["DHL", "Fedex", "Chuyên tuyến", "Aramex"], result.Select(c => c.Carrier));
        Assert.Equal([new ServiceHubDto("SIN VIP", null)], result[0].Hubs);
        Assert.Empty(result[1].Hubs);
        // "2" < "10" theo số, hub không có SortOrder xếp cuối; giữ nguyên chữ tên hub (Dich_Vu khớp đúng chữ), mã ngắn bỏ khoảng trắng.
        Assert.Equal([new ServiceHubDto("AU_AuPost", "CT AU_AuPost"), new ServiceHubDto("AU_Toll (T.Phẩm/M.Phẩm)", "CT-Au_Toll (TP/MP)"), new ServiceHubDto("Singapore", null)],
            result[2].Hubs);
    }

    [Fact]
    public void Khong_co_dong_thi_rong() => Assert.Empty(ServiceHubRules.Build([]));
}
