namespace Tenebit.Application.Licenses;

public sealed record LicenseSeatResponse(Guid PersonId, string PersonName, DateTimeOffset AssignedAt);

public sealed record LicenseResponse(
    Guid Id,
    string Name,
    string? Vendor,
    string? LicenseKey,
    bool HasLicenseKey,
    bool CanViewLicenseKey,
    int SeatsTotal,
    int SeatsAssigned,
    DateOnly? ExpiresAt,
    string? Notes,
    IReadOnlyList<LicenseSeatResponse> Seats);

[ValidatedRequest]
public sealed record CreateLicenseRequest(string Name, string? Vendor, string? LicenseKey, int SeatsTotal, DateOnly? ExpiresAt, string? Notes);
[ValidatedRequest]
public sealed record UpdateLicenseRequest(string Name, string? Vendor, string? LicenseKey, int SeatsTotal, DateOnly? ExpiresAt, string? Notes);
[ValidatedRequest]
public sealed record AssignLicenseSeatRequest(Guid PersonId);
