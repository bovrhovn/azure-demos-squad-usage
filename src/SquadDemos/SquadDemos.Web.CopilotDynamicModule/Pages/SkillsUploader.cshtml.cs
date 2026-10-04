using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Pages;

public sealed class SkillsUploaderModel(ISkillStore skillStore) : PageModel
{
    [BindProperty]
    public IFormFile? SkillFile { get; set; }

    public IReadOnlyList<UploadedSkill> Skills { get; private set; } = [];

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
        Skills = skillStore.GetSkills();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (SkillFile is null)
        {
            ModelState.AddModelError(nameof(SkillFile), "Choose a skill file to upload.");
        }
        else
        {
            try
            {
                var skill = await skillStore.SaveAsync(SkillFile, cancellationToken);
                StatusMessage = $"Uploaded {skill.Name}.";
            }
            catch (ArgumentException exception)
            {
                ModelState.AddModelError(nameof(SkillFile), exception.Message);
            }
        }

        Skills = skillStore.GetSkills();
        return Page();
    }
}
