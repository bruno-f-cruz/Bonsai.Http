[CmdletBinding()]
param([string]$PrefectRoot)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$revision = 'd966177d3b4d3b39c69f63ffcdb3514d59e6c60f'
if (-not $PrefectRoot) {
    $PrefectRoot = Join-Path $repository 'artifacts/tools/prefect'
    if (-not (Test-Path -LiteralPath $PrefectRoot)) {
        git clone https://github.com/bonsai-rx/prefect.git $PrefectRoot
        if ($LASTEXITCODE -ne 0) { throw 'Could not obtain Prefect.' }
    }
    git -C $PrefectRoot checkout $revision
    if ($LASTEXITCODE -ne 0) { throw 'Could not select the pinned Prefect revision.' }
}
$reference = Join-Path $repository 'artifacts/prefect-reference'
New-Item -ItemType Directory -Force -Path $reference | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PrefectRoot 'reference') -Force | Copy-Item -Destination $reference -Recurse -Force
# The generic workflow hardcodes unsupported test frameworks. All other rules remain in effect.
Add-Content -LiteralPath (Join-Path $reference '.prefect-ignore-content') -Value '.github/workflows/$PROJECT$.yml'
dotnet run --project (Join-Path $PrefectRoot 'src/Prefect/Prefect.csproj') -p:NuGetAudit=false -- $reference $repository --project-name Bonsai.Http
if ($LASTEXITCODE -ne 0) { throw 'Prefect validation failed.' }
