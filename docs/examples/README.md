# Public API examples

From a clone of this repository on Windows with the .NET 8 SDK and [Bonsai](https://bonsai-rx.org/docs/) installed, run:

```powershell
pwsh docs/examples/run.ps1 01-public-get
```

The script builds the Framework libraries and opens the selected workflow in your installed Bonsai editor with the local HTTP operators available. It does not require a published HTTP package. The other example names can be passed to the same script.

Press **F5** to run. Double-click `SendRequest`, then select the HTTP response visualizer if more than one visualizer is offered. Inspect the status, headers, request identity, and body. Stop with **Shift+F5**. Before running, double-click `CreateHttpRequest` to open its Dear ImGui editor. Client configuration is read at subscription; restart after changing it.

| Workflow | Service | Expected behavior |
| --- | --- | --- |
| `01-public-get.bonsai` | JSONPlaceholder `GET /posts/1` | Status 200 and a JSON post, every 15 seconds |
| `02-public-post.bonsai` | JSONPlaceholder `POST /posts` | Status 201 and the supplied fields with `id: 101`, every 30 seconds |
| `03-status-as-data.bonsai` | httpbin `GET /status/404` | Status 404 is emitted normally, every 15 seconds |
| `04-timeout.bonsai` | httpbin `GET /delay/5` | A two-second timeout normally raises a sequence error |
| `05-local-server.bonsai` | Your Bonsai workflow | Run it, then visit `http://127.0.0.1:8080/hello` in a browser to receive JSON |

[JSONPlaceholder](https://jsonplaceholder.typicode.com/) does not persist the example POST. [httpbin](https://httpbin.org/) provides request/response testing endpoints. Neither example needs credentials. Public service availability, proxy behavior, and rate limits can change results; these requests are optional checks and are excluded from normal CI.

To try explicit HTTP errors, add `EnsureSuccessStatusCode` after `SendRequest` in example 03. The next 404 will terminate the sequence. To try an unlimited client timeout, clear `Timeout` on `CreateHttpClient` in example 04, stop, and restart. A public proxy may cache a delayed response; the timeout test suite uses a local unresponsive handler for deterministic behavior.

The period on each Timer keeps the workflow running so the response can be inspected. Clear the Timer's `Period` to send once. The XML duration properties use Bonsai's Timer pattern (`PT2S`, `PT5M`), and headers are serialized as editable entries.

To repeat the optional live checks from the command line:

```powershell
$env:BONSAI_HTTP_PUBLIC_API = '1'
dotnet test src/Bonsai.Http.Tests/Bonsai.Http.Tests.csproj -f net472 --filter PublicApiExamplesReturnExpectedStatuses
```
