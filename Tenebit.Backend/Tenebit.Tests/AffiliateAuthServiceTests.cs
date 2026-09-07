using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Affiliates;
using Tenebit.Application.Identity;
using Tenebit.Domain.Affiliates;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AffiliateAuthServiceTests
{
    private sealed record Fixture(
        AffiliateAuthService Service, InMemoryAffiliateRepository Affiliates, InMemoryAffiliateRefreshTokenRepository RefreshTokens,
        InMemoryAffiliatePasswordResetTokenRepository PasswordResetTokens, InMemoryAffiliateEmailVerificationTokenRepository EmailVerificationTokens,
        FakeEmailSender EmailSender, FakeClock Clock);

    private static Fixture CreateFixture()
    {
        var affiliates = new InMemoryAffiliateRepository();
        var refreshTokens = new InMemoryAffiliateRefreshTokenRepository();
        var passwordResetTokens = new InMemoryAffiliatePasswordResetTokenRepository();
        var emailVerificationTokens = new InMemoryAffiliateEmailVerificationTokenRepository();
        var emailSender = new FakeEmailSender();
        var clock = new FakeClock();
        var service = new AffiliateAuthService(
            affiliates, refreshTokens, passwordResetTokens, emailVerificationTokens,
            new InMemoryAffiliateProgramSettingsRepository(), new FakeUnitOfWork(), clock,
            emailSender, new FakeAppLinkBuilder(), NullLogger<AffiliateAuthService>.Instance,
            new InMemoryAffiliateSecurityStateCache());

        return new Fixture(service, affiliates, refreshTokens, passwordResetTokens, emailVerificationTokens, emailSender, clock);
    }

    [Fact]
    public async Task Registration_without_accepting_terms_is_rejected()
    {
        var fixture = CreateFixture();
        var result = await fixture.Service.RegisterAsync("damian@example.com", "password123", "Damian", "Kowalski", "PL", null, acceptTerms: false, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Affiliates.Affiliates);
    }

    [Fact]
    public async Task Registration_stamps_the_current_terms_version_and_sends_a_verification_code()
    {
        var fixture = CreateFixture();
        var result = await fixture.Service.RegisterAsync("damian@example.com", "password123", "Damian", "Kowalski", "PL", "@damian", acceptTerms: true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var affiliate = Assert.Single(fixture.Affiliates.Affiliates);
        Assert.NotNull(affiliate.AcceptedTermsAt);
        Assert.Equal("@damian", affiliate.RevolutTag);
        Assert.Equal(AffiliateStatus.Active, affiliate.Status);
        Assert.Single(fixture.EmailSender.Sent);
    }

    [Fact]
    public async Task Registering_an_existing_email_does_not_disclose_whether_the_account_exists()
    {
        var fixture = CreateFixture();
        await fixture.Service.RegisterAsync("damian@example.com", "password123", "Damian", "Kowalski", null, null, true, CancellationToken.None);
        var countAfterFirst = fixture.Affiliates.Affiliates.Count;

        var second = await fixture.Service.RegisterAsync("damian@example.com", "different-password", "Damian", "Kowalski", null, null, true, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(countAfterFirst, fixture.Affiliates.Affiliates.Count); // no duplicate account created
    }

    [Fact]
    public async Task Login_fails_with_a_generic_error_for_a_wrong_password()
    {
        var fixture = CreateFixture();
        var affiliate = new Affiliate("damian@example.com", PasswordHasher.Hash("correct-password"), "Damian", "Kowalski", null, fixture.Clock.UtcNow);
        affiliate.MarkEmailVerified();
        fixture.Affiliates.Add(affiliate);

        var result = await fixture.Service.LoginAsync("damian@example.com", "wrong-password", CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Login_fails_for_a_blocked_affiliate()
    {
        var fixture = CreateFixture();
        var affiliate = new Affiliate("damian@example.com", PasswordHasher.Hash("password123"), "Damian", "Kowalski", null, fixture.Clock.UtcNow);
        affiliate.MarkEmailVerified();
        affiliate.Block("fraud", fixture.Clock.UtcNow);
        fixture.Affiliates.Add(affiliate);

        var result = await fixture.Service.LoginAsync("damian@example.com", "password123", CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Login_succeeds_for_a_newly_registered_affiliate_without_manual_approval()
    {
        var fixture = CreateFixture();
        var affiliate = new Affiliate("damian@example.com", PasswordHasher.Hash("password123"), "Damian", "Kowalski", null, fixture.Clock.UtcNow);
        affiliate.MarkEmailVerified();
        fixture.Affiliates.Add(affiliate);

        var result = await fixture.Service.LoginAsync("damian@example.com", "password123", CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value!.Affiliate.Status);
    }

    [Fact]
    public async Task Login_returns_the_affiliates_real_persisted_security_stamp()
    {
        // Regression guard: the login/refresh endpoints must embed the affiliate's actual
        // SecurityStamp in the issued JWT, not an unrelated freshly-minted GUID - otherwise every
        // authenticated request after login fails the live security-stamp check with a 401.
        var fixture = CreateFixture();
        var affiliate = new Affiliate("damian@example.com", PasswordHasher.Hash("password123"), "Damian", "Kowalski", null, fixture.Clock.UtcNow);
        affiliate.MarkEmailVerified();
        fixture.Affiliates.Add(affiliate);

        var result = await fixture.Service.LoginAsync("damian@example.com", "password123", CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(affiliate.SecurityStamp, result.Value!.SecurityStamp);
    }

    [Fact]
    public async Task Replaying_an_already_rotated_refresh_token_revokes_the_whole_family()
    {
        var fixture = CreateFixture();
        var affiliate = new Affiliate("damian@example.com", PasswordHasher.Hash("password123"), "Damian", "Kowalski", null, fixture.Clock.UtcNow);
        affiliate.MarkEmailVerified();
        fixture.Affiliates.Add(affiliate);

        var rawToken = await fixture.Service.IssueRefreshTokenAsync(affiliate.Id, CancellationToken.None);
        var firstRefresh = await fixture.Service.RefreshAsync(rawToken, CancellationToken.None);
        Assert.True(firstRefresh.IsSuccess);

        // Replaying the original (now-rotated) token is a reuse signal.
        var replay = await fixture.Service.RefreshAsync(rawToken, CancellationToken.None);
        Assert.True(replay.IsFailure);

        // The successor issued by the first refresh must now be dead too (whole family revoked).
        var successorReplay = await fixture.Service.RefreshAsync(firstRefresh.Value!.RawRefreshToken, CancellationToken.None);
        Assert.True(successorReplay.IsFailure);
    }

    [Fact]
    public async Task Password_reset_changes_the_password_and_revokes_outstanding_sessions()
    {
        var fixture = CreateFixture();
        var affiliate = new Affiliate("damian@example.com", PasswordHasher.Hash("old-password"), "Damian", "Kowalski", null, fixture.Clock.UtcNow);
        affiliate.MarkEmailVerified();
        fixture.Affiliates.Add(affiliate);
        await fixture.Service.IssueRefreshTokenAsync(affiliate.Id, CancellationToken.None);

        await fixture.Service.RequestPasswordResetAsync(affiliate.Email, CancellationToken.None);
        var sentCode = TokenHasher.NormalizeOneTimeCode(ExtractCodeFromLastEmail(fixture.EmailSender));

        var result = await fixture.Service.ResetPasswordAsync(affiliate.Email, sentCode, "new-password-123", CancellationToken.None);
        Assert.True(result.IsSuccess);

        var loginWithOld = await fixture.Service.LoginAsync(affiliate.Email, "old-password", CancellationToken.None);
        Assert.True(loginWithOld.IsFailure);
        var loginWithNew = await fixture.Service.LoginAsync(affiliate.Email, "new-password-123", CancellationToken.None);
        Assert.True(loginWithNew.IsSuccess);

        Assert.All(fixture.RefreshTokens.Tokens, t => Assert.NotNull(t.RevokedAt));
    }

    private static string ExtractCodeFromLastEmail(FakeEmailSender sender)
    {
        // FakeAppLinkBuilder embeds the raw code in the reset link as "code={code}" - a much more
        // specific anchor than scanning the whole HTML body for six digits (which could just as
        // easily match a date or another number in the template copy).
        var body = sender.Bodies[^1];
        var match = System.Text.RegularExpressions.Regex.Match(body, "code=(\\d{6})");
        Assert.True(match.Success, "Expected the reset link with an embedded 6-digit code in the e-mail body.");
        return match.Groups[1].Value;
    }
}
