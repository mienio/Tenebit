using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

/// <summary>
/// The actual bank transfer, confirmed by the admin after moving the money by hand on Revolut - never
/// automated (spec §7: no payout-gateway integration in this program, on purpose). One payout can
/// cover several closed periods at once (e.g. a month the owner missed gets rolled into the next), so
/// <see cref="CoveredPeriodIds"/> is a list, not a single foreign key. Confirming this is a deliberate,
/// irreversible financial declaration, recorded with a timestamp and (via the caller) an
/// <see cref="Tenebit.Domain.Identity.AdminAuditLog"/> entry - the same "what, when, from which IP"
/// guarantee every other admin action gets. There is only ever one platform-admin account in this
/// system (see AdminAccountOptions), so - like AdminAuditLog itself - this type does not track a
/// per-admin actor id; "who" is implicit.
/// </summary>
public sealed class AffiliatePayout
{
    private AffiliatePayout() { }

    public AffiliatePayout(Guid affiliateId, decimal amount, string currency, IReadOnlyCollection<Guid> coveredPeriodIds, string? paymentReference, string? note, DateTimeOffset now)
    {
        if (amount <= 0) throw new DomainException("Kwota wypłaty musi być większa od zera.");
        if (coveredPeriodIds is null || coveredPeriodIds.Count == 0)
            throw new DomainException("Wypłata musi obejmować co najmniej jeden okres rozliczeniowy.");

        Id = Guid.NewGuid();
        AffiliateId = affiliateId;
        Amount = amount;
        Currency = currency;
        CoveredPeriodIds = coveredPeriodIds.ToList();
        PaymentReference = string.IsNullOrWhiteSpace(paymentReference) ? null : paymentReference.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        MarkedPaidAt = now;
    }

    public Guid Id { get; private set; }
    public Guid AffiliateId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public List<Guid> CoveredPeriodIds { get; private set; } = [];
    public DateTimeOffset MarkedPaidAt { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? Note { get; private set; }
}
