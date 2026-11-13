using System.Reactive.Linq;
using System.Text;
using Xunit;

namespace Bonsai.Http.Tests;

public class ReadResponseTextTests
{
    [Theory]
    [InlineData(null, "utf-8")]
    [InlineData("application/json", "utf-8")]
    [InlineData("text/plain; charset=utf-8", "utf-8")]
    [InlineData("text/plain; charset=\"iso-8859-1\"", "iso-8859-1")]
    [InlineData("text/plain; charset=utf-16", "utf-16")]
    [InlineData("text/plain; charset=not-a-real-charset", "utf-8")]
    public async Task DecodesBodyUsingDeclaredCharsetOrUtf8(string? contentType, string encoding)
    {
        const string text = "Ol\u00e1, caf\u00e9!";
        var headers = new Dictionary<string, string[]>();
        if (contentType is not null) headers["Content-Type"] = new[] { contentType };
        var response = new HttpResponse(new HttpRequest("GET", "/"), Guid.NewGuid(),
            new Uri("https://example.com/"), 200, "OK", headers, Encoding.GetEncoding(encoding).GetBytes(text), TimeSpan.Zero);

        var result = await new ReadResponseText().Process(Observable.Return(response)).SingleAsync();

        Assert.Equal(text, result);
    }

    [Fact]
    public async Task EmptyErrorResponseRemainsAnEmptyString()
    {
        var response = new HttpResponse(new HttpRequest("GET", "/"), Guid.NewGuid(),
            new Uri("https://example.com/"), 404, "Not Found", new Dictionary<string, string[]>(), Array.Empty<byte>(), TimeSpan.Zero);

        Assert.Equal(string.Empty, await new ReadResponseText().Process(Observable.Return(response)).SingleAsync());
    }
}
