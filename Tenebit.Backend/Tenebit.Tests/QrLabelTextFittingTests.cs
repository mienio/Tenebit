using System.Text.RegularExpressions;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

/// <summary>
/// Etykieta QR jest jednym SVG o stałej szerokości, bez silnika łamania tekstu - długa nazwa aktywa
/// wychodziła poza krawędzie i była przycinana w podglądzie oraz na wydruku (QA: BUG-009).
/// </summary>
public class QrLabelTextFittingTests
{
    private const string Payload = "https://teneb.it/s/ABCD1234";
    private static readonly Regex TextElement = new("<text[^>]*font-size=\"(?<size>\\d+)\"[^>]*>(?<value>[^<]*)</text>", RegexOptions.Compiled);

    private static (int Width, IReadOnlyList<(int FontSize, string Value)> Lines) Render(params string[] footerLines)
    {
        var render = new QrCodeGenerator().RenderAssetQrLabel(Payload, new QrLabelContent([], footerLines, null, 4));
        var lines = TextElement.Matches(render.Svg)
            .Select(match => (int.Parse(match.Groups["size"].Value), match.Groups["value"].Value))
            .ToList();
        return (render.WidthPx, lines);
    }

    [Fact]
    public void Krotka_nazwa_zostaje_nietknieta()
    {
        var (_, lines) = Render("QAB-0001", "Dell Latitude 5440");

        Assert.Equal("Dell Latitude 5440", lines[1].Value);
        Assert.DoesNotContain("…", lines[1].Value);
    }

    [Fact]
    public void Dluga_nazwa_miesci_sie_w_szerokosci_etykiety()
    {
        const string longName = "QA Claude Asset 01 - Edytowany z bardzo dlugim opisem sprzetu";
        var (width, lines) = Render("QAB-0001", longName);

        var fitted = lines[1];
        // Ta sama miara co w generatorze: średnia szerokość znaku Arial to ok. 0,55 em.
        var estimatedWidth = fitted.Value.Length * fitted.FontSize * 0.55;
        Assert.True(estimatedWidth <= width, $"Podpis '{fitted.Value}' nie mieści się w {width}px.");
        Assert.True(fitted.Value.Length < longName.Length);
        Assert.EndsWith("…", fitted.Value);
    }

    [Fact]
    public void Skrocony_podpis_zachowuje_poczatek_nazwy()
    {
        var (_, lines) = Render("QAB-0001", "Laptop serwisowy dla dzialu wdrozen i utrzymania systemow");

        Assert.StartsWith("Laptop serwisowy", lines[1].Value);
    }
}
