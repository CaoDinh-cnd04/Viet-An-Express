using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal static class CatalogMarkErrors
{
    public static readonly Error InvalidKey = Error.Validation("PRODUCT_KEY_INVALID", "Mã mặt hàng không hợp lệ");
}
