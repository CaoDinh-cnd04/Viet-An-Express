
namespace VietAnExpress.Identity.Support;

/// <summary>Tệp đính kèm khách gửi (đã đọc vào bộ nhớ) — chỉ gửi kèm email về hộp thư Việt An.</summary>
internal sealed record SupportRequestFile(string Name, string ContentType, byte[] Data);
