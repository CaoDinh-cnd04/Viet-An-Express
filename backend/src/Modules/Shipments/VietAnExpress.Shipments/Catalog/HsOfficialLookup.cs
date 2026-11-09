using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>
/// "Tra danh mục mã" của Mỹ / Anh đọc thẳng biểu thuế chính thức (05/11/2026, người dùng chọn):
/// Mỹ — API công khai của hts.usitc.gov (HTSUS); Anh — API UK Trade Tariff (trade-tariff.service.gov.uk). Úc chưa có API công khai → vẫn dùng website.
/// </summary>
internal interface IHsOfficialLookup
{
    /// <summary>Có tra được bằng biểu thuế chính thức không (nước Mỹ / Anh, từ khóa chữ hoặc từ 4 số).</summary>
    bool Supports(string country, HsLookupFilter filter);

    /// <summary>Lỗi kết nối → ném <see cref="HttpRequestException"/> / <see cref="TaskCanceledException"/> (handler chuyển sang website).</summary>
    Task<IReadOnlyList<HsLookupItemDto>> SearchAsync(string country, HsLookupFilter filter, string query, CancellationToken ct);
}

internal sealed class HsOfficialLookup(HttpClient http, IMemoryCache cache, IOptions<HsLookupOptions> options) : IHsOfficialLookup
{
    /// <summary>Số nhóm 4 số tối đa tải về khi tra Mỹ theo chữ (mỗi nhóm 1 lần gọi, có cache).</summary>
    private const int MaxUsHeadings = 4;

    public bool Supports(string country, HsLookupFilter filter) =>
        options.Value.OfficialTariff && HsOfficialMapper.Tariff(country) is not null
        && (filter.Terms.Count > 0 || filter.Digits is { Length: >= 4 });

    public async Task<IReadOnlyList<HsLookupItemDto>> SearchAsync(string country, HsLookupFilter filter, string query, CancellationToken ct) =>
        HsOfficialMapper.Tariff(country) == "US" ? await SearchUsAsync(filter, query, ct) : await SearchUkAsync(filter, query, ct);

    private async Task<IReadOnlyList<HsLookupItemDto>> SearchUsAsync(HsLookupFilter f, string query, CancellationToken ct)
    {
        IReadOnlyList<string> headings;
        if (f.Digits is { } d) headings = [d[..4]];
        else
        {
            using var found = await GetJsonAsync(Us($"search?keyword={Uri.EscapeDataString(query)}"), ct);
            headings = [.. HsOfficialMapper.UsHeadings(found.RootElement, f.Terms).Take(MaxUsHeadings)];
        }

        var result = new List<HsLookupItemDto>();
        foreach (var h in headings)
        {
            var next = (int.Parse(h, CultureInfo.InvariantCulture) + 1).ToString("D4", CultureInfo.InvariantCulture);
            using var rows = await GetJsonAsync(Us($"exportList?from={h}&to={next}&format=JSON&styles=false"), ct);
            result.AddRange(HsOfficialMapper.UsLeaves(rows.RootElement, f.Digits ?? h, f.Terms));
        }
        return HsOfficialMapper.Rank(result, f.Terms);
    }

    private async Task<IReadOnlyList<HsLookupItemDto>> SearchUkAsync(HsLookupFilter f, string query, CancellationToken ct)
    {
        if (f.Digits is { } d) return await UkHeadingAsync(d, ct);

        using var found = await GetJsonAsync(Uk($"search?q={Uri.EscapeDataString(query)}"), ct);
        return HsOfficialMapper.UkExactMatch(found.RootElement) is { } exact
            ? await UkHeadingAsync(exact, ct)
            : HsOfficialMapper.Rank(HsOfficialMapper.UkSearch(found.RootElement), f.Terms);
    }

    /// <summary>Các mã khai báo được trong nhóm 4 số (lọc theo đầu mã đã gõ). Nhóm không tồn tại (404) → rỗng.</summary>
    private async Task<IReadOnlyList<HsLookupItemDto>> UkHeadingAsync(string digits, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(Uk($"headings/{digits[..4]}"), ct, notFoundEmpty: true);
        return doc is null ? [] : HsOfficialMapper.UkHeading(doc.RootElement, digits);
    }

    private string Us(string path) => options.Value.UsTariffApiUrl.TrimEnd('/') + "/" + path;
    private string Uk(string path) => options.Value.UkTariffApiUrl.TrimEnd('/') + "/" + path;

    /// <summary>Đọc JSON (cache nguyên văn theo URL). API Mỹ trả JSON với Content-Type octet-stream — vẫn đọc như UTF-8.</summary>
    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct) => (await GetJsonAsync(url, ct, notFoundEmpty: false))!;

    private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken ct, bool notFoundEmpty)
    {
        var key = "hs-official:" + url;
        if (!cache.TryGetValue(key, out string? text) || text is null)
        {
            using var res = await http.GetAsync(url, ct);
            if (notFoundEmpty && res.StatusCode == System.Net.HttpStatusCode.NotFound) text = "";
            else
            {
                res.EnsureSuccessStatusCode();
                text = Encoding.UTF8.GetString(await res.Content.ReadAsByteArrayAsync(ct));
            }
            if (options.Value.CacheMinutes > 0) cache.Set(key, text, TimeSpan.FromMinutes(options.Value.CacheMinutes));
        }
        return text.Length == 0 ? null : JsonDocument.Parse(text);
    }
}

/// <summary>Đổi JSON biểu thuế Mỹ / Anh sang <see cref="HsLookupItemDto"/> (chưa làm sạch — handler gọi <see cref="HsLookupRules.Present"/>). Hàm thuần — có test.</summary>
internal static class HsOfficialMapper
{
    /// <summary>"US" / "GB" (Anh nhận cả UK); nước khác → null (không có API chính thức).</summary>
    public static string? Tariff(string country) => country.Trim().ToUpperInvariant() switch
    {
        "US" => "US",
        "GB" or "UK" => "GB",
        _ => null
    };

    /// <summary>
    /// Kết quả /search của hts.usitc.gov lẫn nhiều dòng không liên quan → chỉ lấy nhóm 4 số của các dòng mà chính mô tả chứa đủ từ khóa,
    /// theo thứ tự xuất hiện. Bỏ chương 98 / 99 (điều khoản đặc biệt, không phải mã hàng).
    /// </summary>
    public static IEnumerable<string> UsHeadings(JsonElement rows, IReadOnlyList<string> terms) =>
        Rows(rows)
            .Where(r => Digits(Str(r, "htsno")).Length >= 4 && ContainsAll(Str(r, "description"), terms))
            .Select(r => Digits(Str(r, "htsno"))[..4])
            .Where(h => !h.StartsWith("98", StringComparison.Ordinal) && !h.StartsWith("99", StringComparison.Ordinal))
            .Distinct();

    /// <summary>
    /// /exportList của 1 nhóm (đủ cả dòng cha không đánh số như "Men's or boys':"): dựng đường dẫn theo cột indent,
    /// lấy dòng mã 10 số bắt đầu bằng <paramref name="prefix"/>; có từ khóa thì đường dẫn + tên dòng phải chứa đủ.
    /// </summary>
    public static IEnumerable<HsLookupItemDto> UsLeaves(JsonElement rows, string prefix, IReadOnlyList<string> terms)
    {
        var stack = new List<string>();
        foreach (var r in Rows(rows))
        {
            var indent = int.TryParse(Str(r, "indent"), out var i) ? Math.Max(0, i) : 0;
            var description = Str(r, "description") ?? "";
            if (stack.Count > indent) stack.RemoveRange(indent, stack.Count - indent);
            while (stack.Count < indent) stack.Add("");
            stack.Add(description.TrimEnd(':').Trim());

            var code = Digits(Str(r, "htsno"));
            if (code.Length != 10 || !code.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var path = stack.Where(s => s.Length > 0).ToList();
            if (!ContainsAll(string.Join(" ", path), terms)) continue;
            var units = r.TryGetProperty("units", out var u) && u.ValueKind == JsonValueKind.Array
                ? string.Join(", ", u.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()))
                : null;
            yield return Item(code, path, units);
        }
    }

    /// <summary>/search của UK Trade Tariff khớp đúng 1 nhóm / mã (<c>exact_match</c>) → mã nhóm cần tải; chương / phần → null.</summary>
    public static string? UkExactMatch(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("attributes", out var a)) return null;
        if (Str(a, "type") != "exact_match" || !a.TryGetProperty("entry", out var entry)) return null;
        var id = Digits(Str(entry, "id"));
        return Str(entry, "endpoint") is "headings" or "commodities" or "subheadings" && id.Length >= 4 ? id : null;
    }

    /// <summary>/search (fuzzy_match): mã khai báo được (declarable) kèm mô tả các cấp cha (<c>ancestor_descriptions</c>, bỏ cấp chương).</summary>
    public static IReadOnlyList<HsLookupItemDto> UkSearch(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("attributes", out var a)
            || !a.TryGetProperty("goods_nomenclature_match", out var match)) return [];
        var result = new List<HsLookupItemDto>();
        foreach (var group in new[] { "headings", "commodities" })
        {
            if (!match.TryGetProperty(group, out var list) || list.ValueKind != JsonValueKind.Array) continue;
            foreach (var hit in list.EnumerateArray())
            {
                if (!hit.TryGetProperty("_source", out var s) || !s.TryGetProperty("declarable", out var dec) || dec.ValueKind != JsonValueKind.True) continue;
                var code = Digits(Str(s, "goods_nomenclature_item_id"));
                if (code.Length != 10) continue;
                var path = s.TryGetProperty("ancestor_descriptions", out var anc) && anc.ValueKind == JsonValueKind.Array
                    ? anc.EnumerateArray().Select(x => x.GetString() ?? "").Skip(1).ToList()
                    : [];
                if (path.Count == 0 || !string.Equals(path[^1], Str(s, "description"), StringComparison.OrdinalIgnoreCase)) path.Add(Str(s, "description") ?? "");
                result.Add(Item(code, path, null));
            }
        }
        // Giữ thứ tự liên quan của API (điểm tìm kiếm); handler xếp hạng lại theo từ khóa.
        return [.. result.DistinctBy(x => x.NationalCode)];
    }

    /// <summary>
    /// Xếp kết quả tra chữ: dòng có nhiều từ khóa ở phân nhóm / tên dòng (bỏ mô tả nhóm 4 số — chung cả nhóm) lên trước,
    /// cùng điểm thì giữ thứ tự cũ. VD "dried mango": "Guavas, mangoes… › Mangoes › Dried" trước "Dates › Whole".
    /// </summary>
    public static IReadOnlyList<HsLookupItemDto> Rank(IEnumerable<HsLookupItemDto> rows, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0) return [.. rows.Take(HsLookupRules.MaxResults)];
        return [.. rows
            .Select((r, i) => (Row: r, Index: i, Score: Score(r, terms)))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Index)
            .Select(x => x.Row)
            .Take(HsLookupRules.MaxResults)];
    }

    private static int Score(HsLookupItemDto r, IReadOnlyList<string> terms)
    {
        var parts = (r.NationalDescription ?? r.NameEn).Split(" > ");
        var own = Fold(string.Join(" ", parts.Skip(1)));
        var leaf = Fold(r.NameEn);
        // Từ khóa ở tên dòng tính 2, ở phân nhóm cha tính 1.
        return terms.Sum(t => leaf.Contains(Fold(t), StringComparison.Ordinal) ? 2 : own.Contains(Fold(t), StringComparison.Ordinal) ? 1 : 0);
    }

    /// <summary>
    /// /headings/{4 số}: mã lá (leaf) trong <c>included</c>, đường dẫn dựng theo <c>parent_sid</c> (gốc là mô tả nhóm);
    /// nhóm khai báo được luôn (không có mã con) → chính nhóm. Lọc theo đầu mã <paramref name="digits"/>.
    /// </summary>
    public static IReadOnlyList<HsLookupItemDto> UkHeading(JsonElement root, string digits)
    {
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("attributes", out var head)) return [];
        var headingText = Str(head, "description_plain") ?? Str(head, "description") ?? "";
        var nodes = new Dictionary<string, (string? Parent, string Text)>();
        var leaves = new List<(string Sid, string Code)>();
        if (root.TryGetProperty("included", out var inc) && inc.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in inc.EnumerateArray())
            {
                if (Str(x, "type") != "commodity" || !x.TryGetProperty("attributes", out var c)) continue;
                var sid = Str(c, "goods_nomenclature_sid");
                if (sid is null) continue;
                nodes[sid] = (Str(c, "parent_sid"), Str(c, "description_plain") ?? Str(c, "description") ?? "");
                if (c.TryGetProperty("leaf", out var leaf) && leaf.ValueKind == JsonValueKind.True) leaves.Add((sid, Digits(Str(c, "goods_nomenclature_item_id"))));
            }
        }

        var result = new List<HsLookupItemDto>();
        if (nodes.Count == 0 && head.TryGetProperty("declarable", out var dec) && dec.ValueKind == JsonValueKind.True)
        {
            var code = Digits(Str(head, "goods_nomenclature_item_id"));
            if (code.Length == 10 && code.StartsWith(digits, StringComparison.Ordinal)) result.Add(Item(code, [headingText], null));
        }
        foreach (var (sid, code) in leaves)
        {
            if (code.Length != 10 || !code.StartsWith(digits, StringComparison.Ordinal)) continue;
            var path = new List<string>();
            for (string? at = sid; at is not null && nodes.TryGetValue(at, out var n) && path.Count < 20; at = n.Parent) path.Insert(0, n.Text);
            path.Insert(0, headingText);
            result.Add(Item(code, path, null));
        }
        return [.. result.Take(HsLookupRules.MaxResults)];
    }

    private static HsLookupItemDto Item(string code, IReadOnlyList<string> path, string? unit)
    {
        var clean = path.Select(p => p.TrimEnd(':').Trim()).Where(p => p.Length > 0).ToList();
        return new HsLookupItemDto("", clean.Count > 0 ? clean[^1] : "", HsLookupRules.DottedPrefix(code[..6]), HsLookupRules.DottedPrefix(code),
            string.Join(" > ", clean), string.IsNullOrWhiteSpace(unit) ? null : unit, null, false, []);
    }

    /// <summary>Mọi từ khóa có trong chữ (không phân biệt hoa thường / dấu); không có từ khóa → true.</summary>
    private static bool ContainsAll(string? text, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0) return true;
        var t = Fold(text ?? "");
        return terms.All(term => t.Contains(Fold(term), StringComparison.Ordinal));
    }

    private static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
    }

    private static IEnumerable<JsonElement> Rows(JsonElement root) =>
        root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object) : [];

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())?.Trim() is { Length: > 0 } s ? s : null
            : null;

    private static string Digits(string? s) => new([.. (s ?? "").Where(char.IsAsciiDigit)]);
}
