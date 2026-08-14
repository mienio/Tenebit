using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Audit;
using Tenebit.Application.Audits;
using Tenebit.Domain.Audits;
using Tenebit.Application.Dashboard;
using Tenebit.Application.Evidence;
using Tenebit.Application.Identity;
using Tenebit.Application.JobProfiles;
using Tenebit.Application.Licenses;
using Tenebit.Application.Offboarding;
using Tenebit.Application.Onboarding;
using Tenebit.Application.Organizations;
using Tenebit.Application.People;
using Tenebit.Application.Procedures;
using Tenebit.Application.Settings;
using Tenebit.Application.Subscriptions;
using Tenebit.Application.Workspace;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Offboarding;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Api.Endpoints;

public static class TenebitEndpoints
{
    public static RouteGroupBuilder MapTenebitApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Tenebit");
        api.RequireAuthorization();

        api.MapGet("/health", () => Results.Ok(new { status = "ok", product = "Tenebit" }))
            .AllowAnonymous()
            .WithName("Health")
            .WithOpenApi();

        api.MapGet("/health/ready", async (TenebitDbContext db, CancellationToken cancellationToken) =>
            {
                try
                {
                    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                    return canConnect
                        ? Results.Ok(new { status = "ready", database = "ok" })
                        : Results.Json(new { status = "unready", database = "unreachable" }, statusCode: 503);
                }
                catch (Exception ex)
                {
                    return Results.Json(new { status = "unready", database = "error", detail = ex.Message }, statusCode: 503);
                }
            })
            .AllowAnonymous()
            .WithName("HealthReady")
            .WithOpenApi();

        MapAuth(api);
        api.MapExternalAuthEndpoints();
        MapWorkspace(api);
        MapDashboard(api);
        MapOrganization(api);
        MapOnboarding(api);
        MapAssets(api);
        MapAssetEvidence(api);
        api.MapLocationEndpoints();
        MapSettings(api);
        MapPeople(api);
        MapProcedures(api);
        MapAssignments(api);
        MapOffboarding(api);
        MapAssetAudits(api);
        MapPublicAssignments(api);
        MapPublicOffboarding(api);
        MapPublicAssets(api);
        MapActivityLog(api);
        MapSubscription(api);
        MapLicenses(api);

        return api;
    }

    private static void MapAuth(RouteGroupBuilder api)
    {
        api.MapPost("/auth/register", async (RegisterRequest request, AuthService service, TokenIssuer tokens, HttpResponse response, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var result = await service.RegisterAsync(request, cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                var refreshToken = await service.IssueRefreshTokenAsync(result.Value!.Id, cancellationToken);
                RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());
                return Results.Ok(new { token = tokens.Issue(result.Value!), user = result.Value });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/login", async (LoginRequest request, HttpRequest httpRequest, AuthService service, TokenIssuer tokens, TwoFactorChallengeStore challenges, HttpResponse response, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var deviceTrustToken = httpRequest.Cookies[DeviceTrustCookie.CookieName];
                var result = await service.LoginAsync(request, deviceTrustToken, cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                if (result.Value!.RequiresTwoFactor)
                {
                    var challengeToken = challenges.Create(result.Value!.PendingUserId!.Value);
                    return Results.Ok(new { requiresTwoFactor = true, challengeToken });
                }

                var user = result.Value!.User!;
                var refreshToken = await service.IssueRefreshTokenAsync(user.Id, cancellationToken);
                RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());
                return Results.Ok(new { token = tokens.Issue(user), user });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/login/2fa", async (TwoFactorLoginRequest request, AuthService service, TokenIssuer tokens, TwoFactorChallengeStore challenges, HttpResponse response, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var userId = challenges.Consume(request.ChallengeToken);
                if (userId is null)
                {
                    return Results.Json(new ErrorResponse("Sesja logowania wygasła. Zaloguj się ponownie.", "CHALLENGE_EXPIRED"), statusCode: 401);
                }

                var result = await service.CompleteTwoFactorLoginAsync(userId.Value, request.Code, cancellationToken);
                if (result.IsFailure) return result.ToHttpResult();

                var refreshToken = await service.IssueRefreshTokenAsync(result.Value!.Id, cancellationToken);
                RefreshTokenCookie.Append(response, refreshToken, env.IsDevelopment());

                if (request.RememberDevice)
                {
                    var deviceTrustToken = await service.IssueDeviceTrustTokenAsync(result.Value!.Id, cancellationToken);
                    DeviceTrustCookie.Append(response, deviceTrustToken, env.IsDevelopment());
                }

                return Results.Ok(new { token = tokens.Issue(result.Value!), user = result.Value });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/refresh", async (HttpRequest request, HttpResponse response, AuthService service, TokenIssuer tokens, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var rawToken = request.Cookies[RefreshTokenCookie.CookieName];
                if (string.IsNullOrEmpty(rawToken))
                {
                    return Results.Json(new ErrorResponse("Brak aktywnej sesji.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.RefreshAsync(rawToken, cancellationToken);
                if (result.IsFailure)
                {
                    RefreshTokenCookie.Delete(response, env.IsDevelopment());
                    return result.ToHttpResult();
                }

                RefreshTokenCookie.Append(response, result.Value!.RefreshToken, env.IsDevelopment());
                return Results.Ok(new { token = tokens.Issue(result.Value!.User), user = result.Value!.User });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/logout", async (HttpRequest request, HttpResponse response, AuthService service, IWebHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var rawToken = request.Cookies[RefreshTokenCookie.CookieName];
                if (!string.IsNullOrEmpty(rawToken))
                {
                    await service.RevokeRefreshTokenAsync(rawToken, cancellationToken);
                }

                RefreshTokenCookie.Delete(response, env.IsDevelopment());
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/password/forgot", async (ForgotPasswordRequest request, AuthService service, CancellationToken cancellationToken) =>
            {
                await service.RequestPasswordResetAsync(request, cancellationToken);
                return Results.Ok(new { message = "Jeśli podany adres e-mail istnieje w systemie, wysłaliśmy link do resetu hasła." });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/password/reset", async (ResetPasswordRequest request, AuthService service, CancellationToken cancellationToken) =>
            {
                var result = await service.ResetPasswordAsync(request, cancellationToken);
                return result.IsFailure ? result.ToNoContentResult() : Results.Ok(new { message = "Hasło zostało zmienione." });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/verify-email", async (VerifyEmailRequest request, AuthService service, CancellationToken cancellationToken) =>
            {
                var result = await service.VerifyEmailAsync(request, cancellationToken);
                return result.IsFailure ? result.ToNoContentResult() : Results.Ok(new { message = "E-mail został potwierdzony." });
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/resend-verification", async (ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (Guid.TryParse(currentUser.Subject, out var userId))
                {
                    await service.ResendVerificationEmailAsync(userId, cancellationToken);
                }

                return Results.Ok(new { message = "Jeśli Twój e-mail nie jest jeszcze potwierdzony, wysłaliśmy nową wiadomość." });
            })
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/2fa/setup", async (ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.SetupTwoFactorAsync(userId, cancellationToken);
                return result.ToHttpResult();
            })
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/2fa/enable", async (TwoFactorCodeRequest request, ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.EnableTwoFactorAsync(userId, request.Code, cancellationToken);
                return result.ToHttpResult();
            })
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/2fa/disable", async (TwoFactorCodeRequest request, ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.DisableTwoFactorAsync(userId, request.Code, cancellationToken);
                return result.IsFailure ? result.ToNoContentResult() : Results.Ok(new { message = "Dwuskładnikowe uwierzytelnianie zostało wyłączone." });
            })
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapPost("/auth/2fa/recovery-codes/regenerate", async (TwoFactorCodeRequest request, ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.RegenerateRecoveryCodesAsync(userId, request.Code, cancellationToken);
                return result.ToHttpResult();
            })
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();

        api.MapGet("/auth/2fa/recovery-codes/status", async (ICurrentUser currentUser, AuthService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.Subject, out var userId))
                {
                    return Results.Json(new ErrorResponse("Nieprawidłowa sesja.", "UNAUTHORIZED"), statusCode: 401);
                }

                var result = await service.GetRecoveryCodesRemainingAsync(userId, cancellationToken);
                return result.ToHttpResult();
            })
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .WithOpenApi();
    }

    private static void MapWorkspace(RouteGroupBuilder api)
    {
        api.MapGet("/my/workspace", async (MyWorkspaceService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(cancellationToken)))
            .WithTags("Workspace")
            .WithOpenApi();
    }

    private static void MapDashboard(RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", async (DashboardService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetSummaryAsync(cancellationToken)))
            .WithTags("Dashboard")
            .WithOpenApi();

        api.MapGet("/dashboard/layout", async (DashboardService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetLayoutAsync(cancellationToken)))
            .WithTags("Dashboard")
            .WithOpenApi();

        api.MapPut("/dashboard/layout", async (SaveDashboardLayoutRequest request, DashboardService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.SaveLayoutAsync(request, cancellationToken)))
            .WithTags("Dashboard")
            .WithOpenApi();

        api.MapGet("/dashboard/comparison", async (int? daysAgo, DashboardService service, CancellationToken cancellationToken) =>
                (await service.GetComparisonAsync(daysAgo ?? 7, cancellationToken)).ToHttpResult())
            .WithTags("Dashboard")
            .WithOpenApi();
    }

    private static void MapActivityLog(RouteGroupBuilder api)
    {
        api.MapGet("/activity-log", async (int? page, int? pageSize, string? entityType, Guid? entityId, string? search, DateOnly? dateFrom, DateOnly? dateTo, string? actor, string? action, ActivityLogService service, CancellationToken cancellationToken) =>
                (await service.ListAsync(page ?? 1, pageSize ?? 25, entityType, entityId, search, dateFrom, dateTo, actor, action, cancellationToken)).ToHttpResult())
            .WithTags("Audit")
            .WithOpenApi();
    }

    private static void MapOrganization(RouteGroupBuilder api)
    {
        api.MapGet("/organization", async (OrganizationService service, CancellationToken cancellationToken) =>
                (await service.GetCurrentAsync(cancellationToken)).ToHttpResult())
            .WithTags("Organization")
            .WithOpenApi();

        api.MapPut("/organization", async (UpdateOrganizationRequest request, OrganizationService service, CancellationToken cancellationToken) =>
                (await service.UpdateCurrentAsync(request, cancellationToken)).ToHttpResult())
            .WithTags("Organization")
            .WithOpenApi();
    }

    private static void MapOnboarding(RouteGroupBuilder api)
    {
        api.MapGet("/onboarding/status", async (OnboardingService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetStatusAsync(cancellationToken)))
            .WithTags("Onboarding")
            .WithOpenApi();

        api.MapPost("/onboarding/starter-package", async (CreateStarterPackageRequest request, OnboardingService service, CancellationToken cancellationToken) =>
                (await service.CreateStarterPackageAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.AssignmentId}"))
            .WithTags("Onboarding")
            .WithOpenApi();

        api.MapPost("/onboarding/employee-package", async (CreateEmployeePackageRequest request, OnboardingService service, CancellationToken cancellationToken) =>
                (await service.CreateEmployeePackageAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.AssignmentId}"))
            .WithTags("Onboarding")
            .WithOpenApi();

        api.MapGet("/onboarding/checklist/{personId:guid}", async (Guid personId, OnboardingService service, CancellationToken cancellationToken) =>
                (await service.GetChecklistAsync(personId, cancellationToken)).ToHttpResult())
            .WithTags("Onboarding")
            .WithOpenApi();
    }

    private static void MapAssets(RouteGroupBuilder api)
    {
        api.MapGet("/asset-categories", async (AssetCategoryService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithTags("Asset categories")
            .WithOpenApi();

        api.MapPost("/asset-categories", async (CreateAssetCategoryRequest request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/asset-categories/{response.Id}"))
            .WithTags("Asset categories")
            .WithOpenApi();

        api.MapPut("/asset-categories/{id:guid}", async (Guid id, UpdateAssetCategoryRequest request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset categories")
            .WithOpenApi();

        api.MapDelete("/asset-categories/{id:guid}", async (Guid id, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Asset categories")
            .WithOpenApi();

        api.MapPut("/asset-categories/{id:guid}/fields", async (Guid id, IReadOnlyList<SaveAssetFieldDefinitionRequest> request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.SaveFieldDefinitionsAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset categories")
            .WithOpenApi();

        api.MapPut("/asset-categories/{id:guid}/return-policy", async (Guid id, UpdateAssetCategoryReturnPolicyRequest request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.UpdateReturnPolicyAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset categories")
            .WithOpenApi();

        api.MapGet("/assets", async (AssetService service, string? search, AssetStatus? status, string? location, Guid? teamId, string? owner, string? warranty, string? sort, bool? desc, int? page, int? pageSize, CancellationToken cancellationToken) =>
                page.HasValue
                    ? (await service.ListPagedAsync(search, status, location, teamId, owner == "none", warranty == "expiring", sort, desc ?? false, page.Value, pageSize ?? 25, cancellationToken)).ToHttpResult()
                    : (await service.ListAsync(search, status, location, cancellationToken)).ToHttpResult())
            .WithTags("Assets")
            .WithOpenApi();

        api.MapGet("/assets/{id:guid}", async (Guid id, AssetService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithName("GetAsset")
            .WithTags("Assets")
            .WithOpenApi();

        api.MapPost("/assets", async (CreateAssetRequest request, AssetService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assets/{response.Id}"))
            .WithTags("Assets")
            .WithOpenApi();

        api.MapPut("/assets/{id:guid}", async (Guid id, UpdateAssetRequest request, AssetService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Assets")
            .WithOpenApi();

        api.MapDelete("/assets/{id:guid}", async (Guid id, AssetService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Assets")
            .WithOpenApi();

        api.MapGet("/assets/{id:guid}/qr", async (Guid id, AssetService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetQrSvgAsync(id, cancellationToken);
            return result.IsSuccess && result.Value is not null
                ? Results.Text(result.Value, "image/svg+xml")
                : result.ToHttpResult();
        })
            .WithTags("Assets")
            .WithOpenApi();

        api.MapPost("/assets/{id:guid}/fields/{fieldKey}/reveal", async (Guid id, string fieldKey, AssetService service, CancellationToken cancellationToken) =>
                (await service.RevealSensitiveFieldAsync(id, fieldKey, cancellationToken)).ToHttpResult())
            .WithTags("Assets")
            .WithOpenApi();

        api.MapGet("/assets/{id:guid}/inspection", async (Guid id, AssetInspectionService service, CancellationToken cancellationToken) =>
                (await service.GetPendingForAssetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Assets")
            .WithOpenApi();

        api.MapPost("/assets/inspections/{id:guid}/complete", async (Guid id, CompleteAssetInspectionRequest request, AssetInspectionService service, CancellationToken cancellationToken) =>
                (await service.CompleteAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Assets")
            .WithOpenApi();
    }

    private static void MapAssetEvidence(RouteGroupBuilder api)
    {
        api.MapGet("/assets/{assetId:guid}/evidence", async (Guid assetId, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.ListByAssetAsync(assetId, cancellationToken)).ToHttpResult())
            .WithTags("Asset evidence")
            .WithOpenApi();

        api.MapPost("/assets/{assetId:guid}/evidence", async (Guid assetId, HttpRequest request, AssetEvidenceService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Wyślij plik jako multipart/form-data.", code = "VALIDATION_ERROR" });
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "Wybierz zdjęcie.", code = "VALIDATION_ERROR" });
            }

            if (!Enum.TryParse<EvidencePhase>(form["phase"], true, out var phase))
            {
                return Results.BadRequest(new { message = "Nieprawidłowy etap materiału dowodowego.", code = "VALIDATION_ERROR" });
            }

            Guid? assignmentId = Guid.TryParse(form["assignmentId"], out var aid) ? aid : null;
            var caption = form["caption"].ToString();

            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);

            var uploadRequest = new UploadAssetEvidenceRequest(phase, assignmentId, string.IsNullOrWhiteSpace(caption) ? null : caption);
            return (await service.UploadAsync(assetId, uploadRequest, file.FileName, file.ContentType, memory.ToArray(), cancellationToken)).ToHttpResult();
        })
            .DisableAntiforgery()
            .WithTags("Asset evidence");

        api.MapGet("/evidence/{id:guid}", async (Guid id, AssetEvidenceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(id, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var evidence = result.Value;
            return Results.File(evidence.Content, evidence.ContentType, evidence.FileName);
        })
            .WithTags("Asset evidence");

        api.MapPost("/evidence/{id:guid}/lock", async (Guid id, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.LockAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset evidence")
            .WithOpenApi();

        api.MapPut("/evidence/{id:guid}/legal-hold", async (Guid id, SetEvidenceLegalHoldRequest request, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.SetLegalHoldAsync(id, request.Enabled, cancellationToken)).ToHttpResult())
            .WithTags("Asset evidence")
            .WithOpenApi();

        api.MapDelete("/evidence/{id:guid}", async (Guid id, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Asset evidence")
            .WithOpenApi();
    }

    private static void MapSettings(RouteGroupBuilder api)
    {
        static async Task<IResult> ListAssetStatuses(SettingsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAssetStatusesAsync(cancellationToken));

        static async Task<IResult> SaveAssetStatuses(IReadOnlyList<SaveAssetStatusSettingRequest> request, SettingsService service, CancellationToken cancellationToken) =>
            (await service.SaveAssetStatusesAsync(request, cancellationToken)).ToHttpResult();

        api.MapGet("/asset-statuses", ListAssetStatuses)
            .WithTags("Settings")
            .WithOpenApi();

        api.MapGet("/settings/asset-statuses", ListAssetStatuses)
            .WithTags("Settings")
            .WithOpenApi();

        api.MapPut("/asset-statuses", SaveAssetStatuses)
            .WithTags("Settings")
            .WithOpenApi();

        api.MapPut("/settings/asset-statuses", SaveAssetStatuses)
            .WithTags("Settings")
            .WithOpenApi();

        api.MapGet("/settings/evidence-privacy", async (SettingsService service, CancellationToken cancellationToken) =>
                (await service.GetEvidencePrivacyAsync(cancellationToken)).ToHttpResult())
            .WithTags("Settings")
            .WithOpenApi();

        api.MapPut("/settings/evidence-privacy", async (SaveEvidencePrivacySettingsRequest request, SettingsService service, CancellationToken cancellationToken) =>
                (await service.SaveEvidencePrivacyAsync(request, cancellationToken)).ToHttpResult())
            .WithTags("Settings")
            .WithOpenApi();

        static async Task<IResult> ListJobProfiles(JobProfileService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken));

        api.MapGet("/job-profiles", ListJobProfiles)
            .WithTags("Job profiles")
            .WithOpenApi();

        api.MapGet("/settings/job-profiles", ListJobProfiles)
            .WithTags("Job profiles")
            .WithOpenApi();

        api.MapPost("/job-profiles", async (SaveJobProfileRequest request, JobProfileService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/job-profiles/{response.Id}"))
            .WithTags("Job profiles")
            .WithOpenApi();

        api.MapPut("/job-profiles/{id:guid}", async (Guid id, SaveJobProfileRequest request, JobProfileService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Job profiles")
            .WithOpenApi();

        api.MapDelete("/job-profiles/{id:guid}", async (Guid id, JobProfileService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Job profiles")
            .WithOpenApi();

        static async Task<IResult> ListUsers(UserAccessService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken));

        api.MapGet("/organization-users", ListUsers)
            .WithTags("Users")
            .WithOpenApi();

        api.MapGet("/settings/users", ListUsers)
            .WithTags("Users")
            .WithOpenApi();

        api.MapPost("/organization-users", async (SaveOrganizationUserRequest request, UserAccessService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/organization-users/{response.Id}"))
            .WithTags("Users")
            .WithOpenApi();

        api.MapPut("/organization-users/{id:guid}", async (Guid id, SaveOrganizationUserRequest request, UserAccessService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Users")
            .WithOpenApi();

        static IResult ListRoles(UserAccessService service) => Results.Ok(service.Roles());

        api.MapGet("/roles", ListRoles)
            .WithTags("Users")
            .WithOpenApi();

        api.MapGet("/settings/roles", ListRoles)
            .WithTags("Users")
            .WithOpenApi();

        api.MapGet("/role-permissions", async (RolePermissionService service, CancellationToken cancellationToken) =>
                (await service.ListAsync(cancellationToken)).ToHttpResult())
            .WithTags("Users")
            .WithOpenApi();

        api.MapPut("/role-permissions", async (SetRolePermissionRequest request, RolePermissionService service, CancellationToken cancellationToken) =>
                (await service.SetAsync(request, cancellationToken)).ToNoContentResult())
            .WithTags("Users")
            .WithOpenApi();
    }

    private static void MapLicenses(RouteGroupBuilder api)
    {
        api.MapGet("/licenses", async (LicenseService service, CancellationToken cancellationToken) =>
                (await service.ListAsync(cancellationToken)).ToHttpResult())
            .WithTags("Licenses")
            .WithOpenApi();

        api.MapPost("/licenses", async (CreateLicenseRequest request, LicenseService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/licenses/{response.Id}"))
            .WithTags("Licenses")
            .WithOpenApi();

        api.MapPut("/licenses/{id:guid}", async (Guid id, UpdateLicenseRequest request, LicenseService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Licenses")
            .WithOpenApi();

        api.MapDelete("/licenses/{id:guid}", async (Guid id, LicenseService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Licenses")
            .WithOpenApi();

        api.MapPost("/licenses/{id:guid}/seats", async (Guid id, AssignLicenseSeatRequest request, LicenseService service, CancellationToken cancellationToken) =>
                (await service.AssignSeatAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Licenses")
            .WithOpenApi();

        api.MapDelete("/licenses/{id:guid}/seats/{personId:guid}", async (Guid id, Guid personId, LicenseService service, CancellationToken cancellationToken) =>
                (await service.UnassignSeatAsync(id, personId, cancellationToken)).ToHttpResult())
            .WithTags("Licenses")
            .WithOpenApi();
    }

    private static void MapPeople(RouteGroupBuilder api)
    {
        api.MapGet("/teams", async (TeamService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithTags("People")
            .WithOpenApi();

        api.MapPost("/teams", async (CreateTeamRequest request, TeamService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/teams/{response.Id}"))
            .WithTags("People")
            .WithOpenApi();

        api.MapPut("/teams/{id:guid}", async (Guid id, UpdateTeamRequest request, TeamService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapDelete("/teams/{id:guid}", async (Guid id, TeamService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapGet("/person-relation-types", async (PersonRelationTypeService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithTags("People")
            .WithOpenApi();

        api.MapPost("/person-relation-types", async (CreatePersonRelationTypeRequest request, PersonRelationTypeService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/person-relation-types/{response.Id}"))
            .WithTags("People")
            .WithOpenApi();

        api.MapPut("/person-relation-types/{id:guid}", async (Guid id, UpdatePersonRelationTypeRequest request, PersonRelationTypeService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapDelete("/person-relation-types/{id:guid}", async (Guid id, PersonRelationTypeService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapGet("/people", async (PeopleService service, string? search, int? page, int? pageSize, CancellationToken cancellationToken) =>
                page.HasValue
                    ? (await service.ListPagedAsync(search, page.Value, pageSize ?? 25, cancellationToken)).ToHttpResult()
                    : (await service.ListAsync(search, cancellationToken)).ToHttpResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapGet("/people/{id:guid}", async (Guid id, PeopleService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapPost("/people", async (CreatePersonRequest request, PeopleService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/people/{response.Id}"))
            .WithTags("People")
            .WithOpenApi();

        api.MapPut("/people/{id:guid}", async (Guid id, UpdatePersonRequest request, PeopleService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapDelete("/people/{id:guid}", async (Guid id, PeopleService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapPost("/people/{id:guid}/offboarding", async (Guid id, StartOffboardingRequest request, PeopleService service, CancellationToken cancellationToken) =>
                (await service.StartOffboardingAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("People")
            .WithOpenApi();

        api.MapGet("/people/{id:guid}/workspace", async (Guid id, MyWorkspaceService service, CancellationToken cancellationToken) =>
                (await service.GetForPersonAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("People")
            .WithOpenApi();
    }

    private static void MapProcedures(RouteGroupBuilder api)
    {
        api.MapGet("/procedures", async (ProcedureService service, string? search, int? page, int? pageSize, CancellationToken cancellationToken) =>
                page.HasValue
                    ? (await service.ListPagedAsync(search, page.Value, pageSize ?? 25, cancellationToken)).ToHttpResult()
                    : (await service.ListAsync(search, cancellationToken)).ToHttpResult())
            .WithTags("Procedures")
            .WithOpenApi();

        api.MapGet("/procedures/{id:guid}", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures")
            .WithOpenApi();

        api.MapPost("/procedures", async (CreateProcedureRequest request, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/procedures/{response.Id}"))
            .WithTags("Procedures")
            .WithOpenApi();

        api.MapPut("/procedures/{id:guid}", async (Guid id, UpdateProcedureRequest request, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Procedures")
            .WithOpenApi();

        api.MapPost("/procedures/{id:guid}/documents", async (Guid id, HttpRequest request, ProcedureService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Wyślij plik jako multipart/form-data.", code = "VALIDATION_ERROR" });
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return Results.BadRequest(new { message = "Wybierz plik procedury.", code = "VALIDATION_ERROR" });
            }

            if (file.Length == 0)
            {
                return Results.BadRequest(new { message = "Plik procedury jest pusty.", code = "VALIDATION_ERROR" });
            }

            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            return (await service.AttachDocumentAsync(id, file.FileName, file.ContentType, memory.ToArray(), cancellationToken)).ToHttpResult();
        })
            .DisableAntiforgery()
            .WithTags("Procedures");

        api.MapGet("/procedures/{id:guid}/documents/{documentId:guid}", async (Guid id, Guid documentId, ProcedureService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetDocumentAsync(id, documentId, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var document = result.Value;
            return Results.File(document.Content, document.ContentType, document.FileName);
        })
            .WithTags("Procedures");

        api.MapDelete("/procedures/{id:guid}/documents/{documentId:guid}", async (Guid id, Guid documentId, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.RemoveDocumentAsync(id, documentId, cancellationToken)).ToHttpResult())
            .WithTags("Procedures")
            .WithOpenApi();

        api.MapPost("/procedures/{id:guid}/publish", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.PublishAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures")
            .WithOpenApi();

        api.MapPost("/procedures/{id:guid}/archive", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.ArchiveAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures")
            .WithOpenApi();

        api.MapGet("/procedures/{id:guid}/acceptances", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.GetAcceptanceStatusAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures")
            .WithOpenApi();
    }

    private static void MapAssignments(RouteGroupBuilder api)
    {
        api.MapGet("/assignments", async (AssignmentService service, string? search, Tenebit.Domain.Assignments.AssignmentStatus? status, int? page, int? pageSize, CancellationToken cancellationToken) =>
                page.HasValue
                    ? (await service.ListPagedAsync(search, status, page.Value, pageSize ?? 25, cancellationToken)).ToHttpResult()
                    : (await service.ListAsync(cancellationToken)).ToHttpResult())
            .WithTags("Assignments")
            .WithOpenApi();

        api.MapGet("/assignments/{id:guid}", async (Guid id, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Assignments")
            .WithOpenApi();

        api.MapPost("/assignments", async (CreateAssignmentRequest request, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.Id}"))
            .WithTags("Assignments")
            .WithOpenApi();

        api.MapPost("/assignments/{id:guid}/accept", async (Guid id, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.AcceptAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Assignments")
            .WithOpenApi();

        api.MapPost("/assignments/{id:guid}/return", async (Guid id, ReturnAssignmentRequest request, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.ReturnAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Assignments")
            .WithOpenApi();

        api.MapPost("/assignments/{assignmentId:guid}/assets/{assetId:guid}/return", async (Guid assignmentId, Guid assetId, ReturnAssignmentAssetItemRequest request, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.ReturnAssetAsync(assignmentId, assetId, request, cancellationToken)).ToHttpResult())
            .WithTags("Assignments")
            .WithOpenApi();

        api.MapGet("/assignments/{id:guid}/protocol", async (Guid id, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetProtocolPdfAsync(id, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(result.Value, "application/pdf", $"protokol-{id}.pdf");
        })
            .WithTags("Assignments")
            .WithOpenApi();
    }

    private static void MapOffboarding(RouteGroupBuilder api)
    {
        api.MapGet("/offboarding", async (OffboardingCaseStatus? status, int? page, int? pageSize, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ListPagedAsync(status, page ?? 1, pageSize ?? 25, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapGet("/offboarding/{id:guid}", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding", async (CreateOffboardingCaseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/offboarding/{response.Case.Id}"))
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPut("/offboarding/{id:guid}", async (Guid id, UpdateOffboardingCaseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/start", async (Guid id, StartOffboardingCaseRequest? request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.StartAsync(id, request ?? new StartOffboardingCaseRequest(), cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/resend", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ResendLinkAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/regenerate-link", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.RegenerateLinkAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/execute-scheduled-actions", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ExecuteScheduledActionsAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/confirm-return", async (Guid id, Guid itemId, ConfirmOffboardingItemReturnRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ConfirmItemReturnAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/complete-inspection", async (Guid id, Guid itemId, CompleteAssetInspectionRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CompleteItemInspectionAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/release-license", async (Guid id, Guid itemId, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ReleaseItemLicenseAsync(id, itemId, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/resolve", async (Guid id, Guid itemId, ResolveOffboardingItemRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ResolveItemAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/waive", async (Guid id, Guid itemId, WaiveOffboardingItemRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.WaiveItemAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/complete", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CompleteAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/cancel", async (Guid id, CancelOffboardingCaseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CancelAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapPost("/offboarding/{id:guid}/restore-employment", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.RestoreEmploymentAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding")
            .WithOpenApi();

        api.MapGet("/offboarding/{id:guid}/protocol", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetProtocolPdfAsync(id, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(result.Value, "application/pdf", $"protokol-offboarding-{id}.pdf");
        })
            .WithTags("Offboarding")
            .WithOpenApi();
    }

    private static void MapAssetAudits(RouteGroupBuilder api)
    {
        api.MapGet("/asset-audits", async (AssetAuditCampaignStatus? status, int? page, int? pageSize, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.ListPagedAsync(status, page ?? 1, pageSize ?? 25, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits")
            .WithOpenApi();

        api.MapGet("/asset-audits/{id:guid}", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits")
            .WithOpenApi();

        api.MapPost("/asset-audits", async (CreateAssetAuditCampaignRequest request, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/asset-audits/{response.Campaign.Id}"))
            .WithTags("Asset audits")
            .WithOpenApi();

        api.MapPut("/asset-audits/{id:guid}", async (Guid id, UpdateAssetAuditCampaignRequest request, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits")
            .WithOpenApi();

        api.MapPost("/asset-audits/{id:guid}/preview", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.PreviewAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits")
            .WithOpenApi();

        api.MapPost("/asset-audits/{id:guid}/start", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.StartAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits")
            .WithOpenApi();
    }

    private static void MapPublicAssignments(RouteGroupBuilder api)
    {
        api.MapGet("/public/assignments/{organizationId:guid}/{assignmentId:guid}", async (Guid organizationId, Guid assignmentId, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.GetPublicAsync(organizationId, assignmentId, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments")
            .WithOpenApi();

        api.MapPost("/public/assignments/{organizationId:guid}/{assignmentId:guid}/accept", async (Guid organizationId, Guid assignmentId, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.AcceptPublicAsync(organizationId, assignmentId, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments")
            .WithOpenApi();

        api.MapGet("/public/assignments/{organizationId:guid}/{assignmentId:guid}/protocol", async (Guid organizationId, Guid assignmentId, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetPublicProtocolPdfAsync(organizationId, assignmentId, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(result.Value, "application/pdf", $"protokol-{assignmentId}.pdf");
        })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments")
            .WithOpenApi();

        api.MapGet("/public/assignments/{organizationId:guid}/{assignmentId:guid}/procedures/{procedureId:guid}/documents/{documentId:guid}", async (Guid organizationId, Guid assignmentId, Guid procedureId, Guid documentId, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetPublicProcedureDocumentAsync(organizationId, assignmentId, procedureId, documentId, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var document = result.Value;
            return Results.File(document.Content, document.ContentType, document.FileName);
        })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments")
            .WithOpenApi();
    }

    private static void MapPublicOffboarding(RouteGroupBuilder api)
    {
        api.MapGet("/public/offboarding/{token}", async (string token, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.GetPublicAsync(token, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public offboarding")
            .WithOpenApi();

        api.MapPost("/public/offboarding/{token}/response", async (string token, SubmitPublicOffboardingResponseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.RecordEmployeeResponsesAsync(token, request, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public offboarding")
            .WithOpenApi();

        api.MapPost("/public/offboarding/{token}/items/{itemId:guid}/evidence", async (string token, Guid itemId, HttpRequest request, OffboardingService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Wyślij plik jako multipart/form-data.", code = "VALIDATION_ERROR" });
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "Wybierz zdjęcie.", code = "VALIDATION_ERROR" });
            }

            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);

            var result = await service.UploadPublicEvidenceAsync(token, itemId, file.FileName, file.ContentType, memory.ToArray(), cancellationToken);
            return result.ToHttpResult();
        })
            .DisableAntiforgery()
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public offboarding");
    }

    private static void MapPublicAssets(RouteGroupBuilder api)
    {
        api.MapGet("/public/assets/{organizationId:guid}/{assetId:guid}", async (Guid organizationId, Guid assetId, AssetService service, CancellationToken cancellationToken) =>
                (await service.GetPublicScanAsync(organizationId, assetId, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assets")
            .WithOpenApi();

        api.MapPost("/public/assets/{organizationId:guid}/{assetId:guid}/report", async (Guid organizationId, Guid assetId, ReportAssetIssueRequest request, AssetService service, CancellationToken cancellationToken) =>
                (await service.ReportPublicIssueAsync(organizationId, assetId, request, cancellationToken)).ToNoContentResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assets")
            .WithOpenApi();
    }

    private static void MapSubscription(RouteGroupBuilder api)
    {
        api.MapGet("/subscription", async (SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.GetCurrentAsync(cancellationToken)).ToHttpResult())
            .WithTags("Subscription")
            .WithOpenApi();

        api.MapPost("/subscription/upgrade", async (UpgradeRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.UpgradeAsync(request.PlanKey, cancellationToken)).ToHttpResult())
            .WithTags("Subscription")
            .WithOpenApi();

        api.MapPost("/subscription/checkout", async (CheckoutSessionRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.CreateCheckoutSessionAsync(request.SuccessUrl, request.CancelUrl, cancellationToken)).ToHttpResult())
            .WithTags("Subscription")
            .WithOpenApi();

        api.MapPost("/subscription/billing-portal", async (BillingPortalRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.CreateBillingPortalSessionAsync(request.ReturnUrl, cancellationToken)).ToHttpResult())
            .WithTags("Subscription")
            .WithOpenApi();

        api.MapPost("/subscription/webhook", async (HttpRequest httpRequest, SubscriptionService service, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(httpRequest.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = httpRequest.Headers["Stripe-Signature"].ToString();
                return (await service.HandleWebhookAsync(payload, signature, cancellationToken)).ToNoContentResult();
            })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Subscription")
            .WithOpenApi();
    }

    private record UpgradeRequest(string PlanKey);
    private record CheckoutSessionRequest(string SuccessUrl, string CancelUrl);
    private record BillingPortalRequest(string ReturnUrl);
}
