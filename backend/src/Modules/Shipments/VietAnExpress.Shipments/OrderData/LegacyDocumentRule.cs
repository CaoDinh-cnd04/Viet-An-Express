using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.OrderData;

/// <summary>
/// Nhận diện đơn chứng từ (DOC) trong dbo.MaVanDon — bảng không có cột loại hàng nên dựa vào tên hàng (Ten_hang):
/// có "document", "chứng từ", "hồ sơ", hoặc từ riêng "doc" / "docs".
/// <see cref="Matches"/> (cột "Loại" hiển thị) và <see cref="IsDocument"/> (lọc bằng SQL) dùng CHUNG danh sách mẫu LIKE
/// nên bộ lọc DOC / PACK luôn khớp với cột hiển thị.
/// </summary>
internal static class LegacyDocumentRule
{
    /// <summary>Mẫu LIKE (không phân biệt hoa thường theo collation của database).</summary>
    public static readonly string[] Patterns =
    [
        "%document%", "%chứng từ%", "%hồ sơ%",
        "doc", "docs", "doc %", "docs %", "% doc", "% docs", "% doc %", "% docs %"
    ];

    /// <summary>Mẫu "chứa" trong <see cref="Patterns"/>; các mẫu còn lại là từ riêng <see cref="Words"/> (doc, docs).</summary>
    private static readonly string[] ContainsPatterns = ["%document%", "%chứng từ%", "%hồ sơ%"];

    private static readonly string[] Words = ["doc", "docs"];

    public static Expression<Func<LegacyOrder, bool>> IsDocument { get; } = Build();

    public static Expression<Func<LegacyOrder, bool>> IsNotDocument { get; } =
        Expression.Lambda<Func<LegacyOrder, bool>>(Expression.Not(IsDocument.Body), IsDocument.Parameters);

    /// <summary>Cùng kết quả với mẫu LIKE, tính trên bộ nhớ.</summary>
    public static bool Matches(string? goodsName) => goodsName is not null && Patterns.Any(p => Like(goodsName, p));

    private static bool Like(string value, string pattern)
    {
        var starts = pattern.StartsWith('%');
        var ends = pattern.EndsWith('%');
        var core = pattern.Trim('%');
        const StringComparison ci = StringComparison.OrdinalIgnoreCase;
        return (starts, ends) switch
        {
            (true, true) => value.Contains(core, ci),
            (true, false) => value.EndsWith(core, ci),
            (false, true) => value.StartsWith(core, ci),
            _ => value.Equals(core, ci)
        };
    }

    /// <summary>
    /// o => o.GoodsName != null && (UPPER(GoodsName) COLLATE BIN2 LIKE P1 || …) với mẫu in hoa — không phân biệt hoa thường, phân biệt dấu
    /// như collation cột (CI_AS), nhưng nhanh hơn ~14 lần (300k đơn: 7,5 giây → 0,5 giây, đo 06/11/2026).
    /// </summary>
    private static Expression<Func<LegacyOrder, bool>> Build()
    {
        var o = Expression.Parameter(typeof(LegacyOrder), "o");
        var name = Expression.Property(o, nameof(LegacyOrder.GoodsName));
        var like = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var collate = typeof(RelationalDbFunctionsExtensions).GetMethod(nameof(RelationalDbFunctionsExtensions.Collate))!.MakeGenericMethod(typeof(string));
        var upper = Expression.Call(collate, Expression.Constant(EF.Functions),
            Expression.Call(name, typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!), Expression.Constant(OrderTextSearch.Binary));
        // 8 mẫu từ riêng "doc" / "docs" (đứng một mình, đầu, cuối, giữa) = 2 mẫu trên tên hàng có thêm 1 dấu cách 2 đầu:
        // " " + TÊN + " " LIKE "% DOC %" — cùng kết quả, bớt 6 lần so mỗi dòng.
        var padded = Expression.Call(typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string), typeof(string)])!,
            Expression.Constant(" "), upper, Expression.Constant(" "));
        var any = ContainsPatterns
            .Select(p => (Expression)Expression.Call(like, Expression.Constant(EF.Functions), upper, Expression.Constant(p.ToUpperInvariant())))
            .Concat(Words.Select(w => (Expression)Expression.Call(like, Expression.Constant(EF.Functions), padded,
                Expression.Constant($"% {w.ToUpperInvariant()} %"))))
            .Aggregate(Expression.OrElse);
        var body = Expression.AndAlso(Expression.NotEqual(name, Expression.Constant(null, typeof(string))), any);
        return Expression.Lambda<Func<LegacyOrder, bool>>(body, o);
    }
}
