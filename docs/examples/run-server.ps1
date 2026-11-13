[CmdletBinding()]
param(
    [switch]$BuildOnly,
    [string]$Url = 'http://127.0.0.1:8080/'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'server/Extensions.csproj'
dotnet build $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Could not build the modern .NET server workflow host.' }
if ($BuildOnly) { return }
dotnet run --project $project --configuration Release --no-build -- (Join-Path $PSScriptRoot 'server/05-local-server.bonsai') --url $Url
if ($LASTEXITCODE -ne 0) { throw 'The server workflow failed.' }
