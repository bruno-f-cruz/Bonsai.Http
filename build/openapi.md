---
title: Generate typed OpenAPI operations
---

# Generate typed OpenAPI operations

Use OpenAPI when you want request properties and parsed response models generated from an API contract. The HTTP transport remains schema-independent; the generated operation translates between those models and the core request/response values.

`Bonsai.Http.OpenApi` accepts OpenAPI 3.x in JSON or YAML. It invokes the published `Bonsai.Sgen` 0.9.0 CLI for model generation and generates operation nodes around those models.

## Run the complete example

The repository includes a small contract for JSONPlaceholder's `GET /posts/{postId}` endpoint:

- [OpenAPI contract](https://github.com/bruno-f-cruz/Bonsai.Http/blob/main/docs/examples/jsonplaceholder.openapi.json)
- [Typed request workflow](~/workflows/09-openapi-post.bonsai)

From the repository root:

```powershell
pwsh docs/examples/run-openapi.ps1
```

This generates the code, builds `docs/workflows/Extensions`, builds the HTTP design library, and opens the workflow with both assemblies available. The generator restores the pinned Sgen tool automatically; initial generation needs network access to its NuGet feed.

Press **F5**. The workflow requests post 1 from `https://jsonplaceholder.typicode.com/`. Stop and restart to send again.

:::workflow
![Typed OpenAPI request workflow](~/workflows/09-openapi-post.bonsai)
:::

## Understand the generated nodes

The contract names the operation `GetPost` and defines a `Post` model with `Id`, `UserId`, `Title`, and `Body` properties.

| Generated type | Purpose |
| --- | --- |
| `GetPostRequest` | Request data; `PostId` is a required path parameter |
| `CreateGetPostRequest` | A source whose expandable `Value` holds the configured request |
| `Client` | Owns a reusable HTTP client and dispatches API requests to their matching response decoder |
| `ApiRequest` / `ApiResult` | Common request and result families for streams containing multiple operations |
| `GetPost` | Operation-specific sender, with the same transport configuration as `Client` |
| `GetPostResult` | Base result containing the original typed `Request` and raw HTTP `Response` |
| `GetPostStatus200` | A successful result with the parsed `Post` in `Value` |
| `GetPostStatus404` | The explicitly declared 404 result |
| `GetPostUnknown` | A result for an undeclared status or an unexpected non-JSON media type |
| `EnsureGetPostSuccessStatusCode` | Raises a sequence error for non-2xx results |

`CreateGetPostRequest.Value.PostId` is set to 1 in the example. Its output connects directly to `Client`. Configure `BaseAddress`, headers, timeout, and concurrency on that node. The contract's server URLs are not used to configure the client automatically. Configuration is read when the workflow starts.

Each subscription owns its transport, drains outstanding requests after the input completes, and cancels requests on disposal. For advanced resource sharing, both senders also accept an explicit `Bonsai.Http.HttpClient` as their second input; that resource supplies the transport configuration.

The `Client` overload for `GetPostRequest` returns `GetPostResult`. A mixed `ApiRequest` stream returns `ApiResult`, with dispatch based on each concrete request type. Responses can arrive out of order; each result retains the exact original typed request in `Request`. Its raw `Response.OriginalRequest` retains the corresponding serialized HTTP request. Schema discriminator mappings are passed to the published Sgen model generator so a declared base response model can deserialize to its concrete subtype.

The operation emits `GetPostResult`, with a concrete subtype for the returned status. In a C# extension, narrow the sequence before accessing the model:

```csharp
public IObservable<string> Process(IObservable<GetPostResult> source)
{
    return source.OfType<GetPostStatus200>()
        .Select(result => result.Value.Title);
}
```

This snippet needs `JsonPlaceholderApi` and `System.Reactive.Linq` imports. Use `GetHttpResponse` downstream of a typed operation to recover the raw response for visualization, status inspection, or `DecodeResponse`.

## Generate code yourself

The launcher performs these steps. You can also run them individually:

```powershell
dotnet run --project src/Bonsai.Http.OpenApi --configuration Release -- `
  docs/examples/jsonplaceholder.openapi.json `
  artifacts/generated/JsonPlaceholderApi `
  JsonPlaceholderApi

dotnet build docs/workflows/Extensions --configuration Release
```

The arguments after `--` are the input document, output directory, and C# namespace. The namespace is optional and defaults to `GeneratedHttpApi`.

For another API, use your contract and namespace, then include the emitted `*.Generated.cs` files in an extension project referencing `Bonsai.Http` and `Newtonsoft.Json`. Build that project and load its assembly through your Bonsai environment or `--lib` argument. `docs/workflows/Extensions/Extensions.csproj` shows how to include generated files without committing them.

## Regeneration and limitations

Regenerate after changing the contract and rebuild the extension assembly. Restart Bonsai to load the updated assembly. The output manifest tracks generated files so obsolete operation files can be removed without deleting unrelated files in that directory.

The generator supports scalar path, query, and header parameters; JSON request bodies; component and inline models; arrays and primitive bodies; exact status codes; and default responses. Parameters inherited from a path item are merged with operation parameters. Descriptions from the contract become property-grid descriptions.

Cookie, array, and object parameters; external schema references; response status ranges; and multiple JSON media types for one message are currently rejected. It does not generate server route definitions or authentication flows. Supply authentication through request or client-default headers as described in [client workflows](client.md).

An invalid JSON response for a declared JSON result raises `HttpResponseParseException` and retains the raw response. HTTP error statuses remain values until you add the generated success-status operator.
