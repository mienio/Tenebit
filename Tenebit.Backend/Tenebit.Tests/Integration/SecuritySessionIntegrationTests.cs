using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Api.Auth;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Identity;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class SecuritySessionIntegrationTests : IClassFixture<TenebitApiFactory>
{
    private readonly TenebitApiFactory _factory;

    public SecuritySessionIntegrationTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RotatedSecurityStamp_WithCacheInvalidation_InvalidatesExistingAccessTokenImmediately()
    {
        var (_, user, token) = await _factory.SeedTenantAsync("Stamp", TenebitRoles.Owner);
        var client = _factory.CreateAuthenticatedClient(token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/assets")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            var storedUser = await db.OrganizationUsers.SingleAsync(x => x.Id == user.Id);
            storedUser.RotateSecurityStamp();
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IUserSecurityStateCache>().Remove(user.Id);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/assets")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentOwnerDeactivation_LeavesOneActiveOwner()
    {
        var (organization, firstOwner, firstToken) = await _factory.SeedTenantAsync("Owners", TenebitRoles.Owner);

        OrganizationUser secondOwner;
        string secondToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            secondOwner = new OrganizationUser(organization.Id, $"{Guid.NewGuid():N}@example.test", "Second owner", true);
            secondOwner.Update(secondOwner.Email, secondOwner.DisplayName, true, [TenebitRoles.Owner]);
            secondOwner.MarkEmailVerified();
            db.OrganizationUsers.Add(secondOwner);
            await db.SaveChangesAsync();

            var tokenIssuer = scope.ServiceProvider.GetRequiredService<TokenIssuer>();
            secondToken = tokenIssuer.Issue(new AuthUserResponse(
                secondOwner.Id,
                organization.Id,
                organization.Name,
                secondOwner.Email,
                secondOwner.DisplayName,
                [TenebitRoles.Owner],
                true,
                false,
                secondOwner.SecurityStamp));
        }

        var firstClient = _factory.CreateAuthenticatedClient(firstToken);
        var secondClient = _factory.CreateAuthenticatedClient(secondToken);
        var firstRequest = new SaveOrganizationUserRequest(firstOwner.Email, firstOwner.DisplayName, false, [TenebitRoles.Owner]);
        var secondRequest = new SaveOrganizationUserRequest(secondOwner.Email, secondOwner.DisplayName, false, [TenebitRoles.Owner]);

        var responses = await Task.WhenAll(
            firstClient.PutAsJsonAsync($"/api/organization-users/{firstOwner.Id}", firstRequest),
            secondClient.PutAsJsonAsync($"/api/organization-users/{secondOwner.Id}", secondRequest));

        Assert.Single(responses.Where(x => x.IsSuccessStatusCode));
        Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.BadRequest));

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        var owners = await verificationDb.OrganizationUsers
            .Include(x => x.Roles)
            .Where(x => x.OrganizationId == organization.Id && x.IsActive)
            .ToListAsync();
        Assert.Single(owners.Where(x => x.Roles.Any(role => role.Role == TenebitRoles.Owner)));
    }
}
