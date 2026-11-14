---
title: Make your first request
---

# Make your first request

This walkthrough retrieves current weather and decodes the response into text. No API key is needed for Open-Meteo's non-commercial API.

## Open the example

Hover over the workflow below and click its copy button. Paste it into an empty Bonsai workflow. You can also download the workflow and open it in Bonsai.

## Follow the graph

:::workflow
![Weather request workflow](~/workflows/08-response-text.bonsai)
:::

[Download the workflow](~/workflows/08-response-text.bonsai).

1. `Timer` triggers `CreateHttpRequest` once after one second, then every 15 minutes.
2. `CreateHttpRequest` produces a GET request whose URI contains `/v1/forecast`, the coordinates, and the weather fields.
3. `SendRequest` has `BaseAddress = https://api.open-meteo.com/` and a 20-second timeout. It manages a reusable client and emits each response.
4. `EnsureSuccessStatusCode` raises an error for a non-2xx response.
5. `DecodeResponse`, with `Format = Text`, emits the body as a string.

Connect each node to the next. No separate client resource or second input is needed for this workflow.

## Run and inspect

Press **F5** to start. The first request runs after one second; the Timer repeats every 15 minutes.

Double-click `SendRequest` and select the HTTP response visualizer if Bonsai offers more than one. It displays the status, originating request, headers, timing, and body preview. Double-click `DecodeResponse` for the decoded string. Stop with **Shift+F5**.

Look for `current.temperature_2m`, `current.relative_humidity_2m`, and `current.wind_speed_10m`. Units are listed under `current_units`. These are weather model estimates, and values change over time.

## Try a change

- Change the latitude and longitude in `CreateHttpRequest.Uri` to use another location.
- Set `DecodeResponse.Format` to `Json` to output `JsonNode` values instead of strings.
- Disconnect the Timer to have `CreateHttpRequest` emit once and complete. With an input, it emits once per input notification.
- Double-click `CreateHttpRequest` to edit its method, URI, headers, and body with the request editor.

Stop and restart after changing configuration. Settings take effect when the workflow starts. Changing the decoding format also changes the type of data sent to downstream nodes.

## If it does not run

| Symptom | What to check |
| --- | --- |
| Relative requests fail | Set an absolute `BaseAddress`, or use an absolute request URI |
| The sequence times out | Check connectivity and Timeout on SendRequest; increase it or clear it, then restart |
| No response is visible | Start the workflow before opening the response visualizer; check both `SendRequest` inputs |

Continue with [client configuration](client.md) or [running a server](server.md).
