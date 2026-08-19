using System.Net;

namespace Tenebit.Application.Common;

/// <summary>
/// Centralized, email-client-safe transactional templates for the four supported languages.
/// The markup is table based, uses inline styles and deliberately contains no JavaScript.
/// </summary>
public static class EmailTemplates
{
    private const string Background = "#f5efe2";
    private const string Surface = "#ffffff";
    private const string SurfaceSoft = "#fdfbf6";
    private const string Text = "#221d18";
    private const string Muted = "#675f57";
    private const string Accent = "#a63a2e";
    private const string Border = "#d8cebc";

    private static string Normalize(string? language)
    {
        var lang = (language ?? "pl").Trim().ToLowerInvariant();
        return lang is "pl" or "en" or "es" or "de" ? lang : "pl";
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string CleanSubject(string value) => value.Replace("\r", " ").Replace("\n", " ").Trim();

    public static (string Subject, string Html) PasswordReset(string? language, string code, string link)
    {
        var lang = Normalize(language);
        var copy = lang switch
        {
            "en" => new EmailCopy(
                "Reset your Tenebit password",
                "Password reset",
                "Set a new password",
                "We received a request to change the password for your Tenebit account.",
                "Your one-time code",
                "The code is valid for 15 minutes. Paste all six digits into the reset form.",
                "Open the reset form",
                "If you did not request this change, do not share the code and ignore this message.",
                "Security notice"),
            "es" => new EmailCopy(
                "Restablece tu contraseña de Tenebit",
                "Restablecimiento de contraseña",
                "Establece una contraseña nueva",
                "Hemos recibido una solicitud para cambiar la contraseña de tu cuenta de Tenebit.",
                "Tu código de un solo uso",
                "El código es válido durante 15 minutos. Pega los seis dígitos en el formulario.",
                "Abrir el formulario",
                "Si no has solicitado este cambio, no compartas el código e ignora este mensaje.",
                "Aviso de seguridad"),
            "de" => new EmailCopy(
                "Tenebit-Passwort zurücksetzen",
                "Passwort zurücksetzen",
                "Neues Passwort festlegen",
                "Wir haben eine Anfrage erhalten, das Passwort deines Tenebit-Kontos zu ändern.",
                "Dein Einmalcode",
                "Der Code ist 15 Minuten gültig. Füge alle sechs Ziffern in das Formular ein.",
                "Formular öffnen",
                "Falls du diese Änderung nicht angefordert hast, teile den Code nicht und ignoriere diese Nachricht.",
                "Sicherheitshinweis"),
            _ => new EmailCopy(
                "Zresetuj hasło w Tenebit",
                "Reset hasła",
                "Ustaw nowe hasło",
                "Otrzymaliśmy prośbę o zmianę hasła do Twojego konta Tenebit.",
                "Twój jednorazowy kod",
                "Kod jest ważny przez 15 minut. Wklej wszystkie sześć cyfr w formularzu resetu.",
                "Otwórz formularz resetu",
                "Jeśli to nie Ty wysłałeś tę prośbę, nie udostępniaj kodu i zignoruj tę wiadomość.",
                "Informacja bezpieczeństwa")
        };

        return (CleanSubject(copy.Subject), BuildCodeEmail(lang, copy, code, link));
    }

    public static (string Subject, string Html) EmailVerification(string? language, string code, string link)
    {
        var lang = Normalize(language);
        var copy = lang switch
        {
            "en" => new EmailCopy(
                "Confirm your Tenebit email",
                "Email verification",
                "One last step",
                "Confirm that this mailbox belongs to you and choose the password that will protect your account.",
                "Your verification code",
                "The code is valid for 30 minutes. You can paste all six digits at once.",
                "Confirm email",
                "Never forward this message or share the code with another person.",
                "Account protection"),
            "es" => new EmailCopy(
                "Confirma tu correo de Tenebit",
                "Verificación de correo",
                "Solo falta un paso",
                "Confirma que este buzón te pertenece y elige la contraseña que protegerá tu cuenta.",
                "Tu código de verificación",
                "El código es válido durante 30 minutos. Puedes pegar los seis dígitos a la vez.",
                "Confirmar correo",
                "No reenvíes este mensaje ni compartas el código con otra persona.",
                "Protección de la cuenta"),
            "de" => new EmailCopy(
                "Tenebit-E-Mail bestätigen",
                "E-Mail-Bestätigung",
                "Nur noch ein Schritt",
                "Bestätige, dass dieses Postfach dir gehört, und wähle das Passwort zum Schutz deines Kontos.",
                "Dein Bestätigungscode",
                "Der Code ist 30 Minuten gültig. Du kannst alle sechs Ziffern auf einmal einfügen.",
                "E-Mail bestätigen",
                "Leite diese Nachricht nicht weiter und teile den Code mit keiner anderen Person.",
                "Kontoschutz"),
            _ => new EmailCopy(
                "Potwierdź e-mail w Tenebit",
                "Potwierdzenie e-maila",
                "Został jeden krok",
                "Potwierdź, że ta skrzynka należy do Ciebie, i wybierz hasło, które będzie chronić konto.",
                "Twój kod weryfikacyjny",
                "Kod jest ważny przez 30 minut. Możesz wkleić wszystkie sześć cyfr jednocześnie.",
                "Potwierdź adres e-mail",
                "Nie przekazuj tej wiadomości dalej i nie udostępniaj kodu innej osobie.",
                "Ochrona konta")
        };

        return (CleanSubject(copy.Subject), BuildCodeEmail(lang, copy, code, link));
    }

    public static (string Subject, string Html) OrganizationInvitation(string? language, string code, string link)
    {
        var lang = Normalize(language);
        var copy = lang switch
        {
            "en" => new EmailCopy(
                "Your invitation to Tenebit",
                "Organization invitation",
                "Your workspace is ready",
                "An administrator added you to an organization in Tenebit. Claim the account and set your private password.",
                "Your invitation code",
                "The code is valid for 24 hours. Paste all six digits into the account activation form.",
                "Activate account",
                "Only the owner of this mailbox should activate the account. Do not share the code.",
                "Private invitation"),
            "es" => new EmailCopy(
                "Tu invitación a Tenebit",
                "Invitación a la organización",
                "Tu espacio de trabajo está listo",
                "Un administrador te ha añadido a una organización de Tenebit. Reclama la cuenta y establece tu contraseña privada.",
                "Tu código de invitación",
                "El código es válido durante 24 horas. Pega los seis dígitos en el formulario de activación.",
                "Activar cuenta",
                "Solo el propietario de este buzón debe activar la cuenta. No compartas el código.",
                "Invitación privada"),
            "de" => new EmailCopy(
                "Deine Einladung zu Tenebit",
                "Organisationseinladung",
                "Dein Arbeitsbereich ist bereit",
                "Ein Administrator hat dich zu einer Organisation in Tenebit hinzugefügt. Übernimm das Konto und lege dein privates Passwort fest.",
                "Dein Einladungscode",
                "Der Code ist 24 Stunden gültig. Füge alle sechs Ziffern in das Aktivierungsformular ein.",
                "Konto aktivieren",
                "Nur der Inhaber dieses Postfachs sollte das Konto aktivieren. Teile den Code nicht.",
                "Private Einladung"),
            _ => new EmailCopy(
                "Twoje zaproszenie do Tenebit",
                "Zaproszenie do organizacji",
                "Twój obszar pracy jest gotowy",
                "Administrator dodał Cię do organizacji w Tenebit. Przejmij konto i ustaw własne, prywatne hasło.",
                "Twój kod zaproszenia",
                "Kod jest ważny przez 24 godziny. Wklej wszystkie sześć cyfr w formularzu aktywacji.",
                "Aktywuj konto",
                "Konto powinna aktywować wyłącznie osoba mająca dostęp do tej skrzynki. Nie udostępniaj kodu.",
                "Prywatne zaproszenie")
        };

        return (CleanSubject(copy.Subject), BuildCodeEmail(lang, copy, code, link));
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
        var name = Encode(firstName);
        var reference = Encode(protocolNumber);
        var assets = BuildList(assetNames);
        var procedures = BuildList(procedureTitles);

        var copy = lang switch
        {
            "en" => new ActionCopy(
                $"Equipment ready for confirmation | {protocolNumber}",
                "New equipment",
                $"Hi {name}, your equipment is ready",
                "Review the list below, open the secure confirmation page and confirm what you received.",
                "Equipment",
                "Policies and procedures included in this handover",
                "Confirmation reference",
                "Review and confirm receipt",
                "Your confirmation, including the date and time, will be recorded in Tenebit and remain available in the activity history."),
            "es" => new ActionCopy(
                $"Equipo listo para confirmar | {protocolNumber}",
                "Equipo nuevo",
                $"Hola, {name}. Tu equipo está listo",
                "Revisa la lista, abre la página segura y confirma lo que has recibido.",
                "Equipo",
                "Políticas y procedimientos incluidos en esta entrega",
                "Referencia de confirmación",
                "Revisar y confirmar recepción",
                "Tu confirmación, con fecha y hora, quedará registrada en Tenebit y seguirá disponible en el historial de actividad."),
            "de" => new ActionCopy(
                $"Ausrüstung zur Bestätigung bereit | {protocolNumber}",
                "Neue Ausrüstung",
                $"Hallo {name}, deine Ausrüstung ist bereit",
                "Prüfe die Liste, öffne die sichere Bestätigungsseite und bestätige, was du erhalten hast.",
                "Ausrüstung",
                "Richtlinien und Verfahren in dieser Übergabe",
                "Bestätigungsreferenz",
                "Prüfen und Empfang bestätigen",
                "Deine Bestätigung mit Datum und Uhrzeit wird in Tenebit gespeichert und bleibt im Aktivitätsverlauf verfügbar."),
            _ => new ActionCopy(
                $"Sprzęt gotowy do potwierdzenia | {protocolNumber}",
                "Nowy sprzęt",
                $"Cześć {name}, Twój sprzęt jest gotowy",
                "Sprawdź listę, otwórz bezpieczną stronę i potwierdź, co dokładnie zostało Ci przekazane.",
                "Sprzęt",
                "Regulaminy i procedury przekazane razem ze sprzętem",
                "Numer potwierdzenia",
                "Sprawdź i potwierdź odbiór",
                "Twoje potwierdzenie wraz z datą i godziną zostanie zapisane w Tenebit i pozostanie dostępne w historii zdarzeń.")
        };

        var content = $"""
            {BuildSection(copy.PrimaryListTitle, assets, EmptyListText(lang))}
            {BuildOptionalSection(copy.SecondaryListTitle, procedures)}
            {BuildReference(copy.ReferenceLabel, reference)}
            """;

        return (CleanSubject(copy.Subject), BuildShell(lang, copy.Eyebrow, copy.Title, copy.Intro, content, copy.ButtonLabel, acceptanceLink, copy.FooterNote));
    }

    public static (string Subject, string Html) OffboardingLink(
        string? language,
        string firstName,
        DateTimeOffset returnDueDate,
        string link)
    {
        var lang = Normalize(language);
        var name = Encode(firstName);
        var dueDate = Encode(returnDueDate.ToString("yyyy-MM-dd"));
        var copy = lang switch
        {
            "en" => new SimpleActionCopy("Return company equipment", "Equipment return", $"Hi {name}, let us close this properly", "Review the equipment and licenses assigned to you, then update every item on the secure return checklist.", "Return deadline", "Open return checklist", "The checklist is the current source of truth for the return process."),
            "es" => new SimpleActionCopy("Devolución del equipo de la empresa", "Devolución de equipo", $"Hola, {name}. Cerremos esto correctamente", "Revisa el equipo y las licencias que tienes asignados y actualiza cada elemento en la lista segura de devolución.", "Fecha límite", "Abrir lista de devolución", "La lista es la fuente actual de información para todo el proceso de devolución."),
            "de" => new SimpleActionCopy("Firmenausrüstung zurückgeben", "Ausrüstungsrückgabe", $"Hallo {name}, lass uns den Vorgang sauber abschließen", "Prüfe die dir zugewiesene Ausrüstung und Lizenzen und aktualisiere jedes Element in der sicheren Rückgabeliste.", "Rückgabefrist", "Rückgabeliste öffnen", "Die Checkliste ist die aktuelle verbindliche Übersicht für den Rückgabeprozess."),
            _ => new SimpleActionCopy("Zwrot sprzętu firmowego", "Zwrot sprzętu", $"Cześć {name}, zamknijmy ten proces porządnie", "Sprawdź sprzęt i licencje przypisane do Ciebie, a następnie zaktualizuj każdą pozycję na bezpiecznej liście zwrotu.", "Termin zwrotu", "Otwórz listę zwrotu", "Lista w Tenebit jest aktualnym źródłem informacji dla całego procesu zwrotu.")
        };

        return (CleanSubject(copy.Subject), BuildShell(lang, copy.Eyebrow, copy.Title, copy.Intro, BuildReference(copy.ReferenceLabel, dueDate), copy.ButtonLabel, link, copy.FooterNote));
    }

    public static (string Subject, string Html) AssetAuditLink(
        string? language,
        string firstName,
        DateTimeOffset dueDate,
        string link)
    {
        var lang = Normalize(language);
        var name = Encode(firstName);
        var due = Encode(dueDate.ToString("yyyy-MM-dd"));
        var copy = lang switch
        {
            "en" => new SimpleActionCopy("Confirm your assigned equipment", "Equipment check", $"Hi {name}, please check your equipment", "Open the secure form, compare the list with what you have and report any mismatch.", "Complete by", "Open confirmation form", "Your response is recorded directly in Tenebit and can be reviewed by the responsible team."),
            "es" => new SimpleActionCopy("Confirma tu equipo asignado", "Revisión de equipo", $"Hola, {name}. Revisa tu equipo", "Abre el formulario seguro, compara la lista con lo que tienes e informa de cualquier diferencia.", "Completar antes de", "Abrir formulario", "Tu respuesta se registra directamente en Tenebit y el equipo responsable puede revisarla."),
            "de" => new SimpleActionCopy("Zugewiesene Ausrüstung bestätigen", "Ausrüstungsprüfung", $"Hallo {name}, bitte prüfe deine Ausrüstung", "Öffne das sichere Formular, vergleiche die Liste mit deiner Ausrüstung und melde Abweichungen.", "Abschließen bis", "Bestätigungsformular öffnen", "Deine Antwort wird direkt in Tenebit gespeichert und kann vom zuständigen Team geprüft werden."),
            _ => new SimpleActionCopy("Potwierdź przypisany sprzęt", "Kontrola sprzętu", $"Cześć {name}, sprawdź proszę swój sprzęt", "Otwórz bezpieczny formularz, porównaj listę z tym, co masz, i zgłoś każdą niezgodność.", "Wykonaj do", "Otwórz formularz potwierdzenia", "Twoja odpowiedź zostanie zapisana bezpośrednio w Tenebit i będzie dostępna dla odpowiedzialnego zespołu.")
        };

        return (CleanSubject(copy.Subject), BuildShell(lang, copy.Eyebrow, copy.Title, copy.Intro, BuildReference(copy.ReferenceLabel, due), copy.ButtonLabel, link, copy.FooterNote));
    }

    public static (string Subject, string Html) ProcedureUnsignedAlert(
        string? language,
        string? procedureTitle,
        string protocolNumber,
        string? personFullName,
        int deadlineDays)
    {
        var lang = Normalize(language);
        var title = Encode(procedureTitle ?? EmptyValue(lang));
        var reference = Encode(protocolNumber);
        var person = Encode(personFullName ?? EmptyValue(lang));
        var copy = lang switch
        {
            "en" => new AlertCopy($"Procedure not confirmed | {procedureTitle ?? "procedure"}", "Attention required", "A procedure still needs confirmation", $"{person} has not confirmed the procedure within {deadlineDays} days of delivery.", "Procedure", "Person", "Confirmation reference"),
            "es" => new AlertCopy($"Procedimiento sin confirmar | {procedureTitle ?? "procedimiento"}", "Requiere atención", "Un procedimiento sigue pendiente", $"{person} no ha confirmado el procedimiento en los {deadlineDays} días posteriores al envío.", "Procedimiento", "Persona", "Referencia de confirmación"),
            "de" => new AlertCopy($"Verfahren nicht bestätigt | {procedureTitle ?? "Verfahren"}", "Aktion erforderlich", "Ein Verfahren wartet noch auf Bestätigung", $"{person} hat das Verfahren innerhalb von {deadlineDays} Tagen nach der Zustellung nicht bestätigt.", "Verfahren", "Person", "Bestätigungsreferenz"),
            _ => new AlertCopy($"Brak potwierdzenia procedury | {procedureTitle ?? "procedura"}", "Wymaga uwagi", "Procedura nadal czeka na potwierdzenie", $"{person} nie potwierdził procedury w ciągu {deadlineDays} dni od jej przekazania.", "Procedura", "Osoba", "Numer potwierdzenia")
        };

        var content = $"""
            {BuildReference(copy.ProcedureLabel, title)}
            {BuildReference(copy.PersonLabel, person)}
            {BuildReference(copy.ReferenceLabel, reference)}
            """;
        return (CleanSubject(copy.Subject), BuildShell(lang, copy.Eyebrow, copy.Title, copy.Intro, content, null, null, AlertFooter(lang)));
    }

    private static string BuildCodeEmail(string language, EmailCopy copy, string code, string link)
    {
        var encodedCode = Encode(code);
        var content = $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:26px 0 20px;border-collapse:separate;border-spacing:0;">
              <tr><td style="padding:0 0 9px;color:{Muted};font-family:Arial,sans-serif;font-size:12px;font-weight:700;letter-spacing:1.1px;text-transform:uppercase;">{Encode(copy.CodeLabel)}</td></tr>
              <tr><td align="center" style="padding:22px 14px;background:{Text};border:1px solid {Text};color:#ffffff;font-family:'Courier New',monospace;font-size:34px;font-weight:700;letter-spacing:12px;line-height:1;user-select:all;">{encodedCode}</td></tr>
              <tr><td style="padding:11px 13px;background:{SurfaceSoft};border:1px solid {Border};border-top:0;color:{Muted};font-family:Arial,sans-serif;font-size:13px;line-height:1.55;">{Encode(copy.CodeHint)}</td></tr>
            </table>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:18px 0 0;border-collapse:collapse;">
              <tr>
                <td width="34" valign="top" style="padding:1px 10px 0 0;color:{Accent};font-family:Georgia,serif;font-size:22px;font-weight:700;">!</td>
                <td style="color:{Muted};font-family:Arial,sans-serif;font-size:13px;line-height:1.55;"><strong style="color:{Text};">{Encode(copy.SecurityLabel)}:</strong> {Encode(copy.SecurityNote)}</td>
              </tr>
            </table>
            """;

        return BuildShell(language, copy.Eyebrow, copy.Title, copy.Intro, content, copy.ButtonLabel, link, LinkFallback(language));
    }

    private static string BuildShell(
        string language,
        string eyebrow,
        string title,
        string intro,
        string content,
        string? buttonLabel,
        string? link,
        string footerNote)
    {
        var safeLink = Encode(link);
        var action = string.IsNullOrWhiteSpace(buttonLabel) || string.IsNullOrWhiteSpace(link)
            ? string.Empty
            : $"""
                <table role="presentation" cellpadding="0" cellspacing="0" style="margin:28px 0 22px;border-collapse:separate;">
                  <tr><td bgcolor="{Accent}" style="border:2px solid {Accent};"><a href="{safeLink}" style="display:inline-block;padding:14px 22px;color:#ffffff;font-family:Arial,sans-serif;font-size:14px;font-weight:700;letter-spacing:.2px;text-decoration:none;">{Encode(buttonLabel)}</a></td></tr>
                </table>
                <p style="margin:0 0 8px;color:{Muted};font-family:Arial,sans-serif;font-size:11px;line-height:1.55;">{Encode(LinkFallbackLabel(language))}</p>
                <p style="margin:0;padding:11px 12px;background:{SurfaceSoft};border:1px solid {Border};color:{Muted};font-family:'Courier New',monospace;font-size:10px;line-height:1.55;overflow-wrap:anywhere;word-break:break-all;">{safeLink}</p>
                """;

        return $"""
            <!doctype html>
            <html lang="{Encode(language)}">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:{Background};color:{Text};">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" bgcolor="{Background}" style="width:100%;background:{Background};border-collapse:collapse;">
                <tr><td align="center" style="padding:34px 14px;">
                  <table role="presentation" width="620" cellpadding="0" cellspacing="0" style="width:100%;max-width:620px;border-collapse:separate;border-spacing:0;">
                    <tr><td style="padding:0 0 18px;">
                      <table role="presentation" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
                        <tr>
                          <td width="42" height="42" align="center" valign="middle" bgcolor="{Text}" style="width:42px;height:42px;background:{Text};color:#ffffff;font-family:Georgia,serif;font-size:20px;font-weight:700;">T</td>
                          <td style="padding-left:12px;color:{Text};font-family:Georgia,serif;font-size:21px;font-weight:700;letter-spacing:-.4px;">Tenebit<br><span style="color:{Muted};font-family:Arial,sans-serif;font-size:10px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;">Asset operations</span></td>
                        </tr>
                      </table>
                    </td></tr>
                    <tr><td bgcolor="{Surface}" style="background:{Surface};border:1px solid {Border};box-shadow:6px 6px 0 rgba(34,29,24,.12);">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
                        <tr><td height="7" bgcolor="{Accent}" style="height:7px;background:{Accent};font-size:0;line-height:0;">&nbsp;</td></tr>
                        <tr><td style="padding:38px 38px 34px;">
                          <p style="margin:0 0 12px;color:{Accent};font-family:Arial,sans-serif;font-size:11px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;">{Encode(eyebrow)}</p>
                          <h1 style="margin:0;color:{Text};font-family:Georgia,'Times New Roman',serif;font-size:34px;font-weight:600;letter-spacing:-1px;line-height:1.12;">{title}</h1>
                          <p style="margin:18px 0 0;color:{Muted};font-family:Arial,sans-serif;font-size:16px;line-height:1.68;">{intro}</p>
                          {content}
                          {action}
                        </td></tr>
                        <tr><td style="padding:20px 38px;background:{SurfaceSoft};border-top:1px solid {Border};color:{Muted};font-family:Arial,sans-serif;font-size:12px;line-height:1.6;">{Encode(footerNote)}</td></tr>
                      </table>
                    </td></tr>
                    <tr><td align="center" style="padding:22px 16px 0;color:{Muted};font-family:Arial,sans-serif;font-size:11px;line-height:1.6;">Tenebit | {Encode(TransactionalLabel(language))}<br>{Encode(AutomatedMessage(language))}</td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildList(IEnumerable<string> values) =>
        string.Join(string.Empty, values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => $"<li style=\"margin:0 0 8px;padding:0;color:{Text};font-family:Arial,sans-serif;font-size:14px;line-height:1.5;\">{Encode(value)}</li>"));

    private static string BuildSection(string title, string items, string emptyText) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:26px 0 0;border-collapse:collapse;">
          <tr><td style="padding:0 0 9px;color:{Muted};font-family:Arial,sans-serif;font-size:12px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{Encode(title)}</td></tr>
          <tr><td style="padding:17px 18px;background:{SurfaceSoft};border:1px solid {Border};"><ul style="margin:0;padding-left:20px;">{(string.IsNullOrEmpty(items) ? $"<li style=\"color:{Muted};font-family:Arial,sans-serif;font-size:14px;\">{Encode(emptyText)}</li>" : items)}</ul></td></tr>
        </table>
        """;

    private static string BuildOptionalSection(string title, string items) => string.IsNullOrEmpty(items)
        ? string.Empty
        : BuildSection(title, items, string.Empty);

    private static string BuildReference(string label, string value) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:16px 0 0;border-collapse:collapse;">
          <tr>
            <td style="padding:13px 15px;background:{SurfaceSoft};border:1px solid {Border};color:{Muted};font-family:Arial,sans-serif;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.8px;">{Encode(label)}</td>
            <td align="right" style="padding:13px 15px;background:{Surface};border:1px solid {Border};border-left:0;color:{Text};font-family:'Courier New',monospace;font-size:13px;font-weight:700;overflow-wrap:anywhere;">{value}</td>
          </tr>
        </table>
        """;

    private static string EmptyListText(string language) => language switch
    {
        "en" => "No items",
        "es" => "Sin elementos",
        "de" => "Keine Einträge",
        _ => "Brak pozycji"
    };

    private static string EmptyValue(string language) => language switch
    {
        "en" => "Not provided",
        "es" => "Sin datos",
        "de" => "Nicht angegeben",
        _ => "Brak danych"
    };

    private static string LinkFallback(string language) => language switch
    {
        "en" => "Use the button above or paste the secure link into your browser. Never share a one-time code.",
        "es" => "Usa el botón superior o pega el enlace seguro en tu navegador. No compartas nunca un código de un solo uso.",
        "de" => "Nutze die Schaltfläche oben oder füge den sicheren Link in deinen Browser ein. Teile niemals einen Einmalcode.",
        _ => "Użyj przycisku powyżej albo wklej bezpieczny link do przeglądarki. Nigdy nie udostępniaj jednorazowego kodu."
    };

    private static string LinkFallbackLabel(string language) => language switch
    {
        "en" => "If the button does not work, paste this address into your browser:",
        "es" => "Si el botón no funciona, pega esta dirección en el navegador:",
        "de" => "Falls die Schaltfläche nicht funktioniert, füge diese Adresse in den Browser ein:",
        _ => "Jeśli przycisk nie działa, wklej ten adres do przeglądarki:"
    };

    private static string TransactionalLabel(string language) => language switch
    {
        "en" => "transactional message",
        "es" => "mensaje transaccional",
        "de" => "Transaktionsnachricht",
        _ => "wiadomość transakcyjna"
    };

    private static string AutomatedMessage(string language) => language switch
    {
        "en" => "This message was generated automatically. Please do not reply.",
        "es" => "Este mensaje se ha generado automáticamente. No respondas.",
        "de" => "Diese Nachricht wurde automatisch erstellt. Bitte nicht antworten.",
        _ => "Ta wiadomość została wygenerowana automatycznie. Nie odpowiadaj na nią."
    };

    private static string AlertFooter(string language) => language switch
    {
        "en" => "Open Tenebit to review the current status and take the appropriate action.",
        "es" => "Abre Tenebit para revisar el estado actual y realizar la acción adecuada.",
        "de" => "Öffne Tenebit, um den aktuellen Status zu prüfen und die passende Aktion auszuführen.",
        _ => "Otwórz Tenebit, sprawdź aktualny status i wykonaj odpowiednią akcję."
    };

    private sealed record EmailCopy(string Subject, string Eyebrow, string Title, string Intro, string CodeLabel, string CodeHint, string ButtonLabel, string SecurityNote, string SecurityLabel);
    private sealed record ActionCopy(string Subject, string Eyebrow, string Title, string Intro, string PrimaryListTitle, string SecondaryListTitle, string ReferenceLabel, string ButtonLabel, string FooterNote);
    private sealed record SimpleActionCopy(string Subject, string Eyebrow, string Title, string Intro, string ReferenceLabel, string ButtonLabel, string FooterNote);
    private sealed record AlertCopy(string Subject, string Eyebrow, string Title, string Intro, string ProcedureLabel, string PersonLabel, string ReferenceLabel);
}
