# Bonsai.Http

Bonsai packages for schema-agnostic HTTP clients and servers, typed OpenAPI operations, and Dear ImGui inspection.

## Packages

- `Bonsai.Http` provides a reusable `CreateHttpClient` resource, `SendRequest`, request/response values, and status/error operators.
- `Bonsai.Http.Server` provides a Kestrel server resource, route matching, incoming request contexts, and response sending.
- `Bonsai.Http.OpenApi` is a .NET tool that invokes the published `Bonsai.Sgen` tool for model generation and emits typed operation operators.
- `Bonsai.Http.Design` contains Dear ImGui response inspection components.

The runtime targets .NET 8 and uses `Bonsai.Core` 2.9.0. HTTP status codes are values by default; place `EnsureSuccessStatusCode` downstream to raise for non-2xx responses. Transport errors and configured timeouts terminate the observable sequence. Responses are buffered up to the configured 16 MiB default limit.

See the [package guide](index.md) and the [OpenAPI sample](../samples/openapi.json).
