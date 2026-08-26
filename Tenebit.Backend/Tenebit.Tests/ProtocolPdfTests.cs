using System.Text;
using Tenebit.Application.Assignments;
using Tenebit.Application.Protocols;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Evidence;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class ProtocolPdfTests
{
    private static byte[] PngBytes(byte marker)
    {
        // Sygnatura PNG plus wypełnienie - wystarczy, żeby ImageSignature.Detect uznał to za PNG.
        var content = new byte[64];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(content, 0);
        for (var i = signature.Length; i < content.Length; i++) content[i] = marker;
        return content;
    }

    private static ProtocolDocument SampleDocument(byte[]? signature = null, string? hash = null) => new(
        Kind: ProtocolKind.Handover,
        OrganizationName: "Firma Żółć sp. z o.o.",
        ProtocolNumber: "TEN-2026-0001",
        Person: new ProtocolParty("Łukasz Ćwikliński", "EMP-42", "Specjalista ds. wdrożeń", "lukasz@example.com"),
        IssuedAt: new DateTimeOffset(2026, 8, 26, 9, 15, 0, TimeSpan.Zero),
        ConfirmedAt: new DateTimeOffset(2026, 8, 26, 10, 23, 0, TimeSpan.Zero),
        ConfirmationHash: hash,
        SignerName: "Łukasz Ćwikliński",
        SignatureImage: signature,
        Lines: [new ProtocolLine("MacBook Pro 14\"", "AT-0001", "C02XZ1234", "nowy", 8999.00m, "PLN", null)],
        Procedures: ["Regulamin BHP (1.2)"],
        Notes: "Ładowarka w komplecie.",
        Labels: ProtocolLabels.For("pl"));

    [Fact]
    public void Generator_ProducesPdf_WithPolishCharacters()
    {
        var pdf = new ProtocolPdfGenerator().Render(SampleDocument(hash: new string('a', 64)));

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Generator_HandlesMissingSignatureAndEmptyLines()
    {
        var document = SampleDocument() with { Lines = [], Procedures = [], Notes = null, ConfirmedAt = null, SignerName = null };

        var pdf = new ProtocolPdfGenerator().Render(document);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Theory]
    [InlineData("pl", "Protokół przekazania sprzętu")]
    [InlineData("en", "Equipment handover protocol")]
    [InlineData("es", "Protocolo de entrega de equipo")]
    [InlineData("de", "Übergabeprotokoll für Arbeitsmittel")]
    [InlineData("DE", "Übergabeprotokoll für Arbeitsmittel")]
    [InlineData(null, "Equipment handover protocol")]
    [InlineData("fr", "Equipment handover protocol")]
    public void Labels_FollowOrganizationLanguage(string? language, string expectedTitle)
    {
        Assert.Equal(expectedTitle, ProtocolLabels.For(language).HandoverTitle);
    }

    // Klauzula o odpowiedzialności i zastrzeżenie eIDAS to powód istnienia tego dokumentu - żaden
    // język nie może wyjść bez nich, bo protokół bez klauzuli nie jest dowodem powierzenia mienia.
    [Theory]
    [InlineData("pl")]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("de")]
    public void EveryLanguage_CarriesLiabilityAndEidasDisclaimer(string language)
    {
        var labels = ProtocolLabels.For(language);

        Assert.False(string.IsNullOrWhiteSpace(labels.LiabilityClause));
        Assert.Contains("eIDAS", labels.LegalNote);
    }

    [Theory]
    [InlineData("pl")]
    [InlineData("es")]
    [InlineData("de")]
    public void Generator_RendersEveryLanguage(string language)
    {
        var pdf = new ProtocolPdfGenerator().Render(SampleDocument(hash: new string('b', 64)) with { Labels = ProtocolLabels.For(language) });

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    // ---------- Integralność v4: podpis jest częścią pieczęci ----------

    [Fact]
    public void AcceptWithSignature_SealsSignatureIntoAcceptanceHash()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-V4", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(Guid.NewGuid(), "ok");

        assignment.AcceptWithSignature(DateTimeOffset.UtcNow, "1.2.3.4", [], PngBytes(0x11), "Anna Kowalska");

        Assert.Equal(4, assignment.IntegrityVersion);
        Assert.Equal("Anna Kowalska", assignment.SignerName);
        Assert.True(assignment.VerifyIntegrity(Array.Empty<AssetEvidenceIntegrityEntry>()));
    }

    [Fact]
    public void AcceptWithoutSignature_StaysOnVersion3()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-NOSIG", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(Guid.NewGuid(), "ok");

        assignment.AcceptWithSignature(DateTimeOffset.UtcNow, "1.2.3.4", [], null, null);

        Assert.Equal(3, assignment.IntegrityVersion);
        Assert.Null(assignment.SignatureImage);
        Assert.True(assignment.VerifyIntegrity(Array.Empty<AssetEvidenceIntegrityEntry>()));
    }

    [Fact]
    public void SignedAcceptance_SurvivesLaterIpRedaction()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-V4-IP", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(Guid.NewGuid(), "ok");
        assignment.AcceptWithSignature(DateTimeOffset.UtcNow, "1.2.3.4", [], PngBytes(0x22), "Anna Kowalska");

        assignment.ApplyAcceptedIpPrivacyWithEvidenceIntegrity(null, Array.Empty<AssetEvidenceIntegrityEntry>());

        Assert.Null(assignment.AcceptedIp);
        Assert.Equal(4, assignment.IntegrityVersion);
        Assert.True(assignment.VerifyIntegrity(Array.Empty<AssetEvidenceIntegrityEntry>()));
    }

    [Fact]
    public void Signature_LargerThanLimit_IsRejected()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-BIG", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(Guid.NewGuid(), "ok");

        Assert.Throws<Domain.Common.DomainException>(() =>
            assignment.AcceptWithSignature(DateTimeOffset.UtcNow, null, [], new byte[200 * 1024 + 1], null));
    }

    // ---------- Data URL z canvasu ----------

    [Fact]
    public void SignatureDataUrl_AcceptsPngPayload()
    {
        var encoded = "data:image/png;base64," + Convert.ToBase64String(PngBytes(0x33));

        var result = SignatureDataUrl.Decode(encoded);

        Assert.True(result.IsSuccess);
        Assert.Equal(PngBytes(0x33), result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("data:image/jpeg;base64,/9j/4AAQ")]
    [InlineData("data:image/png;base64,%%%not-base64%%%")]
    [InlineData("javascript:alert(1)")]
    public void SignatureDataUrl_RejectsAnythingElse(string? input)
    {
        Assert.True(SignatureDataUrl.Decode(input).IsFailure);
    }

    // Regresja z produkcji: dekoder przepuszczal podpis, ale zadanie nigdy do niego nie docieralo.
    // RequestObjectValidator wnioskuje limit dlugosci z NAZWY pola, a "SignatureDataUrl" zawiera "Url",
    // wiec dostawal limit adresu (2048 znakow) i odrzucal kazdy realny rysunek z canvasu. Testowanie
    // samego dekodera tego nie widzialo - trzeba przejsc ta sama sciezka, co endpoint.
    [Fact]
    public void AcceptRequest_PassesValidator_WithRealSizedSignature()
    {
        var png = "data:image/png;base64," + Convert.ToBase64String(new byte[40_000]);
        Assert.True(png.Length > 2048, "podpis musi byc wiekszy niz stary limit, inaczej test nic nie sprawdza");

        var request = new AcceptPublicAssignmentRequest(png, "Anna Kowalska");

        Assert.Null(Application.Common.RequestObjectValidator.Validate(request));
    }

    [Fact]
    public void AcceptRequest_StillRejects_SignatureBeyondTheCap()
    {
        var zaDuzy = new AcceptPublicAssignmentRequest(new string('a', 300_001), "Anna Kowalska");

        Assert.NotNull(Application.Common.RequestObjectValidator.Validate(zaDuzy));
    }

    [Fact]
    public void AcceptRequest_AllowsEmptyBody()
    {
        Assert.Null(Application.Common.RequestObjectValidator.Validate(new AcceptPublicAssignmentRequest(null, null)));
    }
}
