using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Alerts;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Audit;
using Tenebit.Domain.Alerts;
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
using Tenebit.Application.Reservations;
using Tenebit.Application.Settings;
using Tenebit.Application.Subscriptions;
using Tenebit.Application.Workspace;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Reservations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Api.Endpoints;

public static class LicensesEndpoints
{
    public static RouteGroupBuilder MapLicensesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/licenses", async (LicenseService service, CancellationToken cancellationToken) =>
                (await service.ListAsync(cancellationToken)).ToHttpResult())
            .WithTags("Licenses");

        api.MapPost("/licenses", async (CreateLicenseRequest request, LicenseService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/licenses/{response.Id}"))
            .WithTags("Licenses");

        api.MapPut("/licenses/{id:guid}", async (Guid id, UpdateLicenseRequest request, LicenseService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Licenses");

        api.MapDelete("/licenses/{id:guid}", async (Guid id, LicenseService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Licenses");

        api.MapPost("/licenses/{id:guid}/seats", async (Guid id, AssignLicenseSeatRequest request, LicenseService service, CancellationToken cancellationToken) =>
                (await service.AssignSeatAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Licenses");

        api.MapDelete("/licenses/{id:guid}/seats/{personId:guid}", async (Guid id, Guid personId, LicenseService service, CancellationToken cancellationToken) =>
                (await service.UnassignSeatAsync(id, personId, cancellationToken)).ToHttpResult())
            .WithTags("Licenses");

        return api;
    }
}
