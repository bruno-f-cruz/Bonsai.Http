[CmdletBinding()]
param(
    [ValidateSet('01-public-get', '02-public-post', '03-status-as-data', '04-timeout', '05-local-server')]
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
if ($BuildOnly) { return }
$bonsai = Get-Command bonsai -ErrorAction SilentlyContinue
if ($null -eq $bonsai) { throw 'Install Bonsai with Add to PATH enabled: https://bonsai-rx.org/docs/' }
$libraries = Join-Path $repository 'artifacts/bin/Bonsai.Http.Design/debug_net472_win-x64'
$serverLibraries = Join-Path $repository 'artifacts/bin/Bonsai.Http.Server/debug_net472'
$workflow = Join-Path $PSScriptRoot ($Example + '.bonsai')
& $bonsai.Source '--no-env' "--lib:$libraries" "--lib:$serverLibraries" $workflow
