using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

public sealed class AwesomeCopilotOptions
{
    public const string SectionName = "AwesomeCopilot";

    public string ApiBaseUrl { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string UserAgent { get; init; } = string.Empty;
}

public sealed record AwesomeCopilotSkill(string Name);

public sealed record RemoteSkillFile(string RelativePath, byte[] Content);

public sealed record DownloadedAwesomeCopilotSkill(string Name, IReadOnlyList<RemoteSkillFile> Files);

public sealed record AwesomeCopilotSkillPreview(string Name, string Markdown);

public interface IAwesomeCopilotSkillCatalog
{
    Task<IReadOnlyList<AwesomeCopilotSkill>> SearchAsync(string? searchTerm, CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadedAwesomeCopilotSkill>> DownloadWithDependenciesAsync(
        string skillName,
        CancellationToken cancellationToken);

    Task<AwesomeCopilotSkillPreview> GetPreviewAsync(string skillName, CancellationToken cancellationToken);
}

public sealed class AwesomeCopilotSkillCatalog(
    HttpClient httpClient,
    IOptions<AwesomeCopilotOptions> options) : IAwesomeCopilotSkillCatalog
{
    private static readonly Regex SkillReferencePattern = new(
        @"(?:https://github\.com/github/awesome-copilot/(?:blob|raw)/[^/\s]+/)?skills/(?<name>[A-Za-z0-9_-]+)/SKILL\.md|(?:\.\./)(?<relativeName>[A-Za-z0-9_-]+)/SKILL\.md",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly AwesomeCopilotOptions options = options.Value;

    public async Task<IReadOnlyList<AwesomeCopilotSkill>> SearchAsync(
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        var skillNames = await GetSkillNamesAsync(cancellationToken);
        return skillNames
            .Where(name => string.IsNullOrWhiteSpace(searchTerm)
                || name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Select(name => new AwesomeCopilotSkill(name))
            .ToArray();
    }

    public async Task<IReadOnlyList<DownloadedAwesomeCopilotSkill>> DownloadWithDependenciesAsync(
        string skillName,
        CancellationToken cancellationToken)
    {
        var skillNames = await GetSkillNamesAsync(cancellationToken);
        if (!skillNames.Contains(skillName, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Choose a skill from the Awesome Copilot collection.", nameof(skillName));
        }

        var downloadedSkills = new List<DownloadedAwesomeCopilotSkill>();
        var pendingSkillNames = new Queue<string>([skillName]);
        var downloadedSkillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (pendingSkillNames.TryDequeue(out var pendingSkillName))
        {
            if (!downloadedSkillNames.Add(pendingSkillName))
            {
                continue;
            }

            var skill = await DownloadSkillAsync(pendingSkillName, cancellationToken);
            downloadedSkills.Add(skill);

            var skillMarkdown = skill.Files.Single(file =>
                string.Equals(file.RelativePath, "SKILL.md", StringComparison.OrdinalIgnoreCase));
            foreach (var referencedSkillName in GetReferencedSkillNames(Encoding.UTF8.GetString(skillMarkdown.Content)))
            {
                if (skillNames.Contains(referencedSkillName, StringComparer.OrdinalIgnoreCase))
                {
                    pendingSkillNames.Enqueue(referencedSkillName);
                }
            }
        }

        return downloadedSkills;
    }

    public async Task<AwesomeCopilotSkillPreview> GetPreviewAsync(
        string skillName,
        CancellationToken cancellationToken)
    {
        var skillNames = await GetSkillNamesAsync(cancellationToken);
        if (!skillNames.Contains(skillName, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Choose a skill from the Awesome Copilot collection.", nameof(skillName));
        }

        var files = await GetSkillContentsAsync(skillName, cancellationToken);
        var skillMarkdown = files.SingleOrDefault(file =>
            file.Type == "file"
            && string.Equals(file.Path, $"skills/{skillName}/SKILL.md", StringComparison.OrdinalIgnoreCase));
        if (skillMarkdown is null || string.IsNullOrWhiteSpace(skillMarkdown.DownloadUrl))
        {
            throw new HttpRequestException($"The '{skillName}' skill does not contain a SKILL.md file.");
        }

        var markdown = await httpClient.GetStringAsync(skillMarkdown.DownloadUrl, cancellationToken);
        return new AwesomeCopilotSkillPreview(skillName, markdown);
    }

    private async Task<IReadOnlySet<string>> GetSkillNamesAsync(CancellationToken cancellationToken)
    {
        var tree = await httpClient.GetFromJsonAsync<GitHubTree>(
            $"git/trees/{Uri.EscapeDataString(options.Branch)}?recursive=1",
            cancellationToken)
            ?? throw new HttpRequestException("Awesome Copilot did not return its repository tree.");

        if (tree.Truncated)
        {
            throw new HttpRequestException("Awesome Copilot returned an incomplete repository tree.");
        }

        return tree.Tree
            .Where(entry => entry.Type == "blob"
                && entry.Path.StartsWith("skills/", StringComparison.Ordinal)
                && entry.Path.EndsWith("/SKILL.md", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 3)
            .Select(parts => parts[1])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<DownloadedAwesomeCopilotSkill> DownloadSkillAsync(
        string skillName,
        CancellationToken cancellationToken)
    {
        var files = await GetSkillContentsAsync(skillName, cancellationToken);

        var downloadedFiles = new List<RemoteSkillFile>();
        foreach (var file in files)
        {
            await DownloadFilesAsync(file, skillName, downloadedFiles, cancellationToken);
        }

        if (!downloadedFiles.Any(file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.OrdinalIgnoreCase)))
        {
            throw new HttpRequestException($"The '{skillName}' skill does not contain a SKILL.md file.");
        }

        return new DownloadedAwesomeCopilotSkill(skillName, downloadedFiles);
    }

    private async Task<IReadOnlyList<GitHubContent>> GetSkillContentsAsync(
        string skillName,
        CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<List<GitHubContent>>(
            $"contents/skills/{Uri.EscapeDataString(skillName)}?ref={Uri.EscapeDataString(options.Branch)}",
            cancellationToken)
        ?? throw new HttpRequestException($"Awesome Copilot did not return the '{skillName}' skill.");

    private async Task DownloadFilesAsync(
        GitHubContent content,
        string skillName,
        ICollection<RemoteSkillFile> downloadedFiles,
        CancellationToken cancellationToken)
    {
        var skillPrefix = $"skills/{skillName}/";
        if (!content.Path.StartsWith(skillPrefix, StringComparison.Ordinal))
        {
            throw new HttpRequestException($"Awesome Copilot returned an unexpected path for '{skillName}'.");
        }

        if (content.Type == "dir")
        {
            var children = await httpClient.GetFromJsonAsync<List<GitHubContent>>(content.Url, cancellationToken)
                ?? throw new HttpRequestException($"Awesome Copilot did not return '{content.Path}'.");
            foreach (var child in children)
            {
                await DownloadFilesAsync(child, skillName, downloadedFiles, cancellationToken);
            }

            return;
        }

        if (content.Type != "file" || string.IsNullOrWhiteSpace(content.DownloadUrl))
        {
            return;
        }

        var contentBytes = await httpClient.GetByteArrayAsync(content.DownloadUrl, cancellationToken);
        var relativePath = content.Path[skillPrefix.Length..];
        downloadedFiles.Add(new RemoteSkillFile(relativePath, contentBytes));
    }

    private static IEnumerable<string> GetReferencedSkillNames(string markdown) =>
        SkillReferencePattern.Matches(markdown)
            .Select(match => match.Groups["name"].Success
                ? match.Groups["name"].Value
                : match.Groups["relativeName"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private sealed record GitHubTree(
        [property: JsonPropertyName("truncated")] bool Truncated,
        [property: JsonPropertyName("tree")] IReadOnlyList<GitHubTreeEntry> Tree);

    private sealed record GitHubTreeEntry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type);

    private sealed record GitHubContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("download_url")] string? DownloadUrl);
}
