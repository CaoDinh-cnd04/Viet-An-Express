using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>Khớp <c>HsLookupItem</c> của frontend (API_CONTRACT.md §3). Không trả thuế suất.</summary>
/// <param name="Path">Các cấp cha trong biểu thuế (US ghi dạng "A &gt; B &gt; tên dòng") — rỗng nếu biểu thuế không có.</param>
internal sealed record HsLookupItemDto(
    string NameVi, string NameEn, string Hs6, string? NationalCode, string? NationalDescription,
    string? Unit, string? Note, bool IsVerified, IReadOnlyList<string> Path);

/// <summary>1 mặt hàng để khai invoice nhanh — khớp <c>SavedProduct</c> của frontend.</summary>
/// <param name="Id">Khóa mặt hàng (<see cref="ProductLibrary.KeyOf"/>) — dùng để đánh dấu yêu thích / xóa khỏi thư viện.</param>
internal sealed record SavedProductDto(string Id, string DescEn, string DescVi, string Manufacturer, string Origin, string Hs, string Unit, string Price, bool IsFavorite = false);

/// <summary>Invoice của 1 đơn gần đây — khớp <c>RecentInvoice</c> của frontend.</summary>
internal sealed record RecentInvoiceDto(string Bill, string Cnee, string Date, string Currency, IReadOnlyList<SavedProductDto> Items);

/// <summary>Body PUT /catalog/products/:id/favorite và /catalog/categories/:id/favorite.</summary>
internal sealed record FavoriteInput(bool IsFavorite);
