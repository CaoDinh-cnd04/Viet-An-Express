using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Import;

internal static class ImportErrors
{
    public static readonly Error NotExcel = Error.Validation("IMPORT_NOT_XLSX", "Chỉ nhận file Excel .xlsx — vui lòng dùng file mẫu.");
    public static readonly Error TooLarge = Error.Validation("IMPORT_TOO_LARGE", $"File quá lớn (tối đa {ImportOrdersCommand.MaxBytes / 1024 / 1024} MB).");
    public static readonly Error HubRequired = Error.Validation("IMPORT_HUB_REQUIRED", "Đã chọn dịch vụ thì cần chọn Hub.");
    public static readonly Error Empty = Error.Validation("IMPORT_EMPTY", "File không có dòng dữ liệu nào (dữ liệu bắt đầu từ dòng 3 của sheet DATA).");
    public static readonly Error NothingValid = Error.BusinessRule("IMPORT_NOTHING_VALID", "Không có dòng hợp lệ nào để tạo đơn — sửa lỗi trong file rồi tải lên lại.");
    public static Error Unreadable(string message) => Error.Validation("IMPORT_UNREADABLE", message);
    public static Error MissingColumns(IEnumerable<string> columns) =>
        Error.Validation("IMPORT_MISSING_COLUMNS", $"File thiếu cột: {string.Join(", ", columns)}. Vui lòng dùng đúng file mẫu.");
    public static Error TooManyRows(int count) =>
        Error.Validation("IMPORT_TOO_MANY_ROWS", $"Mỗi lần tối đa {ImportOrdersCommand.MaxRows} đơn — file có {count} dòng. Vui lòng tách file.");
}
