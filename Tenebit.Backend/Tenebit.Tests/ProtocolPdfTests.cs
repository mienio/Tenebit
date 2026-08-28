using System.Text;
using Tenebit.Application.Assignments;
using Tenebit.Application.Protocols;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Evidence;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class ProtocolPdfTests
{
    private static ProtocolDocument SampleDocument(string? hash = null) => new(
        Kind: ProtocolKind.Handover,
        OrganizationName: "Firma Żółć sp. z o.o.",
        ProtocolNumber: "TEN-2026-0001",
        Person: new ProtocolParty("Łukasz Ćwikliński", "EMP-42", "Specjalista ds. wdrożeń", "lukasz@example.com"),
        IssuedAt: new DateTimeOffset(2026, 8, 26, 9, 15, 0, TimeSpan.Zero),
        ConfirmedAt: new DateTimeOffset(2026, 8, 26, 10, 23, 0, TimeSpan.Zero),
        ConfirmationHash: hash,
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
    public void Generator_HandlesEmptyLines()
    {
        var document = SampleDocument() with { Lines = [], Procedures = [], Notes = null, ConfirmedAt = null };

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
    [InlineData("it", "Verbale di consegna delle attrezzature")]
    [InlineData("fr", "Procès-verbal de remise du matériel")]
    [InlineData("cs", "Equipment handover protocol")]
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
}
