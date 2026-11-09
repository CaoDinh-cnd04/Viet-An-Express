using VietAnExpress.Customers.Contracts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Cột "Người tạo" ở trang Đơn khách phụ trách: tên đăng nhập tài khoản đã tạo đơn.</summary>
public class OrderCreatorTests
{
    private static readonly Dictionary<int, string> Agents = new() { [129] = "hanh.vo" };
    private static readonly Dictionary<long, string> Subs = new() { [55] = "sgb.kho" };
    private static readonly Dictionary<long, CustomerSummary> Customers = new()
    {
        [201008] = new CustomerSummary(201008, "SGBHN", "SGB EXPRESS HN", null, null, null, null, null, LoginUserName: " SaigonbayHN ")
    };

    private static string? Creator(int? createUser, long? staffId, long? customerId = 201008) =>
        OrderAccess.CreatorOf(new LegacyOrder { CreateUser = createUser, CustomerStaffId = staffId, CustomerId = customerId }, Agents, Subs, Customers);

    [Fact]
    public void Nhan_vien_Viet_An_tao_giup_hien_ten_dang_nhap_USERS() => Assert.Equal("hanh.vo", Creator(129, 55));

    [Fact]
    public void Tai_khoan_con_hien_ten_dang_nhap_nguoi_lien_he() => Assert.Equal("sgb.kho", Creator(0, 55));

    [Fact]
    public void Tai_khoan_chinh_hien_ten_dang_nhap_cua_khach() => Assert.Equal("SaigonbayHN", Creator(0, 0));

    [Fact]
    public void Khong_tra_duoc_thi_de_trong()
    {
        Assert.Null(Creator(999, 0));        // USERS không còn
        Assert.Null(Creator(null, 77));      // tài khoản con đã xóa tên đăng nhập
        Assert.Null(Creator(null, null, 1)); // khách không còn hồ sơ
    }
}
