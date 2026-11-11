using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UblTr.Validator;

/// <summary>JSON settings shared by the CLI and the HTTP API, so both emit identical reports.</summary>
public static class ReportJson
{
    public static JsonSerializerOptions Options { get; } = Create(indented: true);

    public static JsonSerializerOptions Compact { get; } = Create(indented: false);

    private static JsonSerializerOptions Create(bool indented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Keep Turkish characters readable (ş, ğ, ı) instead of ş escapes; output is JSON, never HTML.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
