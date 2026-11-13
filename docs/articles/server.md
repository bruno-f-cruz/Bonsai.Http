---
title: Serve an HTTP endpoint
---

# Serve an HTTP endpoint

`Bonsai.Http.Server` supports .NET Framework 4.7.2 in the Bonsai editor and modern .NET 8. Framework uses HttpListener; modern .NET uses Kestrel.

Each incoming request runs its own nested `SendResponse` workflow. The nested output supplies the response body, and the operator completes the associated HTTP response.

## Run the local example

From the repository root, with PowerShell 7 and the .NET 8 SDK installed:

```powershell
pwsh docs/examples/run.ps1 05-local-server
```

This opens the server workflow in the Bonsai editor. Press F5, then test it in another terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/hello
```

The response is JSON:

```json
{"message":"Hello from Bonsai"}
```

Stop with Shift+F5. The optional `run-server.ps1` launcher runs the same workflow in a modern .NET console host.

:::workflow
![Local HTTP server workflow](~/examples/server/05-local-server.bonsai)
:::

[Download the workflow](~/examples/server/05-local-server.bonsai).

## Follow the server graph

1. `CreateHttpServer` configures `Url = http://127.0.0.1:8080/` and a ten-second `RequestDeadline`.
2. A `ResourceSubject` named `Server` owns the listener resource.
3. `SubscribeSubject`, also named `Server`, supplies it to `ReceiveRequest`.
4. `ReceiveRequest` registers `Method = GET` and `Pattern = /hello`, starts the server, and emits matching request contexts.
5. `SendResponse` opens a separate nested workflow for each request. Inside this example, `WorkflowInput` triggers a `String` property containing the JSON body, connected to `WorkflowOutput`.
6. The first nested output is sent to that request's caller. The outer operator emits the completed request context.

Changing configuration requires restarting the workflow. If port 8080 is unavailable, pass `-Url http://127.0.0.1:18080/` to the launcher and use the same port in the test URL, or change `CreateHttpServer.Url` in the workflow.

## Configure the response

| Property on `SendResponse` | Meaning | Default |
| --- | --- | --- |
| `StatusCode` | The status to send, from 100 to 599 | 200 |
| `ContentType` | The response media type | `text/plain` for strings; `application/octet-stream` for bytes |
| `Encoding` | Encoding name for string output, such as `UTF-8` or `utf-16` | Unset, using UTF-8 |
| `Headers` | Additional response headers | Empty |

These properties are captured when the outer workflow subscribes. For string output, the selected encoding determines both the body bytes and the content-type charset. A `byte[]` output is sent unchanged; `Encoding` does not apply to it. The example sets `ContentType = application/json`.

The nested workflow must output `string` or `byte[]`. Its first output sends the response and disposes that nested execution. Completion without a value sends an empty body. An error sends a server error and terminates the outer sequence. A request deadline or disconnection cancels unfinished nested processing.

## Match paths and inspect requests

Use a route template such as `/items/{id}`. Parameters must occupy an entire path segment. A request for `/items/42` produces a context whose `RouteValues["id"]` is `"42"`.

`HttpServerRequestContext` exposes `RequestId`, `Method`, `Uri`, `Headers`, `Body`, `RouteValues`, and a cancellation token. The URI includes the query string; query parameters do not participate in route-template matching. Incoming bodies are buffered as bytes.

Unknown paths receive 404. Known paths with an unregistered method receive 405 with an `Allow` header. Bodies larger than `MaxRequestBodyBytes` receive 413; the default limit is 16 MiB. A request that misses its response deadline currently receives 500.

## Build a dynamic body

Inside `SendResponse`, use an extension to map each request to text or bytes. For a `/items/{id}` route:

```csharp
public IObservable<string> Process(
    IObservable<HttpServerRequestContext> source)
{
    return source.Select(request =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            id = request.RouteValues["id"],
            message = "Hello from Bonsai"
        }));
}
```

The snippet needs `System.Reactive.Linq` and `Bonsai.Http.Server` imports. Connect it between the nested `WorkflowInput` and `WorkflowOutput`, then set `SendResponse.ContentType = application/json`. No response wrapper or explicit context completion is required.

## HTTPS

On modern .NET, configure an HTTPS `Url`, `CertificatePath`, and, when needed, `CertificatePassword` on `CreateHttpServer`. On Framework, use an operating-system certificate binding and leave CertificatePath unset. Start with the loopback HTTP example to verify the workflow before configuring certificates.
