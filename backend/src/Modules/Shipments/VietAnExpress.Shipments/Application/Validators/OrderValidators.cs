using FluentValidation;
using System.Text.Json;
using FluentValidation.Results;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Application.Orders.Documents;
using VietAnExpress.Shipments.Domain;

namespace VietAnExpress.Shipments.Application.Validators;

internal sealed class SaveDraftCommandValidator : AbstractValidator<SaveDraftCommand>
{
    /// <summary>Giới hạn kích thước form lưu kèm nháp (ký tự JSON).</summary>
    private const int MaxPayloadChars = 256 * 1024;

    public SaveDraftCommandValidator()
    {
        RuleFor(x => x.Draft).NotNull();
        RuleFor(x => x.Draft.Stt)
            .Must(s => s is DraftInput.StatusDraft or DraftInput.StatusReady)
            .WithMessage("Trạng thái nháp phải là draft hoặc ready")
            .OverridePropertyName("stt");
        RuleFor(x => x.Draft.PayloadJson.Length)
            .LessThanOrEqualTo(MaxPayloadChars).WithMessage("Dữ liệu đơn quá lớn")
            .OverridePropertyName("payload");
        RuleFor(x => x.Draft.Payload).Custom((payload, context) =>
        {
            if (payload is not { ValueKind: JsonValueKind.Object } json || !json.TryGetProperty("receiver", out var receiver)) return;
            try
            {
                var value = receiver.Deserialize<OrderPayload.ReceiverPart>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (value is null) return;
                foreach (var error in new ReceiverTaxValidator().Validate(value).Errors)
                    context.AddFailure(new ValidationFailure($"payload.receiver.{char.ToLowerInvariant(error.PropertyName[0])}{error.PropertyName[1..]}", error.ErrorMessage));
            }
            catch (JsonException)
            {
                context.AddFailure("payload.receiver", "Dữ liệu người nhận không hợp lệ");
            }
        });
    }
}

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
