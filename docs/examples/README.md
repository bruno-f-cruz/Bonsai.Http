# Examples

Copy a workflow using the copy button in its guide, or download a workflow below and open it in Bonsai. Press F5 to run and Shift+F5 to stop.

The public API examples require an internet connection. Inspect SendRequest with the HTTP response visualizer, or DecodeResponse to see the decoded body. Stop and restart after changing client or request settings.

| Workflow | Service | Expected behavior |
| --- | --- | --- |
| `09-openapi-post.bonsai` | JSONPlaceholder through generated OpenAPI operators | A typed post model together with its raw HTTP response; requires the generated API package |
| `08-response-text.bonsai` | Open-Meteo `GET /v1/forecast` | Current weather response decoded into a string by `DecodeResponse`, every 15 minutes |
| `06-current-weather.bonsai` | Open-Meteo `GET /v1/forecast` | Status 200 and current Lisbon temperature, humidity, wind speed, timestamp, and units, every 15 minutes |
| `07-earthquakes.bonsai` | USGS `GET /earthquakes/feed/v1.0/summary/all_hour.geojson` | Status 200 and a GeoJSON collection of past-hour earthquakes, every minute |
| `01-public-get.bonsai` | JSONPlaceholder `GET /posts/1` | Status 200 and a JSON post, every 15 seconds |
| `02-public-post.bonsai` | JSONPlaceholder `POST /posts` | Status 201 and the supplied fields with `id: 101`, every 30 seconds |
| `03-status-as-data.bonsai` | httpbin `GET /status/404` | Status 404 is emitted normally, every 15 seconds |
| `04-timeout.bonsai` | httpbin `GET /delay/5` | A two-second timeout normally raises a sequence error |
| `05-local-server.bonsai` | Modern .NET Bonsai workflow | Run example 05 and press F5, then visit `http://127.0.0.1:8080/hello` in a browser to receive JSON |


## Copy a workflow

- [First request](../articles/quick-start.md): current weather, with each connection explained.
- [Send a JSON body](../articles/client.md#create-a-request): POST example.
- [Local server](../articles/server.md): receive requests and reply from a nested workflow.
- [Typed API request](../articles/openapi.md): use nodes from an installed API package.

- [01-public-get](~/workflows/01-public-get.bonsai)
- [02-public-post](~/workflows/02-public-post.bonsai)
- [03-status-as-data](~/workflows/03-status-as-data.bonsai)
- [04-timeout](~/workflows/04-timeout.bonsai)
- [06-current-weather](~/workflows/06-current-weather.bonsai)
- [07-earthquakes](~/workflows/07-earthquakes.bonsai)
- [08-response-text](~/workflows/08-response-text.bonsai)

JSONPlaceholder simulates POST requests without storing the new post. Open-Meteo returns current weather estimates for Lisbon; change the coordinates in the request URI for another location. The USGS feed returns earthquakes from the past hour and can contain no events.

In example 03, add EnsureSuccessStatusCode after SendRequest to turn the 404 response into a workflow error. In example 04, clear the client timeout and restart to allow the delayed request to complete. Public proxies can affect the timing of this demonstration.
