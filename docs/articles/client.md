---
title: Client workflows
---

# Client workflows

Create one client for a workflow scope and share it between request operators. `Bonsai.Http.HttpClient` owns the underlying .NET client and its connection pool. A `ResourceSubject` disposes it when its scope closes.

## Share the resource

Connect `CreateHttpClient` to a `ResourceSubject` named `Api`. Add `SubscribeSubject` nodes named `Api` wherever a request operator needs the same client. Connect each subscription to the client input of `SendRequest`.

The properties on `CreateHttpClient` are read when subscribed. Changing a property in the editor does not reconfigure the running client; stop and restart the workflow.

| Property | Meaning | Initial value |
| --- | --- | --- |
| `BaseAddress` | Absolute base URI for relative request URIs | Unset |
| `Timeout` | Maximum duration of a request, including reading its body | Unset: no package-imposed timeout |
| `ConnectionLifetime` | Connection renewal interval for DNS refresh | Five minutes |
| `MaxResponseBodyBytes` | Maximum buffered response body size | 16 MiB |
| `DefaultHeaders` | Headers applied unless the request overrides them | Empty |

For example, a base address of `https://jsonplaceholder.typicode.com/` and request URI `posts/1` resolve to `https://jsonplaceholder.typicode.com/posts/1`. A leading `/` resolves from the host root, discarding any path prefix in the base address.

## Create a request

`CreateHttpRequest` is a generator with an optional trigger input. With no input, it emits one request and completes. With an input, it emits a request for every notification. Method, URI, headers, body, and content type are captured once per subscription.

For a JSON POST, configure:

| Property | Value |
| --- | --- |
| `Method` | `POST` |
| `Uri` | `posts` |
| `ContentType` | `application/json; charset=utf-8` |
| `Body` | `{"title":"Hello","body":"From Bonsai","userId":1}` |

Run the [POST example](../examples/02-public-post.bonsai):

```powershell
pwsh docs/examples/run.ps1 02-public-post
```

JSONPlaceholder returns status 201 with the supplied fields and an identifier. It simulates creation; the new post is not persisted.

## Headers and authentication

`Headers` and `DefaultHeaders` are editable collections of name/value entries. Request headers override client defaults with the same name, ignoring case.

Bearer tokens and API keys can be sent as request headers:

| Header name | Example value |
| --- | --- |
| `Authorization` | `Bearer YOUR_TOKEN` |
| `X-Api-Key` | `YOUR_API_KEY` |
| `Accept` | `application/json` |

For credentials shared by a dedicated client, default headers offer the same convenience. For tokens that vary between requests, construct request values downstream with an extension. There are no dedicated authentication operators or built-in OAuth token-refresh flows. Header properties serialize into workflow XML; source real secrets from your own runtime configuration rather than saving them in a shared example.

## Concurrency and ordering

`SendRequest` accepts request values on Source1 and one shared client on Source2. It captures the first client value and uses it for the subscription.

| Property | Initial value | Behavior |
| --- | --- | --- |
| `MaxConcurrency` | 4 | Maximum requests in flight |
| `MaxPendingRequests` | 64 | Maximum requests waiting for a slot |

Responses are emitted when each request finishes, so concurrent responses may arrive out of order. Set `MaxConcurrency = 1` when you need sequential execution. Exceeding the pending limit ends the sequence with an error.

## Build dynamic requests in C#

The core is schema-independent. Extensions can construct request values directly:

```csharp
var request = new HttpRequest("GET", "posts")
    .WithQuery(new[]
    {
        new KeyValuePair<string, string>("userId", "1")
    });

var jsonRequest = HttpContentHelpers.Json("POST", "posts", new
{
    title = "Hello",
    body = "From Bonsai",
    userId = 1
});
```

These expressions use `Bonsai.Http` and `System.Collections.Generic` imports. `HttpRequest` snapshots headers and body bytes. Each send can use the same immutable request description while receiving its own response identifier.

Next: [decode responses and handle errors](responses.md).
