---
title: HTTP workflows for Bonsai
description: Make HTTP requests, serve local endpoints, and generate typed API operators in Bonsai.
---

<div class="http-hero">
<p class="http-eyebrow">BONSAI.HTTP / USER GUIDE</p>

# Connect your workflow to the web.

Make a request. Read the response. Serve an endpoint. Start with ordinary HTTP and add typed OpenAPI operations when you need them.

<div class="http-actions"><a class="http-button" href="articles/quick-start.md">Make your first request →</a><a class="http-button http-button-secondary" href="examples/README.md">Browse runnable examples</a></div>
</div>

<div class="http-cards">
<a class="http-card" href="articles/client.md"><span class="http-card-number">01 / REQUEST</span><strong class="http-card-title">One shared client.</strong><p>Share an HttpClient through a ResourceSubject. Send requests on demand or on every Timer tick.</p><span>Client workflows →</span></a>
<a class="http-card" href="articles/server.md"><span class="http-card-number">02 / RESPOND</span><strong class="http-card-title">Your own endpoint.</strong><p>Receive an HTTP request inside Bonsai, build a reply, and send it back to the caller.</p><span>Server walkthrough →</span></a>
<a class="http-card" href="articles/openapi.md"><span class="http-card-number">03 / TYPE</span><strong class="http-card-title">From schema to nodes.</strong><p>Generate request and response models with Bonsai.Sgen and typed operation nodes.</p><span>OpenAPI walkthrough →</span></a>
</div>

## A response is a value

`SendRequest` emits a complete `HttpResponse`: status, headers, body bytes, duration, and the request that produced it. HTTP 404 and 500 responses are ordinary values. Add `EnsureSuccessStatusCode` when a non-success status should end the sequence.

Use `DecodeResponse` downstream to emit a string or schema-independent JSON. Use generated OpenAPI operations when you want a parsed C# model alongside the transport response.

## Start with live data

From this repository on Windows, with Bonsai on your PATH and the .NET 8 SDK installed:

```powershell
pwsh docs/examples/run.ps1 08-response-text
```

Press **F5**. The example retrieves current Lisbon weather from Open-Meteo. Inspect `SendRequest` for the full response or `DecodeResponse` for its text. Change `DecodeResponse.Format` to `Json`, stop, and restart to change the downstream output type.

[Follow the quick start](articles/quick-start.md) for prerequisites and each connection in the graph.

## Choose the package you need

| Package | Use it for |
| --- | --- |
| `Bonsai.Http` | Client resources, request values, sending, status checks, and decoding |
| `Bonsai.Http.Server` | Listening, route matching, incoming requests, and replies |
| `Bonsai.Http.Design` | Windows Dear ImGui request editing and response visualization |
| `Bonsai.Http.OpenApi` | A .NET CLI that generates models and typed operation nodes |

The runtime libraries target .NET Framework 4.7.2 and .NET 8. The Bonsai editor examples use the Framework assemblies. The current design package targets Windows x64.

## Next steps

- [Configure a client](articles/client.md): headers, timeouts, concurrency, and resource lifetime.
- [Decode responses](articles/responses.md): text, JSON, status codes, and sequence errors.
- [Run a local server](articles/server.md): serve JSON and test it from PowerShell.
- [Generate typed operations](articles/openapi.md): run the CLI and use its output in Bonsai.
- [Find a runnable example](examples/README.md): public APIs, error handling, local endpoints, and OpenAPI.
