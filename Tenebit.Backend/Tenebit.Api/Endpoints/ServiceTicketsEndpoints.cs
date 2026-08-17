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

public static class ServiceTicketsEndpoints
{
    public static RouteGroupBuilder MapServiceTicketsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/assets/{assetId:guid}/service-tickets", async (Guid assetId, ServiceTicketService service, CancellationToken cancellationToken) =>
                (await service.ListByAssetAsync(assetId, cancellationToken)).ToHttpResult())
            .WithTags("Service tickets");

        api.MapGet("/service-tickets", async (ServiceTicketStatus? status, int? page, int? pageSize, ServiceTicketService service, CancellationToken cancellationToken) =>
                (await service.ListPagedAsync(status, page ?? 1, pageSize ?? 25, cancellationToken)).ToHttpResult())
            .WithTags("Service tickets");

        api.MapGet("/service-tickets/{id:guid}", async (Guid id, ServiceTicketService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Service tickets");

        api.MapPost("/service-tickets", async (OpenServiceTicketRequest request, ServiceTicketService service, CancellationToken cancellationToken) =>
                (await service.OpenAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/service-tickets/{response.Id}"))
            .WithTags("Service tickets");

        api.MapPut("/service-tickets/{id:guid}", async (Guid id, UpdateServiceTicketRequest request, ServiceTicketService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Service tickets");

        api.MapPost("/service-tickets/{id:guid}/complete", async (Guid id, CompleteServiceTicketRequest request, ServiceTicketService service, CancellationToken cancellationToken) =>
                (await service.CompleteAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Service tickets");

        api.MapPost("/service-tickets/{id:guid}/cancel", async (Guid id, CancelServiceTicketRequest request, ServiceTicketService service, CancellationToken cancellationToken) =>
                (await service.CancelAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Service tickets");

        return api;
    }
}
