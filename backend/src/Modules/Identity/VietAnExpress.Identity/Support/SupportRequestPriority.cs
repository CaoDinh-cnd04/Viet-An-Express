
namespace VietAnExpress.Identity.Support;

/// <summary>Mức độ — khớp frontend.</summary>
internal static class SupportRequestPriority
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { "low", "mid", "high" };
}
