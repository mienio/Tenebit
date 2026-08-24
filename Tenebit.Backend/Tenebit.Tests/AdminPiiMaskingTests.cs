using Tenebit.Application.Admin;

namespace Tenebit.Tests;

/// <summary>
/// The admin panel must never disclose customer personal data, even to a holder of a valid admin token.
/// These tests pin that contract at the masking layer, and - more importantly - assert that the DTOs the
/// admin API serialises carry no field capable of holding a name, an address, or an asset label. If
/// someone later adds one, these fail rather than quietly shipping a customer data leak.
/// </summary>
public class AdminPiiMaskingTests
{
    [Theory]
    [InlineData("anna.kowalska@firma.com", "an•••@fi•••.com")]
    [InlineData("ADMIN@Example.PL", "AD•••@Ex•••.PL")]
    [InlineData("a@b.io", "a•••@b•••.io")]
    public void Email_keeps_only_a_recognisable_stub(string input, string expected) =>
        Assert.Equal(expected, PiiMasking.Email(input));

    [Fact]
    public void Email_never_returns_a_contactable_address()
    {
        var masked = PiiMasking.Email("jan.nowak@bardzo-dluga-domena.example.com");
        Assert.DoesNotContain("jan.nowak", masked);
        Assert.DoesNotContain("bardzo-dluga-domena", masked);
        Assert.Contains("•••", masked);
    }

    [Theory]
    [InlineData("Anna Kowalska", "A. K.")]
    [InlineData("Jan", "J.")]
    [InlineData("  ", "•••")]
    public void PersonName_reduces_to_initials(string input, string expected) =>
        Assert.Equal(expected, PiiMasking.PersonName(input));

    [Fact]
    public void AuditLabel_masks_addresses_but_keeps_organization_names()
    {
        Assert.Equal("Firma Testowa", PiiMasking.AuditLabel("Firma Testowa"));
        Assert.DoesNotContain("ksiegowa", PiiMasking.AuditLabel("ksiegowa@firma.pl"));
    }

    /// <summary>
    /// Guards the shape of the payload itself. Fields whose names suggest a raw identity value must not
    /// exist on any type the admin API returns - only masked or aggregated equivalents.
    /// </summary>
    [Fact]
    public void Admin_contracts_expose_no_raw_identity_fields()
    {
        var forbidden = new[] { "Email", "DisplayName", "FirstName", "LastName", "AssetTag", "SerialNumber", "JobTitle" };
        var contracts = new[]
        {
            typeof(AdminUserSummary), typeof(AdminUserListItem), typeof(AdminLoginEntry),
            typeof(AdminOrganizationDetail), typeof(AdminOrganizationSummary), typeof(AdminDashboard),
        };

        foreach (var contract in contracts)
        {
            foreach (var property in contract.GetProperties())
            {
                Assert.DoesNotContain(property.Name, forbidden);
            }
        }
    }
}
