using System.Net;
using Tenebit.Application.Abstractions;

namespace Tenebit.Api.Auth;

/// <summary>
/// Out-of-band notification for the affiliate program's two-way messaging (spec §8: "some contact form
/// that reaches me and I can answer back"). Mirrors <see cref="AdminAlertSender"/>'s
/// best-effort-never-blocks contract - a broken SMTP host must never turn into a failed message post.
/// </summary>
public sealed class AffiliateAlertSender
{
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AffiliateAlertSender> _logger;

    public AffiliateAlertSender(IEmailSender emailSender, IConfiguration configuration, ILogger<AffiliateAlertSender> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public Task NewPartnerMessageAsync(string subject, bool isComplaint, CancellationToken cancellationToken) =>
        SendToAdminAsync(
            isComplaint ? "Tenebit Partners: nowe ZASTRZEŻENIE od partnera" : "Tenebit Partners: nowa wiadomość od partnera",
            $"""
             <p>Partner napisał nowy wątek w panelu programu afiliacyjnego{(isComplaint ? " oznaczony jako <strong>zastrzeżenie</strong>" : "")}.</p>
             <p>Temat: {WebUtility.HtmlEncode(subject)}</p>
             <p>Zaloguj się do panelu administracyjnego, aby odpowiedzieć.</p>
             """,
            cancellationToken);

    public Task ReplyPostedAsync(string affiliateEmail, string subject, CancellationToken cancellationToken)
    {
        // Sent to the affiliate, not the admin - the one place this class notifies someone other than
        // the platform owner, since it is the natural counterpart of NewPartnerMessageAsync.
        return SendAsync(
            affiliateEmail,
            "Tenebit Partners: nowa odpowiedź w Twoim wątku",
            $"""
             <p>Otrzymałeś odpowiedź w wątku "{WebUtility.HtmlEncode(subject)}" w panelu programu partnerskiego.</p>
             <p>Zaloguj się do panelu partnera, aby ją przeczytać.</p>
             """,
            cancellationToken);
    }

    private Task SendToAdminAsync(string subject, string html, CancellationToken cancellationToken)
    {
        var recipient = AdminAccountOptions.AlertEmail(_configuration);
        return string.IsNullOrWhiteSpace(recipient) ? Task.CompletedTask : SendAsync(recipient, subject, html, cancellationToken);
    }

    private async Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendAsync(recipient, subject, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać powiadomienia o wiadomości programu partnerskiego.");
        }
    }
}
