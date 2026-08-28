using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Onboarding;

public sealed class OnboardingService
{
    private readonly ITeamRepository _teams;
    private readonly IPersonRepository _people;
    private readonly IAssetCategoryRepository _categories;
    private readonly IAssetRepository _assets;
    private readonly IProcedureRepository _procedures;
    private readonly IAssignmentRepository _assignments;
    private readonly IJobProfileRepository _jobProfiles;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AssignmentService _assignmentService;

    private readonly ManagerScopeService _managerScope;
    private readonly LocationReferenceResolver _locationResolver;
    private readonly ISubscriptionRepository _subscriptions;

    public OnboardingService(ITeamRepository teams, IPersonRepository people, IAssetCategoryRepository categories, IAssetRepository assets, IProcedureRepository procedures, IAssignmentRepository assignments, IJobProfileRepository jobProfiles, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, AssignmentService assignmentService, ManagerScopeService managerScope, LocationReferenceResolver locationResolver, ISubscriptionRepository subscriptions)
    {
        _teams = teams;
        _people = people;
        _categories = categories;
        _assets = assets;
        _procedures = procedures;
        _assignments = assignments;
        _jobProfiles = jobProfiles;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _assignmentService = assignmentService;
        _managerScope = managerScope;
        _locationResolver = locationResolver;
        _subscriptions = subscriptions;
    }

    public async Task<Result<OnboardingStatusResponse>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<OnboardingStatusResponse>.Failure(access.Error!);

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
        return Result<OnboardingStatusResponse>.Success(new OnboardingStatusResponse(steps, (int)Math.Round(completed * 100m / steps.Count)));
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

            // Zespół i kategoria są dopisywane dopiero wewnątrz sekcji limitu poniżej - inaczej pakiet
            // startowy tworzyłby je nawet wtedy, gdy pakiet zostanie odrzucony przez limit planu.
            var teamName = request.TeamName.Trim();
            var existingTeams = await _teams.ListAsync(organizationId, cancellationToken);
            var team = existingTeams.FirstOrDefault(x => string.Equals(x.Name, teamName, StringComparison.OrdinalIgnoreCase));
            var newTeam = team is null ? new Team(organizationId, teamName, null, null) : null;
            team ??= newTeam!;

            var categoryName = request.CategoryName.Trim();
            var existingCategories = await _categories.ListAsync(organizationId, cancellationToken);
            var category = existingCategories.FirstOrDefault(x => string.Equals(x.Name, categoryName, StringComparison.OrdinalIgnoreCase));
            var newCategory = category is null
                ? new AssetCategory(organizationId, categoryName, AssetCategoryType.Physical, "Kategoria utworzona w pakiecie startowym.")
                : null;
            category ??= newCategory!;

            var locationResult = await _locationResolver.ResolveAsync(organizationId, request.Location, cancellationToken);
            if (locationResult.IsFailure) return Result<StarterPackageResponse>.Failure(locationResult.Error!);
            var person = new Person(organizationId, request.EmployeeFirstName, request.EmployeeLastName, request.EmployeeEmail);
            person.Update(request.EmployeeFirstName, request.EmployeeLastName, request.EmployeeEmail, null, null, "Pracownik", request.JobTitle, team.Id, null, locationResult.Value!.FullPath, null);
            if (!person.CanReceiveNewObligations) return Result<StarterPackageResponse>.Failure(Error.Validation("Pakiet onboardingowy można utworzyć tylko dla aktywnej osoby."));
            person.SetLocation(locationResult.Value!.Id, locationResult.Value.FullPath);

            var asset = new Asset(organizationId, category.Id, request.AssetName, request.AssetTag);
            asset.UpdateCore(request.AssetName, request.AssetTag, request.SerialNumber, category.Id, locationResult.Value!.FullPath, null, null, null, null, null, null, team.Id);
            asset.SetLocation(locationResult.Value!.Id, locationResult.Value.FullPath);
            asset.AssignTo(person.Id);

            var procedure = new Procedure(organizationId, request.ProcedureTitle, "1.0", "HR / Onboarding", true);
            procedure.Update(request.ProcedureTitle, "1.0", "HR / Onboarding", request.JobTitle, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime).AddMonths(12), true);

            var assignment = new Assignment(organizationId, person.Id, CreateProtocolNumber(_clock.UtcNow), _clock.UtcNow, request.ReturnDueDate, "Pakiet utworzony przez onboarding pracownika.", _currentUser.Subject);
            assignment.AddAsset(asset.Id, "Wydane w stanie dobrym");

            // The starter package creates a Person, an Asset and a Procedure in one shot, bypassing
            // PeopleService/AssetService/ProcedureService.CreateAsync entirely - it must therefore repeat
            // their subscription-capacity check itself, otherwise this flow would be a way to add unlimited
            // records regardless of plan (the exact bypass this whole limit system exists to prevent).
            var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
            if (subscription is null)
            {
                subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
                _subscriptions.Add(subscription);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            var limit = subscription.GetResourceLimit();

            var withinLimit = await _unitOfWork.ExecuteWithResourceLocksAsync(
                organizationId,
                "onboarding-starter-package",
                [organizationId],
                async ct =>
            {
                if (await _people.CountAsync(organizationId, ct) >= limit) return false;
                if (await _assets.CountAsync(organizationId, ct) >= limit) return false;
                if (await _procedures.CountAsync(organizationId, ct) >= limit) return false;
                if (newTeam is not null && (await _teams.ListAsync(organizationId, ct)).Count >= limit) return false;
                if (newCategory is not null && (await _categories.ListAsync(organizationId, ct)).Count(x => !x.IsSystem) >= limit) return false;

                if (newTeam is not null) _teams.Add(newTeam);
                if (newCategory is not null) _categories.Add(newCategory);
                _people.Add(person);
                _assets.Add(asset);
                _procedures.Add(procedure);
                // Procedura jest w szkicu (brak pliku) - akceptacja zostanie dodana dopiero po jej publikacji,
                // żeby pracownik nie musiał "zaakceptować" dokumentu, którego jeszcze nie może przeczytać.
                _assignments.Add(assignment);

                _activity.Add(new ActivityLog(organizationId, "onboarding.starter_package.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            if (!withinLimit)
            {
                var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
                return Result<StarterPackageResponse>.Failure(Error.Validation($"Limit planu {plan.Name} ({limit}) został osiągnięty dla pracowników, aktywów lub procedur. Przejdź na wyższy plan."));
            }

            return Result<StarterPackageResponse>.Success(new StarterPackageResponse(person.Id, asset.Id, procedure.Id, assignment.Id, assignment.ProtocolNumber, "Pakiet startowy został przygotowany. Procedura jest w szkicu do czasu dodania pliku i publikacji."));
        }
        catch (DomainException ex)
        {
            return Result<StarterPackageResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    // Real onboarding flow: resolves the employee's JobProfile into concrete assets (one available asset per
    // asset category still uncovered by an explicit AssetId) and procedures, then delegates the actual
    // assignment creation (validation, email, reference number) to AssignmentService so both flows share the
    // exact same tamper-evident issuing path.
    public async Task<Result<EmployeePackageResponse>> CreateEmployeePackageAsync(CreateEmployeePackageRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<EmployeePackageResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, request.PersonId, cancellationToken);
        if (person is null) return Result<EmployeePackageResponse>.Failure(Error.Validation("Wybrany pracownik nie istnieje."));
        if (!person.CanReceiveNewObligations) return Result<EmployeePackageResponse>.Failure(Error.Validation("Pakiet onboardingowy można utworzyć tylko dla aktywnej osoby."));

        var assetIds = request.AssetIds.Distinct().ToList();
        var procedureIds = request.ProcedureIds.Distinct().ToList();
        var warnings = new List<string>();

        if (request.JobProfileId is { } profileId)
        {
            var profile = await _jobProfiles.GetAsync(organizationId, profileId, cancellationToken);
            if (profile is null) return Result<EmployeePackageResponse>.Failure(Error.Validation("Wybrany zestaw stanowiskowy nie istnieje."));

            foreach (var procedureId in profile.Procedures.Select(x => x.ProcedureId))
            {
                if (!procedureIds.Contains(procedureId)) procedureIds.Add(procedureId);
            }

            var existingAssets = assetIds.Count > 0 ? await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken) : [];
            var coveredCategories = existingAssets.Select(x => x.CategoryId).ToHashSet();
            var available = await _assets.ListAsync(organizationId, null, AssetStatus.InStock, null, cancellationToken);
            var categories = await _categories.ListAsync(organizationId, cancellationToken);

            foreach (var categoryId in profile.AssetCategories.Select(x => x.AssetCategoryId))
            {
                if (coveredCategories.Contains(categoryId)) continue;
                var candidate = available.FirstOrDefault(a => a.CategoryId == categoryId && !assetIds.Contains(a.Id));
                if (candidate is null)
                {
                    var categoryName = categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "-";
                    warnings.Add($"Brak dostępnego aktywa w kategorii \"{categoryName}\" z zestawu stanowiskowego \"{profile.Name}\".");
                    continue;
                }

                assetIds.Add(candidate.Id);
                coveredCategories.Add(categoryId);
            }
        }

        if (assetIds.Count == 0) return Result<EmployeePackageResponse>.Failure(Error.Validation("Zestaw stanowiskowy nie zawiera żadnego dostępnego aktywa - dodaj aktywo ręcznie."));

        var createResult = await _assignmentService.CreateAsync(
            new CreateAssignmentRequest(person.Id, assetIds.Select(id => new AssignmentAssetRequest(id, request.AssetConditions?.GetValueOrDefault(id.ToString()))).ToList(), procedureIds, request.DueDate, request.Notes),
            cancellationToken);
        if (createResult.IsFailure) return Result<EmployeePackageResponse>.Failure(createResult.Error!);

        var assignment = createResult.Value!;
        _activity.Add(new ActivityLog(organizationId, "onboarding.employee_package.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EmployeePackageResponse>.Success(new EmployeePackageResponse(assignment.Id, assignment.ProtocolNumber, assignment, warnings));
    }

    // Spec 6.4: onboarding variant of CreateEmployeePackageAsync - identical package resolution (job profile
    // to assets/procedures), but delegates to AssignmentService.CreateWithEvidenceAsync so the assignment
    // and photos are persisted atomically in a single transaction.
    public async Task<Result<EmployeePackageResponse>> CreateEmployeePackageWithEvidenceAsync(
        CreateEmployeePackageRequest request,
        IReadOnlyDictionary<string, EvidenceManifestEntry> evidenceManifest,
        IReadOnlyList<EvidenceFileInput> files,
        CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<EmployeePackageResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, request.PersonId, cancellationToken);
        if (person is null) return Result<EmployeePackageResponse>.Failure(Error.Validation("Wybrany pracownik nie istnieje."));
        if (!person.CanReceiveNewObligations) return Result<EmployeePackageResponse>.Failure(Error.Validation("Pakiet onboardingowy można utworzyć tylko dla aktywnej osoby."));

        var assetIds = request.AssetIds.Distinct().ToList();
        var procedureIds = request.ProcedureIds.Distinct().ToList();
        var warnings = new List<string>();

        if (request.JobProfileId is { } profileId)
        {
            var profile = await _jobProfiles.GetAsync(organizationId, profileId, cancellationToken);
            if (profile is null) return Result<EmployeePackageResponse>.Failure(Error.Validation("Wybrany zestaw stanowiskowy nie istnieje."));

            foreach (var procedureId in profile.Procedures.Select(x => x.ProcedureId))
            {
                if (!procedureIds.Contains(procedureId)) procedureIds.Add(procedureId);
            }

            var existingAssets = assetIds.Count > 0 ? await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken) : [];
            var coveredCategories = existingAssets.Select(x => x.CategoryId).ToHashSet();
            var available = await _assets.ListAsync(organizationId, null, AssetStatus.InStock, null, cancellationToken);
            var categories = await _categories.ListAsync(organizationId, cancellationToken);

            foreach (var categoryId in profile.AssetCategories.Select(x => x.AssetCategoryId))
            {
                if (coveredCategories.Contains(categoryId)) continue;
                var candidate = available.FirstOrDefault(a => a.CategoryId == categoryId && !assetIds.Contains(a.Id));
                if (candidate is null)
                {
                    var categoryName = categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "-";
                    warnings.Add($"Brak dostępnego aktywa w kategorii \"{categoryName}\" z zestawu stanowiskowego \"{profile.Name}\".");
                    continue;
                }

                assetIds.Add(candidate.Id);
                coveredCategories.Add(categoryId);
            }
        }

        if (assetIds.Count == 0) return Result<EmployeePackageResponse>.Failure(Error.Validation("Zestaw stanowiskowy nie zawiera żadnego dostępnego aktywa - dodaj aktywo ręcznie."));

        var createResult = await _assignmentService.CreateWithEvidenceAsync(
            new CreateAssignmentRequest(person.Id, assetIds.Select(id => new AssignmentAssetRequest(id, request.AssetConditions?.GetValueOrDefault(id.ToString()))).ToList(), procedureIds, request.DueDate, request.Notes),
            evidenceManifest,
            files,
            cancellationToken);
        if (createResult.IsFailure) return Result<EmployeePackageResponse>.Failure(createResult.Error!);

        var assignment = createResult.Value!;
        _activity.Add(new ActivityLog(organizationId, "onboarding.employee_package.created", "assignment", assignment.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EmployeePackageResponse>.Success(new EmployeePackageResponse(assignment.Id, assignment.ProtocolNumber, assignment, warnings));
    }

    // Task checklist per employee: one row per asset/procedure across all of their assignments, with a
    // per-item completion status - this is what makes onboarding a tracked flow instead of a one-off email.
    public async Task<Result<OnboardingChecklistResponse>> GetChecklistAsync(Guid personId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator, TenebitRoles.Manager, TenebitRoles.Employee);
        if (access.IsFailure) return Result<OnboardingChecklistResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;

        // Employee only sees its own checklist; Manager only its managed team's - the module gate above
        // only proves the actor holds one of these roles, not that personId belongs to their scope
        // (audyt AUD3-006: Employee/Manager mogli podać dowolny personId w tej samej organizacji).
        if (!_currentUser.HasAnyRole(TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator))
        {
            if (_currentUser.HasAnyRole(TenebitRoles.Manager))
            {
                var scope = await _managerScope.ResolveAsync(_currentUser, [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator], cancellationToken);
                if (scope is not null && !scope.ContainsPerson(personId))
                {
                    return Result<OnboardingChecklistResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));
                }
            }
            else
            {
                if (_currentUser.PersonId is not { } currentPersonId || currentPersonId != personId)
                {
                    return Result<OnboardingChecklistResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));
                }
            }
        }

        var person = await _people.GetAsync(organizationId, personId, cancellationToken);
        if (person is null) return Result<OnboardingChecklistResponse>.Failure(Error.NotFound("Pracownik nie istnieje."));

        var assignments = await _assignments.ListByPersonAsync(organizationId, personId, cancellationToken);
        var assetIds = assignments.SelectMany(x => x.Assets.Select(a => a.AssetId)).Distinct().ToArray();
        var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
        var procedureIds = assignments.SelectMany(x => x.ProcedureAcceptances.Select(a => a.ProcedureId)).Distinct().ToArray();
        var procedures = await _procedures.GetByIdsAsync(organizationId, procedureIds, cancellationToken);

        // Status strings intentionally mirror AssignmentStatus/AcceptanceStatus enum member names so the
        // frontend can reuse its existing StatusBadge styling/translations instead of a parallel status set.
        var items = new List<OnboardingChecklistItemResponse>();
        foreach (var assignment in assignments)
        {
            var assetStatus = assignment.Status switch
            {
                AssignmentStatus.Accepted => nameof(AssignmentStatus.Accepted),
                AssignmentStatus.Returned => nameof(AssignmentStatus.Returned),
                AssignmentStatus.Overdue => nameof(AssignmentStatus.Overdue),
                _ => nameof(AssignmentStatus.AwaitingAcceptance)
            };
            foreach (var item in assignment.Assets)
            {
                var asset = assets.FirstOrDefault(x => x.Id == item.AssetId);
                items.Add(new OnboardingChecklistItemResponse("asset", item.AssetId, asset?.Name ?? "-", assetStatus, assignment.AcceptedAt));
            }

            foreach (var acceptance in assignment.ProcedureAcceptances)
            {
                var procedure = procedures.FirstOrDefault(x => x.Id == acceptance.ProcedureId);
                items.Add(new OnboardingChecklistItemResponse("procedure", acceptance.ProcedureId, procedure?.Title ?? "-", acceptance.Status.ToString(), acceptance.AcceptedAt));
            }
        }

        var completed = items.Count(x => x.Status is "Accepted" or "Returned");
        return Result<OnboardingChecklistResponse>.Success(new OnboardingChecklistResponse(personId, person.FullName, items, completed, items.Count));
    }

    private static string CreateProtocolNumber(DateTimeOffset now) => $"TEN-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
