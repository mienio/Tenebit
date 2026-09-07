using Tenebit.Application.Affiliates;

namespace Tenebit.Tests;

/// <summary>
/// Regression guard for spec §6.3/§12.2: whatever `/api/partner/conversions` returns must never carry
/// a field that could identify the buyer, even if a future change adds one without thinking it
/// through. Reflection over the property names, not a copy of today's list, so a newly added field is
/// caught by name/shape rather than by trusting this test to have been updated in lockstep.
/// </summary>
public class AffiliateConversionSummaryDtoTests
{
    private static readonly string[] ForbiddenNameFragments =
    [
        "organization", "customer", "buyer", "client", "email", "company", "name", "address", "nip", "taxid", "phone"
    ];

    [Fact]
    public void The_affiliate_facing_conversion_dto_has_no_field_that_could_identify_the_buyer()
    {
        var properties = typeof(AffiliateConversionSummaryDto).GetProperties();
        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var lowerName = property.Name.ToLowerInvariant();
            Assert.False(
                ForbiddenNameFragments.Any(lowerName.Contains),
                $"AffiliateConversionSummaryDto.{property.Name} looks like it could leak buyer-identifying data - it must never reach the affiliate-facing API.");
        }
    }

    [Fact]
    public void The_affiliate_facing_conversion_dto_has_no_organization_id_field_at_all()
    {
        // Distinct from the name-fragment scan above: explicitly assert the exact field the admin DTO
        // (AffiliateAdminConversionItem) legitimately carries is physically absent here.
        var property = typeof(AffiliateConversionSummaryDto).GetProperty("OrganizationId");
        Assert.Null(property);
    }
}
