using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.Drafts;

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
