using System.Text.Json;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Evidence;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Common;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.People;

namespace Tenebit.Application.Audits;

/// <summary>Tworzenie, podgląd i uruchamianie kampanii potwierdzenia aktywów (spec 5.4/5.8), oraz publiczny
/// przepływ uczestnika (spec 5.5) — token per uczestnik (nie per kampania, w przeciwieństwie do offboardingu).</summary>
public sealed class AssetAuditCampaignService
{
    private static readonly JsonSerializerOptions ScopeJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IAssetAuditCampaignRepository _campaigns;
    private readonly IAssetAuditParticipantRepository _participants;
    private readonly IAssetAuditItemRepository _items;
    private readonly IPersonRepository _people;
    private readonly IAssetRepository _assets;
    private readonly IAssetEvidenceRepository _evidence;
    private readonly AssetEvidenceService _evidenceService;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrganizationRepository _organizations;
    private readonly IEmailSender _emailSender;
    private readonly IAppLinkBuilder _linkBuilder;
    private readonly IPdfProtocolGenerator _pdfGenerator;

    public AssetAuditCampaignService(IAssetAuditCampaignRepository campaigns, IAssetAuditParticipantRepository participants,
        IAssetAuditItemRepository items, IPersonRepository people, IAssetRepository assets, IAssetEvidenceRepository evidence,
        AssetEvidenceService evidenceService, IActivityLogRepository activity,
        ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IOrganizationRepository organizations,
        IEmailSender emailSender, IAppLinkBuilder linkBuilder, IPdfProtocolGenerator pdfGenerator)
    {
        _campaigns = campaigns;
        _participants = participants;
        _items = items;
        _people = people;
        _assets = assets;
        _evidence = evidence;
        _evidenceService = evidenceService;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _organizations = organizations;
        _emailSender = emailSender;
        _linkBuilder = linkBuilder;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<Result<PagedResult<AssetAuditCampaignResponse>>> ListPagedAsync(AssetAuditCampaignStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditViewers);
        if (access.IsFailure) return Result<PagedResult<AssetAuditCampaignResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var (campaigns, total) = await _campaigns.ListPagedAsync(organizationId, status, page, pageSize, cancellationToken);
        return Result<PagedResult<AssetAuditCampaignResponse>>.Success(new PagedResult<AssetAuditCampaignResponse>(campaigns.Select(Map).ToList(), total, page, pageSize));
    }

    public async Task<Result<AssetAuditCampaignDetailsResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditViewers);
        if (access.IsFailure) return Result<AssetAuditCampaignDetailsResponse>.Failure(access.Error!);

        return await BuildDetailsAsync(id, cancellationToken);
    }

    private async Task<Result<AssetAuditCampaignDetailsResponse>> BuildDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.NotFound("Kampania nie istnieje."));

        var participants = await _participants.ListByCampaignAsync(organizationId, id, cancellationToken);
        var campaignItems = await _items.ListByCampaignAsync(organizationId, id, cancellationToken);
        var itemCountByParticipant = campaignItems.GroupBy(x => x.ParticipantId).ToDictionary(g => g.Key, g => g.Count());
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var names = people.ToDictionary(x => x.Id, x => x.FullName);

        var participantResponses = participants.Select(p => new AssetAuditParticipantResponse(
            p.Id, p.PersonId, names.GetValueOrDefault(p.PersonId), p.Email, p.Status, p.SubmittedAt, p.LastReminderAt,
            itemCountByParticipant.GetValueOrDefault(p.Id))).ToList();

        return Result<AssetAuditCampaignDetailsResponse>.Success(new AssetAuditCampaignDetailsResponse(Map(campaign), participantResponses));
    }

    public async Task<Result<AssetAuditCampaignDetailsResponse>> CreateAsync(CreateAssetAuditCampaignRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditManagers);
        if (access.IsFailure) return Result<AssetAuditCampaignDetailsResponse>.Failure(access.Error!);

        try
        {
            var now = _clock.UtcNow;
            var scopeJson = JsonSerializer.Serialize(request.Scope, ScopeJsonOptions);
            var campaign = new AssetAuditCampaign(_currentUser.OrganizationId, request.Name, request.Description, request.DueDate,
                scopeJson, _currentUser.Subject, now);
            _campaigns.Add(campaign);
            _activity.Add(new ActivityLog(campaign.OrganizationId, "asset_audit.created", "asset_audit_campaign", campaign.Id, _currentUser.Subject, campaign.Name, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(campaign.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Edycja dozwolona wyłącznie w Draft — po starcie zakres jest zablokowany (spec 5.4).</summary>
    public async Task<Result<AssetAuditCampaignDetailsResponse>> UpdateAsync(Guid id, UpdateAssetAuditCampaignRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditManagers);
        if (access.IsFailure) return Result<AssetAuditCampaignDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.NotFound("Kampania nie istnieje."));

        if (campaign.Status != AssetAuditCampaignStatus.Draft)
        {
            return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.Validation("Kampanię można edytować tylko w statusie roboczym."));
        }

        var scopeJson = JsonSerializer.Serialize(request.Scope, ScopeJsonOptions);
        try
        {
            campaign.UpdateDraft(request.Name, request.Description, request.DueDate, scopeJson);
        }
        catch (DomainException ex)
        {
            return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.Validation(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildDetailsAsync(id, cancellationToken);
    }

    /// <summary>Wylicza zakres bez zapisu — do ostrzeżenia administratora przed uruchomieniem (spec 5.4 krok 3).</summary>
    public async Task<Result<AssetAuditCampaignPreviewResponse>> PreviewAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditViewers);
        if (access.IsFailure) return Result<AssetAuditCampaignPreviewResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<AssetAuditCampaignPreviewResponse>.Failure(Error.NotFound("Kampania nie istnieje."));

        var scope = DeserializeScope(campaign.ScopeJson);
        if (scope is null) return Result<AssetAuditCampaignPreviewResponse>.Failure(Error.Validation("Nieprawidłowy zakres kampanii."));

        var (personAssets, _) = await ResolveScopeAsync(organizationId, scope, cancellationToken);
        var peopleWithoutEmail = personAssets.Keys.Where(p => string.IsNullOrWhiteSpace(p.Email)).Select(p => p.FullName).ToList();
        var assetCount = personAssets.Values.Sum(a => a.Count);

        return Result<AssetAuditCampaignPreviewResponse>.Success(new AssetAuditCampaignPreviewResponse(personAssets.Count, assetCount, peopleWithoutEmail));
    }

    /// <summary>Draft -> Active. Tworzy migawkę uczestników/pozycji na podstawie zakresu i wysyła linki (spec 5.4 krok 4-5).</summary>
    public async Task<Result<AssetAuditCampaignDetailsResponse>> StartAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditManagers);
        if (access.IsFailure) return Result<AssetAuditCampaignDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.NotFound("Kampania nie istnieje."));

        var scope = DeserializeScope(campaign.ScopeJson);
        if (scope is null) return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.Validation("Nieprawidłowy zakres kampanii."));

        try
        {
            var now = _clock.UtcNow;
            campaign.Start(now);

            var (personAssets, _) = await ResolveScopeAsync(organizationId, scope, cancellationToken);
            // Token żyje do terminu kampanii + 14 dni bufora, analogicznie do offboardingu (tam +30 dni,
            // bo dotyczy fizycznego zwrotu sprzętu; tu wyłącznie potwierdzenia online, więc krótszy margines).
            var tokenExpiresAt = campaign.DueDate.AddDays(14);

            foreach (var (person, assets) in personAssets)
            {
                var participant = new AssetAuditParticipant(organizationId, campaign.Id, person.Id, person.Email);
                var generated = PublicTokenService.Generate();
                participant.SetToken(generated.TokenHash, tokenExpiresAt);
                _participants.Add(participant);

                foreach (var asset in assets)
                {
                    var item = new AssetAuditItem(organizationId, campaign.Id, participant.Id, asset.Id, person.Id, asset.Location);
                    _items.Add(item);
                }

                if (!string.IsNullOrWhiteSpace(person.Email))
                {
                    await SendLinkAsync(campaign, generated.RawToken, person, now, cancellationToken);
                }
            }

            _activity.Add(new ActivityLog(organizationId, "asset_audit.started", "asset_audit_campaign", campaign.Id, _currentUser.Subject, campaign.Name, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(campaign.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetAuditCampaignDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private async Task SendLinkAsync(AssetAuditCampaign campaign, string rawToken, Person person, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var link = _linkBuilder.BuildAssetAuditLink(rawToken);
            var organization = await _organizations.GetAsync(campaign.OrganizationId, cancellationToken);

            var (subject, html) = EmailTemplates.AssetAuditLink(organization?.Language, person.FirstName, campaign.DueDate, link);
            await _emailSender.SendAsync(person.Email, subject, html, cancellationToken);
            _activity.Add(new ActivityLog(campaign.OrganizationId, "asset_audit.link_sent", "asset_audit_campaign", campaign.Id, _currentUser.Subject, person.FullName, now));
        }
        catch (Exception ex)
        {
            _activity.Add(new ActivityLog(campaign.OrganizationId, "asset_audit.email_failed", "asset_audit_campaign", campaign.Id, _currentUser.Subject, ex.Message, now));
        }
    }

    /// <summary>Znajduje osoby spełniające zakres i ich przypisane aktywa (ograniczone do kategorii, jeśli zakres
    /// to AssetCategory). Osoby bez żadnego przypisanego aktywa są pomijane — nie tworzymy pustego uczestnictwa.</summary>
    private async Task<(Dictionary<Person, List<Asset>> PersonAssets, int TotalCandidatePeople)> ResolveScopeAsync(Guid organizationId, AssetAuditScope scope, CancellationToken cancellationToken)
    {
        var allPeople = await _people.ListAsync(organizationId, null, cancellationToken);
        var allAssets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);

        IReadOnlyList<Person> candidates = scope.Type switch
        {
            AssetAuditScopeType.Organization => allPeople,
            AssetAuditScopeType.Team => allPeople.Where(p => p.TeamId.HasValue && (scope.TeamIds ?? []).Contains(p.TeamId.Value)).ToList(),
            AssetAuditScopeType.Location => allPeople.Where(p => p.Location is not null && (scope.Locations ?? []).Contains(p.Location)).ToList(),
            AssetAuditScopeType.Person => allPeople.Where(p => (scope.PersonIds ?? []).Contains(p.Id)).ToList(),
            AssetAuditScopeType.AssetCategory => allPeople.Where(p => allAssets.Any(a => a.AssignedPersonId == p.Id && (scope.AssetCategoryIds ?? []).Contains(a.CategoryId))).ToList(),
            _ => []
        };

        var result = new Dictionary<Person, List<Asset>>();
        foreach (var person in candidates)
        {
            var assets = allAssets.Where(a => a.AssignedPersonId == person.Id).ToList();
            if (scope.Type == AssetAuditScopeType.AssetCategory)
            {
                assets = assets.Where(a => (scope.AssetCategoryIds ?? []).Contains(a.CategoryId)).ToList();
            }

            if (assets.Count > 0)
            {
                result[person] = assets;
            }
        }

        return (result, candidates.Count);
    }

    private static AssetAuditScope? DeserializeScope(string? scopeJson)
    {
        if (string.IsNullOrWhiteSpace(scopeJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<AssetAuditScope>(scopeJson, ScopeJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AssetAuditCampaignResponse Map(AssetAuditCampaign campaign) => new(
        campaign.Id, campaign.Name, campaign.Description, campaign.Status, campaign.DueDate,
        campaign.CreatedAt, campaign.CreatedBy, campaign.StartedAt, campaign.CompletedAt, campaign.CompletedBy);

    /// <summary>Wspólna weryfikacja tokenu dla wszystkich publicznych endpointów audytu — wzorem
    /// <c>OffboardingService.ResolveByTokenAsync</c>, ale token jest tutaj przypisany do uczestnika, nie do
    /// kampanii. Zwraca zawsze ten sam generyczny NotFound dla tokenu nieistniejącego/wygasłego/unieważnionego.</summary>
    private async Task<Result<AssetAuditParticipant>> ResolveByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var candidates = await _participants.ListWithActiveTokenAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            if (PublicTokenService.Verify(token, candidate.TokenHash, candidate.TokenExpiresAt ?? DateTimeOffset.MinValue, candidate.TokenRevokedAt, now))
            {
                return Result<AssetAuditParticipant>.Success(candidate);
            }
        }

        return Result<AssetAuditParticipant>.Failure(Error.NotFound("Link jest nieprawidłowy lub wygasł."));
    }

    public async Task<Result<PublicAssetAuditResponse>> GetPublicAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<PublicAssetAuditResponse>.Failure(resolved.Error!);

        return Result<PublicAssetAuditResponse>.Success(await BuildPublicResponseAsync(resolved.Value!, cancellationToken));
    }

    private async Task<PublicAssetAuditResponse> BuildPublicResponseAsync(AssetAuditParticipant participant, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetAsync(participant.OrganizationId, participant.CampaignId, cancellationToken);
        var organization = await _organizations.GetAsync(participant.OrganizationId, cancellationToken);
        var items = await _items.ListByParticipantAsync(participant.OrganizationId, participant.Id, cancellationToken);

        var itemResponses = new List<PublicAssetAuditItemResponse>();
        foreach (var item in items)
        {
            var asset = await _assets.GetAsync(participant.OrganizationId, item.AssetId, cancellationToken);
            var photo = (await _evidence.ListByAssetAsync(participant.OrganizationId, item.AssetId, cancellationToken))
                .Where(x => x.AssetAuditItemId == item.Id)
                .OrderByDescending(x => x.UploadedAt)
                .FirstOrDefault();

            itemResponses.Add(new PublicAssetAuditItemResponse(item.Id, asset?.Name ?? string.Empty, asset?.AssetTag ?? string.Empty,
                asset?.Model, item.Response, item.Comment, photo?.Id));
        }

        var readOnly = participant.Status is AssetAuditParticipantStatus.Submitted or AssetAuditParticipantStatus.Reviewed;

        return new PublicAssetAuditResponse(organization?.Name ?? string.Empty, campaign?.Name ?? string.Empty, campaign?.DueDate ?? default, readOnly, itemResponses);
    }

    /// <summary>Do momentu wysłania odpowiedzi pracownik może poprawiać wybory (spec 5.5) — po Submit dalsze
    /// zmiany są odrzucane, ponowne otwarcie jest możliwe wyłącznie przez administratora.</summary>
    public async Task<Result<PublicAssetAuditResponse>> RecordItemResponseAsync(string token, Guid itemId, SubmitPublicAssetAuditItemRequest request, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<PublicAssetAuditResponse>.Failure(resolved.Error!);

        var participant = resolved.Value!;
        if (participant.Status is AssetAuditParticipantStatus.Submitted or AssetAuditParticipantStatus.Reviewed)
        {
            return Result<PublicAssetAuditResponse>.Failure(Error.Validation("Odpowiedzi zostały już wysłane i nie można ich zmienić."));
        }

        var item = (await _items.ListByParticipantAsync(participant.OrganizationId, participant.Id, cancellationToken)).FirstOrDefault(x => x.Id == itemId);
        if (item is null) return Result<PublicAssetAuditResponse>.Failure(Error.NotFound("Pozycja nie istnieje."));

        var now = _clock.UtcNow;
        participant.MarkInProgress();
        item.RecordResponse(request.Response, request.Comment, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PublicAssetAuditResponse>.Success(await BuildPublicResponseAsync(participant, cancellationToken));
    }

    public async Task<Result<PublicAssetAuditResponse>> SubmitAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<PublicAssetAuditResponse>.Failure(resolved.Error!);

        var participant = resolved.Value!;
        var now = _clock.UtcNow;

        try
        {
            participant.Submit(now);
        }
        catch (DomainException ex)
        {
            return Result<PublicAssetAuditResponse>.Failure(Error.Validation(ex.Message));
        }

        var campaign = await _campaigns.GetAsync(participant.OrganizationId, participant.CampaignId, cancellationToken);
        if (campaign is not null)
        {
            var allParticipants = await _participants.ListByCampaignAsync(participant.OrganizationId, participant.CampaignId, cancellationToken);
            campaign.RecomputeStatus(allParticipants);
        }

        _activity.Add(new ActivityLog(participant.OrganizationId, "asset_audit.participant_submitted", "asset_audit_participant", participant.Id, "employee", participant.Email, now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PublicAssetAuditResponse>.Success(await BuildPublicResponseAsync(participant, cancellationToken));
    }

    public async Task<Result<Guid>> UploadPublicEvidenceAsync(string token, Guid itemId, string fileName, string? declaredContentType, byte[] content, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<Guid>.Failure(resolved.Error!);

        var participant = resolved.Value!;
        var item = (await _items.ListByParticipantAsync(participant.OrganizationId, participant.Id, cancellationToken)).FirstOrDefault(x => x.Id == itemId);
        if (item is null) return Result<Guid>.Failure(Error.NotFound("Pozycja nie istnieje."));

        var uploadResult = await _evidenceService.UploadViaAuditPublicTokenAsync(participant.OrganizationId, item.AssetId, item.Id, fileName, declaredContentType, content, cancellationToken);
        if (uploadResult.IsFailure) return Result<Guid>.Failure(uploadResult.Error!);

        return Result<Guid>.Success(uploadResult.Value!.Id);
    }

    // --- Rozstrzyganie wyjątków / administracja kampanią (spec 5.7) ---

    /// <summary>Wysyła ponowny e-mail do uczestników, którzy jeszcze nie odpowiedzieli (Pending/InProgress) —
    /// Submitted/Reviewed pomijamy, bo już odpowiedzieli. Jeden zbiorczy wpis w ActivityLog, nie per-osoba,
    /// żeby nie zaspamować dziennika przy dużych kampaniach.</summary>
    public async Task<Result<RemindParticipantsResponse>> RemindParticipantsAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditManagers);
        if (access.IsFailure) return Result<RemindParticipantsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<RemindParticipantsResponse>.Failure(Error.NotFound("Kampania nie istnieje."));

        var now = _clock.UtcNow;
        var participants = await _participants.ListByCampaignAsync(organizationId, id, cancellationToken);
        var pending = participants.Where(p => p.Status is AssetAuditParticipantStatus.Pending or AssetAuditParticipantStatus.InProgress).ToList();

        var remindedCount = 0;
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        foreach (var participant in pending)
        {
            if (string.IsNullOrWhiteSpace(participant.Email)) continue;

            var person = await _people.GetAsync(organizationId, participant.PersonId, cancellationToken);
            if (person is null) continue;

            // PublicTokenService przechowuje wyłącznie hash — poprzedni surowy token nie jest odtwarzalny,
            // więc przypomnienie (jak resend w offboardingu) wystawia nowy token o tym samym terminie ważności
            // i unieważnia poprzedni, dając ten sam efekt końcowy: działający link w treści e-maila.
            try
            {
                var generated = PublicTokenService.Generate();
                participant.SetToken(generated.TokenHash, participant.TokenExpiresAt ?? campaign.DueDate.AddDays(14));
                var link = _linkBuilder.BuildAssetAuditLink(generated.RawToken);
                var (subject, html) = EmailTemplates.AssetAuditLink(organization?.Language, person.FirstName, campaign.DueDate, link);
                await _emailSender.SendAsync(participant.Email, subject, html, cancellationToken);
                participant.MarkReminded(now);
                remindedCount++;
            }
            catch (Exception)
            {
                // Pojedynczy błąd wysyłki nie powinien przerywać przypominania reszty uczestników.
            }
        }

        _activity.Add(new ActivityLog(organizationId, "asset_audit.reminder_sent", "asset_audit_campaign", campaign.Id, _currentUser.Subject, remindedCount.ToString(), now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<RemindParticipantsResponse>.Success(new RemindParticipantsResponse(remindedCount));
    }

    /// <summary>Ponowne otwarcie odpowiedzi jest świadomą decyzją administracyjną (spec 5.5) — celowo węższe
    /// uprawnienia niż reszta zarządzania kampanią (tylko Owner/Admin, bez AssetOperator).</summary>
    public async Task<Result<bool>> ReopenParticipantAsync(Guid id, Guid participantId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<bool>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var participant = await _participants.GetAsync(organizationId, id, participantId, cancellationToken);
        if (participant is null) return Result<bool>.Failure(Error.NotFound("Uczestnik nie istnieje."));

        var now = _clock.UtcNow;
        try
        {
            participant.Reopen(now);
        }
        catch (DomainException ex)
        {
            return Result<bool>.Failure(Error.Validation(ex.Message));
        }

        _activity.Add(new ActivityLog(organizationId, "asset_audit.participant_reopened", "asset_audit_participant", participant.Id, _currentUser.Subject, participant.Email, now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    /// <summary>Rozstrzygnięcie zgłoszonego wyjątku — poza samą decyzją domenową na pozycji, stosuje odpowiedni
    /// efekt na aktywie (spec 5.7). Auditor nie ma dostępu do tej operacji (tylko odczyt/eksport).</summary>
    public async Task<Result<bool>> ResolveItemAsync(Guid id, Guid itemId, ResolveAssetAuditItemRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditManagers);
        if (access.IsFailure) return Result<bool>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaignItems = await _items.ListByCampaignAsync(organizationId, id, cancellationToken);
        var item = campaignItems.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return Result<bool>.Failure(Error.NotFound("Pozycja nie istnieje."));

        if (request.Resolution == AssetAuditResolution.OwnershipCorrected && !request.NewOwnerPersonId.HasValue)
        {
            return Result<bool>.Failure(Error.Validation("Nowy właściciel jest wymagany dla tego rozstrzygnięcia."));
        }

        var asset = await _assets.GetAsync(organizationId, item.AssetId, cancellationToken);
        if (asset is null) return Result<bool>.Failure(Error.NotFound("Aktywo nie istnieje."));

        var now = _clock.UtcNow;
        try
        {
            item.Resolve(request.Resolution, request.Notes, _currentUser.Subject, now);

            switch (request.Resolution)
            {
                case AssetAuditResolution.AssetMarkedLost:
                    asset.ReleaseAssignment(AssetStatus.Lost);
                    break;
                case AssetAuditResolution.AssetMarkedDamaged:
                    // Zgłoszenie audytowe uszkodzenia nie jest fizycznym zwrotem — aktywo zostaje przy osobie
                    // do czasu naprawy/wymiany w osobnym procesie, dlatego zmieniamy TYLKO status.
                    asset.ChangeStatus(AssetStatus.Damaged);
                    break;
                case AssetAuditResolution.OwnershipCorrected:
                    asset.CorrectOwner(request.NewOwnerPersonId!.Value);
                    break;
                case AssetAuditResolution.Dismissed:
                case AssetAuditResolution.Accepted:
                    // Bez zmiany statusu aktywa — administrator uznaje ewidencję za poprawną albo odrzuca zgłoszenie.
                    break;
            }
        }
        catch (DomainException ex)
        {
            return Result<bool>.Failure(Error.Validation(ex.Message));
        }

        _activity.Add(new ActivityLog(organizationId, "asset_audit.exception_resolved", "asset_audit_item", item.Id, _currentUser.Subject, request.Resolution.ToString(), now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    /// <summary>Idempotentny (no-op gdy już Completed) — dozwolony z Active albo Reviewing, administrator może
    /// jawnie zakończyć kampanię z nieudzielonymi odpowiedziami (spec 5.7).</summary>
    public async Task<Result<bool>> CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditManagers);
        if (access.IsFailure) return Result<bool>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<bool>.Failure(Error.NotFound("Kampania nie istnieje."));

        var wasAlreadyCompleted = campaign.Status == AssetAuditCampaignStatus.Completed;
        var now = _clock.UtcNow;

        try
        {
            campaign.Complete(now, _currentUser.Subject);
        }
        catch (DomainException ex)
        {
            return Result<bool>.Failure(Error.Validation(ex.Message));
        }

        // Idempotencja: drugie wywołanie na już zakończonej kampanii nie może dopisać drugiego wpisu do ActivityLog.
        if (!wasAlreadyCompleted)
        {
            _activity.Add(new ActivityLog(organizationId, "asset_audit.completed", "asset_audit_campaign", campaign.Id, _currentUser.Subject, campaign.Name, now));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditManagers);
        if (access.IsFailure) return Result<bool>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<bool>.Failure(Error.NotFound("Kampania nie istnieje."));

        var now = _clock.UtcNow;
        try
        {
            campaign.Cancel(now);
        }
        catch (DomainException ex)
        {
            return Result<bool>.Failure(Error.Validation(ex.Message));
        }

        _activity.Add(new ActivityLog(organizationId, "asset_audit.cancelled", "asset_audit_campaign", campaign.Id, _currentUser.Subject, campaign.Name, now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<string>> ExportCsvAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditViewers);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<string>.Failure(Error.NotFound("Kampania nie istnieje."));

        var (participants, items, personNames, assetLookup) = await LoadCampaignDataAsync(organizationId, campaign, cancellationToken);
        var participantsById = participants.ToDictionary(p => p.Id);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine(string.Join(',', new[]
        {
            "Uczestnik", "E-mail", "Status uczestnika", "Aktywo", "Tag", "Oczekiwany właściciel", "Oczekiwana lokalizacja",
            "Odpowiedź", "Komentarz", "Rozstrzygnięcie", "Notatki rozstrzygnięcia", "Data odpowiedzi"
        }.Select(CsvField)));

        foreach (var item in items)
        {
            var participant = participantsById.GetValueOrDefault(item.ParticipantId);
            var asset = assetLookup.GetValueOrDefault(item.AssetId);

            var row = new[]
            {
                participant is null ? "—" : personNames.GetValueOrDefault(participant.PersonId, "—"),
                participant?.Email ?? "—",
                participant?.Status.ToString() ?? "—",
                asset?.Name ?? "—",
                asset?.AssetTag ?? "—",
                personNames.GetValueOrDefault(item.ExpectedPersonId, "—"),
                item.ExpectedLocation ?? "—",
                item.Response.ToString(),
                item.Comment ?? "",
                item.Resolution.ToString(),
                item.ResolutionNotes ?? "",
                item.RespondedAt?.ToString("yyyy-MM-dd HH:mm") ?? ""
            };

            csv.AppendLine(string.Join(',', row.Select(CsvField)));
        }

        return Result<string>.Success(csv.ToString());
    }

    /// <summary>Escapuje pole wg RFC4180 — cudzysłów wokół pola zawierającego przecinek, cudzysłów lub nową linię,
    /// z podwojeniem wewnętrznych cudzysłowów.</summary>
    private static string CsvField(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    public async Task<Result<byte[]>> GetReportPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetAuditViewers);
        if (access.IsFailure) return Result<byte[]>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var campaign = await _campaigns.GetAsync(organizationId, id, cancellationToken);
        if (campaign is null) return Result<byte[]>.Failure(Error.NotFound("Kampania nie istnieje."));

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var (participants, items, personNames, assetLookup) = await LoadCampaignDataAsync(organizationId, campaign, cancellationToken);

        var confirmed = items.Count(x => x.Response == AssetAuditResponse.Confirmed);
        var missing = items.Count(x => x.Response == AssetAuditResponse.Missing);
        var damaged = items.Count(x => x.Response == AssetAuditResponse.Damaged);
        var wrongOwner = items.Count(x => x.Response == AssetAuditResponse.WrongOwner);
        var nonResponding = participants.Count(p => p.Status is AssetAuditParticipantStatus.Pending or AssetAuditParticipantStatus.InProgress);

        var exceptionResponses = new[] { AssetAuditResponse.Missing, AssetAuditResponse.Damaged, AssetAuditResponse.WrongOwner };
        var participantsById = participants.ToDictionary(p => p.Id);
        var exceptions = items
            .Where(x => exceptionResponses.Contains(x.Response))
            .Select(x =>
            {
                var participant = participantsById.GetValueOrDefault(x.ParticipantId);
                var asset = assetLookup.GetValueOrDefault(x.AssetId);
                return new AssetAuditReportExceptionRow(
                    asset?.Name ?? "—", asset?.AssetTag ?? "—",
                    participant is null ? "—" : personNames.GetValueOrDefault(participant.PersonId, "—"),
                    x.Response.ToString(), x.Resolution.ToString(), x.ResolutionNotes, x.ResolvedBy, x.ResolvedAt);
            })
            .ToList();

        var model = new AssetAuditReportPdfModel(organization?.Name ?? "Tenebit", campaign.Name, campaign.DueDate, campaign.Status.ToString(),
            confirmed, missing, damaged, wrongOwner, nonResponding, exceptions);

        return Result<byte[]>.Success(_pdfGenerator.GenerateAssetAuditReport(model));
    }

    /// <summary>Raport/eksport są migawką historyczną: dane pochodzą wyłącznie z AssetAuditItem/Participant
    /// zapisanych w bazie, a Asset/Person są odpytywane tylko po nazwę do wyświetlenia — jeśli aktywo w
    /// międzyczasie zmieniło przypisanie, zapisany w AssetAuditItem stan (Response/Resolution/ExpectedPersonId)
    /// pozostaje niezmieniony (spec 5.11).</summary>
    private async Task<(IReadOnlyList<AssetAuditParticipant> Participants, IReadOnlyList<AssetAuditItem> Items,
        Dictionary<Guid, string> PersonNames, Dictionary<Guid, Asset> AssetLookup)> LoadCampaignDataAsync(
        Guid organizationId, AssetAuditCampaign campaign, CancellationToken cancellationToken)
    {
        var participants = await _participants.ListByCampaignAsync(organizationId, campaign.Id, cancellationToken);
        var items = await _items.ListByCampaignAsync(organizationId, campaign.Id, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var personNames = people.ToDictionary(x => x.Id, x => x.FullName);
        var assetIds = items.Select(x => x.AssetId).Distinct().ToList();
        var assets = await _assets.GetByIdsAsync(organizationId, assetIds, cancellationToken);
        var assetLookup = assets.ToDictionary(x => x.Id);

        return (participants, items, personNames, assetLookup);
    }
}

