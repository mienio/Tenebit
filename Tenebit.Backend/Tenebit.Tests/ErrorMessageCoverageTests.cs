using System.Text.RegularExpressions;
using Tenebit.Application.Common;

namespace Tenebit.Tests;

/// <summary>
/// Straznik kompletnosci tlumaczen. Luke w pokryciu znaleziono juz trzy razy z rzedu, za kazdym
/// razem w innej sciezce (Error/DomainException, potem wlasne odpowiedzi endpointow, potem walidacja
/// kontraktu zadania) - bo nic nie krzyczy, gdy komunikat ominie slownik: aplikacja dziala, tylko
/// obcojezyczny uzytkownik dostaje polski tekst.
///
/// Test czyta zrodla i sprawdza, ze kazdy komunikat pisany po polsku faktycznie sie tlumaczy.
/// Dzieki temu "czy jezyki sa skonczone" jest pytaniem, na ktore odpowiada CI, a nie pamiec.
/// </summary>
public class ErrorMessageCoverageTests
{
    private const string PolishLetters = "ąćęłńśźżĄĆĘŁŃŚŹŻ";

    private static DirectoryInfo? FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tenebit.sln")))
        {
            directory = directory.Parent;
        }

        return directory;
    }

    private static IEnumerable<string> SourceFiles(DirectoryInfo root) =>
        Directory.EnumerateFiles(root.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Tenebit.Tests{Path.DirectorySeparatorChar}"));

    private static string Relative(DirectoryInfo root, string path) =>
        Path.GetRelativePath(root.FullName, path).Replace('\\', '/');

    /// <summary>
    /// Komunikaty oddawane przez <c>Error.*</c> i <c>DomainException</c> ida do klienta przez
    /// ResultExtensions, ktory wola translator. Kazdy taki literal musi wiec byc w slowniku.
    /// </summary>
    [Fact]
    public void EveryPolishErrorLiteral_HasATranslation()
    {
        var root = FindBackendRoot();
        Assert.True(root is not null, "Nie znaleziono Tenebit.sln - skan zrodel niemozliwy.");

        // Grupa zatrzymuje sie sama na zamykajacym cudzyslowie, wiec wzorzec go nie potrzebuje -
        // i dzieki temu nie konczy sie ciagiem cudzyslowow, ktory zderzalby sie z ogranicznikiem.
        var producer = new Regex(
            """(?:Error\.(?:Validation|Conflict|NotFound|Forbidden|Unauthorized)|DomainException)\(\s*"((?:[^"\\]|\\.)*)""");

        var untranslated = new List<string>();
        foreach (var file in SourceFiles(root!))
        {
            var source = File.ReadAllText(file);
            foreach (Match match in producer.Matches(source))
            {
                var message = match.Groups[1].Value;
                if (!message.Any(PolishLetters.Contains)) continue;

                // Tlumaczenie identyczne ze zrodlem = zaden wpis ani szablon go nie zlapal.
                if (ErrorMessageTranslator.Translate(message, "en") != message) continue;

                var line = source[..match.Index].Count(c => c == '\n') + 1;
                untranslated.Add($"{Relative(root!, file)}:{line}  \"{message}\"");
            }
        }

        Assert.True(untranslated.Count == 0,
            "Komunikaty bez tlumaczenia (dodaj wpis do ErrorMessageTranslator.Exact albo regule do Templates):\n"
            + string.Join('\n', untranslated));
    }

    /// <summary>
    /// Endpointy, ktore odpowiadaja z pominieciem <c>Result</c>, musza jawnie wolac
    /// <c>ResultExtensions.Localize</c>. Panel administracyjny jest swiadomie pozostawiony po polsku
    /// (obsluguje go operator, nie klient), wiec jest wylaczony ze skanu.
    /// </summary>
    [Fact]
    public void EveryApiResponseLiteral_GoesThroughLocalize()
    {
        var root = FindBackendRoot();
        Assert.True(root is not null, "Nie znaleziono Tenebit.sln - skan zrodel niemozliwy.");

        var apiRoot = Path.Combine(root!.FullName, "Tenebit.Api");
        var responseShape = new Regex("""(?:new ErrorResponse\(|message\s*=\s*|validationError\s*=\s*)(\$?"(?:[^"\\]|\\.)*")""");

        var leaks = new List<string>();
        foreach (var file in SourceFiles(root).Where(path => path.StartsWith(apiRoot, StringComparison.Ordinal)))
        {
            var relative = Relative(root, file);
            if (relative.Contains("AdminEndpoints.cs")) continue;

            var source = File.ReadAllText(file);
            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.Contains("Localize", StringComparison.Ordinal)) continue;

                foreach (Match match in responseShape.Matches(line))
                {
                    if (!match.Groups[1].Value.Any(PolishLetters.Contains)) continue;
                    leaks.Add($"{relative}:{i + 1}  {line.Trim()}");
                    break;
                }
            }
        }

        Assert.True(leaks.Count == 0,
            "Odpowiedzi API z polskim tekstem bez ResultExtensions.Localize(...):\n" + string.Join('\n', leaks));
    }
}
