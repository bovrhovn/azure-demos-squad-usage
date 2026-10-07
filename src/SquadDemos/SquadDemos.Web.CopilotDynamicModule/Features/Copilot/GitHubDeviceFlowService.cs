using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

public sealed class GitHubDeviceFlowOptions
{
    public const string SectionName = "GitHubDeviceFlow";

    public string ClientId { get; init; } = string.Empty;

    public string Scopes { get; init; } = "read:user";
}

public sealed record GitHubDeviceAuthorization(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete);

public enum GitHubDeviceFlowResultKind
{
    Authorized,
    Pending,
    Expired,
    Denied
}

public sealed record GitHubDeviceFlowResult(GitHubDeviceFlowResultKind Kind, string? AccessToken = null);

public interface IGitHubDeviceFlowService
{
    Task<GitHubDeviceAuthorization> BeginAsync(CancellationToken cancellationToken);

    Task<GitHubDeviceFlowResult> CompleteAsync(string deviceCode, CancellationToken cancellationToken);

    Task<string> GetLoginAsync(string accessToken, CancellationToken cancellationToken);
}

public sealed class GitHubDeviceFlowService(
    HttpClient httpClient,
    IOptions<GitHubDeviceFlowOptions> options) : IGitHubDeviceFlowService
{
    private readonly GitHubDeviceFlowOptions options = options.Value;

    public async Task<GitHubDeviceAuthorization> BeginAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["scope"] = options.Scopes
            })
        };
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<DeviceAuthorizationResponse>(cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.DeviceCode))
        {
            throw new InvalidOperationException("GitHub could not start device authorization.");
        }

        return new GitHubDeviceAuthorization(
            payload.DeviceCode,
            payload.UserCode,
            payload.VerificationUri,
            payload.VerificationUriComplete);
    }

    public async Task<GitHubDeviceFlowResult> CompleteAsync(string deviceCode, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            })
        };
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(payload?.AccessToken))
        {
            return new GitHubDeviceFlowResult(GitHubDeviceFlowResultKind.Authorized, payload.AccessToken);
        }

        return payload?.Error switch
        {
            "authorization_pending" or "slow_down" => new GitHubDeviceFlowResult(GitHubDeviceFlowResultKind.Pending),
            "expired_token" => new GitHubDeviceFlowResult(GitHubDeviceFlowResultKind.Expired),
            "access_denied" => new GitHubDeviceFlowResult(GitHubDeviceFlowResultKind.Denied),
            _ => throw new InvalidOperationException("GitHub could not complete device authorization.")
        };
    }

    public async Task<string> GetLoginAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("SquadDemos-CopilotDynamicModule");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GitHubUserResponse>(cancellationToken);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(payload?.Login))
        {
            throw new InvalidOperationException("GitHub could not identify the authenticated user.");
        }

        return payload.Login;
    }

    private sealed record DeviceAuthorizationResponse(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_uri")] string VerificationUri,
        [property: JsonPropertyName("verification_uri_complete")] string? VerificationUriComplete);

    private sealed record AccessTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record GitHubUserResponse(
        [property: JsonPropertyName("login")] string? Login);
}
