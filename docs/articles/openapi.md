---
title: OpenAPI operations
---

# OpenAPI operations

An API package can provide request and response nodes generated from its OpenAPI description. These nodes expose named properties for the API's parameters and return typed results, so you do not need to assemble URLs or parse JSON by hand.

If your API already has a Bonsai package, install that package and skip to [Connect a typed request](#connect-a-typed-request). Otherwise, generate the nodes and build a Bonsai extension as described below. Installing Bonsai.Http alone does not add nodes for every service.

## Generate the API nodes

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

## Build a Bonsai extension

Create `ApiExtensions/Extensions.csproj` with the following content for the Framework Bonsai editor:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>JsonPlaceholderApi</AssemblyName>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

Add the HTTP package, then build the extension:

```powershell
dotnet add ApiExtensions/Extensions.csproj package Bonsai.Http --prerelease
dotnet build ApiExtensions/Extensions.csproj --configuration Release
```

Use the same Bonsai.Http version as your Bonsai environment. The generated files under `Generated` are included automatically. For a modern .NET Bonsai host, target `net8.0` instead of `net472`.

Open your workflow with the compiled extension available:

```powershell
bonsai --lib:"ApiExtensions/bin/Release/net472" workflow.bonsai
```

Search the toolbox for `CreateGetPostRequest` and `GetPost`. These nodes come from your compiled extension. Keep this library folder available whenever you reopen the workflow.

Build the generated code with the SDK as above rather than placing it in an older editor's automatically compiled `Extensions` source folder: the generated code uses modern C# syntax. You can still use ordinary Bonsai extensions downstream to process its typed results.

## Connect a typed request

The example below uses a generated JSONPlaceholder API package. It provides CreateGetPostRequest and GetPost nodes for retrieving a post.

:::workflow
![Typed OpenAPI request workflow](~/workflows/09-openapi-post.bonsai)
:::

If these nodes are available in your environment:

1. Add CreateHttpClient and set BaseAddress to https://jsonplaceholder.typicode.com/.
2. Connect it to a ResourceSubject named Api.
3. Add CreateGetPostRequest. Expand its Value property and set PostId to 1.
4. Connect the request node to the first input of GetPost.
5. Add SubscribeSubject named Api and connect it to the second input of GetPost.
6. Press F5 to request the post. Stop and restart to send again.

For another API package, use its corresponding request and operation nodes and set the client's base address to that service.

## Read the result

The result contains both the parsed response and the original HTTP response. For this example:

| Result | Meaning |
| --- | --- |
| GetPostStatus200 | The post was found; Value contains its Id, UserId, Title, and Body |
| GetPostStatus404 | The post was not found |
| GetPostUnknown | The response did not match a declared result |

Filter for the result type you want before selecting its model properties. For example, select successful results before reading Value.Title.

Connect GetHttpResponse after the operation to inspect the status, headers, body, and timing with the HTTP response visualizer. You can also connect that response to DecodeResponse to read its body as text or JSON.

## Handle errors

HTTP error statuses remain values. Add the generated EnsureGetPostSuccessStatusCode node if a non-success status should stop the workflow. Other API packages provide an equivalent node named for their operation.

A response that cannot be parsed as the expected model raises a parsing error. The original response is retained for diagnosis. Network failures and client timeouts behave the same as ordinary [client requests](responses.md).

## Update the generated nodes

When the service's API description changes, run the same generation command again, rebuild the extension, and restart Bonsai. The generator replaces its own files and removes obsolete generated operations. Keep your custom extension code in separate files so regeneration preserves it.
