using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Geo;

/// <summary>Quốc gia: mã ISO 2 ký tự, tên tiếng Anh (khớp cột ConsigneeCountry cũ), mã điện thoại.</summary>
internal sealed record CountryInfo(string Code, string Name, string? DialCode);

/// <summary>
/// Kết quả tra mã bưu chính. City / State / StateCode = nơi đầu tiên (giữ tương thích);
/// <see cref="Places"/> = mọi nơi khác nhau dùng chung mã này — nhiều thành phố thì form để khách tự chọn, không điền bừa.
/// </summary>
internal sealed record PostalInfo(string CountryCode, string PostalCode, string City, string? State, string? StateCode)
{
    public IReadOnlyList<PostalPlace> Places { get; init; } = [];
}

/// <summary>
/// 1 nơi trong mã bưu chính. <see cref="Area"/> = khu phố / bưu cục nhỏ hơn thành phố (colonia ở Mexico, locality ở Ấn Độ),
/// khách ghi vào dòng địa chỉ; null ở các nước tên địa danh đã là thành phố.
/// </summary>
internal sealed record PostalPlace(string City, string? State, string? StateCode, string? Area);

/// <summary>1 gợi ý khi khách đang gõ mã bưu chính: mã đầy đủ + nơi (cùng quy tắc với <see cref="PostalPlace"/>).</summary>
internal sealed record PostalSuggestion(string PostalCode, string City, string? State, string? StateCode, string? Area);
