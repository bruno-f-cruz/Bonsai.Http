---
uid: overview
---

Bonsai.Http lets you send HTTP requests and process their responses in Bonsai. Use it to retrieve data from a public API, send data to a service, or expose an HTTP endpoint from your workflow.

These guides assume the packages are already installed in your Bonsai environment. Use Bonsai.Http for requests and responses, and Bonsai.Http.Server for server endpoints. The HTTP design package adds a request editor and a response visualizer.

## Getting started

Start with [Make your first request](quick-start.md). Copy the example workflow into Bonsai, run it, and inspect the response.

- [Client workflows](client.md): configure requests, headers, and a shared client.
- [Responses and decoding](responses.md): read text or JSON and handle HTTP errors.
- [Serve an HTTP endpoint](server.md): receive requests and produce replies in a nested workflow.
- [OpenAPI operations](openapi.md): use typed nodes supplied by an API package.
- [Examples](../examples/README.md): workflows you can copy into Bonsai.
