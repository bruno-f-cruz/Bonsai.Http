[CmdletBinding()] param (
    [string[]]$LibrarySources,
    [bool]$UseGalleryForWorkflowsDirectory=$false,
    [bool]$UseGalleryForExamplesDirectory=$true,
    [string]$OutputFolder=$null
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($OutputFolder) {
    $OutputFolder = Join-Path (Get-Location) $OutputFolder
}

function Process-Workflow-Collection([bool]$useGallery, [string]$workflowPath, [string]$environmentPath) {
    $libPath = $LibrarySources

    if ($useGallery) {
        $libPath = @()
        $galleryPath = Join-Path $environmentPath 'Gallery'
        $null = New-Item -ItemType Directory -Path $galleryPath -Force
        foreach ($librarySource in $LibrarySources) {
            Get-ChildItem -Path $librarySource -Filter *.nupkg | Copy-Item -Destination $galleryPath
        }
    }

    $bootstrapperPath = (Join-Path $environmentPath 'Bonsai.exe')
    .\bonsai-docfx\modules\Export-Image.ps1 -libPath $libPath -workflowPath $workflowPath -bootstrapperPath $bootstrapperPath -outputFolder $OutputFolder -documentationRoot $PSScriptRoot
}

Push-Location $PSScriptRoot
try {
    if (Test-Path -Path 'workflows/') {
        Process-Workflow-Collection $UseGalleryForWorkflowsDirectory './workflows' '../.bonsai/'
    }

    if (Test-Path -Path 'examples/') {
        foreach ($environment in (Get-ChildItem -Path 'examples/' -Filter '.bonsai' -Recurse -FollowSymlink -Directory)) {
            Process-Workflow-Collection $UseGalleryForExamplesDirectory ($environment.Parent.FullName) ($environment.FullName)
        }
    }

    # The server is modern .NET only; use Bonsai's exporter in a matching host.
    $serverProject = Join-Path $PSScriptRoot 'examples/server/Extensions.csproj'
    if (Test-Path $serverProject) {
        $workflow = Join-Path $PSScriptRoot 'examples/server/05-local-server.bonsai'
        $svgPath = [IO.Path]::ChangeExtension($workflow, '.svg')
        if ($OutputFolder) { $svgPath = Join-Path $OutputFolder 'examples/server/05-local-server.svg' }
        $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $svgPath)
        dotnet run --project $serverProject --configuration Release --no-build -- --export-image $svgPath $workflow
        Import-Module (Join-Path $PSScriptRoot 'bonsai-docfx/modules/Export-Tools.psm1') -Verbose:$false
        Convert-Svg $svgPath
    }
} finally {
    Pop-Location
}
