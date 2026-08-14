namespace Tenebit.Application.Abstractions;

public interface IAppLinkBuilder
{
    string BuildAssignmentAcceptanceLink(Guid organizationId, Guid assignmentId);
    string BuildAssetScanLink(Guid organizationId, Guid assetId);
    string BuildPasswordResetLink(string rawToken);
    string BuildEmailVerificationLink(string rawToken);
    string BuildOffboardingLink(string rawToken);
}
