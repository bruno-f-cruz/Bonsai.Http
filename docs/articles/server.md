---
title: Serve an HTTP endpoint
---

# Serve an HTTP endpoint

`HttpServer` is a source that owns its listener and emits incoming request contexts. The workflow creates a response paired with each request and returns it to the server's input. The server sends that response to the caller who made the request.

## Run the local example

Copy the workflow below into Bonsai, or download and open it. Press F5 to start the server.

Open http://127.0.0.1:8080/hello in your browser. You should receive:

```json
{"message":"Hello from Bonsai"}
```

Stop the workflow with Shift+F5.

:::workflow
![Local HTTP server workflow](~/workflows/10-http-server.bonsai)
:::

[Download the workflow](~/workflows/10-http-server.bonsai).

## Follow the server graph

1. A typed `PublishSubject<HttpServerResponse>` named `Responses` supplies the server's response input.
2. `HttpServer` configures `Url = http://127.0.0.1:8080/`, a ten-second `RequestDeadline`, `Method = GET`, and `Pattern = /hello`. It starts the listener and emits matching requests.
3. `SendResponse` opens a separate nested workflow for each request. Inside this example, `WorkflowInput` triggers a `String` property containing the JSON body, connected to `WorkflowOutput`.
4. The first nested output produces an `HttpServerResponse` retaining that request in `Request`. It does not send the reply itself.
5. `MulticastSubject`, named `Responses`, returns the paired response to `HttpServer`, which sends it to the correct caller.

Stop and restart after changing settings. If port 8080 is unavailable, change HttpServer.Url to use another port and update the URL in your browser. Stopping the workflow closes the listener and cancels unfinished requests.

## Pair requests and responses

The server reverses the client pattern: its output contains requests and its input accepts responses. Each `HttpServerResponse.Request` references the exact context emitted by that server subscription. Responses may arrive out of order; each one completes its own request. Responses without an originating context, or referencing an already answered or different server's request, raise a sequence error. Completing the response input leaves the listener running; an input error stops it. Without an input the server still receives requests, but unanswered requests expire at their deadline.

## Share listeners across routes

Separate `HttpServer` sources each own their listener, so they must use different listening addresses. The lower-level resource pattern remains available for extensions that need to register multiple routes on a shared `HttpServerResource`.

## Configure the response

| Property on `SendResponse` | Meaning | Default |
| --- | --- | --- |
| `StatusCode` | The status to send, from 100 to 599 | 200 |
| `ContentType` | The response media type | `text/plain` for strings; `application/octet-stream` for bytes |
| `Encoding` | Encoding name for string output, such as `UTF-8` or `utf-16` | Unset, using UTF-8 |
| `Headers` | Additional response headers | Empty |

These properties are captured when the outer workflow subscribes. For string output, the selected encoding determines both the body bytes and the content-type charset. A `byte[]` output is sent unchanged; `Encoding` does not apply to it. The example sets `ContentType = application/json`.

The nested workflow must output `string` or `byte[]`. Its first output creates the paired response and disposes that nested execution. Completion without a value creates an empty body. An error terminates the sequence; when propagated through the response subject, it stops the server and cancels unfinished requests. A request deadline or disconnection cancels unfinished nested processing.

## Match paths and inspect requests

Use a route template such as `/items/{id}`. Parameters must occupy an entire path segment. A request for `/items/42` produces a context whose `RouteValues["id"]` is `"42"`.

`HttpServerRequestContext` exposes `RequestId`, `Method`, `Uri`, `Headers`, `Body`, `RouteValues`, and a cancellation token. The URI includes the query string; query parameters do not participate in route-template matching. Incoming bodies are buffered as bytes.

Unknown paths receive 404. Known paths with an unregistered method receive 405 with an `Allow` header. Bodies larger than `MaxRequestBodyBytes` receive 413; the default limit is 16 MiB. A request that misses its response deadline currently receives 500.

## Change the response body

Open SendResponse to edit its nested workflow. In the example, select the String node and change its Value. The text reaching WorkflowOutput becomes the response body.

To respond with information from the request, select the relevant request properties inside the nested workflow and format them into a string. For example, use RouteValues to read a path parameter from a route such as /items/{id}. Connect the resulting text to WorkflowOutput. For binary responses, connect a byte-array output instead.

## HTTPS

On modern .NET, configure an HTTPS `Url`, `CertificatePath`, and, when needed, `CertificatePassword` on `HttpServer`. On Framework, use an operating-system certificate binding and leave CertificatePath unset. Start with the loopback HTTP example to verify the workflow before configuring certificates.
