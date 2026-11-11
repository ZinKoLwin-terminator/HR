using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// Times of day travel as "HH:mm" (24 hours), the format of the frontend models, instead of the default "HH:mm:ss".
/// Reading also accepts "HH:mm:ss"; any other text is a bad request.
/// </summary>
internal sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH':'mm";

    private static readonly string[] Accepted = [Format, "HH':'mm':'ss"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (text is not null && TimeOnly.TryParseExact(text, Accepted, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        throw new JsonException("A time must be written as HH:mm.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
