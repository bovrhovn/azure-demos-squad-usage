using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Options;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed class FoundryChatClient(
    HttpClient httpClient,
    TokenCredential credential,
    IOptions<FoundryOptions> options) : IFoundryChatClient
{
    private readonly FoundryOptions options = options.Value;

    public async Task<string> GetResponseAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var token = await credential.GetTokenAsync(
            new TokenRequestContext([options.Scope]),
            cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.Endpoint.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(options.Deployment)}/chat/completions?api-version={Uri.EscapeDataString(options.ApiVersion)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = JsonContent.Create(new
        {
            messages = BuildMessages(messages),
            temperature = 0.2
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FoundryRequestException(response.StatusCode, payload);
        }

        using var document = JsonDocument.Parse(payload);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(content)
            ? throw new FoundryRequestException(response.StatusCode, "Foundry returned an empty response.")
            : content;
    }

    private object[] BuildMessages(IReadOnlyList<ChatMessage> messages)
    {
        var result = new List<object>();
        if (!string.IsNullOrWhiteSpace(options.SystemPrompt))
        {
            result.Add(new { role = "system", content = options.SystemPrompt });
        }

        result.AddRange(messages.Select(message => new { role = message.Role, content = message.Content }));
        return [.. result];
    }

    private void EnsureConfigured()
    {
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _)
            || string.IsNullOrWhiteSpace(options.Deployment)
            || string.IsNullOrWhiteSpace(options.ApiVersion)
            || string.IsNullOrWhiteSpace(options.Scope))
        {
            throw new InvalidOperationException(
                "Foundry is not configured. Set Foundry:Endpoint, Foundry:Deployment, Foundry:ApiVersion, and Foundry:Scope.");
        }
    }
}

public sealed class FoundryRequestException(System.Net.HttpStatusCode statusCode, string responseBody)
    : Exception($"Foundry request failed with status code {(int)statusCode}.")
{
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;

    public string ResponseBody { get; } = responseBody;
}
