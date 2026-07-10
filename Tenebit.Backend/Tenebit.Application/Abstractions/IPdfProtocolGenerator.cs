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

public interface IPdfProtocolGenerator
{
    byte[] GenerateHandoverProtocol(ProtocolPdfModel model);
}
