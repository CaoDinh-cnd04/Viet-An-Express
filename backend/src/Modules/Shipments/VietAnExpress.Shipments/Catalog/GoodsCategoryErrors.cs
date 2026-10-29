using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal static class GoodsCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound("CATEGORY_NOT_FOUND", "Không tìm thấy nhóm hàng (nhóm chung của Việt An không sửa / xóa được)");
    public static readonly Error NameRequired = Error.Validation("CATEGORY_NAME_REQUIRED", "Nhập tên nhóm hàng");
    public static readonly Error NameTooLong = Error.Validation("CATEGORY_NAME_TOO_LONG", $"Tên nhóm tối đa {GoodsCategory.NameMaxLength} ký tự");
    public static Error Duplicate(string name) => Error.Conflict("CATEGORY_DUPLICATE", $"Đã có nhóm hàng \"{name}\"");
}
