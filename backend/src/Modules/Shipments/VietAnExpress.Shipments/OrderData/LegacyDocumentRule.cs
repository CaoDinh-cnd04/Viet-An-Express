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

    /// <summary>o => o.GoodsName != null && (EF.Functions.Like(o.GoodsName, p1) || …).</summary>
    private static Expression<Func<LegacyOrder, bool>> Build()
    {
        var o = Expression.Parameter(typeof(LegacyOrder), "o");
        var name = Expression.Property(o, nameof(LegacyOrder.GoodsName));
        var like = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var any = Patterns
            .Select(p => (Expression)Expression.Call(like, Expression.Constant(EF.Functions), name, Expression.Constant(p)))
            .Aggregate(Expression.OrElse);
        var body = Expression.AndAlso(Expression.NotEqual(name, Expression.Constant(null, typeof(string))), any);
        return Expression.Lambda<Func<LegacyOrder, bool>>(body, o);
    }
}
