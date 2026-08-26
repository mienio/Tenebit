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

public static class PeopleEndpoints
{
    public static RouteGroupBuilder MapPeopleEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/teams", async (TeamService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithTags("People");

        api.MapPost("/teams", async (CreateTeamRequest request, TeamService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/teams/{response.Id}"))
            .WithTags("People");

        api.MapPut("/teams/{id:guid}", async (Guid id, UpdateTeamRequest request, TeamService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("People");

        api.MapDelete("/teams/{id:guid}", async (Guid id, TeamService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("People");

        api.MapGet("/person-relation-types", async (PersonRelationTypeService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithTags("People");

        api.MapPost("/person-relation-types", async (CreatePersonRelationTypeRequest request, PersonRelationTypeService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/person-relation-types/{response.Id}"))
            .WithTags("People");

        api.MapPut("/person-relation-types/{id:guid}", async (Guid id, UpdatePersonRelationTypeRequest request, PersonRelationTypeService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("People");

        api.MapDelete("/person-relation-types/{id:guid}", async (Guid id, PersonRelationTypeService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("People");

        api.MapGet("/people", async (PeopleService service, string? search, int? page, int? pageSize, CancellationToken cancellationToken) =>
                page.HasValue
                    ? (await service.ListPagedAsync(search, page.Value, pageSize ?? 25, cancellationToken)).ToHttpResult()
                    : (await service.ListAsync(search, cancellationToken)).ToHttpResult())
            .WithTags("People");

        api.MapGet("/people/{id:guid}", async (Guid id, PeopleService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("People");

        api.MapPost("/people", async (CreatePersonRequest request, PeopleService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/people/{response.Id}"))
            .WithTags("People");

        api.MapPut("/people/{id:guid}", async (Guid id, UpdatePersonRequest request, PeopleService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("People");

        api.MapDelete("/people/{id:guid}", async (Guid id, PeopleService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("People");

        api.MapGet("/people/{id:guid}/workspace", async (Guid id, MyWorkspaceService service, CancellationToken cancellationToken) =>
                (await service.GetForPersonAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("People");

        api.MapGet("/people/{id:guid}/offboarding-preview", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.GetPreviewAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("People");

        return api;
    }
}
