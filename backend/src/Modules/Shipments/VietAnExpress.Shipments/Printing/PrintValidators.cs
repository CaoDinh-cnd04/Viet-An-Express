using FluentValidation;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.Printing;

internal sealed class PrintOrdersQueryValidator : AbstractValidator<PrintOrdersQuery>
{
    public PrintOrdersQueryValidator()
    {
        // doc có thể là nhiều loại ngăn bằng dấu phẩy: "bill-a4,invoice,label-a6"
        RuleFor(x => x.Doc)
            .Must(d => PrintDocs.Parse(d) is { Count: > 0 } docs && docs.All(PrintDocs.All.Contains))
            .WithMessage("Loại chứng từ không hợp lệ (bill-a4, invoice, cvck, label-a6 — nhiều loại ngăn bằng dấu phẩy)");
        RuleFor(x => x.Bills)
            .NotEmpty().WithMessage("Chọn ít nhất 1 đơn để in")
            .Must(b => b.Count <= PrintOrdersQuery.MaxBills).WithMessage($"In tối đa {PrintOrdersQuery.MaxBills} đơn mỗi lần");
    }
}
