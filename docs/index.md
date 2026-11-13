[!INCLUDE [](README.md)]
# Bonsai.Http

`Bonsai.Http` connects HTTP requests and responses to Bonsai observable workflows. The core transport does not require OpenAPI or JSON.

## Client workflow

Create a client resource, share it with a `ResourceSubject<HttpClient>`, create request values, and connect both inputs to `SendRequest`. The resource reads its properties when the workflow starts and is disposed when the workflow scope closes.

HTTP 4xx and 5xx responses are emitted as values. Use `EnsureSuccessStatusCode` when an HTTP error should become a workflow error. A configured timeout raises a timeout exception; a null timeout means no package-imposed deadline.

`HttpContentHelpers.Json`, `HttpContentHelpers.Form`, and `HttpRequest.WithQuery` build common request bodies and query strings. `HttpResponse.ReadText` and `HttpResponse.DeserializeJson<T>` decode a materialized response body.

Use `SendRequest → EnsureSuccessStatusCode → DecodeResponse` to decode response bodies in a workflow. Its `Format` property selects the output type: `Text` emits `string`, and `Json` emits `System.Text.Json.Nodes.JsonNode`. Both modes use the response's declared charset, falling back to UTF-8 when absent or unsupported. The output type is selected when the workflow is built; restart after changing the format. JSON objects, arrays, and scalar roots are supported; JSON `null` emits a null value. Empty or malformed JSON raises `HttpResponseParseException`, which retains the original response. Inspect `SendRequest` for the complete response. Omit `EnsureSuccessStatusCode` to decode error response bodies as well.

## Server workflow

Use `CreateHttpServer` and share the resource through `ResourceSubject<HttpServerResource>`. Each `ReceiveRequest` node registers a method and path template. `CreateHttpServerResponse` provides a simple configured reply; for dynamic replies, map an incoming context to an `HttpServerExchange` containing its response and send it through `SendResponse`. Request context carries the reply capability, so concurrent requests remain correlated.

## OpenAPI generation

Install the `Bonsai.Http.OpenApi` .NET tool and run:

```powershell
dotnet tool install --global Bonsai.Http.OpenApi
bonsai.http.openapi openapi.yaml generated MyApi
```

The generator reads JSON or YAML OpenAPI 3.x, sends model schemas through published `Bonsai.Sgen`, and writes generated C# into the output directory. It supports JSON bodies (component models, inline objects, arrays, and primitives), inherited scalar path/query/header parameters, required values, exact response statuses and default responses. Generated responses retain the raw HTTP response alongside the parsed model. Generated projects require `Bonsai.Http` and `Newtonsoft.Json`. Unsupported cookie/object/array parameters, external schema references, status ranges, and multiple JSON media types produce diagnostics. Regeneration removes obsolete files listed in the generator's manifest and preserves other files in the output directory.

## Platform notes

The runtime and server packages target `net472` and `net8.0`. The current Bonsai editor uses the Framework assemblies. The server uses HttpListener on Framework and Kestrel on .NET 8, with shared routing, serialized route notifications, body limits, and reply correlation. Framework HTTPS requires an operating-system certificate binding; .NET 8 HTTPS accepts a certificate file. The Framework client uses an origin's ServicePoint connection lease for DNS refresh; .NET 8 uses SocketsHttpHandler's pooled connection lifetime.

`Bonsai.Http.Design` is Windows-only (`net472`, x64). It uses the existing `Bonsai.ImGui.Design` host for a docked response visualizer and a modal request editor. All request controls, headers, and response previews are drawn with Dear ImGui. The Windows host supplies the window and OpenGL surface. HTTP transport has no UI dependency.

Try the [public API workflows](examples/README.md) for current weather data from Open-Meteo, GET, POST, status handling, and timeout behavior.
