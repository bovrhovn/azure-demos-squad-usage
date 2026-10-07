using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class AwesomeCopilotSkillCatalogTests
{
    [Fact]
    public async Task SearchAsync_returns_matching_top_level_skills()
    {
        using var client = CreateClient(
            ("git/trees/main?recursive=1", JsonResponse("""
                {"truncated":false,"tree":[
                  {"path":"skills/review-code/SKILL.md","type":"blob"},
                  {"path":"skills/test-code/SKILL.md","type":"blob"},
                  {"path":"skills/review-code/references/checklist.md","type":"blob"}]}
                """)));
        var catalog = CreateCatalog(client);

        var skills = await catalog.SearchAsync("review", CancellationToken.None);

        var skill = Assert.Single(skills);
        Assert.Equal("review-code", skill.Name);
    }

    [Fact]
    public async Task DownloadWithDependenciesAsync_downloads_skill_assets_and_referenced_skills()
    {
        using var client = CreateClient(
            ("git/trees/main?recursive=1", JsonResponse("""
                {"truncated":false,"tree":[
                  {"path":"skills/review-code/SKILL.md","type":"blob"},
                  {"path":"skills/shared-rules/SKILL.md","type":"blob"}]}
                """)),
            ("contents/skills/review-code?ref=main", JsonResponse("""
                [{"type":"file","path":"skills/review-code/SKILL.md","url":"https://api.test/files/review-skill","download_url":"https://raw.test/review-skill"},
                 {"type":"dir","path":"skills/review-code/references","url":"https://api.test/directories/review-references","download_url":null}]
                """)),
            ("https://raw.test/review-skill", TextResponse("Use [shared rules](../shared-rules/SKILL.md).")),
            ("directories/review-references", JsonResponse("""
                [{"type":"file","path":"skills/review-code/references/checklist.md","url":"https://api.test/files/checklist","download_url":"https://raw.test/checklist"}]
                """)),
            ("https://raw.test/checklist", TextResponse("Validate tests.")),
            ("contents/skills/shared-rules?ref=main", JsonResponse("""
                [{"type":"file","path":"skills/shared-rules/SKILL.md","url":"https://api.test/files/shared-skill","download_url":"https://raw.test/shared-skill"}]
                """)),
            ("https://raw.test/shared-skill", TextResponse("Use consistent rules.")));
        var catalog = CreateCatalog(client);

        var skills = await catalog.DownloadWithDependenciesAsync("review-code", CancellationToken.None);

        Assert.Collection(
            skills,
            reviewCode =>
            {
                Assert.Equal("review-code", reviewCode.Name);
                Assert.Contains(reviewCode.Files, file => file.RelativePath == "SKILL.md");
                Assert.Contains(reviewCode.Files, file => file.RelativePath == "references/checklist.md");
            },
            sharedRules => Assert.Equal("shared-rules", sharedRules.Name));
    }

    [Fact]
    public async Task GetPreviewAsync_returns_only_the_selected_skill_markdown()
    {
        using var client = CreateClient(
            ("git/trees/main?recursive=1", JsonResponse("""
                {"truncated":false,"tree":[
                  {"path":"skills/review-code/SKILL.md","type":"blob"}]}
                """)),
            ("contents/skills/review-code?ref=main", JsonResponse("""
                [{"type":"file","path":"skills/review-code/SKILL.md","url":"https://api.test/files/review-skill","download_url":"https://raw.test/review-skill"},
                 {"type":"file","path":"skills/review-code/references/checklist.md","url":"https://api.test/files/checklist","download_url":"https://raw.test/checklist"}]
                """)),
            ("https://raw.test/review-skill", TextResponse("# Review code")));
        var catalog = CreateCatalog(client);

        var preview = await catalog.GetPreviewAsync("review-code", CancellationToken.None);

        Assert.Equal("review-code", preview.Name);
        Assert.Equal("# Review code", preview.Markdown);
    }

    private static AwesomeCopilotSkillCatalog CreateCatalog(HttpClient client) =>
        new(
            client,
            Options.Create(new AwesomeCopilotOptions
            {
                ApiBaseUrl = "https://api.test/",
                Branch = "main",
                UserAgent = "test-client"
            }));

    private static HttpClient CreateClient(params (string Path, HttpResponseMessage Response)[] responses) =>
        new(new TestHttpMessageHandler(responses))
        {
            BaseAddress = new Uri("https://api.test/")
        };

    private static HttpResponseMessage JsonResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage TextResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/plain")
        };

    private sealed class TestHttpMessageHandler(
        IEnumerable<(string Path, HttpResponseMessage Response)> responses) : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpResponseMessage> responses = responses.ToDictionary(
            response => response.Path,
            response => response.Response,
            StringComparer.Ordinal);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestPath = request.RequestUri!.IsAbsoluteUri
                && request.RequestUri.AbsoluteUri.StartsWith("https://api.test/", StringComparison.Ordinal)
                ? request.RequestUri.PathAndQuery.TrimStart('/')
                : request.RequestUri!.AbsoluteUri;

            if (!responses.Remove(requestPath, out var response))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(response);
        }
    }
}
