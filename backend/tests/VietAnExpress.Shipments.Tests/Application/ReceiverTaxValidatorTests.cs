using System.Text.Json;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class ReceiverTaxValidatorTests
{
    [Theory]
    [InlineData("DE", "IM1234567890", "DE123456789012345", true)]
    [InlineData("FR", "", "", true)]
    [InlineData("Germany", "IM1234567890", "DE123", true)]
    [InlineData("DE", "IM123", "DE123", false)]
    [InlineData("DE", "im1234567890", "DE123", false)]
    [InlineData("DE", "IM1234567890", "DE1234567890123456", false)]
    [InlineData("DE", "IM1234567890", "DE12-34", false)]
    [InlineData("GB", "invalid", "invalid", true)]
    [InlineData("NO", "invalid", "invalid", true)]
    [InlineData("CH", "invalid", "invalid", true)]
    public void Optional_identifiers_validate_only_for_eu(string country, string ioss, string eori, bool valid)
    {
        var receiver = new OrderPayload.ReceiverPart { Country = country, IossNo = ioss, EoriNo = eori };
        Assert.Equal(valid, new ReceiverTaxValidator().Validate(receiver).IsValid);
    }

    [Fact]
    public void Shared_list_contains_exactly_27_countries()
    {
        Assert.Equal(27, EuCountries.EU_COUNTRIES.Count);
        Assert.True(EuCountries.IsEuCountry(" de "));
        Assert.False(EuCountries.IsEuCountry("GB"));
        Assert.False(EuCountries.IsEuCountry("NO"));
        Assert.False(EuCountries.IsEuCountry("CH"));
    }

    [Fact]
    public void Iso_code_is_supported_but_stale_eu_code_does_not_override_non_eu_country()
    {
        var receiver = new OrderPayload.ReceiverPart { CountryCode = "DE", IossNo = "invalid" };
        Assert.False(new ReceiverTaxValidator().Validate(receiver).IsValid);
        receiver = new OrderPayload.ReceiverPart { Country = "United States", CountryCode = "DE", IossNo = "invalid" };
        Assert.True(new ReceiverTaxValidator().Validate(receiver).IsValid);
        var input = new DraftInput("draft", "", "United States", "", "", "", "", "",
            JsonSerializer.SerializeToElement(new { receiver = new { country = "United States", countryCode = "DE", iossNo = "invalid" } }));
        using var json = JsonDocument.Parse(input.PayloadJson);
        Assert.Equal("", json.RootElement.GetProperty("receiver").GetProperty("iossNo").GetString());
    }

    [Fact]
    public void Save_draft_validates_payload_and_clears_non_eu_identifiers()
    {
        var eu = Input("DE", "invalid");
        var result = new SaveDraftCommandValidator().Validate(new SaveDraftCommand(null, eu));
        Assert.Contains(result.Errors, e => e.PropertyName == "payload.receiver.iossNo");
        var nonEu = Input("GB", "invalid");
        Assert.True(new SaveDraftCommandValidator().Validate(new SaveDraftCommand(null, nonEu)).IsValid);
        using var json = JsonDocument.Parse(nonEu.PayloadJson);
        Assert.Equal("", json.RootElement.GetProperty("receiver").GetProperty("iossNo").GetString());
        Assert.Equal("", json.RootElement.GetProperty("receiver").GetProperty("eoriNo").GetString());
    }

    [Theory]
    [InlineData("DE", "IM1234567890", "DE123")]
    [InlineData("GB", null, null)]
    public void Issued_order_persists_identifiers_only_for_eu(string country, string? ioss, string? eori)
    {
        var payload = new OrderPayload { Receiver = new() { Country = country, IossNo = "IM1234567890", EoriNo = "DE123" } };
        var order = LegacyOrderFactory.FromPayload(payload, new LegacyCustomerRef(1, "Test", null, null, null), 90000001, DateTime.Today);
        Assert.Equal(ioss, order.ConsigneeIossNo);
        Assert.Equal(eori, order.ConsigneeEoriNo);
        var dto = LegacyOrderView.ToDetailDto(order, DateTime.Today);
        Assert.Equal(ioss ?? "", dto.Receiver!.IossNo);
        Assert.Equal(eori ?? "", dto.Receiver.EoriNo);
    }

    private static DraftInput Input(string country, string ioss) => new("draft", "", country, "", "", "", "", "",
        JsonSerializer.SerializeToElement(new { receiver = new { country, iossNo = ioss, eoriNo = "invalid" } }));
}
