---
uid: overview
---

Bonsai.Http is a [Bonsai](https://bonsai-rx.org/) package for sending HTTP requests and processing their responses in observable workflows. You can use it to retrieve data from public APIs, send data to a service, or expose HTTP endpoints from Bonsai.

The core package works with request and response values without requiring a schema. `Bonsai.Http.Server` adds server operators, and `Bonsai.Http.OpenApi` generates typed client operations from OpenAPI contracts using the published `Bonsai.Sgen` tool. `Bonsai.Http.Design` provides request editing and response visualization using Dear ImGui on Windows.

## Installing the package

The runtime libraries target .NET Framework 4.7.2 for the Bonsai editor and .NET 8 for other hosts. The design package currently requires Windows x64.

When using a package feed containing these packages, install `Bonsai.Http` and `Bonsai.Http.Design` through the Bonsai package manager. Install `Bonsai.Http.Server` to create server endpoints. The OpenAPI generator is a separate .NET CLI tool.

For the current development checkout, the example launchers build the local libraries and open Bonsai with them available. With Bonsai on PATH, PowerShell 7, and the .NET 8 SDK installed, run the following from the repository root:

```powershell
pwsh docs/examples/run.ps1 08-response-text
```

See [Make your first request](quick-start.md) for the complete setup and workflow connections, [Serve an HTTP endpoint](server.md) for the server example, or [Generate typed OpenAPI operations](openapi.md) for the generator walkthrough.
