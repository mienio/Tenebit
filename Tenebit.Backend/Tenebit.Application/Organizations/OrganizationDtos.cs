namespace Tenebit.Application.Organizations;

public sealed record OrganizationResponse(Guid Id, string Name, string Country, string Language, string Currency, string TimeZone, string? LogoUrl);
[ValidatedRequest]
public sealed record UpdateOrganizationRequest(string Name, string Country, string Language, string Currency, string TimeZone, string? LogoUrl);
