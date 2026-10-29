using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Import;

/// <summary>1 dòng kết quả: số liệu xem trước + lỗi / cảnh báo; <see cref="Bill"/> có khi đã tạo đơn.</summary>
internal sealed record ImportRowDto(
    int Line, string Ref, string Type, string Consignee, string CountryCode, string Country, string City,
    int Pieces, decimal WeightKg, decimal ChargeableKg, decimal Value, string Currency, int Products,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, string? Bill);

internal sealed record ImportResultDto(int Total, int Valid, int Invalid, int Created, IReadOnlyList<ImportRowDto> Rows);
