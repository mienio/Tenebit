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

    public MyWorkspaceService(IPersonRepository people, IAssetRepository assets, IAssetCategoryRepository categories, IAssignmentRepository assignments, IProcedureRepository procedures, ICurrentUser currentUser)
    {
        _people = people;
        _assets = assets;
        _categories = categories;
        _assignments = assignments;
        _procedures = procedures;
        _currentUser = currentUser;
    }

    public async Task<MyWorkspaceResponse> GetAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var person = string.IsNullOrEmpty(_currentUser.Email) ? null : await _people.FindByEmailAsync(organizationId, _currentUser.Email, cancellationToken);
        if (person is null)
        {
            return new MyWorkspaceResponse(false, null, [], []);
        }

        return await BuildAsync(organizationId, person, cancellationToken);
    }

    public async Task<Result<MyWorkspaceResponse>> GetForPersonAsync(Guid personId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Manager, TenebitRoles.Hr, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<MyWorkspaceResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, personId, cancellationToken);
        if (person is null) return Result<MyWorkspaceResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));

        return Result<MyWorkspaceResponse>.Success(await BuildAsync(organizationId, person, cancellationToken));
    }

    private async Task<MyWorkspaceResponse> BuildAsync(Guid organizationId, Person person, CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var allAssets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var myAssets = allAssets
            .Where(x => x.AssignedPersonId == person.Id)
            .Select(x =>
            {
                var category = categories.FirstOrDefault(c => c.Id == x.CategoryId);
                return new MyAssetResponse(x.Id, x.Name, x.AssetTag, category?.Name, category?.Icon, x.Location, x.WarrantyUntil);
            })
            .ToList();

        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        var assignments = (await _assignments.ListAsync(organizationId, cancellationToken))
            .Where(x => x.PersonId == person.Id)
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
                        var document = procedure?.Documents.OrderByDescending(d => d.UploadedAt).FirstOrDefault();
                        return new MyProcedureResponse(acceptance.ProcedureId, procedure?.Title, acceptance.Status, document?.Id, document?.FileName);
                    })
                    .ToList();
                return new MyAssignmentResponse(assignment.Id, assignment.ProtocolNumber, assignment.Status, assignment.IssuedAt, assignment.DueDate, assetNames, procedureAcceptances);
            })
            .ToList();

        return new MyWorkspaceResponse(true, person.FullName, myAssets, assignments);
    }
}
