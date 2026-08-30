using Microsoft.Extensions.Configuration;
using QRCoder;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

/// <summary>
/// The scan link looks stripped-down on purpose - a ten-character code, upper case, a one-letter path -
/// and that shape is the only reason a label scans on the first try at small sizes. Someone tidying it
/// back into a conventional URL would make every printed label denser with no visible symptom until a
/// warehouse phone starts failing on them, so the properties are pinned here.
/// </summary>
public class AssetScanCodeTests
{
    private static int Modules(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return data.ModuleMatrix.Count;
    }

    private static AppLinkBuilder LinkBuilder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:PublicUrl"] = "https://teneb.it" })
            .Build();
        return new AppLinkBuilder(configuration);
    }

    [Fact]
    public void Create_ProducesTenCharactersFromAnUnambiguousUpperCaseAlphabet()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = AssetScanCode.Create();
            Assert.Equal(AssetScanCode.Length, code.Length);
            Assert.True(AssetScanCode.IsWellFormed(code), code);
            // I, L, O and U are the characters people misread off a scuffed label.
            Assert.DoesNotContain(code, c => c is 'I' or 'L' or 'O' or 'U');
        }
    }

    [Fact]
    public void Create_DoesNotRepeatItself()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => AssetScanCode.Create()).ToHashSet();
        Assert.Equal(500, codes.Count);
    }

    [Fact]
    public void IsWellFormed_RejectsAnythingOtherThanTheCodeShape()
    {
        Assert.False(AssetScanCode.IsWellFormed(null));
        Assert.False(AssetScanCode.IsWellFormed(""));
        Assert.False(AssetScanCode.IsWellFormed("K7M2QX9V4"));      // za krótki
        Assert.False(AssetScanCode.IsWellFormed("K7M2QX9V4BB"));    // za długi
        Assert.False(AssetScanCode.IsWellFormed("k7m2qx9v4b"));     // małe litery
        Assert.False(AssetScanCode.IsWellFormed("K7M2QX9V4I"));     // znak spoza alfabetu
        Assert.True(AssetScanCode.IsWellFormed("K7M2QX9V4B"));
    }

    [Fact]
    public void NewAsset_GetsAScanCodeImmediately()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "LAP-0001");
        Assert.True(AssetScanCode.IsWellFormed(asset.ScanCode));

        var first = asset.ScanCode;
        asset.RegenerateScanCode();
        Assert.NotEqual(first, asset.ScanCode);
    }

    [Fact]
    public void BuildAssetScanLink_StaysInAlphanumericQrMode()
    {
        var link = LinkBuilder().BuildAssetScanLink("K7M2QX9V4B");

        Assert.Equal("HTTPS://TENEB.IT/S/K7M2QX9V4B", link);
        Assert.Equal(link.ToUpperInvariant(), link);
    }

    [Fact]
    public void ScanLink_FitsTheSmallestQrVersionThatCanHoldAUrl()
    {
        var link = LinkBuilder().BuildAssetScanLink(AssetScanCode.Create());
        var modules = Modules(link);

        // 33 = wersja 2 (25 modułów) plus wymagana strefa cisza 4 moduły z każdej strony. Krótszy kod
        // nie zmniejszy tego dalej, bo wersja 1 mieści tylko 16 znaków, a sam adres bazowy ma 19.
        Assert.Equal(33, modules);
    }

    [Fact]
    public void ScanLink_IsSparserThanTheGuidBasedUrlItReplaced()
    {
        var conventional = $"https://teneb.it/scan/{Guid.NewGuid()}/{Guid.NewGuid()}";
        var link = LinkBuilder().BuildAssetScanLink(AssetScanCode.Create());

        Assert.True(Modules(link) < Modules(conventional),
            $"Nowy kod ma {Modules(link)} modulow, stary {Modules(conventional)}.");
    }
}
