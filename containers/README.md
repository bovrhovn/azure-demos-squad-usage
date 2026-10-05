# Containers

## Copilot Dynamic Module web app

Build from the repository root so the Dockerfile can copy the web-project source:

```powershell
wslc build --file containers\copilot-dynamic-module\Dockerfile --tag squad-demos-copilot-web:local .
```

Run the image on port 8080:

```powershell
wslc run --rm --publish 8080:8080 --name squad-demos-copilot-web squad-demos-copilot-web:local
```

Browse to `http://localhost:8080` and complete the GitHub device-authentication flow. The Copilot CLI
authentication state must be available to the container for the GitHub Copilot SDK to authenticate.

### Reverse proxies

The app consumes `X-Forwarded-For` and `X-Forwarded-Proto` before HTTPS redirection. It only trusts loopback
proxies by default. Configure the specific address of an upstream proxy with
`ReverseProxy__TrustedProxyAddresses__0`; for example:

```powershell
wslc run --rm --publish 8080:8080 `
  --env ReverseProxy__TrustedProxyAddresses__0=172.20.0.10 `
  squad-demos-copilot-web:local
```

Set `ReverseProxy__TrustAllForwardedHeaders=true` only when the container network is private and every caller
is a trusted proxy. Trusting forwarded headers from arbitrary clients permits spoofing the original scheme and
client IP address.

The chat UI connects to SignalR at `/hubs/chat`. A proxy must support WebSocket upgrades and route that path
to the same application instance. For multiple application instances, use an Azure SignalR Service backplane
or enable session affinity.
