using FluentValidation;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.OrderData;

internal sealed class ReceiverTaxValidator : AbstractValidator<OrderPayload.ReceiverPart>
{
    public ReceiverTaxValidator()
    {
        When(r => EuCountries.IsEuCountry(EuCountries.ReceiverCode(r.Country, r.CountryCode)), () =>
        {
            RuleFor(r => r.IossNo).Must(v => string.IsNullOrWhiteSpace(v) || System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"\AIM[0-9]{10}\z"))
                .WithMessage("IOSS No phải có dạng IM và 10 chữ số");
            RuleFor(r => r.EoriNo).Must(v => string.IsNullOrWhiteSpace(v) || System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"\A[A-Z]{2}[A-Z0-9]{1,15}\z"))
                .WithMessage("EORI No phải gồm 2 chữ cái mã nước và 1–15 chữ cái hoặc chữ số");
        });
    }
}
