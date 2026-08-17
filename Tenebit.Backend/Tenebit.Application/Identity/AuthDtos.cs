using System.ComponentModel.DataAnnotations;

namespace Tenebit.Application.Identity;

// AUD-007: [Required] łapie null/pusty string przed warstwą Application/Infrastructure — np.
// OrganizationUserRepository.FindByEmailAsync robił email.Trim() na null i kończył się 500 (potwierdzone
// w logach audytu). ValidationEndpointFilter waliduje każdy request DTO z atrybutami przed handlerem.
public sealed record RegisterRequest(
    [property: Required, StringLength(160, MinimumLength = 1)] string OrganizationName,
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required] string Password,
    [property: Required, StringLength(160, MinimumLength = 1)] string DisplayName,
    [property: Required, StringLength(8, MinimumLength = 1)] string Currency,
    string? Language = null);

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(240)] string Email,
    [property: Required] string Password);

public sealed record AuthUserResponse(Guid Id, Guid OrganizationId, string OrganizationName, string Email, string DisplayName, IReadOnlyList<string> Roles, bool IsEmailVerified, bool IsTwoFactorEnabled);

public sealed record ExternalUserInfo(string Provider, string ProviderUserId, string? Email, bool EmailVerified, string? DisplayName);

public sealed record ForgotPasswordRequest([property: Required, EmailAddress, StringLength(240)] string Email);

public sealed record ResetPasswordRequest([property: Required] string Token, [property: Required] string NewPassword);

public sealed record VerifyEmailRequest([property: Required] string Token);

public sealed record RefreshResult(AuthUserResponse User, string RefreshToken);

public sealed record LoginOutcome(bool RequiresTwoFactor, Guid? PendingUserId, AuthUserResponse? User);

public sealed record TwoFactorLoginRequest([property: Required] string ChallengeToken, [property: Required] string Code, bool RememberDevice = false);

public sealed record TwoFactorSetupResponse(string Secret, string OtpAuthUri, string QrSvg);

public sealed record TwoFactorCodeRequest([property: Required] string Code);

public sealed record TwoFactorEnableResponse(IReadOnlyList<string> RecoveryCodes);

public sealed record TwoFactorRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes, int RemainingUnused);
