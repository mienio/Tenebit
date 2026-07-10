using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Identity;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AuthServiceTests
{
    private static (AuthService Service, InMemoryOrganizationRepository Organizations, InMemoryOrganizationUserRepository Users) CreateService()
    {
        var organizations = new InMemoryOrganizationRepository();
        var users = new InMemoryOrganizationUserRepository();
        var service = new AuthService(
            organizations,
            users,
            new InMemoryAssetCategoryRepository(),
            new InMemoryActivityLogRepository(),
            new InMemoryExternalLoginRepository(users),
            new InMemoryPasswordResetTokenRepository(),
            new InMemoryEmailVerificationTokenRepository(),
            new InMemoryRefreshTokenRepository(),
            new FakeEmailSender(),
            new FakeAppLinkBuilder(),
            new FakeQrCodeGenerator(),
            new FakeClock(),
            new FakeUnitOfWork(),
            NullLogger<AuthService>.Instance);

        return (service, organizations, users);
    }

    [Fact]
    public async Task RegisterAsync_RejectsPasswordShorterThanEightCharacters()
    {
        var (service, _, _) = CreateService();
        var result = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "short", "Owner", "PLN"), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateEmail()
    {
        var (service, _, _) = CreateService();
        var request = new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN");
        var first = await service.RegisterAsync(request, CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.RegisterAsync(request with { OrganizationName = "Acme 2" }, CancellationToken.None);
        Assert.True(second.IsFailure);
        Assert.Equal("CONFLICT", second.Error!.Code);
    }

    [Fact]
    public async Task RegisterAsync_GrantsOwnerRoleAndUnverifiedEmailOnSuccess()
    {
        var (service, _, _) = CreateService();
        var result = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("owner", result.Value!.Roles);
        Assert.False(result.Value!.IsEmailVerified);
    }

    [Fact]
    public async Task LoginAsync_RejectsWrongPassword()
    {
        var (service, _, _) = CreateService();
        await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);

        var result = await service.LoginAsync(new LoginRequest("owner@acme.test", "wrong-password"), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task LoginAsync_ReturnsAuthenticatedOutcomeForCorrectCredentials_WhenTwoFactorDisabled()
    {
        var (service, _, _) = CreateService();
        await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);

        var result = await service.LoginAsync(new LoginRequest("owner@acme.test", "password123"), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RequiresTwoFactor);
        Assert.NotNull(result.Value!.User);
    }

    [Fact]
    public async Task ExternalLoginAsync_ProvisionsNewOrganizationOnFirstLogin()
    {
        var (service, organizations, _) = CreateService();
        var info = new ExternalUserInfo("google", "google-sub-1", "new.user@example.com", true, "New User");

        var result = await service.ExternalLoginAsync(info, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("owner", result.Value!.Roles);
        Assert.True(result.Value!.IsEmailVerified);
        Assert.Single(organizations.Organizations);
    }

    [Fact]
    public async Task ExternalLoginAsync_ReusesSameAccountOnRepeatLogin()
    {
        var (service, organizations, _) = CreateService();
        var info = new ExternalUserInfo("google", "google-sub-1", "new.user@example.com", true, "New User");

        var first = await service.ExternalLoginAsync(info, CancellationToken.None);
        var second = await service.ExternalLoginAsync(info, CancellationToken.None);

        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Single(organizations.Organizations);
    }

    [Fact]
    public async Task ExternalLoginAsync_RefusesToLinkUnverifiedEmailToExistingAccount()
    {
        var (service, _, _) = CreateService();
        await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);

        var info = new ExternalUserInfo("facebook", "fb-sub-1", "owner@acme.test", false, "Owner");
        var result = await service.ExternalLoginAsync(info, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ExternalLoginAsync_LinksVerifiedEmailToExistingAccount()
    {
        var (service, organizations, _) = CreateService();
        var registered = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);

        var info = new ExternalUserInfo("google", "google-sub-2", "owner@acme.test", true, "Owner");
        var result = await service.ExternalLoginAsync(info, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(registered.Value!.Id, result.Value!.Id);
        Assert.Single(organizations.Organizations);
    }
}
