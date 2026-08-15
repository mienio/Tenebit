using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

public sealed class ServiceTicket
{
    private ServiceTicket() { }

    public ServiceTicket(Guid organizationId, Guid assetId, string vendor, string? description, Guid? assetInspectionId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AssetId = assetId;
        AssetInspectionId = assetInspectionId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        OpenedAt = CreatedAt;
        Status = ServiceTicketStatus.Open;
        UpdateDetails(vendor, description, null, null, null);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid? AssetInspectionId { get; private set; }
    public string Vendor { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal? EstimatedCost { get; private set; }
    public decimal? ActualCost { get; private set; }
    public string? Currency { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? SlaDueAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public ServiceTicketStatus Status { get; private set; }
    public string? Resolution { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool IsClosed => Status is ServiceTicketStatus.Completed or ServiceTicketStatus.Cancelled;

    public void UpdateDetails(string vendor, string? description, decimal? estimatedCost, string? currency, DateTimeOffset? slaDueAt)
    {
        if (string.IsNullOrWhiteSpace(vendor))
        {
            throw new DomainException("Vendor zgłoszenia serwisowego jest wymagany.");
        }

        if (estimatedCost is < 0)
        {
            throw new DomainException("Szacowany koszt nie może być ujemny.");
        }

        if (IsClosed)
        {
            throw new DomainException("Zamkniętego zgłoszenia serwisowego nie można edytować.");
        }

        Vendor = vendor.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        EstimatedCost = estimatedCost;
        Currency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
        SlaDueAt = slaDueAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(ServiceTicketStatus status)
    {
        if (status is ServiceTicketStatus.Completed or ServiceTicketStatus.Cancelled)
        {
            throw new DomainException("Przejście do statusu Completed lub Cancelled wymaga metody Complete() lub Cancel().");
        }

        if (Status is ServiceTicketStatus.Completed or ServiceTicketStatus.Cancelled)
        {
            throw new DomainException("Nie można zmienić statusu zamkniętego zgłoszenia serwisowego.");
        }

        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Complete(decimal? actualCost, string? resolution)
    {
        if (IsClosed)
        {
            throw new DomainException("Zgłoszenie serwisowe zostało już zamknięte.");
        }

        if (actualCost is < 0)
        {
            throw new DomainException("Koszt końcowy nie może być ujemny.");
        }

        Status = ServiceTicketStatus.Completed;
        ActualCost = actualCost;
        Resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();
        ClosedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ClosedAt.Value;
    }

    public void Cancel(string? resolution)
    {
        if (IsClosed)
        {
            throw new DomainException("Zgłoszenie serwisowe zostało już zamknięte.");
        }

        Status = ServiceTicketStatus.Cancelled;
        Resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();
        ClosedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ClosedAt.Value;
    }
}
