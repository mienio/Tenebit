namespace Tenebit.Application.Abstractions;

public interface IAppLinkBuilder
{
    string BuildAssignmentAcceptanceLink(string rawToken);
    string BuildAssetScanLink(Guid organizationId, Guid assetId);
    string BuildPasswordResetLink(string rawToken);
    string BuildEmailVerificationLink(string rawToken);
    string BuildOffboardingLink(string rawToken);
    string BuildAssetAuditLink(string rawToken);
}
