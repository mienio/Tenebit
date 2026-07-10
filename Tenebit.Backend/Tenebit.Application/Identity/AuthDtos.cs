namespace Tenebit.Application.Identity;

public sealed record RegisterRequest(string OrganizationName, string Email, string Password, string DisplayName, string Currency, string? Language = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthUserResponse(Guid Id, Guid OrganizationId, string OrganizationName, string Email, string DisplayName, IReadOnlyList<string> Roles, bool IsEmailVerified, bool IsTwoFactorEnabled);

public sealed record ExternalUserInfo(string Provider, string ProviderUserId, string? Email, bool EmailVerified, string? DisplayName);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record VerifyEmailRequest(string Token);

public sealed record RefreshResult(AuthUserResponse User, string RefreshToken);

public sealed record LoginOutcome(bool RequiresTwoFactor, Guid? PendingUserId, AuthUserResponse? User);

public sealed record TwoFactorLoginRequest(string ChallengeToken, string Code, bool RememberDevice = false);

public sealed record TwoFactorSetupResponse(string Secret, string OtpAuthUri, string QrSvg);

public sealed record TwoFactorCodeRequest(string Code);
