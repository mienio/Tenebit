using System.Text;

namespace Tenebit.Application.Common;

public static class CsvWriter
{
    public static void WriteRow(StringBuilder csv, IEnumerable<string> fields) =>
        csv.AppendLine(string.Join(',', fields.Select(EscapeField)));

    /// <summary>Escapuje pole wg RFC4180 - cudzysłów wokół pola zawierającego przecinek, cudzysłów lub nową linię,
    /// z podwojeniem wewnętrznych cudzysłowów. Pola zaczynające się od =, +, -, @ dostają wiodący apostrof,
    /// żeby Excel/Sheets nie interpretowały danych użytkownika jako formuły (CSV/formula injection, CWE-1236).</summary>
    public static string EscapeField(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
