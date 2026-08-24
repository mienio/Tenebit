namespace Tenebit.Application.Admin;

/// <summary>
/// Data minimisation for the platform admin panel.
///
/// The panel exists to moderate organization names and to spot abuse - not to browse customer records.
/// So the admin API deliberately never emits customer personal data: no asset names, no people's names,
/// no full e-mail addresses. Only counts, organization names, and masked identifiers leave the server.
///
/// Masking happens HERE, in the application layer, on the way out - not in the browser. A client-side
/// blur would be cosmetic: anyone holding an admin token could call the endpoint directly and read the
/// raw JSON. Because the value is reduced before it is serialised, a stolen admin session yields
/// aggregates and masked handles, never a usable dump of customer contacts.
/// </summary>
public static class PiiMasking
{
    private const string Ellipsis = "•••";

    /// <summary>
    /// Keeps just enough of an address to recognise an account and to see repeated attempts against it,
    /// while never disclosing a contactable address. "anna.kowalska@firma.com" becomes "an•••@fi•••.com".
    /// </summary>
    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Ellipsis;

        var trimmed = email.Trim();
        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1) return Mask(trimmed, 2);

        var local = trimmed[..at];
        var domain = trimmed[(at + 1)..];

        var dot = domain.LastIndexOf('.');
        // Keep the public suffix: a bare TLD identifies no one but tells you whether an attack is
        // spraying one customer domain or many unrelated ones.
        var tld = dot > 0 && dot < domain.Length - 1 ? domain[(dot + 1)..] : string.Empty;
        var domainName = dot > 0 ? domain[..dot] : domain;

        return string.IsNullOrEmpty(tld)
            ? $"{Mask(local, 2)}@{Mask(domainName, 2)}"
            : $"{Mask(local, 2)}@{Mask(domainName, 2)}.{tld}";
    }

    /// <summary>
    /// A display name is usually a real person's name, so only initials survive: "Anna Kowalska" becomes
    /// "A. K.". Enough to tell two accounts apart inside one organization; useless as a contact list.
    /// </summary>
    public static string PersonName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Ellipsis;

        var initials = displayName
            .Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => char.IsLetter(part[0]))
            .Take(3)
            .Select(part => $"{char.ToUpperInvariant(part[0])}.")
            .ToArray();

        return initials.Length == 0 ? Ellipsis : string.Join(' ', initials);
    }

    /// <summary>
    /// Masks a free-text label that may embed an address (admin audit entries record who was acted on).
    /// Anything shaped like an e-mail is reduced; everything else is left alone so action targets such as
    /// organization names stay readable.
    /// </summary>
    public static string? AuditLabel(string? label) =>
        string.IsNullOrWhiteSpace(label) ? label
            : label.Contains('@') ? Email(label)
            : label;

    private static string Mask(string value, int visible)
    {
        var keep = Math.Min(visible, value.Length);
        // Very short segments would otherwise be shown in full, so reveal at most half of them.
        if (value.Length <= visible) keep = Math.Max(1, value.Length / 2);
        return value[..keep] + Ellipsis;
    }
}
