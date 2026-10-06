using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed class ChatStorageOptions
{
    public const string SectionName = "Cosmos";

    public string ConnectionString { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string ContainerName { get; init; } = string.Empty;
}

public static class ChatStorageEnvironment
{
    public static string GetLabel(string connectionString) =>
        connectionString.Contains("AccountEndpoint=https://localhost:8081/", StringComparison.OrdinalIgnoreCase)
            ? "LOCAL"
            : "PRODUCTION";
}

public sealed class CosmosChatStoreInitializer(
    CosmosClient cosmosClient,
    IOptions<ChatStorageOptions> options) : IHostedService
{
    private readonly ChatStorageOptions options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var database = (await cosmosClient.CreateDatabaseIfNotExistsAsync(
            options.DatabaseName,
            cancellationToken: cancellationToken)).Database;

        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(options.ContainerName, "/userId"),
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CosmosChatSessionStore(
    CosmosClient cosmosClient,
    IOptions<ChatStorageOptions> options) : IChatSessionStore
{
    private readonly ChatStorageOptions options = options.Value;

    public async Task<IReadOnlyList<ChatSession>> GetSessionsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
            "SELECT * FROM sessions WHERE sessions.userId = @userId ORDER BY sessions.updatedAt DESC")
            .WithParameter("@userId", userId);
        var iterator = GetContainer().GetItemQueryIterator<CosmosChatSessionDocument>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
        var sessions = new List<ChatSession>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            sessions.AddRange(response.Select(session => session.ToChatSession()));
        }

        return sessions;
    }

    public async Task<ChatSession?> GetSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await GetContainer().ReadItemAsync<CosmosChatSessionDocument>(
                sessionId.ToString("D"),
                new PartitionKey(userId),
                cancellationToken: cancellationToken);
            return response.Resource.ToChatSession();
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task SaveSessionAsync(
        string userId,
        ChatSession session,
        CancellationToken cancellationToken) =>
        GetContainer().UpsertItemAsync(
            CosmosChatSessionDocument.From(userId, session),
            new PartitionKey(userId),
            cancellationToken: cancellationToken);

    private Container GetContainer() =>
        cosmosClient.GetContainer(options.DatabaseName, options.ContainerName);
}

public sealed record CosmosChatSessionDocument(
    [property: JsonProperty(PropertyName = "id")] string Id,
    [property: JsonProperty(PropertyName = "userId")] string UserId,
    [property: JsonProperty(PropertyName = "title")] string Title,
    [property: JsonProperty(PropertyName = "updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonProperty(PropertyName = "messages")] IReadOnlyList<ChatMessage> Messages)
{
    public static CosmosChatSessionDocument From(string userId, ChatSession session) =>
        new(session.Id.ToString("D"), userId, session.Title, session.UpdatedAt, session.Messages);

    public ChatSession ToChatSession() =>
        new(Guid.Parse(Id), Title, UpdatedAt, Messages);
}
