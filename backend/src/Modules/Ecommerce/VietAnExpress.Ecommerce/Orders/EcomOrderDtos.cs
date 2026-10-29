using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.Ecommerce.Stores;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Orders;

/// <summary>Khớp <c>EcomOrder</c> của frontend (API_CONTRACT.md §5).</summary>
internal sealed record EcomOrderDto(
    string Id, string Src, string Ref, string Bill, string Cnee, string Ct, int Items, decimal Kg, string St,
    string? Note, IReadOnlyList<EcomProductDto>? Products, string CreatedAt,
    decimal? Value, string? Currency, EcomReceiverDto Receiver, string? Service, string? Hub, string? Branch,
    IReadOnlyList<string> Issues, bool Editable, bool Confirmed, string? ConfirmedAt, bool Deletable = false);

/// <summary>Người nhận đầy đủ cho ngăn chi tiết đơn.</summary>
internal sealed record EcomReceiverDto(
    string? Name, string? Company, string? Phone, string? Email, string? Address1, string? Address2,
    string? City, string? State, string? Postal, string? CountryCode, string? Country);
