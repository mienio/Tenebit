using System.Net;

namespace Tenebit.Application.Common;

/// <summary>
/// Centralizes the HTML/subject templates for the app's transactional emails (password reset, email
/// verification, org invitation, new-assignment notification, procedure-unsigned alert) in pl/en/es/de,
/// selected by the recipient organization's <c>Organization.Language</c>. Polish remains the default
/// when the organization's language isn't one of the four supported values.
/// </summary>
public static class EmailTemplates
{
    private static string Normalize(string? language)
    {
        var lang = (language ?? "pl").Trim().ToLowerInvariant();
        return lang is "pl" or "en" or "es" or "de" ? lang : "pl";
    }

    public static (string Subject, string Html) PasswordReset(string? language, string link)
    {
        var lang = Normalize(language);
        return lang switch
        {
            "en" => ("Reset your password — Tenebit", $"""
                <p>We received a request to reset the password for your Tenebit account.</p>
                <p><a href="{link}">Set a new password</a></p>
                <p>This link is valid for 1 hour. If you didn't request this, you can safely ignore this email.</p>
                """),
            "es" => ("Restablece tu contraseña — Tenebit", $"""
                <p>Hemos recibido una solicitud para restablecer la contraseña de tu cuenta de Tenebit.</p>
                <p><a href="{link}">Establecer una nueva contraseña</a></p>
                <p>Este enlace es válido durante 1 hora. Si no has solicitado esto, puedes ignorar este correo.</p>
                """),
            "de" => ("Passwort zurücksetzen — Tenebit", $"""
                <p>Wir haben eine Anfrage zum Zurücksetzen des Passworts für Ihr Tenebit-Konto erhalten.</p>
                <p><a href="{link}">Neues Passwort festlegen</a></p>
                <p>Dieser Link ist 1 Stunde gültig. Wenn Sie diese Anfrage nicht gestellt haben, ignorieren Sie diese E-Mail einfach.</p>
                """),
            _ => ("Reset hasła — Tenebit", $"""
                <p>Otrzymaliśmy prośbę o zresetowanie hasła do konta Tenebit.</p>
                <p><a href="{link}">Ustaw nowe hasło</a></p>
                <p>Link jest ważny przez 1 godzinę. Jeśli to nie Ty wysłałeś/aś tę prośbę, zignoruj tę wiadomość.</p>
                """),
        };
    }

    public static (string Subject, string Html) EmailVerification(string? language, string link)
    {
        var lang = Normalize(language);
        return lang switch
        {
            "en" => ("Confirm your email — Tenebit", $"""
                <p>Thanks for creating a Tenebit account.</p>
                <p><a href="{link}">Confirm your email address</a></p>
                <p>This link is valid for 48 hours.</p>
                """),
            "es" => ("Confirma tu correo electrónico — Tenebit", $"""
                <p>Gracias por crear una cuenta en Tenebit.</p>
                <p><a href="{link}">Confirmar dirección de correo</a></p>
                <p>Este enlace es válido durante 48 horas.</p>
                """),
            "de" => ("Bestätigen Sie Ihre E-Mail-Adresse — Tenebit", $"""
                <p>Danke, dass Sie ein Tenebit-Konto erstellt haben.</p>
                <p><a href="{link}">E-Mail-Adresse bestätigen</a></p>
                <p>Dieser Link ist 48 Stunden gültig.</p>
                """),
            _ => ("Potwierdź e-mail — Tenebit", $"""
                <p>Dziękujemy za założenie konta w Tenebit.</p>
                <p><a href="{link}">Potwierdź adres e-mail</a></p>
                <p>Link jest ważny przez 48 godzin.</p>
                """),
        };
    }

    public static (string Subject, string Html) OrganizationInvitation(string? language, string link)
    {
        var lang = Normalize(language);
        return lang switch
        {
            "en" => ("Invitation to Tenebit", $"""
                <p>You've been added as a user to an organization in Tenebit.</p>
                <p><a href="{link}">Set a password and sign in</a></p>
                <p>This link is valid for 24 hours.</p>
                """),
            "es" => ("Invitación a Tenebit", $"""
                <p>Se te ha añadido como usuario en una organización de Tenebit.</p>
                <p><a href="{link}">Establecer contraseña e iniciar sesión</a></p>
                <p>Este enlace es válido durante 24 horas.</p>
                """),
            "de" => ("Einladung zu Tenebit", $"""
                <p>Sie wurden als Benutzer zu einer Organisation in Tenebit hinzugefügt.</p>
                <p><a href="{link}">Passwort festlegen und anmelden</a></p>
                <p>Dieser Link ist 24 Stunden gültig.</p>
                """),
            _ => ("Zaproszenie do Tenebit", $"""
                <p>Dodano Cię jako użytkownika w organizacji w Tenebit.</p>
                <p><a href="{link}">Ustaw hasło i zaloguj się</a></p>
                <p>Link jest ważny przez 24 godziny.</p>
                """),
        };
    }

    public static (string Subject, string Html) NewAssignmentNotification(
        string? language,
        string firstName,
        string protocolNumber,
        IEnumerable<string> assetNames,
        IReadOnlyList<string> procedureTitles,
        string acceptanceLink)
    {
        var lang = Normalize(language);
        var assetsHtml = string.Join("", assetNames.Select(name => $"<li>{WebUtility.HtmlEncode(name)}</li>"));
        var encodedFirstName = WebUtility.HtmlEncode(firstName);
        var encodedProtocolNumber = WebUtility.HtmlEncode(protocolNumber);

        return lang switch
        {
            "en" => ($"New equipment to collect — {protocolNumber}", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Hi {encodedFirstName}!</h2>
                    <p>New equipment is waiting for you to collect. Protocol number: <strong>{encodedProtocolNumber}</strong></p>
                    <p><strong>Equipment:</strong></p>
                    <ul>{assetsHtml}</ul>
                    {BuildProcedureListHtml(lang, procedureTitles)}
                    <p><a href="{acceptanceLink}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Review and confirm receipt</a></p>
                    <p style="color:#687385;font-size:13px;">If the button doesn't work, copy this link into your browser: {acceptanceLink}</p>
                </div>
                """),
            "es" => ($"Nuevo equipo para recoger — {protocolNumber}", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>¡Hola, {encodedFirstName}!</h2>
                    <p>Tienes nuevo equipo pendiente de recoger. Número de protocolo: <strong>{encodedProtocolNumber}</strong></p>
                    <p><strong>Equipo:</strong></p>
                    <ul>{assetsHtml}</ul>
                    {BuildProcedureListHtml(lang, procedureTitles)}
                    <p><a href="{acceptanceLink}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Ver y confirmar recepción</a></p>
                    <p style="color:#687385;font-size:13px;">Si el botón no funciona, copia este enlace en tu navegador: {acceptanceLink}</p>
                </div>
                """),
            "de" => ($"Neue Geräte zur Abholung — {protocolNumber}", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Hallo {encodedFirstName}!</h2>
                    <p>Neue Geräte warten auf die Abholung. Protokollnummer: <strong>{encodedProtocolNumber}</strong></p>
                    <p><strong>Geräte:</strong></p>
                    <ul>{assetsHtml}</ul>
                    {BuildProcedureListHtml(lang, procedureTitles)}
                    <p><a href="{acceptanceLink}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Ansehen und Empfang bestätigen</a></p>
                    <p style="color:#687385;font-size:13px;">Falls die Schaltfläche nicht funktioniert, kopieren Sie diesen Link in Ihren Browser: {acceptanceLink}</p>
                </div>
                """),
            _ => ($"Nowy sprzęt do odebrania — {protocolNumber}", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Witaj, {encodedFirstName}!</h2>
                    <p>Otrzymujesz nowy sprzęt do odebrania. Numer protokołu: <strong>{encodedProtocolNumber}</strong></p>
                    <p><strong>Sprzęt:</strong></p>
                    <ul>{assetsHtml}</ul>
                    {BuildProcedureListHtml(lang, procedureTitles)}
                    <p><a href="{acceptanceLink}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Zobacz i potwierdź odbiór</a></p>
                    <p style="color:#687385;font-size:13px;">Jeśli przycisk nie działa, skopiuj ten link do przeglądarki: {acceptanceLink}</p>
                </div>
                """),
        };
    }

    private static string BuildProcedureListHtml(string lang, IReadOnlyList<string> procedureTitles)
    {
        if (procedureTitles.Count == 0)
        {
            return "";
        }

        var heading = lang switch
        {
            "en" => "Procedures and policies to review:",
            "es" => "Procedimientos y políticas que debes revisar:",
            "de" => "Zu prüfende Prozeduren und Richtlinien:",
            _ => "Procedury i regulaminy do zapoznania:",
        };
        var items = string.Join("", procedureTitles.Select(title => $"<li>{WebUtility.HtmlEncode(title)}</li>"));
        return $"<p><strong>{heading}</strong></p><ul>{items}</ul>";
    }

    public static (string Subject, string Html) OffboardingLink(
        string? language,
        string firstName,
        DateTimeOffset returnDueDate,
        string link)
    {
        var lang = Normalize(language);
        var encodedFirstName = WebUtility.HtmlEncode(firstName);
        var dueDate = returnDueDate.ToString("yyyy-MM-dd");

        return lang switch
        {
            "en" => ("Returning company equipment", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Hi {encodedFirstName}!</h2>
                    <p>Please review the company equipment and licenses associated with you. Return due date: <strong>{dueDate}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Open the return checklist</a></p>
                    <p style="color:#687385;font-size:13px;">If the button doesn't work, copy this link into your browser: {link}</p>
                </div>
                """),
            "es" => ("Devolución del equipo de la empresa", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>¡Hola, {encodedFirstName}!</h2>
                    <p>Por favor, revisa el equipo y las licencias de la empresa asociadas a ti. Fecha límite de devolución: <strong>{dueDate}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Abrir la lista de devolución</a></p>
                    <p style="color:#687385;font-size:13px;">Si el botón no funciona, copia este enlace en tu navegador: {link}</p>
                </div>
                """),
            "de" => ("Rückgabe der Firmenausrüstung", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Hallo {encodedFirstName}!</h2>
                    <p>Bitte überprüfen Sie die Ihnen zugeordnete Firmenausrüstung und Lizenzen. Rückgabetermin: <strong>{dueDate}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Rückgabeliste öffnen</a></p>
                    <p style="color:#687385;font-size:13px;">Falls die Schaltfläche nicht funktioniert, kopieren Sie diesen Link in Ihren Browser: {link}</p>
                </div>
                """),
            _ => ("Zwrot sprzętu firmowego", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Witaj, {encodedFirstName}!</h2>
                    <p>Sprawdź proszę listę sprzętu firmowego i licencji przypisanych do Ciebie. Termin zwrotu: <strong>{dueDate}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Otwórz listę zwrotu</a></p>
                    <p style="color:#687385;font-size:13px;">Jeśli przycisk nie działa, skopiuj ten link do przeglądarki: {link}</p>
                </div>
                """),
        };
    }

    public static (string Subject, string Html) AssetAuditLink(
        string? language,
        string firstName,
        DateTimeOffset dueDate,
        string link)
    {
        var lang = Normalize(language);
        var encodedFirstName = WebUtility.HtmlEncode(firstName);
        var due = dueDate.ToString("yyyy-MM-dd");

        return lang switch
        {
            "en" => ("Please confirm your assigned equipment", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Hi {encodedFirstName}!</h2>
                    <p>Please confirm the company equipment currently assigned to you. Due date: <strong>{due}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Open the confirmation form</a></p>
                    <p style="color:#687385;font-size:13px;">If the button doesn't work, copy this link into your browser: {link}</p>
                </div>
                """),
            "es" => ("Confirma tu equipo asignado", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>¡Hola, {encodedFirstName}!</h2>
                    <p>Por favor, confirma el equipo de la empresa actualmente asignado a ti. Fecha límite: <strong>{due}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Abrir el formulario de confirmación</a></p>
                    <p style="color:#687385;font-size:13px;">Si el botón no funciona, copia este enlace en tu navegador: {link}</p>
                </div>
                """),
            "de" => ("Bitte bestätigen Sie Ihre zugewiesene Ausrüstung", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Hallo {encodedFirstName}!</h2>
                    <p>Bitte bestätigen Sie die Ihnen aktuell zugewiesene Firmenausrüstung. Termin: <strong>{due}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Bestätigungsformular öffnen</a></p>
                    <p style="color:#687385;font-size:13px;">Falls die Schaltfläche nicht funktioniert, kopieren Sie diesen Link in Ihren Browser: {link}</p>
                </div>
                """),
            _ => ("Potwierdź przypisany sprzęt", $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
                    <h2>Witaj, {encodedFirstName}!</h2>
                    <p>Potwierdź proszę sprzęt firmowy aktualnie przypisany do Ciebie. Termin: <strong>{due}</strong></p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;">Otwórz formularz potwierdzenia</a></p>
                    <p style="color:#687385;font-size:13px;">Jeśli przycisk nie działa, skopiuj ten link do przeglądarki: {link}</p>
                </div>
                """),
        };
    }

    public static (string Subject, string Html) ProcedureUnsignedAlert(
        string? language,
        string? procedureTitle,
        string protocolNumber,
        string? personFullName,
        int deadlineDays)
    {
        var lang = Normalize(language);
        var placeholder = lang switch { "pl" => "—", _ => "—" };
        var encodedTitle = WebUtility.HtmlEncode(procedureTitle ?? placeholder);
        var encodedProtocolNumber = WebUtility.HtmlEncode(protocolNumber);
        var encodedFullName = WebUtility.HtmlEncode(personFullName ?? placeholder);
        var procedureLabel = lang switch
        {
            "en" => "procedure",
            "es" => "procedimiento",
            "de" => "Prozedur",
            _ => "procedura",
        };

        return lang switch
        {
            "en" => ($"Unsigned procedure — {procedureTitle ?? procedureLabel}", $"""
                <p>The procedure <strong>{encodedTitle}</strong> (protocol {encodedProtocolNumber}) for <strong>{encodedFullName}</strong> has not been signed within {deadlineDays} days of being sent.</p>
                """),
            "es" => ($"Procedimiento sin firmar — {procedureTitle ?? procedureLabel}", $"""
                <p>El procedimiento <strong>{encodedTitle}</strong> (protocolo {encodedProtocolNumber}) para <strong>{encodedFullName}</strong> no se ha firmado en los {deadlineDays} días posteriores a su envío.</p>
                """),
            "de" => ($"Nicht unterzeichnete Prozedur — {procedureTitle ?? procedureLabel}", $"""
                <p>Die Prozedur <strong>{encodedTitle}</strong> (Protokoll {encodedProtocolNumber}) für <strong>{encodedFullName}</strong> wurde nicht innerhalb von {deadlineDays} Tagen nach dem Versand unterzeichnet.</p>
                """),
            _ => ($"Procedura niepodpisana — {procedureTitle ?? procedureLabel}", $"""
                <p>Procedura <strong>{encodedTitle}</strong> (protokół {encodedProtocolNumber}) dla osoby <strong>{encodedFullName}</strong> nie została podpisana w ciągu {deadlineDays} dni od wysłania.</p>
                """),
        };
    }
}
