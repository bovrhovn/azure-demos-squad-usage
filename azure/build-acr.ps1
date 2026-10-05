[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AcrName,

    [string]$ResourceGroup,

    [string]$Repository = "github/squad-dynamics",

    [Nullable[int]]$Version,

    [switch]$NoPush
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI is required. Install it from https://aka.ms/installazurecliwindows."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$dockerfile = Join-Path $repoRoot "containers\copilot-dynamic-module\Dockerfile"

if (-not (Test-Path $dockerfile -PathType Leaf)) {
    throw "Container Dockerfile was not found at '$dockerfile'."
}

if ($null -eq $Version) {
    $existingTags = az acr repository show-tags --name $AcrName --repository $Repository --output tsv 2>$null
    if ($LASTEXITCODE -ne 0 -and $existingTags) {
        throw "Unable to determine the next version tag for '$Repository'."
    }

    $existingVersions = @(
        $existingTags |
            Where-Object { $_ -match '^v(?<number>\d+)$' } |
            ForEach-Object { [int]$Matches.number }
    )
    $Version = if ($existingVersions.Count -eq 0) { 1 } else { ($existingVersions | Measure-Object -Maximum).Maximum + 1 }
}

if ($Version -lt 1) {
    throw "Version must be greater than zero."
}

$versionTag = "v$Version"
$arguments = @(
    "acr", "build",
    "--registry", $AcrName,
    "--source-acr-auth-id", "[caller]",
    "--image", "${Repository}:$versionTag",
    "--image", "${Repository}:latest",
    "--file", $dockerfile
)

if ($ResourceGroup) {
    $arguments += @("--resource-group", $ResourceGroup)
}

if ($NoPush) {
    $arguments += "--no-push"
}

$arguments += $repoRoot

Write-Host "Building $Repository with tags $versionTag and latest in $AcrName.azurecr.io from $repoRoot" -ForegroundColor Cyan
az @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Azure Container Registry build failed."
}

if (-not $NoPush) {
    Write-Host "Images available at $AcrName.azurecr.io/${Repository}:$versionTag and $AcrName.azurecr.io/${Repository}:latest" -ForegroundColor Green
}
