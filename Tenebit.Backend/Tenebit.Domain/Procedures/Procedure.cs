using Tenebit.Domain.Common;

namespace Tenebit.Domain.Procedures;

public sealed class Procedure
{
    private Procedure() { }

    public Procedure(Guid organizationId, string title, string version, string owner, bool requiresAcceptance)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Status = ProcedureStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(title, version, owner, null, null, requiresAcceptance);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Version { get; private set; } = "1.0";
    public string Owner { get; private set; } = string.Empty;
    public ProcedureStatus Status { get; private set; }
    public string? AppliesTo { get; private set; }
    public DateOnly? ReviewDate { get; private set; }
    public bool RequiresAcceptance { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public List<ProcedureDocument> Documents { get; private set; } = [];

    public void Update(string title, string version, string owner, string? appliesTo, DateOnly? reviewDate, bool requiresAcceptance)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Tytuł procedury jest wymagany.");
        if (string.IsNullOrWhiteSpace(version)) throw new DomainException("Wersja procedury jest wymagana.");

        Title = title.Trim();
        Version = version.Trim();
        Owner = string.IsNullOrWhiteSpace(owner) ? "Operations" : owner.Trim();
        AppliesTo = string.IsNullOrWhiteSpace(appliesTo) ? null : appliesTo.Trim();
        ReviewDate = reviewDate;
        RequiresAcceptance = requiresAcceptance;
    }

    public ProcedureDocument AttachDocument(string fileName, string contentType, byte[] content, string uploadedBy, DateTimeOffset uploadedAt)
    {
        var document = new ProcedureDocument(OrganizationId, Id, fileName, contentType, content, uploadedBy, uploadedAt);
        Documents.Add(document);
        return document;
    }

    public ProcedureDocument RemoveDocument(Guid documentId)
    {
        var document = Documents.FirstOrDefault(x => x.Id == documentId);
        if (document is null) throw new DomainException("Plik procedury nie istnieje.");
        Documents.Remove(document);
        return document;
    }

    public void Publish(DateTimeOffset publishedAt)
    {
        if (!Documents.Any()) throw new DomainException("Nie można opublikować procedury bez pliku.");
        Status = ProcedureStatus.Published;
        PublishedAt = publishedAt;
    }

    public void Archive() => Status = ProcedureStatus.Archived;
}
