using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.AddressBook;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>Gợi ý mô tả hàng theo nhóm — khớp <c>Category.suggestions</c> của frontend.</summary>
internal sealed record CategorySuggestionDto(string En, string Vi, string Hs);

/// <summary>1 nhóm hàng — khớp <c>Category</c> của frontend (web/src/features/create-order/api.ts).</summary>
/// <param name="IsOwn">Nhóm khách tự tạo (được sửa / xóa); false = nhóm chung Việt An.</param>
internal sealed record GoodsCategoryDto(string Id, string Name, bool IsFavorite, bool IsOwn, IReadOnlyList<CategorySuggestionDto> Suggestions);

/// <summary>Body POST / PUT /catalog/categories.</summary>
internal sealed record GoodsCategoryInput(string? Name, bool IsFavorite);
