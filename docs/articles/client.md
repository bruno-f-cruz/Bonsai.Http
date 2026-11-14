---
title: Client workflows
---

# Client workflows

Connect request values to `HttpClient`. Set the service address, timeout, and default headers on that node. It manages one reusable HTTP client while the workflow is running and releases it when the workflow stops.

`HttpClient` is a source with an optional request input, following the same pattern as Harp's `Device`. Without an input it stays active without sending requests or producing responses. With an input it sends each request asynchronously and emits its response when ready. Each response keeps the exact originating request in `OriginalRequest`, so replies remain paired even when they arrive out of order.

## Configure the client

Settings are read when the workflow starts. Stop and restart after changing them.

| Property | Meaning | Initial value |
| --- | --- | --- |
| `BaseAddress` | Absolute base URI for relative request URIs | Unset |
| `Timeout` | Maximum duration of a request, including reading its body | Unset: no package-imposed timeout |
| `ConnectionLifetime` | Connection renewal interval for DNS refresh | Five minutes |
| `MaxResponseBodyBytes` | Maximum buffered response body size | 16 MiB |
| `DefaultHeaders` | Headers applied unless the request overrides them | Empty |

For example, a base address of `https://jsonplaceholder.typicode.com/` and request URI `posts/1` resolve to `https://jsonplaceholder.typicode.com/posts/1`. A leading `/` resolves from the host root, discarding any path prefix in the base address.

## Create a request

`CreateHttpRequest` is a generator with an optional trigger input. With no input, it emits one request and completes. With an input, it emits a request for every notification. Method, URI, headers, body, and content type are read when the workflow starts.

For a JSON POST, configure:

| Property | Value |
| --- | --- |
| `Method` | `POST` |
| `Uri` | `posts` |
| `ContentType` | `application/json; charset=utf-8` |
| `Body` | `{"title":"Hello","body":"From Bonsai","userId":1}` |

Copy this POST example into Bonsai and press F5:

:::workflow
![POST a JSON body](~/workflows/02-public-post.bonsai)
:::

JSONPlaceholder returns status 201 with the supplied fields and an identifier. It simulates creation; the new post is not persisted.

## Headers and authentication

`Headers` and `DefaultHeaders` are editable collections of name/value entries. Request headers override client defaults with the same name, ignoring case.

Bearer tokens and API keys can be sent as request headers:

| Header name | Example value |
| --- | --- |
| `Authorization` | `Bearer YOUR_TOKEN` |
| `X-Api-Key` | `YOUR_API_KEY` |
| `Accept` | `application/json` |

Use DefaultHeaders when all requests through the client share the same credentials. Use request Headers when credentials differ between requests. Headers are saved with the workflow, so avoid sharing workflows containing real tokens.

## Concurrency and ordering

`HttpClient` accepts request values and emits responses as they finish. When its request input completes, it finishes pending requests before completing. Stopping the workflow cancels unfinished requests.

| Property | Initial value | Behavior |
| --- | --- | --- |
| `MaxConcurrency` | 4 | Maximum requests in flight |
| `MaxPendingRequests` | 64 | Maximum requests waiting for a slot |

Responses are emitted when each request finishes, so concurrent responses may arrive out of order. Set `MaxConcurrency = 1` when you need sequential execution. Exceeding the pending limit ends the sequence with an error.

## Share a client between branches

For advanced workflows, `HttpClient` also accepts an explicitly shared client as a second input. Connect `CreateHttpClient` to a `ResourceSubject` and use `SubscribeSubject` to supply that client to each sender's second input. Configure client settings on `CreateHttpClient` in this mode; each sender still controls its own concurrency and queue limits.

Next: [decode responses and handle errors](responses.md).
