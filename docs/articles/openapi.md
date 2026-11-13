---
title: OpenAPI generation
---

# OpenAPI generation

> [!WARNING]
> Under construction. This guide currently covers code generation only. Bonsai integration documentation will follow.

Generate request and response code from an API's OpenAPI description using Bonsai.Http.OpenApi.

## Generate code

You need the .NET 8 SDK for this preparation step. Install the generator from the package feed you use for Bonsai.Http:

```powershell
dotnet tool install --global Bonsai.Http.OpenApi --prerelease
```

If your packages come from a custom feed, configure that feed for the .NET CLI as well. The generator command is `bonsai.http.openapi`.

Obtain the service's OpenAPI description as a JSON or YAML file. For the post example, download [this small OpenAPI description](https://raw.githubusercontent.com/bruno-f-cruz/Bonsai.Http/main/docs/examples/jsonplaceholder.openapi.json) and save it as `api.openapi.json` in your workflow folder.

Run the generator from that folder:

```powershell
bonsai.http.openapi api.openapi.json ApiExtensions/Generated JsonPlaceholderApi
```

The arguments are the OpenAPI file, the output folder, and the namespace for the generated nodes. The generator uses the published Bonsai.Sgen tool automatically. The output contains request nodes, operation nodes, and response models.
