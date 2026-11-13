# Public API examples

From a clone of this repository on Windows with the .NET 8 SDK and [Bonsai](https://bonsai-rx.org/docs/) installed, run:

```powershell
pwsh docs/examples/run.ps1 06-current-weather
```

The script builds the Framework libraries and opens the selected workflow in your installed Bonsai editor with the local HTTP operators available. It does not require a published HTTP package. The other example names can be passed to the same script.

Press **F5** to run. Double-click `SendRequest`, then select the HTTP response visualizer if more than one visualizer is offered. Inspect the status, headers, request identity, and body. Stop with **Shift+F5**. Before running, double-click `CreateHttpRequest` to open its Dear ImGui editor. Client configuration is read at subscription; restart after changing it.

| Workflow | Service | Expected behavior |
| --- | --- | --- |
| `08-response-text.bonsai` | Open-Meteo `GET /v1/forecast` | Current weather response decoded into a string by `ReadResponseText`, every 15 minutes |
| `06-current-weather.bonsai` | Open-Meteo `GET /v1/forecast` | Status 200 and current Lisbon temperature, humidity, wind speed, timestamp, and units, every 15 minutes |
| `07-earthquakes.bonsai` | USGS `GET /earthquakes/feed/v1.0/summary/all_hour.geojson` | Status 200 and a GeoJSON collection of past-hour earthquakes, every minute |
| `01-public-get.bonsai` | JSONPlaceholder `GET /posts/1` | Status 200 and a JSON post, every 15 seconds |
| `02-public-post.bonsai` | JSONPlaceholder `POST /posts` | Status 201 and the supplied fields with `id: 101`, every 30 seconds |
| `03-status-as-data.bonsai` | httpbin `GET /status/404` | Status 404 is emitted normally, every 15 seconds |
| `04-timeout.bonsai` | httpbin `GET /delay/5` | A two-second timeout normally raises a sequence error |
| `05-local-server.bonsai` | Your Bonsai workflow | Run it, then visit `http://127.0.0.1:8080/hello` in a browser to receive JSON |

For live data, start with `06-current-weather`. [Open-Meteo](https://open-meteo.com/en/docs) returns weather model data for Lisbon without an API key for non-commercial use. Look for `current.time`, `current.temperature_2m`, `current.relative_humidity_2m`, and `current.wind_speed_10m` in the body, with units in `current_units`. These are model estimates. Change `latitude` and `longitude` in `CreateHttpRequest.Uri` for your location, then restart. The 15-minute polling interval matches the resolution of current conditions.

Ping the same endpoint in PowerShell without Bonsai:

```powershell
$weatherUri = 'https://api.open-meteo.com/v1/forecast?latitude=38.7223&longitude=-9.1393&current=temperature_2m,relative_humidity_2m,wind_speed_10m&timezone=auto&forecast_days=1'
(Invoke-RestMethod $weatherUri).current
```

[JSONPlaceholder](https://jsonplaceholder.typicode.com/) does not persist the example POST. [httpbin](https://httpbin.org/) provides request/response testing endpoints. The public examples need no credentials. Public service availability, proxy behavior, and rate limits can change results; these requests are optional checks and are excluded from normal CI.

To try explicit HTTP errors, add `EnsureSuccessStatusCode` after `SendRequest` in example 03. The next 404 will terminate the sequence. To try an unlimited client timeout, clear `Timeout` on `CreateHttpClient` in example 04, stop, and restart. A public proxy may cache a delayed response; the timeout test suite uses a local unresponsive handler for deterministic behavior.

For a string output, run `pwsh docs/examples/run.ps1 08-response-text`. Its pipeline is `SendRequest → EnsureSuccessStatusCode → ReadResponseText`. Double-click `ReadResponseText` to inspect the decoded body in the text visualizer. It uses the response charset, defaulting to UTF-8 when absent or unsupported. The JSON remains plain text; `SendRequest` still exposes the full response for inspection.

The period on each Timer keeps the workflow running so the response can be inspected. Clear the Timer's `Period` to send once, or disconnect the Timer: `CreateHttpRequest` without an input emits one request when the workflow starts and completes. With an input, it emits one request per notification. Request properties are captured when the workflow starts. The XML duration properties use Bonsai's Timer pattern (`PT2S`, `PT5M`), and headers are serialized as editable entries.

For recorded observations, try `07-earthquakes`. The [USGS GeoJSON feed](https://earthquake.usgs.gov/earthquakes/feed/v1.0/geojson.php) includes `metadata.count` and a `features` array. Each feature contains `properties.mag`, `properties.place`, `properties.time` (Unix milliseconds), and `geometry.coordinates` (longitude, latitude, depth in kilometres). An empty array is valid when there have been no events in the past hour. No API key is needed.

```powershell
(Invoke-RestMethod 'https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_hour.geojson').features
```

To repeat the optional live checks from the command line:

```powershell
$env:BONSAI_HTTP_PUBLIC_API = '1'
dotnet test src/Bonsai.Http.Tests/Bonsai.Http.Tests.csproj -f net472 --filter PublicApiExamplesReturnExpectedStatuses
```
