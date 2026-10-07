[CmdletBinding()]
param(
    [string]$ResourceGroup = "rg-containers",

    [string]$AcrName = "bovrhovncr",

    [string]$CosmosAccountName = "bovrhovncdb",

    [string]$EnvironmentName = "rg-containers-private-env",

    [string]$AppName = "bovrhovn-gh-dynamic-module",

    [string]$Repository = "github/squad-dynamics",

    [string]$CosmosDatabaseName = "DynamicModuleDb",

    [Parameter(Mandatory = $true)]
    [string]$GitHubDeviceFlowClientId,

    [int]$HealthCheckAttempts = 30,

    [int]$HealthCheckDelaySeconds = 10
)

$ErrorActionPreference = "Stop"

function Invoke-Az {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    az @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')"
    }
}

function Get-NextVersion {
    param(
        [Parameter(Mandatory = $true)][string]$RegistryName,
        [Parameter(Mandatory = $true)][string]$ImageRepository
    )

    $tags = az acr repository show-tags --name $RegistryName --repository $ImageRepository --output tsv 2>$null
    if ($LASTEXITCODE -ne 0 -and $tags) {
        throw "Unable to determine the next version tag for '$ImageRepository'."
    }

    $versions = @(
        $tags |
            Where-Object { $_ -match '^v(?<number>\d+)$' } |
            ForEach-Object { [int]$Matches.number }
    )

    if ($versions.Count -eq 0) {
        return 1
    }

    return (($versions | Measure-Object -Maximum).Maximum + 1)
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI is required. Install it from https://aka.ms/installazurecliwindows."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "build-acr.ps1"
if (-not (Test-Path $buildScript -PathType Leaf)) {
    throw "ACR build script was not found at '$buildScript'."
}

Write-Host "Step 1/8: Validating Azure resources." -ForegroundColor Cyan
Invoke-Az @("group", "show", "--name", $ResourceGroup, "--output", "none")
Invoke-Az @("acr", "show", "--name", $AcrName, "--resource-group", $ResourceGroup, "--output", "none")
Invoke-Az @("cosmosdb", "show", "--name", $CosmosAccountName, "--resource-group", $ResourceGroup, "--output", "none")
Invoke-Az @("containerapp", "env", "show", "--name", $EnvironmentName, "--resource-group", $ResourceGroup, "--output", "none")

Write-Host "Step 2/8: Verifying Cosmos DB uses Microsoft Entra authentication." -ForegroundColor Cyan
$localAuthDisabled = az cosmosdb show --name $CosmosAccountName --resource-group $ResourceGroup --query "disableLocalAuth" --output tsv
if ($LASTEXITCODE -ne 0) {
    throw "Unable to determine the authentication configuration for Cosmos DB account '$CosmosAccountName'."
}

if ($localAuthDisabled -ne "true") {
    throw "Cosmos DB account '$CosmosAccountName' must have key authentication disabled so this deployment uses managed identity."
}

Write-Host "Step 3/8: Creating the Cosmos DB database and retrieving its account endpoint." -ForegroundColor Cyan
Invoke-Az @(
    "cosmosdb", "sql", "database", "create",
    "--account-name", $CosmosAccountName,
    "--resource-group", $ResourceGroup,
    "--name", $CosmosDatabaseName,
    "--output", "none"
)

$cosmosEndpoint = az cosmosdb show `
    --name $CosmosAccountName `
    --resource-group $ResourceGroup `
    --query "documentEndpoint" `
    --output tsv
if ($LASTEXITCODE -ne 0 -or -not [Uri]::IsWellFormedUriString($cosmosEndpoint, [UriKind]::Absolute)) {
    throw "Unable to retrieve the Cosmos DB account endpoint for '$CosmosAccountName'."
}

Write-Host "Step 4/8: Selecting the ACR image version." -ForegroundColor Cyan
$version = Get-NextVersion -RegistryName $AcrName -ImageRepository $Repository
$image = "$AcrName.azurecr.io/${Repository}:v$version"
Write-Host "Selected image tag v$version." -ForegroundColor Green

Write-Host "Step 5/8: Building and pushing the image through Azure Container Registry." -ForegroundColor Cyan
& $buildScript -AcrName $AcrName -ResourceGroup $ResourceGroup -Repository $Repository -Version $version
if ($LASTEXITCODE -ne 0) {
    throw "ACR build script failed."
}

$appExists = $true
az containerapp show --name $AppName --resource-group $ResourceGroup --output none 2>$null
if ($LASTEXITCODE -ne 0) {
    $appExists = $false
}

Write-Host "Step 6/8: Creating or updating Azure Container App '$AppName' with one to two replicas." -ForegroundColor Cyan
if (-not $appExists) {
    Invoke-Az @(
        "containerapp", "create",
        "--name", $AppName,
        "--resource-group", $ResourceGroup,
        "--environment", $EnvironmentName,
        "--image", "mcr.microsoft.com/k8se/quickstart:latest",
        "--system-assigned",
        "--ingress", "external",
        "--target-port", "8080",
        "--min-replicas", "1",
        "--max-replicas", "2",
        "--output", "none"
    )
}

Invoke-Az @("containerapp", "identity", "assign", "--name", $AppName, "--resource-group", $ResourceGroup, "--system-assigned", "--output", "none")
Invoke-Az @("containerapp", "registry", "set", "--name", $AppName, "--resource-group", $ResourceGroup, "--server", "$AcrName.azurecr.io", "--identity", "system", "--output", "none")
Invoke-Az @("containerapp", "secret", "set", "--name", $AppName, "--resource-group", $ResourceGroup, "--secrets", "cosmos-account-endpoint=$cosmosEndpoint", "--output", "none")

Write-Host "Step 7/8: Granting the app's managed identity access to ACR and Cosmos DB." -ForegroundColor Cyan
$principalId = az containerapp identity show --name $AppName --resource-group $ResourceGroup --query "principalId" --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($principalId)) {
    throw "Unable to retrieve the managed identity for Container App '$AppName'."
}

$acrResourceId = az acr show --name $AcrName --resource-group $ResourceGroup --query "id" --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($acrResourceId)) {
    throw "Unable to retrieve the resource ID for Azure Container Registry '$AcrName'."
}

$existingRoleAssignment = az role assignment list `
    --assignee-object-id $principalId `
    --scope $acrResourceId `
    --query "[?roleDefinitionName=='Container Registry Repository Reader' && contains(condition, '$Repository')].id | [0]" `
    --output tsv
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect ACR role assignments for Container App '$AppName'."
}

if ([string]::IsNullOrWhiteSpace($existingRoleAssignment)) {
    $condition = "((!(ActionMatches{'Microsoft.ContainerRegistry/registries/repositories/content/read'})) OR (@Resource[Microsoft.ContainerRegistry/registries/repositories:name] StringEqualsIgnoreCase '$Repository'))"
    Invoke-Az @(
        "role", "assignment", "create",
        "--assignee-object-id", $principalId,
        "--assignee-principal-type", "ServicePrincipal",
        "--role", "Container Registry Repository Reader",
        "--scope", $acrResourceId,
        "--condition", $condition,
        "--condition-version", "2.0",
        "--output", "none"
    )
}

$cosmosRoleAssignment = az cosmosdb sql role assignment list `
    --account-name $CosmosAccountName `
    --resource-group $ResourceGroup `
    --query "[?principalId=='$principalId' && ends_with(roleDefinitionId, '00000000-0000-0000-0000-000000000002')].id | [0]" `
    --output tsv
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect Cosmos DB role assignments for Container App '$AppName'."
}

if ([string]::IsNullOrWhiteSpace($cosmosRoleAssignment)) {
    Invoke-Az @(
        "cosmosdb", "sql", "role", "assignment", "create",
        "--account-name", $CosmosAccountName,
        "--resource-group", $ResourceGroup,
        "--scope", "/",
        "--principal-id", $principalId,
        "--role-definition-id", "00000000-0000-0000-0000-000000000002",
        "--output", "none"
    )
}

Write-Host "Step 8/8: Deploying the ACR image and waiting for the public health endpoint." -ForegroundColor Cyan
Invoke-Az @(
    "containerapp", "update",
    "--name", $AppName,
    "--resource-group", $ResourceGroup,
    "--image", $image,
    "--min-replicas", "1",
    "--max-replicas", "2",
    "--remove-env-vars", "Cosmos__ConnectionString",
    "--set-env-vars",
    "Cosmos__AccountEndpoint=secretref:cosmos-account-endpoint",
    "GitHubDeviceFlow__ClientId=$GitHubDeviceFlowClientId",
    "--output", "none"
)
Invoke-Az @("containerapp", "ingress", "enable", "--name", $AppName, "--resource-group", $ResourceGroup, "--type", "external", "--target-port", "8080", "--transport", "auto", "--output", "none")

$fqdn = az containerapp show --name $AppName --resource-group $ResourceGroup --query "properties.configuration.ingress.fqdn" --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($fqdn)) {
    throw "Unable to determine the public hostname for Container App '$AppName'."
}

$healthUrl = "https://$fqdn/health"
for ($attempt = 1; $attempt -le $HealthCheckAttempts; $attempt++) {
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 20
        if ($response.StatusCode -eq 200) {
            Write-Host "Health check passed on attempt ${attempt}: $healthUrl" -ForegroundColor Green
            Write-Host ""
            Write-Host "Deployment summary" -ForegroundColor Green
            Write-Host "  Resource group: $ResourceGroup"
            Write-Host "  Container App: $AppName"
            Write-Host "  Image: $image"
            Write-Host "  Scale: 1 to 2 replicas"
            Write-Host "  Cosmos DB: $CosmosAccountName (managed identity with an ACA secret endpoint)"
            Write-Host "  GitHub device flow: client ID configured"
            Write-Host "  Health: $healthUrl"
            exit 0
        }
    }
    catch {
        Write-Host "Health check attempt $attempt/$HealthCheckAttempts has not succeeded yet." -ForegroundColor Yellow
    }

    if ($attempt -lt $HealthCheckAttempts) {
        Start-Sleep -Seconds $HealthCheckDelaySeconds
    }
}

throw "Container App '$AppName' did not return HTTP 200 from '$healthUrl' after $HealthCheckAttempts attempts."
