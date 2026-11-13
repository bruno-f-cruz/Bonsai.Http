[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 8090,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repository
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore the documentation tools.' }
    if (-not $NoBuild) {
        & (Join-Path $PSScriptRoot 'build.ps1') -docfxArgs @('--warningsAsErrors')
    }
    $site = Join-Path $repository 'artifacts/docs/site'
    if (-not (Test-Path (Join-Path $site 'index.html'))) { throw 'Build the documentation before serving it.' }
    Write-Host "Documentation: http://localhost:$Port (Ctrl+C to stop)"
    dotnet docfx serve $site --hostname localhost --port $Port
} finally {
    Pop-Location
}
