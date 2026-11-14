# Examples

Hover over a workflow and click its copy button, then paste it into Bonsai. Press **F5** to run and **Shift+F5** to stop. Public API examples require an internet connection. Stop and restart after changing settings.

## Get a post

Requests a post from JSONPlaceholder every 15 seconds. Inspect HttpClient for the status, headers, and JSON body.

:::workflow
![Get a post](~/workflows/01-public-get.bonsai)
:::

## Send a JSON body

Posts a title, body, and user identifier to JSONPlaceholder every 30 seconds. The response has status 201 and an identifier. This service simulates creation without storing the new post.

:::workflow
![Send a JSON body](~/workflows/02-public-post.bonsai)
:::

## Treat an HTTP error as data

Requests httpbin's 404 response every 15 seconds. HttpClient emits it as a normal response. Add EnsureSuccessStatusCode downstream to make the status a workflow error.

:::workflow
![Treat an HTTP error as data](~/workflows/03-status-as-data.bonsai)
:::

## Set a request timeout

Requests a five-second delay with a two-second client timeout. A timeout stops the workflow with an error. Clear Timeout and restart to allow the request to complete. Public proxies can affect this demonstration's timing.

:::workflow
![Set a request timeout](~/workflows/04-timeout.bonsai)
:::

## Serve a local endpoint

Receives GET requests at http://127.0.0.1:8080/hello and replies with JSON. Press F5, then open that URL in a browser. Open SendResponse to change its nested response-body workflow. See the [server guide](../articles/server.md) for details.

:::workflow
![Serve a local endpoint](~/workflows/10-http-server.bonsai)
:::

## Read current weather

Retrieves current weather estimates for Lisbon from Open-Meteo every 15 minutes. The body includes temperature, humidity, wind speed, timestamps, and units. Change the latitude and longitude in CreateHttpRequest.Uri for another location.

:::workflow
![Read current weather](~/workflows/06-current-weather.bonsai)
:::

## Read earthquake data

Retrieves the USGS past-hour earthquake feed every minute. Each event includes its magnitude, location, time, and coordinates. An empty event collection is valid when no earthquakes have been recorded in the past hour.

:::workflow
![Read earthquake data](~/workflows/07-earthquakes.bonsai)
:::

## Decode a response body

Retrieves weather data and uses DecodeResponse to output the body as text. Set Format to Json and restart to output JSON values instead. The [first-request guide](../articles/quick-start.md) explains the connections.

:::workflow
![Decode a response body](~/workflows/08-response-text.bonsai)
:::
