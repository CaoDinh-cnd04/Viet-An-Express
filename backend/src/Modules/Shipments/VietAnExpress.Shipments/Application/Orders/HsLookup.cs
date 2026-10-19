using System.Net;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Catalog;

namespace VietAnExpress.Shipments.Application.Orders;

/// <summary>Khớp <c>HsLookupItem</c> của frontend (API_CONTRACT.md §3). Không trả thuế suất.</summary>
/// <param name="Path">Các cấp cha trong biểu thuế (US ghi dạng "A &gt; B &gt; tên dòng") — rỗng nếu biểu thuế không có.</param>
internal sealed record HsLookupItemDto(
    string NameVi, string NameEn, string Hs6, string? NationalCode, string? NationalDescription,
    string? Unit, string? Note, bool IsVerified, IReadOnlyList<string> Path);

/// <param name="Mode">"verified" = chỉ mặt hàng Việt An đã xác minh; "catalog" = toàn bộ danh mục của nước đó.</param>
internal sealed record HsLookupQuery(string? Country, string? Search, string? Mode) : IRequest<IReadOnlyList<HsLookupItemDto>>;

/// <summary>Điều kiện tra cứu đã chuẩn hóa từ query string.</summary>
/// <param name="Digits">Từ khóa toàn số (bỏ dấu chấm / khoảng trắng) → tìm theo đầu mã HS6 / mã quốc gia.</param>
/// <param name="Terms">Từ khóa chữ → mọi từ phải có trong tên Việt / Anh hoặc mô tả biểu thuế.</param>
internal sealed record HsLookupFilter(IReadOnlyList<string> Countries, string? Digits, IReadOnlyList<string> Terms, bool VerifiedOnly);

/// <summary>Chuẩn hóa tham số tra mã HS. Hàm thuần — có test.</summary>
internal static partial class HsLookupRules
{
    public const int MinSearchLength = 2;
    public const int MaxResults = 50;
    private const int MaxTerms = 5;
    /// <summary>Collation tìm theo chữ: không phân biệt hoa/thường và dấu tiếng Việt (kể cả đ/Đ).</summary>
    private const string SearchCollation = "Latin1_General_100_CI_AI";

    /// <summary>Nước có biểu thuế riêng hỗ trợ tra cứu; Anh nhận cả GB và UK (dữ liệu nạp có thể ghi kiểu nào cũng được).</summary>
    private static readonly Dictionary<string, string[]> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = ["US"],
        ["GB"] = ["GB", "UK"],
        ["UK"] = ["GB", "UK"],
        ["AU"] = ["AU"]
    };

    /// <summary>Null = không tra (nước không hỗ trợ hoặc từ khóa quá ngắn) → trả danh sách rỗng.</summary>
    public static HsLookupFilter? Parse(string? country, string? search, string? mode)
    {
        if (country is null || !CountryAliases.TryGetValue(country.Trim(), out var countries)) return null;
        var q = search?.Trim() ?? "";
        if (q.Length < MinSearchLength) return null;

        var verifiedOnly = !string.Equals(mode?.Trim(), "catalog", StringComparison.OrdinalIgnoreCase);
        // "0804.50", "0804 50 00" → tìm theo mã; còn lại tìm theo chữ.
        if (q.All(c => char.IsAsciiDigit(c) || c is '.' or ' ' or '-'))
        {
            var digits = new string(q.Where(char.IsAsciiDigit).ToArray());
            return digits.Length >= MinSearchLength ? new HsLookupFilter(countries, digits, [], verifiedOnly) : null;
        }
        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTerms).ToList();
        return new HsLookupFilter(countries, null, terms, verifiedOnly);
    }

    /// <summary>Truy vấn danh mục theo điều kiện đã chuẩn hóa: đã xác minh lên trước, tối đa <see cref="MaxResults"/> dòng.</summary>
    public static IQueryable<HsLookupItemDto> Build(IQueryable<HsCodeEntry> rows, HsLookupFilter f)
    {
        rows = rows.Where(x => f.Countries.Contains(x.CountryCode));
        if (f.VerifiedOnly) rows = rows.Where(x => x.IsVerified);
        if (f.Digits is { } d)
        {
            // Mã nạp dạng có dấu chấm (0804.50.00.10); vẫn nhận dữ liệu nạp không dấu và mã AU lẫn "?" (?0805.40.00.26).
            var dotted = DottedPrefix(d);
            rows = rows.Where(x => x.NationalCode.StartsWith(dotted) || x.NationalCode.StartsWith(d)
                || x.NationalCode.Replace("?", "").StartsWith(dotted));
        }
        foreach (var term in f.Terms)
        {
            // Bỏ qua hoa/thường và dấu: "xoai say" khớp "Xoài sấy", "da lat" khớp "Đà Lạt" (cột đang là CI_AS — phân biệt dấu).
            rows = rows.Where(x => (x.NameVi != null && EF.Functions.Collate(x.NameVi, SearchCollation).Contains(term))
                || EF.Functions.Collate(x.NameEn, SearchCollation).Contains(term)
                || (x.NationalDescription != null && EF.Functions.Collate(x.NationalDescription, SearchCollation).Contains(term)));
        }
        return rows
            // Bỏ "?" (lỗi dữ liệu AU) khi sắp xếp để mã đứng đúng chỗ theo số.
            .OrderByDescending(x => x.IsVerified).ThenBy(x => x.NationalCode.Replace("?", ""))
            .Take(MaxResults)
            .Select(x => new HsLookupItemDto(x.NameVi ?? "", x.NameEn, x.Hs6, x.NationalCode, x.NationalDescription, x.Unit, x.Note, x.IsVerified, Array.Empty<string>()));
    }

    /// <summary>"080450" → "0804.50", "08045000" → "0804.50.00", "61091" → "6109.1" (4 số đầu, sau đó nhóm 2 số).</summary>
    public static string DottedPrefix(string digits)
    {
        if (digits.Length <= 4) return digits;
        var parts = new List<string> { digits[..4] };
        for (var i = 4; i < digits.Length; i += 2) parts.Add(digits.Substring(i, Math.Min(2, digits.Length - i)));
        return string.Join('.', parts);
    }

    /// <summary>
    /// Làm sạch dòng đọc từ biểu thuế cho khách xem: bỏ thẻ HTML (m&lt;sup&gt;3&lt;/sup&gt; → m3, &lt;u&gt;kg&lt;/u&gt; → kg, &amp;#160;),
    /// tách đường dẫn cấp cha của mô tả US / AU ("A &gt; B &gt; tên dòng"), sửa lỗi lấy dữ liệu của mã AU
    /// ("?0805.40.00.26", "0402.2.160"). Null = dòng rác không có mã HS (chú thích bị lấy nhầm). Hàm thuần — có test.
    /// </summary>
    public static HsLookupItemDto? Present(HsLookupItemDto x)
    {
        if (CleanCode(x.NationalCode) is not { } code) return null;
        var name = Text(x.NameEn)?.TrimEnd(':').TrimEnd() ?? "";
        var description = Text(x.NationalDescription);
        var path = description?.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [];
        // Bỏ cấp cuối nếu chính là tên dòng; mô tả không có cấp cha (UK) thì không có đường dẫn.
        if (path.Count > 0 && string.Equals(path[^1], name, StringComparison.OrdinalIgnoreCase)) path.RemoveAt(path.Count - 1);
        if (path.Count == 1 && string.Equals(path[0], description, StringComparison.OrdinalIgnoreCase)) path.Clear();
        return x with
        {
            NameVi = Text(x.NameVi) ?? "",
            NameEn = name,
            Hs6 = CleanCode(x.Hs6) ?? code,
            NationalCode = code,
            NationalDescription = description,
            Unit = Text(x.Unit) is { } unit && unit.Any(char.IsLetterOrDigit) ? unit : null,
            Note = Text(x.Note),
            Path = [.. path.Select(p => p.TrimEnd(':'))]
        };
    }

    /// <summary>
    /// Sửa lỗi lấy dữ liệu của mã AU: bỏ "?", "*", khoảng trắng / &amp;#160; ("? 0307.49.00.40", "2711.12.90**.02");
    /// cắt phần "160" thừa (còn lại của &amp;#160;) ở nhóm cuối dài quá 2 số ("0805.21.00.27160" → "0805.21.00.27",
    /// "8423.90.160160160160" → "8423.90", "0402.2.160" → "0402.2"). Mã vẫn sai cấu trúc (4 số rồi các nhóm 1–2 số, tối đa 10 số) → null.
    /// </summary>
    public static string? CleanCode(string? raw)
    {
        var text = Text(raw);
        if (text is null) return null;
        var code = new string([.. text.Where(c => char.IsAsciiDigit(c) || c == '.')]).Trim('.');
        if (code.Length == 0 || !char.IsAsciiDigit(text.TrimStart('?', '*', ' ')[0])) return null;

        var parts = code.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count > 1 && parts[^1].Length > 2 && parts[^1].EndsWith("160", StringComparison.Ordinal))
        {
            parts[^1] = parts[^1][..^3];
            if (parts[^1].Length == 0) parts.RemoveAt(parts.Count - 1);
        }
        var valid = parts.Count > 0 && parts[0].Length == 4 && parts.Skip(1).All(p => p.Length is 1 or 2) && parts.Sum(p => p.Length) <= 10;
        return valid ? string.Join('.', parts) : null;
    }

    private static string? Text(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var plain = WebUtility.HtmlDecode(HtmlTag().Replace(html, ""));
        plain = Spaces().Replace(plain, " ").Trim();
        return plain.Length == 0 ? null : plain;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

internal sealed class HsLookupHandler(ShipmentsDbContext db) : IRequestHandler<HsLookupQuery, IReadOnlyList<HsLookupItemDto>>
{
    public async Task<IReadOnlyList<HsLookupItemDto>> Handle(HsLookupQuery q, CancellationToken ct)
    {
        if (HsLookupRules.Parse(q.Country, q.Search, q.Mode) is not { } f) return [];

        var rows = await HsLookupRules.Build(db.Set<HsCodeEntry>().AsNoTracking(), f).ToListAsync(ct);
        return [.. rows.Select(HsLookupRules.Present).OfType<HsLookupItemDto>()];
    }
}
