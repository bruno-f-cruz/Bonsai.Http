using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Bonsai.Http;

/// <summary>Schema-neutral helpers for common request and response body formats.</summary>
public static class HttpContentHelpers
{
    public static HttpRequest Json<T>(string method, string uri, T value,
        IReadOnlyDictionary<string, string>? headers = null, JsonSerializerOptions? options = null) =>
        new(method, uri, headers, JsonSerializer.SerializeToUtf8Bytes(value, options), "application/json");

    public static HttpRequest Form(string method, string uri, IEnumerable<KeyValuePair<string, string>> values,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var encoded = string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new HttpRequest(method, uri, headers, Encoding.UTF8.GetBytes(encoded), "application/x-www-form-urlencoded");
    }

    public static HttpRequest WithQuery(this HttpRequest request, IEnumerable<KeyValuePair<string, string>> values)
    {
        var separator = request.Uri.Contains('?') ? "&" : "?";
        var query = string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        var uri = query.Length == 0 ? request.Uri : request.Uri + separator + query;
        return new HttpRequest(request.Method, uri, request.Headers, request.Body, request.ContentType);
    }

    public static string ReadText(this HttpResponse response)
    {
        var encoding = Encoding.UTF8;
        if (response.Headers.TryGetValue("Content-Type", out var contentTypes))
        {
            var charset = contentTypes.FirstOrDefault()?.Split(';', StringSplitOptions.TrimEntries)
                .FirstOrDefault(part => part.StartsWith("charset=", StringComparison.OrdinalIgnoreCase))?[8..];
            if (!string.IsNullOrWhiteSpace(charset))
            {
                try { encoding = Encoding.GetEncoding(charset); }
                catch (ArgumentException) { }
            }
        }
        return encoding.GetString(response.Body);
    }

    public static T? DeserializeJson<T>(this HttpResponse response, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(response.Body, options);

    public static string EscapeQueryValue(string value) => Uri.EscapeDataString(value);
}
