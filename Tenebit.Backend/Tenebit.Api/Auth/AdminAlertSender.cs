using System.Net;
using Tenebit.Application.Abstractions;

namespace Tenebit.Api.Auth;

/// <summary>
/// Out-of-band notification that someone reached the admin panel. This is the control that turns a silent
/// compromise into a noticed one: even if an attacker holds the password, the TOTP seed and a valid token,
/// the owner gets a message the moment it is used, and the moderation cap keeps the damage small and
/// reversible until they react.
///
/// Sending is best-effort and never blocks or fails the request it describes - a broken SMTP host must not
/// become a way to lock the owner out of their own panel.
/// </summary>
public sealed class AdminAlertSender
{
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAlertSender> _logger;

    public AdminAlertSender(IEmailSender emailSender, IConfiguration configuration, ILogger<AdminAlertSender> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public Task SignInSucceededAsync(IPAddress? ip, string? userAgent, CancellationToken cancellationToken) =>
        SendAsync(
            "Tenebit: logowanie do panelu administracyjnego",
            $"""
             <p>Nastąpiło <strong>udane logowanie</strong> do panelu administracyjnego Tenebit.</p>
             <ul>
               <li>Czas (UTC): {WebUtility.HtmlEncode(DateTimeOffset.UtcNow.ToString("u"))}</li>
               <li>Adres IP: {WebUtility.HtmlEncode(ip?.ToString() ?? "nieznany")}</li>
               <li>Przeglądarka: {WebUtility.HtmlEncode(Shorten(userAgent))}</li>
             </ul>
             <p>Jeśli to nie Ty — natychmiast zmień hasło administratora i sekret 2FA w konfiguracji serwera.</p>
             """,
            cancellationToken);

    public Task SignInFailedAsync(IPAddress? ip, int failureCount, bool lockedOut, CancellationToken cancellationToken) =>
        SendAsync(
            lockedOut
                ? "Tenebit: panel administracyjny ZABLOKOWANY po nieudanych próbach"
                : "Tenebit: nieudana próba logowania do panelu administracyjnego",
            $"""
             <p>Odnotowano <strong>nieudaną próbę logowania</strong> do panelu administracyjnego.</p>
             <ul>
               <li>Czas (UTC): {WebUtility.HtmlEncode(DateTimeOffset.UtcNow.ToString("u"))}</li>
               <li>Adres IP: {WebUtility.HtmlEncode(ip?.ToString() ?? "nieznany")}</li>
               <li>Liczba nieudanych prób w oknie: {failureCount}</li>
             </ul>
             {(lockedOut ? "<p><strong>Panel został tymczasowo zablokowany</strong> na 30 minut.</p>" : string.Empty)}
             """,
            cancellationToken);

    public Task ModerationActionAsync(string action, string? target, IPAddress? ip, CancellationToken cancellationToken) =>
        SendAsync(
            "Tenebit: akcja moderacyjna w panelu administracyjnym",
            $"""
             <p>W panelu administracyjnym wykonano akcję: <strong>{WebUtility.HtmlEncode(action)}</strong></p>
             <ul>
               <li>Obiekt: {WebUtility.HtmlEncode(target ?? "-")}</li>
               <li>Czas (UTC): {WebUtility.HtmlEncode(DateTimeOffset.UtcNow.ToString("u"))}</li>
               <li>Adres IP: {WebUtility.HtmlEncode(ip?.ToString() ?? "nieznany")}</li>
             </ul>
             <p>Jeśli to nie Ty — akcja jest odwracalna z poziomu panelu, a pełny ślad znajdziesz w dzienniku administratora.</p>
             """,
            cancellationToken);

    private async Task SendAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var recipient = AdminAccountOptions.AlertEmail(_configuration);
        if (string.IsNullOrWhiteSpace(recipient)) return;

        try
        {
            await _emailSender.SendAsync(recipient, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać alertu bezpieczeństwa panelu administracyjnego.");
        }
    }

    private static string Shorten(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "nieznana" : value.Length <= 160 ? value : value[..160];
}
