namespace Tenebit.Domain.Common;

/// <summary>
/// The ISO 4217 codes Tenebit accepts anywhere a currency is stored (asset purchase prices, licence costs,
/// an organization's reporting currency). Mirrors the list the registration form offers - roughly the top
/// 50 currencies by trade volume - so anything a customer can pick is accepted and nothing else is.
///
/// Without this a currency was only length-checked, so "XXXXXXXX" was stored happily and then silently
/// mixed into value reports (stress test 19.09.2026, błędy 9 i 10).
/// </summary>
public static class SupportedCurrencies
{
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "EUR", "USD", "GBP", "CHF", "JPY", "CNY", "AUD", "CAD", "NZD", "SEK",
        "NOK", "DKK", "PLN", "CZK", "HUF", "RON", "BGN", "ISK", "TRY", "RUB",
        "UAH", "ILS", "AED", "SAR", "QAR", "KWD", "BHD", "OMR", "JOD", "EGP",
        "ZAR", "NGN", "KES", "GHS", "MAD", "INR", "PKR", "BDT", "LKR", "IDR",
        "MYR", "SGD", "THB", "PHP", "VND", "KRW", "TWD", "HKD", "MXN", "BRL"
    };

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Codes.Contains(code.Trim().ToUpperInvariant());

    public static IReadOnlyCollection<string> All => Codes;
}
