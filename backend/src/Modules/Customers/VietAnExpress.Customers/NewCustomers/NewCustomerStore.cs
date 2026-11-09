using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.Shared;

namespace VietAnExpress.Customers.NewCustomers;

/// <summary>
/// Ghi dòng dbo.TCustomer cho khách mới (<see cref="NewCustomerRules"/>). SQL trực tiếp vì model TCustomer của portal chỉ map vài cột,
/// còn bảng có cột NOT NULL không mặc định (RegionID, Tiem_Nang_ID, KH_Cong_Ty, Total_*) — điền như dữ liệu CMS đang có.
/// Chỉ đụng dòng còn đánh dấu khách mới: CS đã hoàn thiện ở CMS (đổi CustomerSource) thì portal không ghi nữa.
/// </summary>
internal sealed class NewCustomerStore(CustomersDbContext db)
{
    /// <summary>
    /// Tạo khách mới (Staff_ID = Reg_ID = nhân viên đang làm, để trong phạm vi của họ). Nhân viên đã có khách mới chưa điền tên
    /// (bấm nút rồi bỏ dở, chưa lưu đơn nào) thì dùng lại dòng đó — không đẻ dòng rác.
    /// </summary>
    public async Task<long> CreateAsync(int staffId, CancellationToken ct)
    {
        var marker = NewCustomerRules.Marker;
        var unused = await db.Database.SqlQuery<long>($"""
            SELECT TOP 1 [CustomerID] AS [Value] FROM [dbo].[TCustomer]
            WHERE [CustomerSource] = {marker} AND [Staff_ID] = {staffId} AND [Reg_ID] = {staffId}
              AND ([CustomerName] IS NULL OR LTRIM(RTRIM([CustomerName])) = N'')
            ORDER BY [CustomerID] DESC
            """).ToListAsync(ct);
        if (unused.Count > 0) return unused[0];

        var created = await db.Database.SqlQuery<long>($"""
            INSERT INTO [dbo].[TCustomer]
                ([CustomerSource], [Staff_ID], [Reg_ID], [RegDate], [Modify_ID], [Modify_Date],
                 [RegionID], [Tiem_Nang_ID], [KH_Cong_Ty], [Total_Contract], [Total_Payment])
            OUTPUT INSERTED.[CustomerID] AS [Value]
            VALUES ({marker}, {staffId}, {staffId}, GETDATE(), {staffId}, GETDATE(), 1, 0, 0, 0, 0)
            """).ToListAsync(ct);
        return created[0];
    }

    /// <summary>Thông tin người gửi của đơn vừa lưu → hồ sơ khách mới. Ô trống giữ giá trị cũ. false = không còn là khách mới.</summary>
    public async Task<bool> FillAsync(long customerId, NewCustomerInfo info, int modifiedBy, CancellationToken ct)
    {
        var v = NewCustomerRules.Clean(info);
        var marker = NewCustomerRules.Marker;
        var rows = await db.Database.ExecuteSqlAsync($"""
            UPDATE [dbo].[TCustomer] SET
                [CustomerName] = COALESCE({v.CompanyName}, [CustomerName]),
                [ContactName] = COALESCE({v.ContactName}, [ContactName]),
                [ContactPhone] = COALESCE({v.Phone}, [ContactPhone]),
                [Tel] = COALESCE({v.Phone}, [Tel]),
                [Address1] = COALESCE({v.Address}, [Address1]),
                [TaxCode] = COALESCE({v.TaxCode}, [TaxCode]),
                [ContactEmail] = COALESCE({v.Email}, [ContactEmail]),
                [Modify_ID] = {modifiedBy}, [Modify_Date] = GETDATE()
            WHERE [CustomerID] = {customerId} AND [CustomerSource] = {marker}
            """, ct);
        return rows > 0;
    }

    /// <summary>Gán nhân viên phụ trách (TCustomer.Staff_ID) cho khách mới. false = không còn là khách mới / không có khách.</summary>
    public async Task<bool> AssignManagerAsync(long customerId, int managerId, int modifiedBy, CancellationToken ct)
    {
        var marker = NewCustomerRules.Marker;
        var rows = await db.Database.ExecuteSqlAsync($"""
            UPDATE [dbo].[TCustomer] SET [Staff_ID] = {managerId}, [Modify_ID] = {modifiedBy}, [Modify_Date] = GETDATE()
            WHERE [CustomerID] = {customerId} AND [CustomerSource] = {marker}
            """, ct);
        return rows > 0;
    }
}
