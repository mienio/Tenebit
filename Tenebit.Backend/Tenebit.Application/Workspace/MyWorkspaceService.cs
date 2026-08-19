using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;

namespace Tenebit.Application.Workspace;

public sealed class MyWorkspaceService
{
    private readonly IPersonRepository _people;
    private readonly IAssetRepository _assets;
    private readonly IAssetCategoryRepository _categories;
    private readonly IAssignmentRepository _assignments;
    private readonly IProcedureRepository _procedures;
    private readonly ICurrentUser _currentUser;
    private readonly ManagerScopeService _managerScope;

    // Roles allowed to open GetForPersonAsync that see the whole organization; Manager alone is
    // scoped to its own team by ManagerScopeService (audyt AUD3-006: Manager mógł podać dowolny
    // personId w organizacji, bez sprawdzenia zarządzanego zespołu).
    private static readonly string[] OrgWideRoles = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator];

    public MyWorkspaceService(IPersonRepository people, IAssetRepository assets, IAssetCategoryRepository categories, IAssignmentRepository assignments, IProcedureRepository procedures, ICurrentUser currentUser, ManagerScopeService managerScope)
    {
        _people = people;
        _assets = assets;
        _categories = categories;
        _assignments = assignments;
        _procedures = procedures;
        _currentUser = currentUser;
        _managerScope = managerScope;
    }

    public async Task<MyWorkspaceResponse> GetAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        if (_currentUser.PersonId is not { } personId)
            return new MyWorkspaceResponse(false, null, [], []);

        var person = await _people.GetAsync(organizationId, personId, cancellationToken);
        if (person is null)
            return new MyWorkspaceResponse(false, null, [], []);

        return await BuildAsync(organizationId, person, cancellationToken);
    }

    public async Task<Result<MyWorkspaceResponse>> GetForPersonAsync(Guid personId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Manager, TenebitRoles.Hr, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<MyWorkspaceResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, personId, cancellationToken);
        if (person is null) return Result<MyWorkspaceResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));

        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        if (scope is not null && !scope.ContainsPerson(person.Id))
        {
            return Result<MyWorkspaceResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));
        }

        return Result<MyWorkspaceResponse>.Success(await BuildAsync(organizationId, person, cancellationToken));
    }

    private async Task<MyWorkspaceResponse> BuildAsync(Guid organizationId, Person person, CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var assignedAssets = await _assets.ListByAssignedPersonAsync(organizationId, person.Id, cancellationToken);
        var myAssets = assignedAssets
            .Select(x =>
            {
                var category = categories.FirstOrDefault(c => c.Id == x.CategoryId);
                return new MyAssetResponse(x.Id, x.Name, x.AssetTag, category?.Name, category?.Icon, x.Location, x.WarrantyUntil);
            })
            .ToList();

        var assignmentRows = await _assignments.ListByPersonAsync(organizationId, person.Id, cancellationToken);
        var assignmentAssetIds = assignmentRows.SelectMany(x => x.Assets).Select(x => x.AssetId).Distinct().ToArray();
        var assignmentAssets = await _assets.GetByIdsAsync(organizationId, assignmentAssetIds, cancellationToken);
        var allAssets = assignedAssets.Concat(assignmentAssets).GroupBy(x => x.Id).Select(x => x.First()).ToList();
        var procedureIds = assignmentRows.SelectMany(x => x.ProcedureAcceptances).Select(x => x.ProcedureId).Distinct().ToArray();
        var procedures = await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken);
        var procedureDocuments = await _procedures.ListDocumentMetadataByProcedureIdsAsync(organizationId, procedureIds, cancellationToken);
        var assignments = assignmentRows
            .OrderByDescending(x => x.IssuedAt)
            .Select(assignment =>
            {
                var assetNames = assignment.Assets
                    .Select(item => allAssets.FirstOrDefault(a => a.Id == item.AssetId)?.Name ?? "Aktywo")
                    .ToList();
                var procedureAcceptances = assignment.ProcedureAcceptances
                    .Select(acceptance =>
                    {
                        var procedure = procedures.FirstOrDefault(p => p.Id == acceptance.ProcedureId);
                        var document = procedureDocuments
                            .Where(item => item.ProcedureId == acceptance.ProcedureId)
                            .OrderByDescending(item => item.UploadedAt)
                            .FirstOrDefault();
                        return new MyProcedureResponse(acceptance.ProcedureId, procedure?.Title, acceptance.Status, document?.Id, document?.FileName);
                    })
                    .ToList();
                return new MyAssignmentResponse(assignment.Id, assignment.ProtocolNumber, assignment.Status, assignment.IssuedAt, assignment.DueDate, assetNames, procedureAcceptances);
            })
            .ToList();

        return new MyWorkspaceResponse(true, person.FullName, myAssets, assignments);
    }
}
