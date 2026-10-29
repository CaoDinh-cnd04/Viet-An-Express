using System.Linq.Expressions;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Orders;

/// <summary>
/// Thống kê cùng tập đơn bằng một truy vấn SQL, dùng lại điều kiện trạng thái chuẩn.
/// Ngoài tổng chung còn có tổng kiện / cân của từng nhóm trạng thái để khi lọc 1 nhóm, tổng khớp danh sách đang xem
/// mà không cần thêm truy vấn. Các nhóm có thể trùng nhau nên lọc từ 2 nhóm phải tính lại trên tập đã lọc (xem GetOrdersHandler).
/// </summary>
internal static class OrderSummaryQuery
{
    private sealed record Row(bool Waiting, bool InTransit, bool NotDelivered, bool Delivered, int Pieces, decimal Weight);

    internal sealed record Totals(int All, int Waiting, int InTransit, int NotDelivered, int Delivered, int Pieces, decimal Weight,
        int WaitingPieces, decimal WaitingWeight, int InTransitPieces, decimal InTransitWeight, int NotDeliveredPieces, decimal NotDeliveredWeight,
        int DeliveredPieces, decimal DeliveredWeight)
    {
        public static readonly Totals Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        /// <summary>Lọc từ 2 nhóm (chưa đủ cả 4) — các nhóm trùng nhau nên không cộng được, phải tính lại trên tập đã lọc.</summary>
        public static bool NeedsRecount(IReadOnlyCollection<string> statuses) => statuses.Count >= 2 && statuses.Count < LegacyOrderStatus.All.Length;

        /// <summary>Tổng kiện / cân khi không lọc / chọn đủ 4 nhóm (tổng chung) hoặc lọc đúng 1 nhóm.</summary>
        public (int Pieces, decimal Weight) For(IReadOnlyCollection<string> statuses)
        {
            if (statuses.Count != 1) return (Pieces, Weight);
            return statuses.First() switch
            {
                LegacyOrderStatus.Waiting => (WaitingPieces, WaitingWeight),
                LegacyOrderStatus.InTransit => (InTransitPieces, InTransitWeight),
                LegacyOrderStatus.NotDelivered => (NotDeliveredPieces, NotDeliveredWeight),
                LegacyOrderStatus.Delivered => (DeliveredPieces, DeliveredWeight),
                _ => (0, 0m)
            };
        }

        public Dictionary<string, int> Counts() => new()
        {
            ["all"] = All, [LegacyOrderStatus.Waiting] = Waiting,
            [LegacyOrderStatus.InTransit] = InTransit, [LegacyOrderStatus.NotDelivered] = NotDelivered,
            [LegacyOrderStatus.Delivered] = Delivered
        };
    }

    public static IQueryable<Totals> Build(IQueryable<LegacyOrder> orders, DateTime today)
    {
        var parameter = Expression.Parameter(typeof(LegacyOrder), "order");
        Expression Condition(string code)
        {
            var predicate = LegacyOrderStatus.Is(code, today);
            return new ReplaceParameter(predicate.Parameters[0], parameter).Visit(predicate.Body)!;
        }
        var constructor = typeof(Row).GetConstructors().Single();
        // Each COUNT evaluates its own predicate instead of repeating a multi-branch status CASE.
        // Supplying constructor members lets EF inline this projection into SQL aggregates.
        var row = Expression.New(constructor, [Condition(LegacyOrderStatus.Waiting),
            Condition(LegacyOrderStatus.InTransit), Condition(LegacyOrderStatus.NotDelivered),
            Condition(LegacyOrderStatus.Delivered),
            Expression.Coalesce(Expression.Property(parameter, nameof(LegacyOrder.Pieces)), Expression.Constant(0)),
            Expression.Coalesce(Expression.Property(parameter, nameof(LegacyOrder.WeightKg)), Expression.Constant(0m))],
            constructor.GetParameters().Select(p => typeof(Row).GetProperty(p.Name!)!));
        var projection = Expression.Lambda<Func<LegacyOrder, Row>>(row, parameter);

        return orders.Select(projection).GroupBy(_ => 1).Select(g => new Totals(
            g.Count(), g.Count(o => o.Waiting), g.Count(o => o.InTransit),
            g.Count(o => o.NotDelivered), g.Count(o => o.Delivered),
            g.Sum(o => o.Pieces), g.Sum(o => o.Weight),
            g.Sum(o => o.Waiting ? o.Pieces : 0), g.Sum(o => o.Waiting ? o.Weight : 0m),
            g.Sum(o => o.InTransit ? o.Pieces : 0), g.Sum(o => o.InTransit ? o.Weight : 0m),
            g.Sum(o => o.NotDelivered ? o.Pieces : 0), g.Sum(o => o.NotDelivered ? o.Weight : 0m),
            g.Sum(o => o.Delivered ? o.Pieces : 0), g.Sum(o => o.Delivered ? o.Weight : 0m)));
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
