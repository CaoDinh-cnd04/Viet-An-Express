using System.Globalization;
using System.Text;

namespace VietAnExpress.Shipments.Application.Orders.Documents;

/// <summary>
/// Mã vạch Code 128 dạng SVG để in trên bill / nhãn (máy quét kho đọc được).
/// Đoạn chữ số đầu chuỗi (≥ 4 số) mã bằng bộ C (2 số / ký tự → vạch to hơn, máy quét dễ đọc); số lẻ thì chữ số đầu
/// đi bộ B rồi chuyển C. Phần còn lại (vd "/1" của số kiện) chuyển về bộ B (ASCII 32–127).
/// </summary>
internal static class Code128
{
    // Độ rộng vạch / khoảng trắng (đơn vị module) cho giá trị 0–106; mỗi mẫu tổng 11 module, riêng Stop 13.
    internal static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    private const int StartB = 104;
    private const int StartC = 105;
    private const int Stop = 106;
    /// <summary>Đổi sang bộ C (khi đang ở bộ B) / sang bộ B (khi đang ở bộ C).</summary>
    private const int CodeC = 99;
    private const int CodeB = 100;

    /// <summary>Dãy giá trị mã hoá (start + dữ liệu + checksum + stop).</summary>
    public static IReadOnlyList<int> Encode(string text)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Mã vạch không được rỗng", nameof(text));

        foreach (var ch in text)
            if (ch < 32 || ch > 127) throw new ArgumentException($"Ký tự '{ch}' không mã hoá được bằng Code 128B", nameof(text));

        var values = new List<int>();
        var digits = 0;
        while (digits < text.Length && char.IsAsciiDigit(text[digits])) digits++;
        var i = 0;
        if (digits >= 4)
        {
            if (digits % 2 == 1)
            {
                // Số chữ số lẻ: chữ số đầu ở bộ B, phần chẵn còn lại ở bộ C.
                values.Add(StartB);
                values.Add(text[0] - 32);
                values.Add(CodeC);
                i = 1;
            }
            else values.Add(StartC);
            for (; i < digits; i += 2) values.Add(int.Parse(text.AsSpan(i, 2), CultureInfo.InvariantCulture));
            if (i < text.Length) values.Add(CodeB);
        }
        else values.Add(StartB);
        for (; i < text.Length; i++) values.Add(text[i] - 32);

        // Checksum = (start + Σ giá trị × vị trí) mod 103
        var sum = values[0];
        for (var k = 1; k < values.Count; k++) sum += values[k] * k;
        values.Add(sum % 103);
        values.Add(Stop);
        return values;
    }

    /// <summary>Vùng trắng hai bên mã vạch (module).</summary>
    private const int Quiet = 10;

    /// <summary>SVG co giãn theo khung chứa (width 100%); có vùng trắng 10 module hai bên để máy quét đọc.</summary>
    public static string Svg(string text, int height = 60)
    {
        const int quiet = Quiet;
        var bars = new StringBuilder();
        var x = quiet;
        foreach (var value in Encode(text))
        {
            var pattern = Patterns[value];
            for (var i = 0; i < pattern.Length; i++)
            {
                var width = pattern[i] - '0';
                if (i % 2 == 0) bars.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x}\" y=\"0\" width=\"{width}\" height=\"{height}\"/>");
                x += width;
            }
        }
        var total = x + quiet;
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {height}\" preserveAspectRatio=\"none\" " +
               $"class=\"barcode\" role=\"img\" aria-label=\"{System.Net.WebUtility.HtmlEncode(text)}\" shape-rendering=\"crispEdges\">{bars}</svg>";
    }
}
