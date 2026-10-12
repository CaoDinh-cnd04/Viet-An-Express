using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.Infrastructure;

namespace VietAnExpress.Customers.Application;

internal sealed class CustomersApi(CustomersDbContext db) : ICustomersApi
{
    public async Task<CustomerSummary?> GetByIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        return c is null ? null : ToSummary(c);
    }

    public async Task<IReadOnlyList<CustomerSummary>> GetByIdsAsync(IReadOnlyCollection<long> customerIds, CancellationToken cancellationToken = default)
    {
        var result = new List<CustomerSummary>(customerIds.Count);
        // Chia lô: SQL Server giới hạn ~2100 tham số mỗi câu lệnh.
        foreach (var chunk in customerIds.Distinct().Chunk(1000))
        {
            var rows = await db.Customers.AsNoTracking().Where(x => chunk.Contains(x.Id)).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(ToSummary));
        }
        return result;
    }

    /// <summary>
    /// Tên / email / SĐT: ưu tiên thông tin người liên hệ, không có thì lấy của công ty.
    /// SĐT: ContactPhone → Tel → Phone (dữ liệu cũ hay ghi lẫn tên vào cột Phone, Tel thường chỉ có số).
    /// </summary>
    internal static CustomerSummary ToSummary(LegacyCustomer c) => new(
        c.Id,
        Clean(c.Code) ?? c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Clean(c.CompanyName) ?? Clean(c.ContactName) ?? "",
        Clean(c.ContactName),
        Clean(c.ContactEmail) ?? Clean(c.Email),
        Clean(c.ContactPhone) ?? Clean(c.Tel) ?? Clean(c.Phone),
        Clean(c.Address1),
        Clean(c.TaxCode));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
