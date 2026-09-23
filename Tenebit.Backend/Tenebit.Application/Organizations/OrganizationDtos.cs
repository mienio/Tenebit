namespace Tenebit.Application.Organizations;

public sealed record OrganizationResponse(
    Guid Id, string Name, string Country, string Language, string Currency, string TimeZone, string? LogoUrl,
    // Dane nabywcy na fakturę Paddle (patrz Organization.UpdateBillingDetails). Opcjonalne: organizacja na
    // planie Free nigdy ich nie potrzebuje, a checkout sam mówi, czego brakuje.
    string? BillingCompanyName = null,
    string? TaxId = null,
    string? BillingAddressLine1 = null,
    string? BillingAddressLine2 = null,
    string? BillingCity = null,
    string? BillingPostalCode = null,
    string? BillingCountry = null,
    /// <summary>Czy dane wystarczają, by Paddle wystawił fakturę na firmę - liczone po stronie domeny, żeby
    /// ostrzeżenie w podglądzie faktury i walidacja przy checkoucie nie mogły się rozjechać.</summary>
    bool HasCompleteBillingDetails = false);

[ValidatedRequest]
public sealed record UpdateOrganizationRequest(
    string Name, string Country, string Language, string Currency, string TimeZone, string? LogoUrl,
    string? BillingCompanyName = null,
    string? TaxId = null,
    string? BillingAddressLine1 = null,
    string? BillingAddressLine2 = null,
    string? BillingCity = null,
    string? BillingPostalCode = null,
    string? BillingCountry = null);
