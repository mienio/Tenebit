using System.IO.Compression;
using System.Text;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Procedures;

internal static class ProcedureDocumentValidator
{
    private const int MaxFileNameLength = 260;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static ValidatedProcedureDocument Validate(string fileName, byte[] content)
    {
        if (content.Length == 0) throw new DomainException("Plik procedury jest pusty.");
        if (content.Length > 25 * 1024 * 1024) throw new DomainException("Plik procedury może mieć maksymalnie 25 MB.");

        var safeFileName = ValidateFileName(fileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" when IsPdf(content) => new ValidatedProcedureDocument(safeFileName, "application/pdf"),
            ".docx" when IsDocx(content) => new ValidatedProcedureDocument(safeFileName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            ".txt" when IsUtf8Text(content) => new ValidatedProcedureDocument(safeFileName, "text/plain; charset=utf-8"),
            ".pdf" => throw new DomainException("Plik PDF ma nieprawidłową sygnaturę."),
            ".docx" => throw new DomainException("Plik DOCX ma nieprawidłową strukturę."),
            ".txt" => throw new DomainException("Plik TXT musi być poprawnym tekstem UTF-8 bez danych binarnych."),
            _ => throw new DomainException("Dozwolone formaty dokumentów procedur to PDF, DOCX i TXT.")
        };
    }

    private static string ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new DomainException("Nazwa pliku jest wymagana.");

        var trimmed = fileName.Trim();
        if (trimmed.Length > MaxFileNameLength) throw new DomainException($"Nazwa pliku może mieć maksymalnie {MaxFileNameLength} znaków.");
        if (!string.Equals(Path.GetFileName(trimmed), trimmed, StringComparison.Ordinal))
        {
            throw new DomainException("Nazwa pliku nie może zawierać ścieżki katalogów.");
        }

        if (trimmed.Any(character => char.IsControl(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'))
        {
            throw new DomainException("Nazwa pliku zawiera niedozwolone znaki.");
        }

        return trimmed;
    }

    private static bool IsPdf(ReadOnlySpan<byte> content) =>
        content.Length >= 5 && content[..5].SequenceEqual("%PDF-"u8);

    private static bool IsDocx(byte[] content)
    {
        if (content.Length < 4 || content[0] != (byte)'P' || content[1] != (byte)'K') return false;

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            var hasContentTypes = false;
            var hasDocument = false;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith('/')
                    || name.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
                {
                    return false;
                }

                if (string.Equals(name, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) hasContentTypes = true;
                if (string.Equals(name, "word/document.xml", StringComparison.OrdinalIgnoreCase)) hasDocument = true;
            }

            return hasContentTypes && hasDocument;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsUtf8Text(byte[] content)
    {
        if (content.AsSpan().Contains((byte)0)) return false;

        try
        {
            _ = StrictUtf8.GetString(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}

internal sealed record ValidatedProcedureDocument(string FileName, string ContentType);
