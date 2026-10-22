using VietAnExpress.SharedKernel.Exceptions;
using VietAnExpress.Shipments.Infrastructure;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Số VA bill nối tiếp dãy 7 chữ số của hệ thống cũ.</summary>
public class OrderNumberAllocatorTests
{
    [Fact]
    public void Noi_tiep_ngay_sau_so_lon_nhat() =>
        Assert.Equal([6166454L, 6166455L, 6166456L], LegacyOrderNumberAllocator.Next(6166453, 3));

    [Fact]
    public void Bang_chua_co_don_thi_bat_dau_tu_1() => Assert.Equal([1L], LegacyOrderNumberAllocator.Next(null, 1));

    [Fact]
    public void Khong_vuot_qua_7_chu_so()
    {
        Assert.Equal([9_999_999L], LegacyOrderNumberAllocator.Next(9_999_998, 1));
        Assert.Throws<ConflictException>(() => LegacyOrderNumberAllocator.Next(9_999_998, 2));
    }
}
