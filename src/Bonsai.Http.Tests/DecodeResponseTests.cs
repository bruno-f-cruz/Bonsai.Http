using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Bonsai.Http.Tests;

public class DecodeResponseTests
{
    static HttpResponse Response(string text, string? contentType = null, Encoding? encoding = null) =>
        new(new HttpRequest("GET", "/"), Guid.NewGuid(), new Uri("https://example.com/"), 200, "OK",
            contentType is null ? new Dictionary<string, string[]>() : new Dictionary<string, string[]> { ["Content-Type"] = new[] { contentType } },
            (encoding ?? Encoding.UTF8).GetBytes(text), TimeSpan.Zero);

    static IObservable<T> Decode<T>(DecodeResponse decoder, params HttpResponse[] responses)
    {
        var expression = decoder.Build(new[] { Expression.Constant(responses.ToObservable(), typeof(IObservable<HttpResponse>)) });
        Assert.Equal(typeof(IObservable<T>), expression.Type);
        return Expression.Lambda<Func<IObservable<T>>>(expression).Compile()();
    }

    [Theory]
    [InlineData(null, "utf-8")]
    [InlineData("application/json", "utf-8")]
    [InlineData("text/plain; charset=utf-8", "utf-8")]
    [InlineData("text/plain; charset=\"iso-8859-1\"", "iso-8859-1")]
    [InlineData("text/plain; charset=utf-16", "utf-16")]
    [InlineData("text/plain; charset=unknown-charset", "utf-8")]
    public async Task TextFormatBuildsStringOutputAndRespectsCharset(string? contentType, string encoding)
    {
        const string text = "Ol\u00e1, caf\u00e9!";
        var source = Decode<string>(new DecodeResponse(), Response(text, contentType, Encoding.GetEncoding(encoding)));
        Assert.Equal(text, await source.SingleAsync());
    }

    [Theory]
    [InlineData("{\"value\":42}")]
    [InlineData("[1,2,3]")]
    [InlineData("42")]
    [InlineData("\"hello\"")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task JsonFormatBuildsJsonNodeOutputForAnyJsonRoot(string json)
    {
        var result = await Decode<JsonNode?>(new DecodeResponse { Format = ResponseFormat.Json }, Response(json)).SingleAsync();
        using var expected = JsonDocument.Parse(json);
        using var actual = JsonDocument.Parse(result?.ToJsonString() ?? "null");
        Assert.Equal(expected.RootElement.ValueKind, actual.RootElement.ValueKind);
        Assert.Equal(expected.RootElement.ToString(), actual.RootElement.ToString());
    }

    [Fact]
    public async Task JsonModeUsesResponseCharset()
    {
        var result = await Decode<JsonNode?>(new DecodeResponse { Format = ResponseFormat.Json },
            Response("{\"name\":\"caf\u00e9\"}", "application/json; charset=iso-8859-1", Encoding.GetEncoding("iso-8859-1"))).SingleAsync();
        Assert.Equal("caf\u00e9", result!["name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    public async Task InvalidJsonRaisesCorrelatedParseError(string text)
    {
        var response = Response(text);
        var error = await Assert.ThrowsAsync<HttpResponseParseException>(async () =>
            await Decode<JsonNode?>(new DecodeResponse { Format = ResponseFormat.Json }, response).SingleAsync());
        Assert.Same(response, error.Response);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
    }

    [Fact]
    public async Task ChangingFormatDoesNotMutateBuiltSequence()
    {
        var decoder = new DecodeResponse();
        var text = Decode<string>(decoder, Response("{\"value\":42}"));
        decoder.Format = ResponseFormat.Json;
        Assert.Equal("{\"value\":42}", await text.SingleAsync());
        Assert.Equal(42, (await Decode<JsonNode?>(decoder, Response("{\"value\":42}")).SingleAsync())!["value"]!.GetValue<int>());
    }
}
