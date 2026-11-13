[!INCLUDE [](README.md)]
# Bonsai.Http

`Bonsai.Http` connects HTTP requests and responses to Bonsai observable workflows. The core transport does not require OpenAPI or JSON.

## Client workflow

Create a client resource, share it with a `ResourceSubject<HttpClientResource>`, create request values, and connect both inputs to `SendRequest`. The resource reads its properties when the workflow starts and is disposed when the workflow scope closes.

HTTP 4xx and 5xx responses are emitted as values. Use `EnsureSuccessStatusCode` when an HTTP error should become a workflow error. A configured timeout raises a timeout exception; a null timeout means no package-imposed deadline.

`HttpContentHelpers.Json`, `HttpContentHelpers.Form`, and `HttpRequest.WithQuery` build common request bodies and query strings. `HttpResponse.ReadText` and `HttpResponse.DeserializeJson<T>` decode a materialized response body.

## Server workflow

Use `CreateHttpServer` and share the resource through `ResourceSubject<HttpServerResource>`. Each `ReceiveRequest` node registers a method and path template. `CreateHttpServerResponse` provides a simple configured reply; for dynamic replies, map an incoming context to an `HttpServerExchange` containing its response and send it through `SendResponse`. Request context carries the reply capability, so concurrent requests remain correlated.

## OpenAPI generation

Install the `Bonsai.Http.OpenApi` .NET tool and run:

```powershell
dotnet tool install --global Bonsai.Http.OpenApi
bonsai.http.openapi openapi.yaml generated MyApi
```

The generator reads JSON or YAML OpenAPI 3.x, sends component schemas through published `Bonsai.Sgen`, and writes generated C# into the output directory. The initial operation generator supports component references for JSON request and response bodies, path parameters, and query parameters. Unsupported inline operation schemas report an error.

## Platform notes

The HTTP runtime and server use .NET 8 APIs. `Bonsai.Http.Design` uses Hexa.NET.ImGui and provides a panel to draw inside a configured Dear ImGui frame. Integration with the Bonsai editor's visualizer host is still under development; the upstream ImGui visualizer design package currently targets Windows/.NET Framework.
