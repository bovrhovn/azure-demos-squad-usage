using Microsoft.AspNetCore.Http.HttpResults;
using SquadDemos.Web.CopilotDynamicModule.Features.Hosting;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

public static class SkillsFeature
{
    public static RouteGroupBuilder MapSkillsFeatureApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiEndpoints.Skills).WithTags("Skills");
        group.MapGet("/uploaded", (int page, ISkillStore store, IPagingService paging) =>
            TypedResults.Ok(paging.CreatePage(store.GetSkills(), page))).WithName("GetUploadedSkills");
        group.MapGet("/search", async Task<Results<Ok<PagedResult<AwesomeCopilotSkill>>, ProblemHttpResult>> (
            string term, int page, IAwesomeCopilotSkillCatalog catalog, IPagingService paging,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return TypedResults.Ok(paging.CreatePage(await catalog.SearchAsync(term, cancellationToken), page));
            }
            catch (HttpRequestException)
            {
                return TypedResults.Problem("Awesome Copilot could not be reached.", statusCode: 503);
            }
        }).WithName("SearchAwesomeCopilotSkills");
        group.MapGet("/preview/{skillName}",
            async Task<Results<Ok<AwesomeCopilotSkillPreview>, BadRequest<string>, ProblemHttpResult>> (
                string skillName, IAwesomeCopilotSkillCatalog catalog, CancellationToken cancellationToken) =>
            {
                try
                {
                    return TypedResults.Ok(await catalog.GetPreviewAsync(skillName, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return TypedResults.BadRequest(exception.Message);
                }
                catch (HttpRequestException)
                {
                    return TypedResults.Problem("The skill could not be previewed.", statusCode: 503);
                }
            }).WithName("PreviewAwesomeCopilotSkill");
        group.MapPost("/upload", async Task<Results<Ok<UploadedSkill>, BadRequest<string>>> (
            IFormFile skillFile, ISkillStore store, CancellationToken cancellationToken) =>
        {
            try
            {
                return TypedResults.Ok(await store.SaveAsync(skillFile, cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return TypedResults.BadRequest(exception.Message);
            }
        }).WithName("UploadSkill");
        group.MapPost("/install/{skillName}", async Task<Results<Ok<string>, BadRequest<string>, ProblemHttpResult>> (
            string skillName, IAwesomeCopilotSkillCatalog catalog, ISkillStore store,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var skills = await catalog.DownloadWithDependenciesAsync(skillName, cancellationToken);
                await store.SaveAwesomeCopilotSkillsAsync(skills, cancellationToken);
                return TypedResults.Ok($"Downloaded {skills.Count} skill(s).");
            }
            catch (ArgumentException exception)
            {
                return TypedResults.BadRequest(exception.Message);
            }
            catch (HttpRequestException)
            {
                return TypedResults.Problem("Awesome Copilot could not be reached.", statusCode: 503);
            }
        }).WithName("InstallAwesomeCopilotSkill");
        group.MapDelete("/uploaded/{skillName}", async Task<Results<NoContent, BadRequest<string>>> (
            string skillName, ISkillStore store, CancellationToken cancellationToken) =>
        {
            try
            {
                await store.DeleteAsync(skillName, cancellationToken);
                return TypedResults.NoContent();
            }
            catch (ArgumentException exception)
            {
                return TypedResults.BadRequest(exception.Message);
            }
        }).WithName("DeleteUploadedSkill");
        return group;
    }
}