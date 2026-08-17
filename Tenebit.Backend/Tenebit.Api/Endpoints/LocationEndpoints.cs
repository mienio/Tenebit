using System.Data;
using Microsoft.EntityFrameworkCore;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.People;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Api.Endpoints;

public static class LocationEndpoints
{
    // Mutacje struktury lokalizacji zmieniają dane całej organizacji — ta sama granica ról co
    // pozostałe zakładki "organizationOnly" w ustawieniach (patrz SettingsPage.tsx canManageOrganization).
    private static readonly string[] LocationManagers = [TenebitRoles.Owner, TenebitRoles.Admin];

    public static RouteGroupBuilder MapLocationEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/locations", async (TenebitDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        {
            var rows = await LoadLocationsAsync(db, currentUser.OrganizationId, cancellationToken);
            var assets = await db.Assets.AsNoTracking().Where(x => x.OrganizationId == currentUser.OrganizationId).ToListAsync(cancellationToken);
            var people = await db.People.AsNoTracking().Where(x => x.OrganizationId == currentUser.OrganizationId).ToListAsync(cancellationToken);
            return Results.Ok(MapLocations(rows, assets, people));
        }).WithTags("Locations");

        api.MapPost("/locations", async (CreateLocationRequest request, TenebitDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        {
            var access = AccessPolicy.EnsureAnyRole(currentUser, LocationManagers).ToHttpResultIfFailure();
            if (access is not null) return access;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { message = "Nazwa lokalizacji jest wymagana.", code = "VALIDATION_ERROR" });
            }

            var existingRows = await LoadLocationsAsync(db, currentUser.OrganizationId, cancellationToken);
            if (request.ParentId.HasValue && existingRows.All(x => x.Id != request.ParentId.Value))
            {
                return Results.BadRequest(new { message = "Lokalizacja nadrzędna nie istnieje.", code = "VALIDATION_ERROR" });
            }

            var id = Guid.NewGuid();
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO tenebit.asset_locations ("Id", "OrganizationId", "Name", "Type", "ParentId", "IsActive", "CreatedAt")
                VALUES (@id, @organizationId, @name, @type, @parentId, TRUE, @createdAt);
                """;
            Add(command, "id", id);
            Add(command, "organizationId", currentUser.OrganizationId);
            Add(command, "name", request.Name.Trim());
            Add(command, "type", NormalizeType(request.Type));
            Add(command, "parentId", request.ParentId);
            Add(command, "createdAt", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);

            var locations = await LoadLocationsAsync(db, currentUser.OrganizationId, cancellationToken);
            var response = MapLocations(locations, [], []).First(x => x.Id == id);
            return Results.Created($"/api/locations/{id}", response);
        }).WithTags("Locations");

        api.MapPut("/locations/{id:guid}", async (Guid id, UpdateLocationRequest request, TenebitDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        {
            var access = AccessPolicy.EnsureAnyRole(currentUser, LocationManagers).ToHttpResultIfFailure();
            if (access is not null) return access;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { message = "Nazwa lokalizacji jest wymagana.", code = "VALIDATION_ERROR" });
            }

            if (request.ParentId == id)
            {
                return Results.BadRequest(new { message = "Lokalizacja nie może być nadrzędna sama dla siebie.", code = "VALIDATION_ERROR" });
            }

            var existingRows = await LoadLocationsAsync(db, currentUser.OrganizationId, cancellationToken);
            if (request.ParentId.HasValue)
            {
                var byId = existingRows.ToDictionary(x => x.Id);
                if (!byId.ContainsKey(request.ParentId.Value))
                {
                    return Results.BadRequest(new { message = "Lokalizacja nadrzędna nie istnieje.", code = "VALIDATION_ERROR" });
                }

                var ancestor = byId[request.ParentId.Value];
                var guard = 0;
                while (ancestor is not null && guard++ < 20)
                {
                    if (ancestor.Id == id)
                    {
                        return Results.BadRequest(new { message = "Nie można ustawić lokalizacji podrzędnej jako nadrzędnej — utworzyłoby to cykl.", code = "VALIDATION_ERROR" });
                    }

                    ancestor = ancestor.ParentId.HasValue && byId.TryGetValue(ancestor.ParentId.Value, out var next) ? next : null;
                }
            }

            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE tenebit.asset_locations
                SET "Name" = @name, "Type" = @type, "ParentId" = @parentId, "IsActive" = @isActive
                WHERE "Id" = @id AND "OrganizationId" = @organizationId;
                """;
            Add(command, "id", id);
            Add(command, "organizationId", currentUser.OrganizationId);
            Add(command, "name", request.Name.Trim());
            Add(command, "type", NormalizeType(request.Type));
            Add(command, "parentId", request.ParentId);
            Add(command, "isActive", request.IsActive);
            var changed = await command.ExecuteNonQueryAsync(cancellationToken);
            if (changed == 0)
            {
                return Results.NotFound(new { message = "Lokalizacja nie istnieje.", code = "NOT_FOUND" });
            }

            var locations = await LoadLocationsAsync(db, currentUser.OrganizationId, cancellationToken);
            var response = MapLocations(locations, [], []).First(x => x.Id == id);
            return Results.Ok(response);
        }).WithTags("Locations");

        api.MapDelete("/locations/{id:guid}", async (Guid id, TenebitDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        {
            var access = AccessPolicy.EnsureAnyRole(currentUser, LocationManagers).ToHttpResultIfFailure();
            if (access is not null) return access;

            var rows = await LoadLocationsAsync(db, currentUser.OrganizationId, cancellationToken);
            var target = rows.FirstOrDefault(x => x.Id == id);
            if (target is null)
            {
                return Results.NotFound(new { message = "Lokalizacja nie istnieje.", code = "NOT_FOUND" });
            }

            if (rows.Any(x => x.ParentId == id))
            {
                return Results.BadRequest(new { message = "Najpierw usuń podlokalizacje tej pozycji.", code = "VALIDATION_ERROR" });
            }

            // BUG FIX: Previously loaded ALL assets and ALL people into memory only to count how many
            // belong to this location. Replaced with efficient COUNT queries that filter in the DB.
            // Note: Location is stored as full path string, so we compute it first.
            var mapped = MapLocations(rows, [], []);
            var location = mapped.First(x => x.Id == id);

            var assetCount = await db.Assets
                .AsNoTracking()
                .CountAsync(x => x.OrganizationId == currentUser.OrganizationId && x.Location == location.FullPath, cancellationToken);

            var personCount = await db.People
                .AsNoTracking()
                .CountAsync(x => x.OrganizationId == currentUser.OrganizationId && x.Location == location.FullPath, cancellationToken);

            if (assetCount > 0 || personCount > 0)
            {
                return Results.BadRequest(new { message = "Nie można usunąć lokalizacji z przypisanymi aktywami albo osobami.", code = "VALIDATION_ERROR" });
            }

            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM tenebit.asset_locations
                WHERE "Id" = @id AND "OrganizationId" = @organizationId;
                """;
            Add(command, "id", id);
            Add(command, "organizationId", currentUser.OrganizationId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return Results.NoContent();
        }).WithTags("Locations");

        api.MapGet("/locations/{id:guid}/inventory", async (Guid id, TenebitDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        {
            var rows = await LoadLocationsAsync(db, currentUser.OrganizationId, cancellationToken);
            var assets = await db.Assets.AsNoTracking().Where(x => x.OrganizationId == currentUser.OrganizationId).ToListAsync(cancellationToken);
            var people = await db.People.AsNoTracking().Where(x => x.OrganizationId == currentUser.OrganizationId).ToListAsync(cancellationToken);
            var mapped = MapLocations(rows, assets, people);
            var location = mapped.FirstOrDefault(x => x.Id == id);
            if (location is null)
            {
                return Results.NotFound(new { message = "Lokalizacja nie istnieje.", code = "NOT_FOUND" });
            }

            var locationAssets = assets.Where(x => string.Equals(x.Location, location.FullPath, StringComparison.OrdinalIgnoreCase)).Select(MapAsset).ToList();
            var locationPeople = people.Where(x => string.Equals(x.Location, location.FullPath, StringComparison.OrdinalIgnoreCase)).Select(MapPerson).ToList();
            return Results.Ok(new LocationInventoryResponse(location, locationAssets, locationPeople));
        }).WithTags("Locations");

        return api;
    }

    private static async Task<List<LocationRow>> LoadLocationsAsync(TenebitDbContext db, Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = new List<LocationRow>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "Id", "Name", "Type", "ParentId", "IsActive"
            FROM tenebit.asset_locations
            WHERE "OrganizationId" = @organizationId
            ORDER BY "CreatedAt", "Name";
            """;
        Add(command, "organizationId", organizationId);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new LocationRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetBoolean(4)));
        }

        return rows;
    }

    private static IReadOnlyList<LocationResponse> MapLocations(IReadOnlyList<LocationRow> rows, IReadOnlyList<Asset> assets, IReadOnlyList<Person> people)
    {
        var byId = rows.ToDictionary(x => x.Id);
        string FullPath(LocationRow row)
        {
            var stack = new Stack<string>();
            var current = row;
            var guard = 0;
            while (current is not null && guard++ < 20)
            {
                stack.Push(current.Name);
                current = current.ParentId.HasValue && byId.TryGetValue(current.ParentId.Value, out var parent) ? parent : null;
            }

            return string.Join(" / ", stack);
        }

        return rows.Select(row =>
        {
            var path = FullPath(row);
            var pathPrefix = path + " / ";
            var assetCount = assets.Count(x =>
                string.Equals(x.Location, path, StringComparison.OrdinalIgnoreCase)
                || (x.Location != null && x.Location.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase)));
            var personCount = people.Count(x =>
                string.Equals(x.Location, path, StringComparison.OrdinalIgnoreCase)
                || (x.Location != null && x.Location.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase)));
            return new LocationResponse(
                row.Id,
                row.Name,
                row.Type,
                row.ParentId,
                path,
                assetCount,
                personCount,
                row.IsActive);
        }).OrderBy(x => x.FullPath).ToList();
    }

    private static AssetListItem MapAsset(Asset asset) => new(asset.Id, asset.Name, asset.AssetTag, asset.Status, asset.AssignedPersonId, asset.Location);
    private static PersonListItem MapPerson(Person person) => new(person.Id, person.FullName, person.Email, person.JobTitle, person.ManagerId, person.Location);

    private static string NormalizeType(string? value) => string.IsNullOrWhiteSpace(value) ? "Room" : value.Trim();

    private static void Add(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record LocationRow(Guid Id, string Name, string Type, Guid? ParentId, bool IsActive);
    public sealed record CreateLocationRequest(string Name, string? Type, Guid? ParentId);
    public sealed record UpdateLocationRequest(string Name, string? Type, Guid? ParentId, bool IsActive);
    public sealed record LocationResponse(Guid Id, string Name, string Type, Guid? ParentId, string FullPath, int AssetCount, int PersonCount, bool IsActive);
    public sealed record AssetListItem(Guid Id, string Name, string AssetTag, AssetStatus Status, Guid? AssignedPersonId, string? Location);
    public sealed record PersonListItem(Guid Id, string FullName, string Email, string? JobTitle, Guid? ManagerId, string? Location);
    public sealed record LocationInventoryResponse(LocationResponse Location, IReadOnlyList<AssetListItem> Assets, IReadOnlyList<PersonListItem> People);
}
