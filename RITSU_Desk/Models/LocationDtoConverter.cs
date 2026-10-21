using System.Text.Json;
using System.Text.Json.Serialization;

namespace RITSU_Desk.Models;

public sealed class LocationDtoConverter : JsonConverter<LocationDto>
{
    public override LocationDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return FromStoredText(reader.GetString() ?? string.Empty);
            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    var root = doc.RootElement;
                    var raw = Str(root, "raw");
                    return new LocationDto(Num(root, "lat"), Num(root, "lng"), Str(root, "name"), raw ?? string.Empty);
                }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, LocationDto value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Lat is { } lat) writer.WriteNumber("lat", lat); else writer.WriteNull("lat");
        if (value.Lng is { } lng) writer.WriteNumber("lng", lng); else writer.WriteNull("lng");
        writer.WriteString("name", value.Name);
        writer.WriteString("raw", value.Raw);
        writer.WriteEndObject();
    }

    public static LocationDto FromStoredText(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                return new LocationDto(Num(root, "lat"), Num(root, "lng"), Str(root, "name"), raw);
            }
        }
        catch (JsonException)
        {
        }
        return new LocationDto(null, null, null, raw);
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Num(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d)) return d;
        if (v.ValueKind == JsonValueKind.String &&
            double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) &&
            double.IsFinite(s))
        {
            return s;
        }
        return null;
    }
}
