using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tenebit.Application.Identity;

// AUD-007: [Required] catches null/empty input before Application/Infrastructure code receives it.
[ValidatedRequest]
public sealed record RegisterRequest(
    [property: Required, StringLength(160, MinimumLength = 1)] string OrganizationName,
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required] string Password,
    [property: Required, StringLength(160, MinimumLength = 1)] string DisplayName,
    [property: Required, StringLength(8, MinimumLength = 1)] string Currency,
    string? Language = null,
    bool AcceptTerms = false);

/// <summary>
/// Transport details of the sign-in attempt, captured by the endpoint and recorded in the login history.
/// Not part of the request body - a client must never be able to dictate the IP attributed to it.
/// </summary>
public sealed record AuthRequestContext(string? IpAddress, string? UserAgent);

[ValidatedRequest]
public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required] string Password);

public sealed record AuthUserResponse(Guid Id, Guid OrganizationId, string OrganizationName, string Email, string DisplayName, IReadOnlyList<string> Roles, bool IsEmailVerified, bool IsTwoFactorEnabled, [property: JsonIgnore] Guid SecurityStamp, Guid? PersonId = null);

public sealed record ExternalUserInfo(string Provider, string ProviderUserId, string? Email, bool EmailVerified, string? DisplayName);

[ValidatedRequest]
public sealed record ForgotPasswordRequest([property: Required, EmailAddress, StringLength(240)] string Email);

[ValidatedRequest]
public sealed record ResetPasswordRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required, RegularExpression(@"^\d{6}$")] string Code,
    [property: Required] string NewPassword);

[ValidatedRequest]
public sealed record VerifyEmailRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required, RegularExpression(@"^\d{6}$")] string Code);

[ValidatedRequest]
public sealed record ResendVerificationRequest([property: Required, EmailAddress, StringLength(240)] string Email);

public sealed record RefreshResult(AuthUserResponse User, string RefreshToken);

public sealed record LoginOutcome(bool RequiresTwoFactor, Guid? PendingUserId, AuthUserResponse? User);

[ValidatedRequest]
public sealed record TwoFactorLoginRequest([property: Required] string ChallengeToken, [property: Required] string Code, bool RememberDevice = false);

public sealed record TwoFactorSetupResponse(string Secret, string OtpAuthUri, string QrSvg);

[ValidatedRequest]
public sealed record TwoFactorCodeRequest([property: Required] string Code);

[ValidatedRequest]
public sealed record UpdateDisplayNameRequest([property: Required, StringLength(160, MinimumLength = 1)] string DisplayName);

public sealed record TwoFactorEnableResponse(IReadOnlyList<string> RecoveryCodes, AuthUserResponse User);

public sealed record TwoFactorRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes, int RemainingUnused);
