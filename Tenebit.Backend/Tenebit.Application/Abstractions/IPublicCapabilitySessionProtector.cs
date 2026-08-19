namespace Tenebit.Application.Abstractions;

public interface IPublicCapabilitySessionProtector
{
    string Protect(string purpose, string rawToken, DateTimeOffset expiresAt);
    string? Unprotect(string protectedSession, string expectedPurpose, DateTimeOffset now);
}
