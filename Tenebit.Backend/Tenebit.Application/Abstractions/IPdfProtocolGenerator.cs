namespace Tenebit.Application.Abstractions;

public sealed record ProtocolPdfAssetRow(string Name, string AssetTag, string? SerialNumber, string IssueCondition, string? ReturnCondition);

public sealed record ProtocolPdfModel(
    string OrganizationName,
    string? OrganizationLogoUrl,
    string OrganizationCountry,
    string ProtocolNumber,
    DateTimeOffset IssuedAt,
    DateOnly? DueDate,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? ReturnedAt,
    string PersonFullName,
    string? PersonJobTitle,
    string? TeamName,
    IReadOnlyList<ProtocolPdfAssetRow> Assets,
    IReadOnlyList<string> ProcedureTitlesRequiringAcceptance,
    string? Notes);

public sealed record OffboardingProtocolAssetRow(string Name, string AssetTag, string Status, string? ReturnCondition, string? CompletedBy, DateTimeOffset? CompletedAt);

public sealed record OffboardingProtocolLicenseRow(string LicenseName, DateTimeOffset? ReleasedAt, string? ReleasedBy);

public sealed record OffboardingProtocolExceptionRow(string ItemLabel, string Status, string? ResolutionNotes, string ResolvedBy, DateTimeOffset? ResolvedAt);

public sealed record OffboardingProtocolPhoto(string FileName, string ContentType, byte[] Content, string Sha256);

public sealed record OffboardingProtocolPdfModel(
    string OrganizationName,
    string? OrganizationLogoUrl,
    string OrganizationCountry,
    string ProtocolNumber,
    string PersonFullName,
    DateTimeOffset? StartedAt,
    DateTimeOffset ReturnDueDate,
    DateTimeOffset CompletedAt,
    IReadOnlyList<OffboardingProtocolAssetRow> Assets,
    IReadOnlyList<OffboardingProtocolLicenseRow> ReleasedLicenses,
    IReadOnlyList<OffboardingProtocolExceptionRow> Exceptions,
    IReadOnlyList<OffboardingProtocolPhoto> Photos,
    string FinalOutcome,
    string? Notes);

public interface IPdfProtocolGenerator
{
    byte[] GenerateHandoverProtocol(ProtocolPdfModel model);
    byte[] GenerateOffboardingProtocol(OffboardingProtocolPdfModel model);
}
