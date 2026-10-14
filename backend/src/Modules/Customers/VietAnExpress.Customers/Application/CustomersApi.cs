using Microsoft.EntityFrameworkCore;
using System.Data.Common;
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

    public async Task<CustomerSummary?> UpdateSenderInfoAsync(long customerId, SenderInfoUpdate update, CancellationToken cancellationToken = default)
    {
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (c is null) return null;
        if (c.UpdateSenderInfo(update)) await db.SaveChangesAsync(cancellationToken);
        return ToSummary(c);
    }

    public async Task<SenderInfoWriteResult> UpdateSenderInfoInTransactionAsync(long customerId, SenderInfoUpdate update, SenderInfoUpdate expected,
        DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        db.ChangeTracker.Clear(); // A rolled-back retry must not reuse the previous attempt's tracked profile.
        db.Database.SetDbConnection(transaction.Connection!, contextOwnsConnection: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        try
        {
            var c = await db.Customers.FromSqlInterpolated($"SELECT * FROM dbo.TCustomer WITH (UPDLOCK, HOLDLOCK) WHERE CustomerID = {customerId}")
                .SingleOrDefaultAsync(cancellationToken);
            if (c is null) return new(null, false);
            var live = ToSummary(c);
            static bool Different(string? change, string? old, string? now) => change is not null && (old ?? "") != (now ?? "");
            var outdated = Different(update.CompanyName, expected.CompanyName, live.CompanyName)
                || Different(update.ContactName, expected.ContactName, live.ContactName) || Different(update.Phone, expected.Phone, live.Phone)
                || Different(update.Email, expected.Email, live.Email) || Different(update.Address, expected.Address, live.Address)
                || Different(update.TaxCode, expected.TaxCode, live.TaxCode);
            if (outdated) return new(live, true);
            if (c.UpdateSenderInfo(update)) await db.SaveChangesAsync(cancellationToken);
            return new(ToSummary(c), false);
        }
        finally { await db.Database.UseTransactionAsync((DbTransaction?)null, CancellationToken.None); }
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
