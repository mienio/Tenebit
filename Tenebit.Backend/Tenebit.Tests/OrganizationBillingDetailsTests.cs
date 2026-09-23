using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Subscriptions;
using Tenebit.Domain.Common;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

/// <summary>Dane nabywcy na fakturę: normalizacja numeru VAT i to, że faktycznie docierają do Paddle -
/// bez tego kupujący na firmę nie ma gdzie podać NIP-u, a faktura wychodzi na osobę prywatną.</summary>
public class OrganizationBillingDetailsTests
{
    private static Organization CreateOrganization() => new("Acme sp. z o.o.", "PL", "pl", "PLN", "Europe/Warsaw");

    [Theory]
    // Firmy przepisują NIP z pieczątki - ze spacjami, myślnikami i bez prefiksu kraju. Paddle przyjmuje
    // tylko jedną z tych postaci, więc wszystkie muszą sprowadzić się do niej.
    [InlineData("123-456-32-18", "PL", "PL1234563218")]
    [InlineData("123 456 32 18", "PL", "PL1234563218")]
    [InlineData("pl1234563218", "PL", "PL1234563218")]
    [InlineData("PL1234563218", "PL", "PL1234563218")]
    // Grecja podaje numer z prefiksem EL, choć kod kraju to GR.
    [InlineData("040127797", "GR", "EL040127797")]
    // Kraj spoza UE nie ma prefiksu - numer zostaje taki, jaki wpisano.
    [InlineData("12-3456789", "US", "123456789")]
    public void NormalizeTaxId_NormalizesWhatACompanyActuallyTypes(string input, string country, string expected)
    {
        Assert.Equal(expected, Organization.NormalizeTaxId(input, country));
    }

    [Fact]
    public void NormalizeTaxId_TreatsBlankAsNoNumber()
    {
        Assert.Null(Organization.NormalizeTaxId("   ", "PL"));
        Assert.Null(Organization.NormalizeTaxId(null, "PL"));
    }

    [Theory]
    [InlineData("12")]
    [InlineData("PL12345678901234567890123")]
    public void NormalizeTaxId_RejectsAnImpossibleLength(string input)
    {
        Assert.Throws<DomainException>(() => Organization.NormalizeTaxId(input, "US"));
    }

    [Fact]
    public void UpdateBillingDetails_KeepsTheNormalizedNumberAndTheAddress()
    {
        var organization = CreateOrganization();

        organization.UpdateBillingDetails("Acme Sp. z o.o.", "123-456-32-18", "Główna 1", "lok. 4", "Warszawa", "00-001", "pl");

        Assert.Equal("PL1234563218", organization.TaxId);
        Assert.Equal("Acme Sp. z o.o.", organization.BillingCompanyName);
        Assert.Equal("PL", organization.BillingCountry);
        Assert.True(organization.HasCompleteBillingDetails);
    }

    [Fact]
    public void UpdateBillingDetails_FallsBackToTheOrganizationsOwnNameAndCountry()
    {
        var organization = CreateOrganization();

        organization.UpdateBillingDetails(null, null, null, null, null, null, null);

        Assert.Equal("Acme sp. z o.o.", organization.InvoiceName);
        Assert.Equal("PL", organization.InvoiceCountry);
        Assert.False(organization.HasCompleteBillingDetails);
    }

    [Fact]
    public void UpdateBillingDetails_RejectsACountryThatIsNotAnIsoCode()
    {
        var organization = CreateOrganization();

        Assert.Throws<DomainException>(() => organization.UpdateBillingDetails(null, null, null, null, null, null, "Polska"));
    }

    private static SubscriptionService CreateService(out FakePaymentGateway paymentGateway, out InMemoryOrganizationRepository organizations, out FakeCurrentUser currentUser, out InMemorySubscriptionRepository subscriptions)
    {
        paymentGateway = new FakePaymentGateway();
        organizations = new InMemoryOrganizationRepository();
        currentUser = new FakeCurrentUser();
        subscriptions = new InMemorySubscriptionRepository();
        return new SubscriptionService(
            subscriptions, new InMemoryProcessedPaddleEventRepository(), new InMemoryAssetRepository(),
            new InMemoryActivityLogRepository(), currentUser, new FakeClock(), new FakeUnitOfWork(), paymentGateway,
            new FakeAppLinkBuilder(), new InMemoryPromoCodeRepository(), organizations, new InMemoryOrganizationUserRepository(),
            new FakeEmailSender(), NullLogger<SubscriptionService>.Instance);
    }

    private static Organization SeedOrganization(InMemoryOrganizationRepository organizations, FakeCurrentUser currentUser)
    {
        var organization = Organization.CreateSeed(currentUser.OrganizationId, "Acme sp. z o.o.", "PL", "pl", "PLN", "Europe/Warsaw");
        organizations.Add(organization);
        return organization;
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_SendsTheInvoiceDetailsToPaddleAndReturnsItsObjects()
    {
        var service = CreateService(out var paymentGateway, out var organizations, out var currentUser, out _);
        var organization = SeedOrganization(organizations, currentUser);
        organization.UpdateBillingDetails("Acme Sp. z o.o.", "1234563218", "Główna 1", null, "Warszawa", "00-001", "PL");

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, BillingInterval.Monthly, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PL1234563218", paymentGateway.LastBillingProfile!.TaxId);
        Assert.Equal("Acme Sp. z o.o.", paymentGateway.LastBillingProfile.CompanyName);
        Assert.Equal("PL", paymentGateway.LastBillingProfile.CountryCode);
        Assert.Equal("add_fake", result.Value!.AddressId);
        Assert.Equal("biz_fake", result.Value.BusinessId);
    }

    // Odrzucony przez Paddle numer VAT nie może zablokować zapłaty - checkout ma się otworzyć bez
    // podstawionych danych, a nie wcale.
    [Fact]
    public async Task GetCheckoutParamsAsync_StillOpensTheCheckoutWhenPaddleRejectsTheInvoiceDetails()
    {
        var service = CreateService(out var paymentGateway, out var organizations, out var currentUser, out _);
        SeedOrganization(organizations, currentUser);
        paymentGateway.ThrowOnSyncCustomerBilling = new PaymentGatewayException("Paddle API error 400", 400);

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, BillingInterval.Monthly, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.AddressId);
        Assert.Null(result.Value.BusinessId);
    }

    // Zmiana planu na wyższy fakturowana jest od razu, więc NIP poprawiony po pierwszym zakupie musi
    // dotrzeć do Paddle jeszcze przed obciążeniem - inaczej podgląd faktury obiecuje co innego, niż
    // Paddle wydrukuje.
    [Fact]
    public async Task ChangePlanAsync_RefreshesTheInvoiceDetailsBeforeChargingForTheUpgrade()
    {
        var service = CreateService(out var paymentGateway, out var organizations, out var currentUser, out var subscriptions);
        var organization = SeedOrganization(organizations, currentUser);
        organization.UpdateBillingDetails(null, "1234563218", "Główna 1", null, "Warszawa", "00-001", "PL");
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(currentUser.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, BillingInterval.Monthly, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), currentUser.OrganizationId);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, BillingInterval.Monthly, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ctm_1", paymentGateway.LastBillingCustomerId);
        Assert.Equal("PL1234563218", paymentGateway.LastBillingProfile!.TaxId);
    }

    [Fact]
    public async Task ListInvoicesAsync_ReturnsWhatPaddleIssued()
    {
        var service = CreateService(out var paymentGateway, out var organizations, out var currentUser, out var subscriptions);
        SeedOrganization(organizations, currentUser);
        var subscription = new OrganizationSubscription(currentUser.OrganizationId, SubscriptionPlan.Business.Key);
        subscription.AttachPaddleCustomer("ctm_1");
        subscriptions.Add(subscription);
        paymentGateway.NextInvoices =
        [
            new PaymentInvoice("txn_1", "INV-001", 24.60m, 24.60m, "EUR", "completed", DateTimeOffset.UtcNow, null, "https://paddle.test/invoice.pdf")
        ];

        var result = await service.ListInvoicesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value!);
        Assert.Equal("INV-001", invoice.Number);
        Assert.Equal("https://paddle.test/invoice.pdf", invoice.PdfUrl);
    }

    [Fact]
    public async Task ListInvoicesAsync_IsEmptyBeforeTheFirstPurchase()
    {
        var service = CreateService(out _, out var organizations, out var currentUser, out _);
        SeedOrganization(organizations, currentUser);

        var result = await service.ListInvoicesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListInvoicesAsync_IsOwnerOnly()
    {
        var service = CreateService(out _, out var organizations, out var currentUser, out _);
        SeedOrganization(organizations, currentUser);
        currentUser.Roles = ["finance"];

        var result = await service.ListInvoicesAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
