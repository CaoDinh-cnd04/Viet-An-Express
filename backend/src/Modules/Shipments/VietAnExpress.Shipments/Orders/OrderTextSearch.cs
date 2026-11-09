using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments.OrderData;

namespace VietAnExpress.Shipments.Orders;

/// <summary>
/// So "chứa" 1 từ khóa trên cột dbo.MaVanDon — nhanh mà vẫn không phân biệt hoa / thường, dấu tiếng Việt (kể cả đ / Đ).
/// <para>
/// So bằng collation bỏ dấu (Latin1_General_100_CI_AI) tốn ~1 giây / cột / 300k đơn; so nhị phân trên chữ in hoa
/// (<c>UPPER(cột) COLLATE BIN2</c>) chỉ ~0,13 giây (đo 06/11/2026). Nên:
/// </para>
/// <list type="bullet">
/// <item>Cột mã (bill, mã hãng, Ref) và từ khóa không có chữ cái (số, ký hiệu): chỉ so nhị phân chữ in hoa — kết quả y như bỏ dấu.</item>
/// <item>Cột chữ (tên, nước, dịch vụ): so nhị phân với từ khóa đã bỏ dấu + in hoa; chỉ dòng có ký tự ngoài ASCII (có dấu) mới so thêm
/// bằng collation bỏ dấu. Dữ liệu người nhận nước ngoài gần hết là ASCII nên phần đắt hầu như không chạy — kết quả vẫn y như cũ.</item>
/// </list>
/// </summary>
internal static class OrderTextSearch
{
    public const string AccentInsensitive = "Latin1_General_100_CI_AI";
    public const string Binary = "Latin1_General_100_BIN2";

    /// <summary>Có ký tự ngoài ASCII in được (chữ có dấu, đ…) — so theo điểm mã khi dùng <see cref="Binary"/>.</summary>
    private const string NonAscii = "%[^ -~]%";

    /// <summary>Từ khóa đã chuẩn bị: mẫu LIKE gốc (so bỏ dấu), mẫu LIKE bỏ dấu + in hoa (so nhị phân), có chữ cái hay không.</summary>
    internal sealed record Term(string Like, string Upper, bool HasLetters)
    {
        public static Term Of(string text) =>
            new(text.ToLikePattern()!, Fold(text).ToLikePattern()!, text.Any(char.IsLetter));
    }

    /// <summary>Bỏ dấu tiếng Việt / Latin (kể cả đ → d) rồi in hoa: "Xoài sấy Đà Nẵng" → "XOAI SAY DA NANG". Hàm thuần — có test.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    /// <summary>Cột mã (bill, mã hãng, Ref): không phân biệt hoa thường, so nhị phân.</summary>
    public static Expression<Func<LegacyOrder, bool>> Code(Expression<Func<LegacyOrder, string?>> column, Term t) =>
        On(column, s => s != null && EF.Functions.Like(EF.Functions.Collate(s.ToUpper(), Binary), t.Upper));

    /// <summary>Cột chữ (tên, nước, dịch vụ): không phân biệt hoa thường và dấu.</summary>
    public static Expression<Func<LegacyOrder, bool>> Text(Expression<Func<LegacyOrder, string?>> column, Term t) =>
        t.HasLetters
            ? On(column, s => s != null && (EF.Functions.Like(EF.Functions.Collate(s.ToUpper(), Binary), t.Upper)
                || (EF.Functions.Like(EF.Functions.Collate(s, Binary), NonAscii)
                    && EF.Functions.Like(EF.Functions.Collate(s, AccentInsensitive), t.Like))))
            : Code(column, t);

    /// <summary>Số bill VA (OrderNumber, bigint) chứa chuỗi số đã gõ — đổi sang chữ rồi so nhị phân.</summary>
    public static Expression<Func<LegacyOrder, bool>> Number(Term t) =>
        o => o.OrderNumber != null && EF.Functions.Like(EF.Functions.Collate(o.OrderNumber.Value.ToString(), Binary), t.Like);

    /// <summary>Ghép điều kiện trên 1 chuỗi với cột của đơn: column = o => o.X, predicate = s => … → o => predicate(o.X).</summary>
    private static Expression<Func<LegacyOrder, bool>> On(Expression<Func<LegacyOrder, string?>> column, Expression<Func<string?, bool>> predicate)
    {
        var body = new Replace(predicate.Parameters[0], column.Body).Visit(predicate.Body)!;
        return Expression.Lambda<Func<LegacyOrder, bool>>(body, column.Parameters);
    }

    private sealed class Replace(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
