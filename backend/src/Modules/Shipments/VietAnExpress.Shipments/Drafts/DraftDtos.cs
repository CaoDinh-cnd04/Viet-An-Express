using System.Text.Json;
using System.Text.Json.Nodes;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>Body POST / PUT /drafts — khớp NewDraft của frontend (web/src/features/drafts/api.ts).</summary>
internal sealed record DraftInput(
    string Stt,
    string? Cnee,
    string? Ct,
    string? Service,
    string? Branch,
    string? Ref,
    string? Pcs,
    string? Content,
    JsonElement? Payload)
{
    public const string StatusDraft = "draft";
    public const string StatusReady = "ready";

    public string PayloadJson
    {
        get
        {
            if (Payload is not { ValueKind: JsonValueKind.Object } p) return "{}";
            var node = JsonNode.Parse(p.GetRawText())!;
            if (node["receiver"] is JsonObject receiver)
            {
                var country = receiver["country"] is JsonValue cv && cv.TryGetValue<string>(out var c) ? c : "";
                var code = receiver["countryCode"] is JsonValue cc && cc.TryGetValue<string>(out var iso) ? iso : "";
                if (!EuCountries.IsEuCountry(EuCountries.ReceiverCode(country, code)))
                {
                    receiver["iossNo"] = "";
                    receiver["eoriNo"] = "";
                }
            }
            return node.ToJsonString();
        }
    }

    /// <summary>Form tạo đơn đã gửi; không đọc được → null.</summary>
    public OrderPayload? ReadPayload()
    {
        try { return JsonSerializer.Deserialize<OrderPayload>(PayloadJson, DraftOrders.Json); }
        catch (JsonException) { return null; }
    }
}

/// <summary>1 dòng ở "Đơn nháp &amp; chưa in" — khớp Draft của frontend (web/src/features/drafts/types.ts).</summary>
internal sealed record DraftDto(
    string Id,
    string Stt,
    string Cnee,
    string Ct,
    string Service,
    string Branch,
    string Ref,
    string Pcs,
    string Content,
    string Date,
    JsonElement? Payload);

/// <summary>POST /drafts/:id/print — frontend đọc message + billCode.</summary>
internal sealed record PrintDraftResponse(string Message, string BillCode)
{
    public bool Success => true;
}
