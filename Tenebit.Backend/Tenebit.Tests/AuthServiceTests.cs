using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Identity;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AuthServiceTests
{
    // Independent RFC 6238 reference implementation used only to compute a valid TOTP code for a known secret in tests.
    private static string ComputeTotpCode(string secret)
    {
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var key = Base32.Decode(secret);
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        var hash = new HMACSHA1(key).ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000).ToString().PadLeft(6, '0');
    }

    private static (AuthService Service, InMemoryOrganizationRepository Organizations, InMemoryOrganizationUserRepository Users) CreateService()
    {
        var organizations = new InMemoryOrganizationRepository();
        var users = new InMemoryOrganizationUserRepository();
        var service = new AuthService(
            organizations,
            users,
            new InMemoryAssetCategoryRepository(),
            new InMemoryPersonRelationTypeRepository(),
            new InMemoryAlertRuleRepository(),
            new InMemoryActivityLogRepository(),
            new InMemoryExternalLoginRepository(users),
            new InMemoryPasswordResetTokenRepository(),
            new InMemoryEmailVerificationTokenRepository(),
            new InMemoryRefreshTokenRepository(),
            new InMemoryDeviceTrustTokenRepository(),
            new InMemoryTwoFactorRecoveryCodeRepository(),
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

        var result = await service.LoginAsync(new LoginRequest("owner@acme.test", "wrong-password"), null, CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task LoginAsync_ReturnsAuthenticatedOutcomeForCorrectCredentials_WhenTwoFactorDisabled()
    {
        var (service, _, _) = CreateService();
        await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);

        var result = await service.LoginAsync(new LoginRequest("owner@acme.test", "password123"), null, CancellationToken.None);
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

    [Fact]
    public async Task LoginAsync_RequiresTwoFactorWhenEnabledAndNoTrustedDevicePresented()
    {
        var (service, _, _) = CreateService();
        var registered = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);
        var setup = await service.SetupTwoFactorAsync(registered.Value!.Id, CancellationToken.None);
        await service.EnableTwoFactorAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);

        var result = await service.LoginAsync(new LoginRequest("owner@acme.test", "password123"), null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RequiresTwoFactor);
    }

    [Fact]
    public async Task LoginAsync_SkipsTwoFactorWhenValidDeviceTrustTokenPresented()
    {
        var (service, _, _) = CreateService();
        var registered = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);
        var setup = await service.SetupTwoFactorAsync(registered.Value!.Id, CancellationToken.None);
        await service.EnableTwoFactorAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);

        var deviceTrustToken = await service.IssueDeviceTrustTokenAsync(registered.Value!.Id, CancellationToken.None);
        var result = await service.LoginAsync(new LoginRequest("owner@acme.test", "password123"), deviceTrustToken, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RequiresTwoFactor);
        Assert.NotNull(result.Value!.User);
    }

    [Fact]
    public async Task LoginAsync_DoesNotHonorDeviceTrustTokenIssuedForADifferentUser()
    {
        var (service, _, _) = CreateService();
        var owner = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);
        var setup = await service.SetupTwoFactorAsync(owner.Value!.Id, CancellationToken.None);
        await service.EnableTwoFactorAsync(owner.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);

        var otherUser = await service.RegisterAsync(new RegisterRequest("OtherCo", "other@other.test", "password123", "Other", "PLN"), CancellationToken.None);
        var foreignDeviceToken = await service.IssueDeviceTrustTokenAsync(otherUser.Value!.Id, CancellationToken.None);

        var result = await service.LoginAsync(new LoginRequest("owner@acme.test", "password123"), foreignDeviceToken, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RequiresTwoFactor);
    }

    [Fact]
    public async Task EnableTwoFactorAsync_ReturnsTenUniqueRecoveryCodes()
    {
        var (service, _, _) = CreateService();
        var registered = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);
        var setup = await service.SetupTwoFactorAsync(registered.Value!.Id, CancellationToken.None);

        var result = await service.EnableTwoFactorAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.RecoveryCodes.Count);
        Assert.Equal(10, result.Value!.RecoveryCodes.Distinct().Count());
    }

    [Fact]
    public async Task CompleteTwoFactorLoginAsync_AcceptsRecoveryCodeAndConsumesItOnUse()
    {
        var (service, _, _) = CreateService();
        var registered = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);
        var setup = await service.SetupTwoFactorAsync(registered.Value!.Id, CancellationToken.None);
        var enabled = await service.EnableTwoFactorAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);
        var recoveryCode = enabled.Value!.RecoveryCodes[0];

        var firstUse = await service.CompleteTwoFactorLoginAsync(registered.Value!.Id, recoveryCode, CancellationToken.None);
        Assert.True(firstUse.IsSuccess);

        var secondUse = await service.CompleteTwoFactorLoginAsync(registered.Value!.Id, recoveryCode, CancellationToken.None);
        Assert.True(secondUse.IsFailure);
    }

    [Fact]
    public async Task RegenerateRecoveryCodesAsync_InvalidatesPreviouslyIssuedCodes()
    {
        var (service, _, _) = CreateService();
        var registered = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);
        var setup = await service.SetupTwoFactorAsync(registered.Value!.Id, CancellationToken.None);
        var enabled = await service.EnableTwoFactorAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);
        var oldCode = enabled.Value!.RecoveryCodes[0];

        var regenerated = await service.RegenerateRecoveryCodesAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);
        Assert.True(regenerated.IsSuccess);

        var loginWithOldCode = await service.CompleteTwoFactorLoginAsync(registered.Value!.Id, oldCode, CancellationToken.None);
        Assert.True(loginWithOldCode.IsFailure);

        var loginWithNewCode = await service.CompleteTwoFactorLoginAsync(registered.Value!.Id, regenerated.Value!.RecoveryCodes[0], CancellationToken.None);
        Assert.True(loginWithNewCode.IsSuccess);
    }

    [Fact]
    public async Task DisableTwoFactorAsync_RemovesRecoveryCodes()
    {
        var (service, _, _) = CreateService();
        var registered = await service.RegisterAsync(new RegisterRequest("Acme", "owner@acme.test", "password123", "Owner", "PLN"), CancellationToken.None);
        var setup = await service.SetupTwoFactorAsync(registered.Value!.Id, CancellationToken.None);
        await service.EnableTwoFactorAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);

        await service.DisableTwoFactorAsync(registered.Value!.Id, ComputeTotpCode(setup.Value!.Secret), CancellationToken.None);

        var remaining = await service.GetRecoveryCodesRemainingAsync(registered.Value!.Id, CancellationToken.None);
        Assert.Equal(0, remaining.Value);
    }
}
