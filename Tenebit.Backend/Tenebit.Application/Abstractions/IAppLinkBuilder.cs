namespace Tenebit.Application.Abstractions;

public interface IAppLinkBuilder
{
    string BuildAssignmentAcceptanceLink(string rawToken);
    string BuildAssetScanLink(Guid organizationId, Guid assetId);
    string BuildPasswordResetLink(string email, string code);
    string BuildEmailVerificationLink(string email, string code);
    string BuildOffboardingLink(string rawToken);
    string BuildAssetAuditLink(string rawToken);

    // Builds an absolute app URL from a server-trusted relative path used by redirects such as Stripe.
    string BuildAppUrl(string relativePath);
}
