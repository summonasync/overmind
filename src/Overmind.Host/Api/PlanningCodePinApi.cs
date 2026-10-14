using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningCodePinApi
{
    public static Task<IResult> PinCode(
        Guid projectId,
        string slug,
        SetPlanningFeatureCodePinRequest request,
        IValidator<SetPlanningFeatureCodePinRequest> validator,
        PlanningFeatureService service,
        CancellationToken ct) =>
        PinCodeForDoor(projectId, slug, request, validator, service, PlanningLogSource.Ui, ct);

    internal static Task<IResult> PinCodeForAgent(
        Guid projectId,
        string slug,
        SetPlanningFeatureCodePinRequest request,
        IValidator<SetPlanningFeatureCodePinRequest> validator,
        PlanningFeatureService service,
        CancellationToken ct) =>
        PinCodeForDoor(projectId, slug, request, validator, service, PlanningLogSource.Agent, ct);

    internal static async Task<IResult> PinCodeForDoor(
        Guid projectId,
        string slug,
        SetPlanningFeatureCodePinRequest request,
        IValidator<SetPlanningFeatureCodePinRequest> validator,
        PlanningFeatureService service,
        PlanningLogSource source,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var feature = await service.SetCodePinAsync(
                projectId, slug, request.Branch, request.WorktreePath, source, ct);

            return Results.Ok(new PlanningFeatureCodePinResponse(
                feature.Slug,
                feature.PinnedBranch,
                feature.PinnedWorktreePath,
                feature.Version,
                feature.LastLogLine));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }
}
