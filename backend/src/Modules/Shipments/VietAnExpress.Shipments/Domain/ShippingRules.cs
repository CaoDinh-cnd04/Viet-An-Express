namespace VietAnExpress.Shipments.Domain;

/// <summary>
/// Quy tắc tính cân — PHẢI khớp frontend (web/src/features/create-order/constants.ts, RULES).
/// Đổi ở đây thì đổi cả frontend.
/// </summary>
internal static class ShippingRules
{
    /// <summary>Hệ số quy đổi thể tích: cân quy đổi (kg) = D × R × C (cm) / 5000.</summary>
    public const decimal VolumetricDivisor = 5000m;

    /// <summary>Chứng từ nặng hơn mức này tự chuyển thành hàng hoá (hãng không nhận DOC quá 2kg).</summary>
    public const decimal DocumentMaxWeightKg = 2m;

    public const int MaxPackageLines = 100;

    public static decimal Round(decimal kg) => decimal.Round(kg, 2, MidpointRounding.AwayFromZero);

    /// <summary>Cân tính cước = max(cân thực, cân quy đổi).</summary>
    public static decimal ChargeableWeight(decimal actualKg, decimal volumetricKg) => Math.Max(actualKg, volumetricKg);

    // Rules for ordinary customer packaging; the divisor is always 5,000.
    public static WeightRule Rule(string? carrier = null, string? hub = null)
    {
        var name = $"{carrier} {hub}".Trim().ToUpperInvariant();
        if (name == "UPS" || name.StartsWith("UPS ") || name.StartsWith("UPS|"))
            return new("nearest", name.Contains("EXPEDITED") ? 1m : 0.5m, 0);
        if (name == "DHL" || name.StartsWith("DHL ") || name.StartsWith("DHL|")) return new("none", 0.5m, 0.5m, 30m, 1m);
        if (name == "FEDEX" || name.StartsWith("FEDEX ") || name.StartsWith("FEDEX|"))
            return new("up", 0, 0.5m, name.Contains("FICP") ? 32.5m : 20.5m, 1m);
        return new("none", 0, 0);
    }

    public static decimal Dimension(decimal cm, WeightRule rule) => rule.Dimension switch
    {
        "nearest" => decimal.Round(Math.Max(0, cm), 0, MidpointRounding.AwayFromZero),
        "up" => decimal.Ceiling(Math.Max(0, cm)),
        _ => Math.Max(0, cm)
    };

    public static decimal RoundUp(decimal kg, decimal step) => step > 0 ? decimal.Ceiling(Math.Max(0, kg) / step) * step : Math.Max(0, kg);

    public static decimal Volume(decimal length, decimal width, decimal height, WeightRule rule) =>
        Dimension(length, rule) * Dimension(width, rule) * Dimension(height, rule) / VolumetricDivisor;

    public static decimal PieceWeight(decimal actual, decimal volume, WeightRule rule) => RoundUp(Math.Max(actual, volume), rule.PieceStep);

    public static decimal ShipmentWeight(decimal kg, WeightRule rule) =>
        Round(RoundUp(kg, rule.Threshold is { } threshold && kg > threshold ? rule.AboveStep : rule.ShipmentStep));
}

internal sealed record WeightRule(string Dimension, decimal PieceStep, decimal ShipmentStep, decimal? Threshold = null, decimal AboveStep = 0);
