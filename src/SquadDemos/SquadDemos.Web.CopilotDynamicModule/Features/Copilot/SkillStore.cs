using Microsoft.Extensions.Options;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

public sealed record UploadedSkill(string Name, string Path);

public interface ISkillStore
{
    string FolderPath { get; }

    IReadOnlyList<UploadedSkill> GetSkills();

    Task<UploadedSkill> SaveAsync(IFormFile skillFile, CancellationToken cancellationToken);

    Task SaveAwesomeCopilotSkillsAsync(
        IReadOnlyList<DownloadedAwesomeCopilotSkill> skills,
        CancellationToken cancellationToken);

    Task DeleteAsync(string skillName, CancellationToken cancellationToken);
}

public sealed class SkillStore : ISkillStore
{
    private readonly long maxFileSizeBytes;

    public SkillStore(IWebHostEnvironment environment, IOptions<CopilotOptions> options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environment.WebRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.SkillsFolderName);

        maxFileSizeBytes = options.Value.MaxSkillFileSizeBytes;
        FolderPath = GetSkillsFolderPath(environment.WebRootPath, options.Value.SkillsFolderName);
        Directory.CreateDirectory(FolderPath);
    }

    public string FolderPath { get; }

    public IReadOnlyList<UploadedSkill> GetSkills() =>
        Directory.EnumerateFiles(FolderPath, "SKILL.md", SearchOption.AllDirectories)
            .Select(path => new UploadedSkill(Path.GetFileName(Path.GetDirectoryName(path)!), path))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public async Task<UploadedSkill> SaveAsync(IFormFile skillFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skillFile);
        if (skillFile.Length is <= 0 or > long.MaxValue)
        {
            throw new ArgumentException("A non-empty skill file is required.", nameof(skillFile));
        }

        if (skillFile.Length > maxFileSizeBytes)
        {
            throw new ArgumentException($"Skill files cannot exceed {maxFileSizeBytes} bytes.", nameof(skillFile));
        }

        var skillName = Path.GetFileNameWithoutExtension(skillFile.FileName);
        if (string.IsNullOrWhiteSpace(skillName)
            || !string.Equals(Path.GetExtension(skillFile.FileName), ".md", StringComparison.OrdinalIgnoreCase)
            || skillName.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("Upload a Markdown file whose name uses letters, numbers, hyphens, or underscores.", nameof(skillFile));
        }

        var skillDirectory = Path.Combine(FolderPath, skillName);
        Directory.CreateDirectory(skillDirectory);
        var destinationPath = Path.Combine(skillDirectory, "SKILL.md");
        await using var destination = File.Create(destinationPath);
        await skillFile.CopyToAsync(destination, cancellationToken);
        return new UploadedSkill(skillName, destinationPath);
    }

    public async Task SaveAwesomeCopilotSkillsAsync(
        IReadOnlyList<DownloadedAwesomeCopilotSkill> skills,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skills);
        foreach (var skill in skills)
        {
            ValidateSkillName(skill.Name);
            foreach (var file in skill.Files)
            {
                if (file.Content.LongLength > maxFileSizeBytes)
                {
                    throw new ArgumentException(
                        $"Skill files cannot exceed {maxFileSizeBytes} bytes.",
                        nameof(skills));
                }

                var destinationPath = GetContainedSkillFilePath(skill.Name, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await File.WriteAllBytesAsync(destinationPath, file.Content, cancellationToken);
            }
        }
    }

    public Task DeleteAsync(string skillName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSkillName(skillName);
        var skillDirectory = Path.Combine(FolderPath, skillName);
        if (Directory.Exists(skillDirectory))
        {
            Directory.Delete(skillDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private void ValidateSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName)
            || skillName.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("Skill names may contain only letters, numbers, hyphens, or underscores.", nameof(skillName));
        }
    }

    private string GetContainedSkillFilePath(string skillName, string relativePath)
    {
        var skillDirectory = Path.GetFullPath(Path.Combine(FolderPath, skillName));
        var destinationPath = Path.GetFullPath(Path.Combine(skillDirectory, relativePath));
        if (!destinationPath.StartsWith(skillDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Skill files must resolve beneath their skill directory.", nameof(relativePath));
        }

        return destinationPath;
    }

    private static string GetSkillsFolderPath(string webRootPath, string folderName)
    {
        var webRoot = Path.GetFullPath(webRootPath);
        var skillsFolder = Path.GetFullPath(Path.Combine(webRoot, folderName));

        if (!skillsFolder.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Copilot:SkillsFolderName must resolve beneath the web root.");
        }

        return skillsFolder;
    }
}
