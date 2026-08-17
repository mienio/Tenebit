using Tenebit.Api.Http;
using Tenebit.Application.Assets;

namespace Tenebit.Api.Endpoints;

public static class LocationEndpoints
{
    public static RouteGroupBuilder MapLocationEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/locations", async (LocationService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithTags("Locations");

        api.MapPost("/locations", async (CreateLocationRequest request, LocationService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/locations/{response.Id}"))
            .WithTags("Locations");

        api.MapPut("/locations/{id:guid}", async (Guid id, UpdateLocationRequest request, LocationService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Locations");

        api.MapDelete("/locations/{id:guid}", async (Guid id, LocationService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Locations");

        api.MapGet("/locations/{id:guid}/inventory", async (Guid id, LocationService service, CancellationToken cancellationToken) =>
                (await service.GetInventoryAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Locations");

        return api;
    }
}
