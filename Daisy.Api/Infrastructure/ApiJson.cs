using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daisy.Application.Common;

namespace Daisy.Api.Infrastructure;

/// <summary>JSON conventions of API_DESIGN §2.1: camelCase, enums as strings, times with +07:00.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        if (!options.Converters.OfType<JsonStringEnumConverter>().Any())
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        if (!options.Converters.OfType<VietnamDateTimeConverter>().Any())
        {
            options.Converters.Add(new VietnamDateTimeConverter());
        }

        return options;
    }
}

/// <summary>
/// DateTime values are stored in Vietnam time. They are written with the +07:00 offset, and any
/// incoming offset is converted to Vietnam time; values without an offset are taken as Vietnam time.
/// </summary>
public sealed class VietnamDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException("A date and time is required.");
        }

        var timeStart = text.IndexOf('T');
        var hasOffset = text.EndsWith('Z') || (timeStart > 0 && text.LastIndexOfAny(['+', '-']) > timeStart);
        if (hasOffset && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var withOffset))
        {
            return withOffset.ToVietnamTime();
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        }

        throw new JsonException($"'{text}' is not a valid ISO 8601 date and time.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), VietnamTime.Offset)
            .ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
}
