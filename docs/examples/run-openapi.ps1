[CmdletBinding()]
param([switch]$BuildOnly)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$generator = Join-Path $repository 'src/Bonsai.Http.OpenApi/Bonsai.Http.OpenApi.csproj'
$schema = Join-Path $PSScriptRoot 'jsonplaceholder.openapi.json'
$generated = Join-Path $repository 'artifacts/generated/JsonPlaceholderApi'
dotnet run --project $generator --configuration Release -- $schema $generated JsonPlaceholderApi
if ($LASTEXITCODE -ne 0) { throw 'OpenAPI generation failed.' }

$project = Join-Path $repository 'docs/workflows/Extensions/Extensions.csproj'
dotnet build $project --configuration Release -p:CopyLocalLockFileAssemblies=true
if ($LASTEXITCODE -ne 0) { throw 'The generated API could not be built.' }

$design = Join-Path $repository 'src/Bonsai.Http.Design/Bonsai.Http.Design.csproj'
dotnet build $design --configuration Release --framework net472 --runtime win-x64 -p:CopyLocalLockFileAssemblies=true
if ($LASTEXITCODE -ne 0) { throw 'The HTTP editor could not be built.' }
if ($BuildOnly) { return }

$libraries = (dotnet msbuild $project -nologo -getProperty:TargetDir -p:Configuration=Release -p:TargetFramework=net472 | Select-Object -Last 1).Trim()
$designLibraries = (dotnet msbuild $design -nologo -getProperty:TargetDir -p:Configuration=Release -p:TargetFramework=net472 -p:RuntimeIdentifier=win-x64 | Select-Object -Last 1).Trim()
$bonsai = Get-Command bonsai -ErrorAction Stop
& $bonsai.Source --no-env "--lib:$libraries" "--lib:$designLibraries" (Join-Path $repository 'docs/workflows/09-openapi-post.bonsai')
