[CmdletBinding()]
param(
    [ValidateSet('01-public-get', '02-public-post', '03-status-as-data', '04-timeout', '05-local-server', '06-current-weather', '07-earthquakes', '08-response-text')]
    [string]$Example = '01-public-get',
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repository 'src/Bonsai.Http.Design/Bonsai.Http.Design.csproj'
dotnet build $project --configuration Debug --framework net472 --runtime win-x64 -p:CopyLocalLockFileAssemblies=true
if ($LASTEXITCODE -ne 0) { throw 'The example libraries could not be built.' }
dotnet build (Join-Path $repository 'src/Bonsai.Http.Server/Bonsai.Http.Server.csproj') --configuration Debug --framework net472 -p:CopyLocalLockFileAssemblies=true
if ($LASTEXITCODE -ne 0) { throw 'The server example library could not be built.' }
$libraries = (dotnet msbuild $project -nologo -getProperty:TargetDir -p:Configuration=Debug -p:TargetFramework=net472 -p:RuntimeIdentifier=win-x64 | Select-Object -Last 1).Trim()
$serverProject = Join-Path $repository 'src/Bonsai.Http.Server/Bonsai.Http.Server.csproj'
$serverLibraries = (dotnet msbuild $serverProject -nologo -getProperty:TargetDir -p:Configuration=Debug -p:TargetFramework=net472 | Select-Object -Last 1).Trim()
if (-not (Test-Path (Join-Path $libraries 'Bonsai.Http.Design.dll')) -or -not (Test-Path (Join-Path $serverLibraries 'Bonsai.Http.Server.dll'))) { throw 'The built example assemblies could not be found.' }
if ($BuildOnly) { return }
$bonsai = Get-Command bonsai -ErrorAction SilentlyContinue
if ($null -eq $bonsai) { throw 'Install Bonsai with Add to PATH enabled: https://bonsai-rx.org/docs/' }
$workflow = Join-Path $repository ('docs/workflows/' + $Example + '.bonsai')
& $bonsai.Source '--no-env' "--lib:$libraries" "--lib:$serverLibraries" $workflow
