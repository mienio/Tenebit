namespace Tenebit.Application.Abstractions;

public interface IAppLinkBuilder
{
    string BuildAssignmentAcceptanceLink(string rawToken);
    string BuildAssetScanLink(Guid organizationId, Guid assetId);
    string BuildPasswordResetLink(string rawToken);
    string BuildEmailVerificationLink(string rawToken);
    string BuildOffboardingLink(string rawToken);
    string BuildAssetAuditLink(string rawToken);

    // Builds an absolute app URL from a server-trusted relative path — used where a redirect target
    // (e.g. Stripe checkout success/cancel) must be constructed from App:PublicUrl, never accepted
    // as a full URL from the client (audyt AUD3-010: open redirect przez zaufaną ścieżkę płatności).
    string BuildAppUrl(string relativePath);
}
