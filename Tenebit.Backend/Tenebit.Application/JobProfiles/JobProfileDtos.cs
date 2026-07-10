namespace Tenebit.Application.JobProfiles;

public sealed record JobProfileResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? DefaultManagerId,
    IReadOnlyList<Guid> AssetCategoryIds,
    IReadOnlyList<Guid> ProcedureIds,
    DateTimeOffset CreatedAt);

public sealed record SaveJobProfileRequest(string Name, string? Description, Guid? DefaultManagerId, IReadOnlyList<Guid> AssetCategoryIds, IReadOnlyList<Guid> ProcedureIds);
