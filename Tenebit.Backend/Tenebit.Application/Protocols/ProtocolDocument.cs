namespace Tenebit.Application.Protocols;

public enum ProtocolKind
{
    /// <summary>Wydanie sprzętu pracownikowi.</summary>
    Handover,

    /// <summary>Zwrot sprzętu przy zakończeniu zatrudnienia.</summary>
    Return
}

public sealed record ProtocolLine(
    string Name,
    string? AssetTag,
    string? SerialNumber,
    string? Condition,
    decimal? Value,
    string? Currency,
    string? Status);

public sealed record ProtocolParty(
    string FullName,
    string? EmployeeNumber,
    string? JobTitle,
    string? Email);

/// <summary>
/// Wszystko, co trafia na protokół zdawczo-odbiorczy, w formie niezależnej od źródła (wydanie albo
/// sprawa offboardingowa) i od biblioteki renderującej.
/// </summary>
public sealed record ProtocolDocument(
    ProtocolKind Kind,
    string OrganizationName,
    string ProtocolNumber,
    ProtocolParty Person,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmationHash,
    IReadOnlyList<ProtocolLine> Lines,
    IReadOnlyList<string> Procedures,
    string? Notes,
    ProtocolLabels Labels);

public sealed record ProtocolFile(byte[] Content, string FileName)
{
    public string ContentType => "application/pdf";
}
