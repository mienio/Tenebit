using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Identity;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class UserAccessServiceTests
{
    private sealed record Fixture(
        UserAccessService Service,
        Organization Organization,
        InMemoryOrganizationUserRepository Users,
        InMemoryPersonRepository People,
        InMemoryRefreshTokenRepository RefreshTokens,
        InMemoryDeviceTrustTokenRepository DeviceTrustTokens,
        FakeCurrentUser CurrentUser,
        FakeClock Clock);

    private static Fixture CreateFixture(params string[] actorRoles)
    {
        var organizations = new InMemoryOrganizationRepository();
        var organization = new Organization("Acme", "PL", "pl", "PLN", "Europe/Warsaw");
        organizations.Add(organization);

        var users = new InMemoryOrganizationUserRepository();
        var people = new InMemoryPersonRepository();
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var deviceTrustTokens = new InMemoryDeviceTrustTokenRepository();
        var currentUser = new FakeCurrentUser
        {
            OrganizationId = organization.Id,
            Roles = actorRoles.Length == 0 ? [TenebitRoles.Owner] : actorRoles,
            Email = "actor@acme.test"
        };
        var clock = new FakeClock();

        var service = new UserAccessService(
            users,
            people,
            organizations,
            new InMemoryActivityLogRepository(),
            new InMemoryPasswordResetTokenRepository(),
            refreshTokens,
            deviceTrustTokens,
            new FakeEmailSender(),
            new FakeAppLinkBuilder(),
            currentUser,
            clock,
            new FakeUnitOfWork(),
            NullLogger<UserAccessService>.Instance);

        return new Fixture(service, organization, users, people, refreshTokens, deviceTrustTokens, currentUser, clock);
    }

    private static OrganizationUser AddUser(Fixture fixture, string email, bool isActive, params string[] roles)
    {
        var user = new OrganizationUser(fixture.Organization.Id, email, email, isActive);
        user.Update(email, email, isActive, roles);
        fixture.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task Admin_CannotCreateOwner()
    {
        var fixture = CreateFixture(TenebitRoles.Admin);

        var result = await fixture.Service.CreateAsync(
            new SaveOrganizationUserRequest("new-owner@acme.test", "New owner", true, [TenebitRoles.Owner]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Users.Users);
    }

    [Fact]
    public async Task Admin_CannotGrantOwnerToSelf()
    {
        var fixture = CreateFixture(TenebitRoles.Admin);
        var admin = AddUser(fixture, fixture.CurrentUser.Email, true, TenebitRoles.Admin);
        fixture.CurrentUser.Subject = admin.Id.ToString();

        var result = await fixture.Service.UpdateAsync(
            admin.Id,
            new SaveOrganizationUserRequest(admin.Email, admin.DisplayName, true, [TenebitRoles.Admin, TenebitRoles.Owner]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.DoesNotContain(admin.Roles, x => string.Equals(x.Role, TenebitRoles.Owner, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Admin_CannotDemoteOwner()
    {
        var fixture = CreateFixture(TenebitRoles.Admin);
        var owner = AddUser(fixture, "owner@acme.test", true, TenebitRoles.Owner);
        AddUser(fixture, "second-owner@acme.test", true, TenebitRoles.Owner);

        var result = await fixture.Service.UpdateAsync(
            owner.Id,
            new SaveOrganizationUserRequest(owner.Email, owner.DisplayName, true, [TenebitRoles.Admin]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(owner.Roles, x => string.Equals(x.Role, TenebitRoles.Owner, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Admin_CannotDeactivateOwner_EvenWhenAnotherOwnerRemains()
    {
        var fixture = CreateFixture(TenebitRoles.Admin);
        var owner = AddUser(fixture, "owner@acme.test", true, TenebitRoles.Owner);
        AddUser(fixture, "second-owner@acme.test", true, TenebitRoles.Owner);

        var result = await fixture.Service.UpdateAsync(
            owner.Id,
            new SaveOrganizationUserRequest(owner.Email, owner.DisplayName, false, [TenebitRoles.Owner]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(owner.IsActive);
    }

    [Fact]
    public async Task Admin_CannotChangeOwnerEmail()
    {
        var fixture = CreateFixture(TenebitRoles.Admin);
        var owner = AddUser(fixture, "owner@acme.test", true, TenebitRoles.Owner);

        var result = await fixture.Service.UpdateAsync(
            owner.Id,
            new SaveOrganizationUserRequest("admin-controlled@acme.test", owner.DisplayName, true, [TenebitRoles.Owner]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("owner@acme.test", owner.Email);
    }

    [Fact]
    public async Task CannotDeactivateLastActiveOwner()
    {
        var fixture = CreateFixture(TenebitRoles.Owner);
        var owner = AddUser(fixture, fixture.CurrentUser.Email, true, TenebitRoles.Owner);
        fixture.CurrentUser.Subject = owner.Id.ToString();

        var result = await fixture.Service.UpdateAsync(
            owner.Id,
            new SaveOrganizationUserRequest(owner.Email, owner.DisplayName, false, [TenebitRoles.Owner]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(owner.IsActive);
    }

    [Fact]
    public async Task Owner_CanCreateSecondOwner()
    {
        var fixture = CreateFixture(TenebitRoles.Owner);
        AddUser(fixture, fixture.CurrentUser.Email, true, TenebitRoles.Owner);

        var result = await fixture.Service.CreateAsync(
            new SaveOrganizationUserRequest("second-owner@acme.test", "Second owner", true, [TenebitRoles.Owner]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Users.Users.Count(x => x.IsActive && x.Roles.Any(r => r.Role == TenebitRoles.Owner)));
    }


    [Fact]
    public async Task CreateAsync_AutoLinksExactSameTenantPersonByEmail()
    {
        var fixture = CreateFixture(TenebitRoles.Owner);
        var person = new Person(fixture.Organization.Id, "Anna", "Pracownik", "anna@acme.test");
        fixture.People.Add(person);

        var result = await fixture.Service.CreateAsync(
            new SaveOrganizationUserRequest(person.Email, person.FullName, true, [TenebitRoles.Employee]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(person.Id, result.Value!.PersonId);
        Assert.Equal(person.Id, Assert.Single(fixture.Users.Users).PersonId);
    }

    [Fact]
    public async Task CreateAsync_RejectsPersonFromAnotherOrganization()
    {
        var fixture = CreateFixture(TenebitRoles.Owner);
        var otherPerson = new Person(Guid.NewGuid(), "Ola", "Inna", "ola@other.test");
        fixture.People.Add(otherPerson);

        var result = await fixture.Service.CreateAsync(
            new SaveOrganizationUserRequest("login@acme.test", "Login", true, [TenebitRoles.Employee], otherPerson.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Users.Users);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicatePersonLink()
    {
        var fixture = CreateFixture(TenebitRoles.Owner);
        var person = new Person(fixture.Organization.Id, "Anna", "Pracownik", "anna@acme.test");
        fixture.People.Add(person);
        var existing = AddUser(fixture, "first@acme.test", true, TenebitRoles.Employee);
        existing.LinkPerson(person.Id);

        var result = await fixture.Service.CreateAsync(
            new SaveOrganizationUserRequest("second@acme.test", "Second", true, [TenebitRoles.Employee], person.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Single(fixture.Users.Users);
    }

    [Fact]
    public async Task RoleChange_RotatesSecurityStamp_AndRevokesRefreshAndTrustedDevices()
    {
        var fixture = CreateFixture(TenebitRoles.Owner);
        AddUser(fixture, fixture.CurrentUser.Email, true, TenebitRoles.Owner);
        var target = AddUser(fixture, "employee@acme.test", true, TenebitRoles.Employee);
        var oldStamp = target.SecurityStamp;

        const string rawRefresh = "refresh-token";
        const string rawDevice = "device-token";
        fixture.RefreshTokens.Add(new RefreshToken(target.Id, TokenHasher.Hash(rawRefresh), fixture.Clock.UtcNow.AddDays(30)));
        fixture.DeviceTrustTokens.Add(new DeviceTrustToken(target.Id, TokenHasher.Hash(rawDevice), fixture.Clock.UtcNow.AddDays(30)));

        var result = await fixture.Service.UpdateAsync(
            target.Id,
            new SaveOrganizationUserRequest(target.Email, target.DisplayName, true, [TenebitRoles.Manager]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(oldStamp, target.SecurityStamp);
        Assert.Null(await fixture.RefreshTokens.FindValidAsync(TokenHasher.Hash(rawRefresh), fixture.Clock.UtcNow, CancellationToken.None));
        Assert.Null(await fixture.DeviceTrustTokens.FindValidAsync(target.Id, TokenHasher.Hash(rawDevice), fixture.Clock.UtcNow, CancellationToken.None));
    }
}
