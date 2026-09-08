using Tenebit.Application.Affiliates;
using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AffiliateCodeServiceTests
{
    private sealed record Fixture(
        AffiliateCodeService Service, InMemoryAffiliateRepository Affiliates, InMemoryAffiliateCodeRepository Codes,
        InMemoryPromoCodeRepository PromoCodes, InMemoryAffiliateProgramSettingsRepository Settings, FakeClock Clock);

    private static (Fixture Fixture, Affiliate Affiliate) CreateFixture(int? maxActiveCodesOverride = null)
    {
        var affiliates = new InMemoryAffiliateRepository();
        var codes = new InMemoryAffiliateCodeRepository();
        var promoCodes = new InMemoryPromoCodeRepository();
        var settings = new InMemoryAffiliateProgramSettingsRepository();
        var clock = new FakeClock();
        var service = new AffiliateCodeService(codes, affiliates, promoCodes, settings, new FakeUnitOfWork(), clock);

        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", clock.UtcNow);
        if (maxActiveCodesOverride.HasValue) affiliate.OverrideMaxActiveCodes(maxActiveCodesOverride.Value);
        affiliates.Add(affiliate);

        return (new Fixture(service, affiliates, codes, promoCodes, settings, clock), affiliate);
    }

    [Fact]
    public async Task Creating_a_code_beyond_the_active_limit_is_rejected()
    {
        var (fixture, affiliate) = CreateFixture(maxActiveCodesOverride: 1);
        var first = await fixture.Service.CreateAsync(affiliate.Id, "DAMIAN20", null, CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await fixture.Service.CreateAsync(affiliate.Id, "DAMIAN21", null, CancellationToken.None);
        Assert.True(second.IsFailure);
        Assert.Equal(400, second.Error!.StatusCode);
    }

    [Fact]
    public async Task Deactivating_a_code_frees_a_slot_for_a_new_one()
    {
        var (fixture, affiliate) = CreateFixture(maxActiveCodesOverride: 1);
        var first = await fixture.Service.CreateAsync(affiliate.Id, "DAMIAN20", null, CancellationToken.None);
        await fixture.Service.SetActiveAsync(affiliate.Id, first.Value!.Id, false, CancellationToken.None);

        var second = await fixture.Service.CreateAsync(affiliate.Id, "DAMIAN21", null, CancellationToken.None);
        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task An_explicit_code_colliding_with_an_existing_promo_code_is_rejected()
    {
        var (fixture, affiliate) = CreateFixture();
        fixture.PromoCodes.Add(new PromoCode("SHARED10", "starter", PromoDiscountType.Percentage, 10, null, null, PromoDurationType.Once, null, null, fixture.Clock.UtcNow));

        var result = await fixture.Service.CreateAsync(affiliate.Id, "SHARED10", null, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task An_explicit_code_colliding_with_another_affiliates_code_is_rejected()
    {
        var (fixture, affiliate) = CreateFixture();
        fixture.Codes.Add(new AffiliateCode(Guid.NewGuid(), "TAKEN20", null, fixture.Clock.UtcNow));

        var result = await fixture.Service.CreateAsync(affiliate.Id, "TAKEN20", null, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Availability_check_reports_false_for_a_reserved_word()
    {
        var (fixture, _) = CreateFixture();
        Assert.False(await fixture.Service.IsAvailableAsync("ADMIN2026", CancellationToken.None));
    }

    [Fact]
    public async Task Auto_generated_code_is_derived_from_the_affiliates_last_name_and_is_unique()
    {
        var (fixture, affiliate) = CreateFixture(maxActiveCodesOverride: 10);
        var result = await fixture.Service.CreateAsync(affiliate.Id, null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("KOWALSKI-", result.Value!.Code);
    }

    [Fact]
    public async Task An_explicit_code_under_the_seven_character_minimum_is_rejected()
    {
        var (fixture, affiliate) = CreateFixture();
        var result = await fixture.Service.CreateAsync(affiliate.Id, "AB12CD", null, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Auto_generated_code_clears_the_seven_character_minimum_even_for_a_one_letter_last_name()
    {
        var affiliates = new InMemoryAffiliateRepository();
        var codes = new InMemoryAffiliateCodeRepository();
        var promoCodes = new InMemoryPromoCodeRepository();
        var settings = new InMemoryAffiliateProgramSettingsRepository();
        var clock = new FakeClock();
        var service = new AffiliateCodeService(codes, affiliates, promoCodes, settings, new FakeUnitOfWork(), clock);
        var affiliate = new Affiliate("x@example.com", "hash", "X", "X", null, clock.UtcNow);
        affiliate.OverrideMaxActiveCodes(10);
        affiliates.Add(affiliate);

        var result = await service.CreateAsync(affiliate.Id, null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Code.Length >= 7, $"expected at least 7 characters, got '{result.Value.Code}'");
    }
}
