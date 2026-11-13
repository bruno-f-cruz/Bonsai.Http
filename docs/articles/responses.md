---
title: Responses and decoding
---

# Responses and decoding

`SendRequest` emits `HttpResponse` after the response body has been fully read. A response owns buffered data, so downstream operators do not have to dispose a native network response.

## Keep the response context

| Property | What it contains |
| --- | --- |
| `Request` | Originating request with its resolved URI and effective headers |
| `RequestId` | Unique identifier for this execution |
| `EffectiveUri` | Final response URI after redirects |
| `StatusCode`, `ReasonPhrase` | Server status |
| `Headers` | Response and content headers, retaining multiple values |
| `Body` | Buffered body bytes |
| `Duration` | Elapsed time to send and fully read the response |
| `IsSuccessStatusCode` | Whether status is 200–299 |

The Dear ImGui response visualizer shows this information together. Its body preview is bounded; the response object retains the complete buffered body up to the client's configured limit.

## Choose text or JSON

```text
SendRequest → EnsureSuccessStatusCode → DecodeResponse
```

| `DecodeResponse.Format` | Workflow output | Use it for |
| --- | --- | --- |
| `Text` | `string` | Text responses, logging, or a JSON string for another parser |
| `Json` | `System.Text.Json.Nodes.JsonNode` | Schema-independent JSON objects, arrays, and scalar values |

The selected format determines the type of data sent to downstream nodes. Stop and restart after changing `Format`, then update downstream nodes if they expect the old type. Both modes use the declared response charset and fall back to UTF-8 when it is missing or unsupported.

In JSON mode, a JSON `null` emits a null value. An empty body or invalid JSON raises `HttpResponseParseException`, whose `Response` property retains the response that failed to parse. Text mode emits an empty string for an empty body.

The decoder follows your selected format; it does not require a particular content-type header. Use [OpenAPI operations](openapi.md) to work with typed response properties instead.

## Decide which failures end the sequence

| Situation | Default behavior |
| --- | --- |
| HTTP 404 or 500 | Emit a normal `HttpResponse` |
| Non-2xx status followed by `EnsureSuccessStatusCode` | Raise `HttpStatusException`, retaining `Response` |
| Configured request timeout | Raise `HttpRequestTimeoutException` |
| Network or TLS failure | Raise a transport error |
| Response body exceeds the client limit | Raise `HttpResponseSizeException` |
| Malformed body in JSON decoding mode | Raise `HttpResponseParseException` |

Place `EnsureSuccessStatusCode` before decoding if you want status failures reported first. Leave it out when you need to decode error bodies as data. Recovery and retry remain explicit workflow choices.

## Try the error examples

Copy these examples into Bonsai and press F5:

:::workflow
![HTTP status as a value](~/workflows/03-status-as-data.bonsai)
:::

:::workflow
![Request timeout](~/workflows/04-timeout.bonsai)
:::

Example 03 emits httpbin's 404 response. Add `EnsureSuccessStatusCode` after `SendRequest` and rerun to make the 404 a sequence error. Example 04 requests a five-second delay with a two-second timeout. Public proxies may affect that demonstration's timing.

Clearing `CreateHttpClient.Timeout` removes the package-imposed request deadline after restarting. Cancelling the workflow still cancels in-flight requests. Bodies are currently buffered; these operators do not expose a streaming response API.
