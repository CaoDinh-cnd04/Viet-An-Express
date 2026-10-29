using System.Text.Unicode;
using Microsoft.Extensions.Configuration;
using VietAnExpress.Identity.Notifications;

namespace VietAnExpress.Identity.Shared;

internal sealed record EmailField(string Label, string Value);

/// <summary>1 khối nội dung: tiêu đề nhỏ (không bắt buộc), đoạn chữ nhiều dòng và / hoặc bảng nhãn – giá trị, ảnh (link tuyệt đối) và link riêng của khối.</summary>
/// <see cref="Html"/>: nội dung có định dạng đã lọc (<see cref="RichText.Sanitize"/>) — có thì hiện thay cho <see cref="Text"/> ở bản HTML.
internal sealed record EmailSection(
    string? Heading, string? Text = null, IReadOnlyList<EmailField>? Fields = null, IReadOnlyList<string>? Images = null, EmailLink? Link = null,
    string? Html = null);

internal sealed record EmailLink(string Label, string Url);

/// <summary>Nội dung 1 email theo khung chung của Việt An.</summary>
internal sealed record EmailContent(
    string Title,
    IReadOnlyList<EmailSection> Sections,
    string? Badge = null,
    string? Meta = null,
    EmailLink? Button = null,
    EmailLink? SecondaryLink = null,
    string? FooterNote = null)
{
    /// <summary>Dòng xem trước hiện cạnh tiêu đề trong danh sách thư.</summary>
    public string Preheader => Sections.Select(s => s.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) is { } t
        ? (t.ReplaceLineEndings(" ").Trim() is var one && one.Length > 120 ? one[..120] + "…" : one)
        : Title;
}
