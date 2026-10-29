using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Import;

/// <summary>Danh mục nước cho import: API world-countries (tên + mã điện thoại) bổ sung bằng danh mục vùng của .NET khi API lỗi.</summary>
internal static class ImportCountries
{
    public static IReadOnlyDictionary<string, ImportCountry> Build(IReadOnlyList<CountryInfo> fromApi)
    {
        var map = new Dictionary<string, ImportCountry>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in fromApi)
            if (c.Code.Length == 2) map.TryAdd(c.Code.ToUpperInvariant(), new ImportCountry(c.Code.ToUpperInvariant(), c.Name, c.DialCode));
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;
            try { region = new RegionInfo(culture.Name); }
            catch (ArgumentException) { continue; }
            var code = region.TwoLetterISORegionName.ToUpperInvariant();
            if (code.Length == 2 && char.IsAsciiLetter(code[0]) && char.IsAsciiLetter(code[1]))
                map.TryAdd(code, new ImportCountry(code, region.EnglishName, null));
        }
        return map;
    }
}
