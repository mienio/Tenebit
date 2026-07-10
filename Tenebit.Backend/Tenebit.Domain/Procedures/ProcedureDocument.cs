using Tenebit.Domain.Common;

namespace Tenebit.Domain.Procedures;

public sealed class ProcedureDocument
{
    private ProcedureDocument() { }

    public ProcedureDocument(Guid organizationId, Guid procedureId, string fileName, string contentType, byte[] content, string uploadedBy, DateTimeOffset uploadedAt)
    {
        if (content.Length == 0) throw new DomainException("Plik procedury jest pusty.");
        if (content.Length > 25 * 1024 * 1024) throw new DomainException("Plik procedury może mieć maksymalnie 25 MB.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new DomainException("Nazwa pliku jest wymagana.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProcedureId = procedureId;
        FileName = fileName.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        Content = content;
        SizeBytes = content.LongLength;
        UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "system" : uploadedBy.Trim();
        UploadedAt = uploadedAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProcedureId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = "application/octet-stream";
    public long SizeBytes { get; private set; }
    public byte[] Content { get; private set; } = [];
    public string UploadedBy { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }
}
