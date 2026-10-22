using VietAnExpress.Shipments.Application.Orders.Documents;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class Code128Tests
{
    [Fact]
    public void Bang_mau_dung_107_ma_moi_ma_11_module_rieng_stop_13()
    {
        Assert.Equal(107, Code128.Patterns.Length);
        Assert.All(Code128.Patterns[..106], p => Assert.Equal(11, p.Sum(c => c - '0')));
        Assert.Equal(13, Code128.Patterns[106].Sum(c => c - '0'));
        Assert.Equal(107, Code128.Patterns.Distinct().Count());
    }

    [Fact]
    public void So_chan_dung_bo_C_va_checksum_dung()
    {
        // Start C (105) + 90,00,00,01; checksum = (105 + 90×1 + 0 + 0 + 1×4) mod 103 = 199 mod 103 = 96
        Assert.Equal([105, 90, 0, 0, 1, 96, 106], Code128.Encode("90000001"));
    }

    [Fact]
    public void Chu_hoac_so_le_dung_bo_B()
    {
        // Start B (104) + 'V'(54) 'A'(33) '1'(17); checksum = (104 + 54 + 66 + 51) mod 103 = 275 mod 103 = 69
        Assert.Equal([104, 54, 33, 17, 69, 106], Code128.Encode("VA1"));
    }

    [Theory]
    [InlineData("6177728")]       // số bill 7 chữ số (lẻ)
    [InlineData("617772")]
    [InlineData("6177728/1")]     // số bill / số kiện trên nhãn
    [InlineData("61777281/12")]
    [InlineData("VA1")]
    [InlineData("123")]
    public void Giai_ma_lai_dung_chuoi_va_checksum(string text)
    {
        Assert.Equal(text, Decode(Code128.Encode(text)));
    }

    [Fact]
    public void So_bill_7_chu_so_dung_bo_C_nen_ngan_hon_bo_B()
    {
        // Start B + '6' + Code C + 17 77 28 + checksum + stop = 8 ký tự (bộ B thuần là 10) → vạch to hơn khi in cùng khổ.
        Assert.Equal(8, Code128.Encode("6177728").Count);
    }

    /// <summary>Đọc lại như máy quét: kiểm checksum rồi dịch theo bộ B / C (kể cả ký tự đổi bộ).</summary>
    private static string Decode(IReadOnlyList<int> v)
    {
        Assert.Equal(106, v[^1]);
        var sum = v[0];
        for (var i = 1; i < v.Count - 2; i++) sum += v[i] * i;
        Assert.Equal(sum % 103, v[^2]);
        var setC = v[0] == 105;
        var sb = new System.Text.StringBuilder();
        for (var i = 1; i < v.Count - 2; i++)
        {
            if (setC && v[i] == 100) { setC = false; continue; }
            if (!setC && v[i] == 99) { setC = true; continue; }
            sb.Append(setC ? v[i].ToString("00", System.Globalization.CultureInfo.InvariantCulture) : ((char)(v[i] + 32)).ToString());
        }
        return sb.ToString();
    }

    [Fact]
    public void Svg_co_vach_va_chan_ky_tu_la()
    {
        Assert.Contains("<rect", Code128.Svg("6010839"));
        Assert.Throws<ArgumentException>(() => Code128.Encode("Mã"));
        Assert.Throws<ArgumentException>(() => Code128.Encode(""));
    }
}
