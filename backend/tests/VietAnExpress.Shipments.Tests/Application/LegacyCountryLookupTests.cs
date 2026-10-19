using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class LegacyCountryLookupTests
{
    private static readonly IReadOnlyList<LegacyCountryRow> Countries =
    [
        new(191, "SG", "Singapore"),
        new(224, "GB", "United Kingdom"),
        new(225, "US", "United States"),
        new(231, "VN", "Vietnam"),
        new(258, "CN", "China"),
        new(76, null, "Gaza Strip")
    ];

    [Theory]
    [InlineData("SG", "Singapore", 191)]
    [InlineData(" us ", "United States of America", 225)]
    [InlineData("CN", "China", 258)]
    [InlineData("GB", "Singapore", 224)]
    [InlineData("", " singapore ", 191)]
    [InlineData(null, "VIETNAM", 231)]
    [InlineData("", "Gaza Strip", 76)]
    [InlineData("ZZ", "Singapore", null)]
    [InlineData("", "Unknown", null)]
    [InlineData(null, null, null)]
    [InlineData(" ", " ", null)]
    public void Resolves_legacy_ID_by_code_or_by_name_for_old_drafts(string? code, string? name, int? expected) =>
        Assert.Equal(expected, LegacyCountryLookup.FindId(Countries, code, name));

    [Fact]
    public void Ambiguous_codes_are_not_assigned_an_arbitrary_ID()
    {
        LegacyCountryRow[] countries = [new(1, "US", "United States"), new(2, "US", "Other")];
        Assert.Null(LegacyCountryLookup.FindId(countries, "US", "United States"));
    }
}
