using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;

namespace Tenebit.Application.Onboarding;

public sealed class OnboardingService
{
    private readonly ITeamRepository _teams;
    private readonly IPersonRepository _people;
    private readonly IAssetCategoryRepository _categories;
    private readonly IAssetRepository _assets;
    private readonly IProcedureRepository _procedures;
    private readonly IAssignmentRepository _assignments;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public OnboardingService(ITeamRepository teams, IPersonRepository people, IAssetCategoryRepository categories, IAssetRepository assets, IProcedureRepository procedures, IAssignmentRepository assignments, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _teams = teams;
        _people = people;
        _categories = categories;
        _assets = assets;
        _procedures = procedures;
        _assignments = assignments;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<OnboardingStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var procedures = await _procedures.ListAsync(organizationId, null, cancellationToken);
        var assignments = await _assignments.ListAsync(organizationId, cancellationToken);

        var steps = new List<OnboardingStepResponse>
        {
            new("team", "Utwórz pierwszy zespół", teams.Count > 0, "Dodaj zespół lub użyj pakietu startowego."),
            new("person", "Dodaj pierwszego pracownika", people.Count > 0, "Dodaj osobę, która otrzyma sprzęt i procedury."),
            new("category", "Wybierz lub utwórz kategorię aktywów", categories.Count > 0, "Użyj kategorii bazowych albo dodaj własną."),
            new("asset", "Dodaj pierwsze aktywo", assets.Count > 0, "Dodaj laptop, telefon albo inny zasób z tagiem."),
            new("procedure", "Dodaj procedurę do akceptacji", procedures.Count > 0, "Dodaj regulamin sprzętu lub procedurę stanowiskową."),
            new("assignment", "Wyślij pierwszy pakiet wydania", assignments.Count > 0, "Połącz pracownika, sprzęt i procedury w jednym wydaniu.")
        };

        var completed = steps.Count(step => step.Completed);
        return new OnboardingStatusResponse(steps, (int)Math.Round(completed * 100m / steps.Count));
    }

    public async Task<Result<StarterPackageResponse>> CreateStarterPackageAsync(CreateStarterPackageRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<StarterPackageResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;

            if (string.IsNullOrWhiteSpace(request.TeamName) || string.IsNullOrWhiteSpace(request.EmployeeFirstName) ||
                string.IsNullOrWhiteSpace(request.EmployeeLastName) || string.IsNullOrWhiteSpace(request.EmployeeEmail) ||
                string.IsNullOrWhiteSpace(request.AssetName) || string.IsNullOrWhiteSpace(request.AssetTag) ||
                string.IsNullOrWhiteSpace(request.CategoryName) || string.IsNullOrWhiteSpace(request.ProcedureTitle))
            {
                return Result<StarterPackageResponse>.Failure(Error.Validation("Uzupełnij wymagane pola pakietu startowego."));
            }

            if (await _people.EmailExistsAsync(organizationId, request.EmployeeEmail, null, cancellationToken))
            {
                return Result<StarterPackageResponse>.Failure(Error.Conflict("Pracownik z tym adresem e-mail już istnieje."));
            }

            if (await _assets.AssetTagExistsAsync(organizationId, request.AssetTag, null, cancellationToken))
            {
                return Result<StarterPackageResponse>.Failure(Error.Conflict("Tag aktywa jest już używany."));
            }

            var teamName = request.TeamName.Trim();
            var team = (await _teams.ListAsync(organizationId, cancellationToken))
                .FirstOrDefault(x => string.Equals(x.Name, teamName, StringComparison.OrdinalIgnoreCase));
            if (team is null)
            {
                team = new Team(organizationId, teamName, null, null);
                _teams.Add(team);
            }

            var categoryName = request.CategoryName.Trim();
            var category = (await _categories.ListAsync(organizationId, cancellationToken))
                .FirstOrDefault(x => string.Equals(x.Name, categoryName, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                category = new AssetCategory(organizationId, categoryName, AssetCategoryType.Physical, "Kategoria utworzona w pakiecie startowym.");
                _categories.Add(category);
            }

            var person = new Person(organizationId, request.EmployeeFirstName, request.EmployeeLastName, request.EmployeeEmail);
            person.Update(request.EmployeeFirstName, request.EmployeeLastName, request.EmployeeEmail, null, null, PersonRelationType.Employee, request.JobTitle, team.Id, null, request.Location, null);
            _people.Add(person);

            var asset = new Asset(organizationId, category.Id, request.AssetName, request.AssetTag);
            asset.UpdateCore(request.AssetName, request.AssetTag, request.SerialNumber, category.Id, request.Location, null, null, null, null, null, null, team.Id);
            asset.AssignTo(person.Id);
            _assets.Add(asset);

            var procedure = new Procedure(organizationId, request.ProcedureTitle, "1.0", "HR / Onboarding", true);
            procedure.Update(request.ProcedureTitle, "1.0", "HR / Onboarding", request.JobTitle, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime).AddMonths(12), true);
            _procedures.Add(procedure);

            var assignment = new Assignment(organizationId, person.Id, CreateProtocolNumber(_clock.UtcNow), _clock.UtcNow, request.ReturnDueDate, "Pakiet utworzony przez onboarding pracownika.", _currentUser.Subject);
            assignment.AddAsset(asset.Id, "Wydane w stanie dobrym");
            // Procedura jest w szkicu (brak pliku) — akceptacja zostanie dodana dopiero po jej publikacji,
            // żeby pracownik nie musiał "zaakceptować" dokumentu, którego jeszcze nie może przeczytać.
            _assignments.Add(assignment);

            _activity.Add(new ActivityLog(organizationId, "onboarding.starter_package.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<StarterPackageResponse>.Success(new StarterPackageResponse(person.Id, asset.Id, procedure.Id, assignment.Id, assignment.ProtocolNumber, "Pakiet startowy został przygotowany. Procedura jest w szkicu do czasu dodania pliku i publikacji."));
        }
        catch (DomainException ex)
        {
            return Result<StarterPackageResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private static string CreateProtocolNumber(DateTimeOffset now) => $"TEN-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
