# Azure Container Registry builds

`build-acr.ps1` submits the repository root as the Azure Container Registry (ACR) Task build context. ACR builds
the `containers\copilot-dynamic-module\Dockerfile` remotely, so no local container runtime is required.

## Prerequisites

- Azure CLI authenticated with `az login`
- An existing Azure Container Registry
- Permission to queue ACR Tasks, such as the **AcrPush** role on the registry

## Build and push

From the repository root:

```powershell
.\azure\build-acr.ps1 -AcrName bovrhovncr -ResourceGroup rg-containers
```

The script determines the next numeric version and publishes both
`github/squad-dynamics:vN` and `github/squad-dynamics:latest`. The first invocation publishes `v1`; use
`-Version` to publish a specific version or `-Repository` to select a different repository path.

The command uses `--source-acr-auth-id [caller]`, which passes the Azure CLI caller identity to ACR Tasks. This is
required by registries that use ABAC repository permissions. Use `-NoPush` to validate the cloud build without
publishing the image:

```powershell
.\azure\build-acr.ps1 -AcrName bovrhovncr -ResourceGroup rg-containers -NoPush
```

## Deploy to Azure Container Apps

`deploy-containerapp.ps1` builds a new version through `build-acr.ps1`, assigns the Container App's
managed identity the Cosmos DB data-contributor role, and supplies the account endpoint from an ACA
secret. This preserves the account's disabled key authentication setting. It configures external HTTPS
ingress on port 8080 and scales between one and two replicas. The script finishes only after the
public `/health` endpoint reports success.

```powershell
.\azure\deploy-containerapp.ps1 -GitHubDeviceFlowClientId <github-oauth-app-client-id>
```

The defaults target `rg-containers`, `bovrhovncr`, `bovrhovncdb`, the existing
`rg-containers-private-env` environment, and the `bovrhovn-gh-dynamic-module` app. Override these
with the matching script parameters when deploying to another environment. The GitHub OAuth app
client ID is required and becomes the `GitHubDeviceFlow__ClientId` Container App environment variable.
