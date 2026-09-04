using System.Net;

namespace Tenebit.Application.Common;

/// <summary>
/// Centralized, email-client-safe transactional templates for every supported UI language.
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

    private static string Normalize(string? language) => AppLanguages.Normalize(language);

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
            "it" => new EmailCopy(
                "Reimposta la password di Tenebit",
                "Reimpostazione della password",
                "Imposta una nuova password",
                "Abbiamo ricevuto una richiesta di modifica della password del tuo account Tenebit.",
                "Il tuo codice monouso",
                "Il codice è valido per 15 minuti. Incolla tutte e sei le cifre nel modulo.",
                "Apri il modulo",
                "Se non hai richiesto questa modifica, non condividere il codice e ignora questo messaggio.",
                "Avviso di sicurezza"),
            "fr" => new EmailCopy(
                "Réinitialisez votre mot de passe Tenebit",
                "Réinitialisation du mot de passe",
                "Définissez un nouveau mot de passe",
                "Nous avons reçu une demande de modification du mot de passe de votre compte Tenebit.",
                "Votre code à usage unique",
                "Le code est valable 15 minutes. Collez les six chiffres dans le formulaire.",
                "Ouvrir le formulaire",
                "Si vous n'êtes pas à l'origine de cette demande, ne partagez pas le code et ignorez ce message.",
                "Avis de sécurité"),
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
            "it" => new EmailCopy(
                "Conferma la tua e-mail Tenebit",
                "Verifica dell'e-mail",
                "Un ultimo passaggio",
                "Conferma che questa casella di posta è tua e scegli la password che proteggerà il tuo account.",
                "Il tuo codice di verifica",
                "Il codice è valido per 30 minuti. Puoi incollare tutte e sei le cifre in una volta.",
                "Conferma l'e-mail",
                "Non inoltrare mai questo messaggio e non condividere il codice con altre persone.",
                "Protezione dell'account"),
            "fr" => new EmailCopy(
                "Confirmez votre e-mail Tenebit",
                "Vérification de l'e-mail",
                "Une dernière étape",
                "Confirmez que cette boîte de réception vous appartient et choisissez le mot de passe qui protégera votre compte.",
                "Votre code de vérification",
                "Le code est valable 30 minutes. Vous pouvez coller les six chiffres en une seule fois.",
                "Confirmer l'e-mail",
                "Ne transférez jamais ce message et ne communiquez le code à personne.",
                "Protection du compte"),
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
            "it" => new EmailCopy(
                "Il tuo invito a Tenebit",
                "Invito all'organizzazione",
                "Il tuo spazio di lavoro è pronto",
                "Un amministratore ti ha aggiunto a un'organizzazione in Tenebit. Rivendica l'account e imposta la tua password personale.",
                "Il tuo codice di invito",
                "Il codice è valido per 24 ore. Incolla tutte e sei le cifre nel modulo di attivazione.",
                "Attiva l'account",
                "Solo il titolare di questa casella di posta dovrebbe attivare l'account. Non condividere il codice.",
                "Invito personale"),
            "fr" => new EmailCopy(
                "Votre invitation à Tenebit",
                "Invitation à l'organisation",
                "Votre espace de travail est prêt",
                "Un administrateur vous a ajouté à une organisation dans Tenebit. Revendiquez le compte et définissez votre mot de passe personnel.",
                "Votre code d'invitation",
                "Le code est valable 24 heures. Collez les six chiffres dans le formulaire d'activation.",
                "Activer le compte",
                "Seul le titulaire de cette boîte de réception doit activer le compte. Ne partagez pas le code.",
                "Invitation personnelle"),
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
            "it" => new ActionCopy(
                $"Attrezzatura pronta per la conferma | {protocolNumber}",
                "Nuova attrezzatura",
                $"Ciao {name}, la tua attrezzatura è pronta",
                "Controlla l'elenco qui sotto, apri la pagina sicura di conferma e conferma ciò che hai ricevuto.",
                "Attrezzatura",
                "Regolamenti e procedure inclusi in questa consegna",
                "Riferimento della conferma",
                "Controlla e conferma la ricezione",
                "La tua conferma, con data e ora, verrà registrata in Tenebit e resterà disponibile nella cronologia delle attività."),
            "fr" => new ActionCopy(
                $"Matériel prêt à être confirmé | {protocolNumber}",
                "Nouveau matériel",
                $"Bonjour {name}, votre matériel est prêt",
                "Consultez la liste ci-dessous, ouvrez la page sécurisée de confirmation et confirmez ce que vous avez reçu.",
                "Matériel",
                "Règlements et procédures inclus dans cette remise",
                "Référence de la confirmation",
                "Vérifier et confirmer la réception",
                "Votre confirmation, date et heure incluses, sera enregistrée dans Tenebit et restera disponible dans l'historique des activités."),
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
            "it" => new SimpleActionCopy("Restituisci l'attrezzatura aziendale", "Restituzione dell'attrezzatura", $"Ciao {name}, chiudiamo la pratica come si deve", "Controlla l'attrezzatura e le licenze a te assegnate, poi aggiorna ogni voce nell'elenco sicuro di restituzione.", "Termine di restituzione", "Apri l'elenco di restituzione", "L'elenco è il riferimento aggiornato per l'intero processo di restituzione."),
            "fr" => new SimpleActionCopy("Restituer le matériel de l'entreprise", "Restitution du matériel", $"Bonjour {name}, clôturons cela correctement", "Vérifiez le matériel et les licences qui vous sont attribués, puis mettez à jour chaque élément dans la liste sécurisée de restitution.", "Date limite de restitution", "Ouvrir la liste de restitution", "La liste fait référence pour l'ensemble du processus de restitution."),
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
            "it" => new SimpleActionCopy("Conferma l'attrezzatura a te assegnata", "Verifica dell'attrezzatura", $"Ciao {name}, controlla la tua attrezzatura", "Apri il modulo sicuro, confronta l'elenco con ciò che possiedi e segnala eventuali differenze.", "Da completare entro", "Apri il modulo di conferma", "La tua risposta viene registrata direttamente in Tenebit e può essere verificata dal team responsabile."),
            "fr" => new SimpleActionCopy("Confirmez le matériel qui vous est attribué", "Vérification du matériel", $"Bonjour {name}, vérifiez votre matériel", "Ouvrez le formulaire sécurisé, comparez la liste avec ce que vous détenez et signalez toute différence.", "À compléter avant le", "Ouvrir le formulaire de confirmation", "Votre réponse est enregistrée directement dans Tenebit et peut être consultée par l'équipe responsable."),
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
            "it" => new AlertCopy($"Procedura non confermata | {procedureTitle ?? "procedura"}", "Richiede attenzione", "Una procedura attende ancora la conferma", $"{person} non ha confermato la procedura entro {deadlineDays} giorni dalla consegna.", "Procedura", "Persona", "Riferimento della conferma"),
            "fr" => new AlertCopy($"Procédure non confirmée | {procedureTitle ?? "procédure"}", "Action requise", "Une procédure attend toujours confirmation", $"{person} n'a pas confirmé la procédure dans les {deadlineDays} jours suivant sa remise.", "Procédure", "Personne", "Référence de la confirmation"),
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

    /// <summary>Sent the moment a paid plan actually takes effect - a brand-new subscription, or an
    /// in-app upgrade, both apply immediately. A scheduled downgrade uses <see cref="PlanChangeScheduled"/>
    /// instead, since nothing has actually changed yet at the point that's sent.</summary>
    public static (string Subject, string Html) PlanChanged(string? language, string planName, string dashboardLink)
    {
        var lang = Normalize(language);
        var plan = Encode(planName);
        var copy = lang switch
        {
            "en" => new SimpleActionCopy(
                $"Welcome to {planName}! 🎉",
                "Thank you",
                $"Welcome to {plan}!",
                "That's great news - your account just got more room to grow. Thank you for being with Tenebit; we'll keep working to make it worth your while.",
                "Your plan",
                "Go to dashboard",
                "Questions about your invoice or plan? Just reply to this email - we're happy to help."),
            "es" => new SimpleActionCopy(
                $"¡Bienvenido al plan {planName}! 🎉",
                "Gracias",
                $"¡Bienvenido al plan {plan}!",
                "Es una gran noticia: tu cuenta acaba de ganar más margen para crecer. Gracias por confiar en Tenebit; seguiremos trabajando para que merezca la pena.",
                "Tu plan",
                "Ir al panel",
                "¿Dudas sobre tu factura o tu plan? Responde a este correo, estaremos encantados de ayudarte."),
            "de" => new SimpleActionCopy(
                $"Willkommen im Tarif {planName}! 🎉",
                "Danke",
                $"Willkommen im Tarif {plan}!",
                "Das ist eine tolle Nachricht - dein Konto hat gerade mehr Raum zum Wachsen bekommen. Danke, dass du Tenebit nutzt; wir arbeiten weiter daran, dass es sich lohnt.",
                "Dein Tarif",
                "Zum Dashboard",
                "Fragen zu deiner Rechnung oder deinem Tarif? Antworte einfach auf diese E-Mail - wir helfen gerne."),
            "it" => new SimpleActionCopy(
                $"Benvenuto nel piano {planName}! 🎉",
                "Grazie",
                $"Benvenuto nel piano {plan}!",
                "Ottima notizia: il tuo account ha appena guadagnato più spazio per crescere. Grazie per essere con Tenebit; continueremo a impegnarci per ripagare questa scelta.",
                "Il tuo piano",
                "Vai alla dashboard",
                "Domande sulla fattura o sul piano? Rispondi pure a questa e-mail, saremo felici di aiutarti."),
            "fr" => new SimpleActionCopy(
                $"Bienvenue dans le forfait {planName} ! 🎉",
                "Merci",
                $"Bienvenue dans le forfait {plan} !",
                "C'est une excellente nouvelle - votre compte vient de gagner plus de marge de manœuvre. Merci de faire confiance à Tenebit ; nous continuons à faire en sorte que cela en vaille la peine.",
                "Votre forfait",
                "Aller au tableau de bord",
                "Une question sur votre facture ou votre forfait ? Répondez simplement à cet e-mail, nous serons ravis de vous aider."),
            _ => new SimpleActionCopy(
                $"Witaj na planie {planName}! 🎉",
                "Dziękujemy",
                $"Witaj na planie {plan}!",
                "To świetna wiadomość - Twoje konto właśnie zyskało więcej przestrzeni do rozwoju. Dziękujemy, że jesteś z Tenebit - robimy wszystko, żeby ta decyzja się opłaciła.",
                "Twój plan",
                "Przejdź do panelu",
                "Masz pytania o fakturę albo plan? Po prostu odpowiedz na tego e-maila - chętnie pomożemy.")
        };

        return (CleanSubject(copy.Subject), BuildShell(lang, copy.Eyebrow, copy.Title, copy.Intro, BuildReference(copy.ReferenceLabel, plan), copy.ButtonLabel, dashboardLink, copy.FooterNote));
    }

    /// <summary>Sent the moment a downgrade is scheduled - the org keeps its current plan until
    /// <paramref name="effectiveAt"/>, so this deliberately doesn't say the switch already happened.</summary>
    public static (string Subject, string Html) PlanChangeScheduled(string? language, string planName, DateTimeOffset effectiveAt, string manageLink)
    {
        var lang = Normalize(language);
        var plan = Encode(planName);
        var date = Encode(effectiveAt.ToString("yyyy-MM-dd"));
        var copy = lang switch
        {
            "en" => new SimpleActionCopy(
                $"Your plan will switch to {planName} soon",
                "Change scheduled",
                "Thank you for staying with us",
                $"We've scheduled your plan change to {plan}. Until then, everything stays exactly as it is - you keep every bit of what you already paid for.",
                "New plan starts",
                "Manage your plan",
                "Changed your mind? You can cancel this scheduled change any time before it takes effect."),
            "es" => new SimpleActionCopy(
                $"Tu plan cambiará pronto a {planName}",
                "Cambio programado",
                "Gracias por seguir con nosotros",
                $"Hemos programado el cambio de tu plan a {plan}. Hasta entonces, todo sigue exactamente igual: conservas todo lo que ya has pagado.",
                "El nuevo plan empieza el",
                "Gestionar tu plan",
                "¿Has cambiado de opinión? Puedes cancelar este cambio programado en cualquier momento antes de que entre en vigor."),
            "de" => new SimpleActionCopy(
                $"Dein Tarif wechselt bald zu {planName}",
                "Wechsel geplant",
                "Danke, dass du bei uns bleibst",
                $"Wir haben deinen Tarifwechsel zu {plan} geplant. Bis dahin bleibt alles genau so, wie es ist - du behältst alles, wofür du bereits bezahlt hast.",
                "Neuer Tarif ab",
                "Tarif verwalten",
                "Hast du es dir anders überlegt? Du kannst diesen geplanten Wechsel jederzeit abbrechen, bevor er wirksam wird."),
            "it" => new SimpleActionCopy(
                $"Il tuo piano passerà presto a {planName}",
                "Cambio programmato",
                "Grazie per essere rimasto con noi",
                $"Abbiamo programmato il passaggio del tuo piano a {plan}. Fino ad allora tutto resta esattamente com'è - mantieni tutto ciò per cui hai già pagato.",
                "Il nuovo piano inizia il",
                "Gestisci il tuo piano",
                "Hai cambiato idea? Puoi annullare questo cambio programmato in qualsiasi momento prima che entri in vigore."),
            "fr" => new SimpleActionCopy(
                $"Votre forfait passera bientôt à {planName}",
                "Changement planifié",
                "Merci de rester avec nous",
                $"Nous avons planifié le passage de votre forfait à {plan}. D'ici là, rien ne change - vous conservez tout ce pour quoi vous avez déjà payé.",
                "Le nouveau forfait commence le",
                "Gérer votre forfait",
                "Vous avez changé d'avis ? Vous pouvez annuler ce changement planifié à tout moment avant sa prise d'effet."),
            _ => new SimpleActionCopy(
                $"Twój plan zmieni się wkrótce na {planName}",
                "Zmiana zaplanowana",
                "Dziękujemy, że zostajesz z nami",
                $"Zaplanowaliśmy zmianę Twojego planu na {plan}. Do tego czasu wszystko zostaje dokładnie tak, jak jest - zachowujesz wszystko, za co już zapłaciłeś/aś.",
                "Nowy plan od",
                "Zarządzaj planem",
                "Zmieniłeś/aś zdanie? Możesz anulować tę zaplanowaną zmianę w dowolnym momencie, zanim wejdzie w życie.")
        };

        return (CleanSubject(copy.Subject), BuildShell(lang, copy.Eyebrow, copy.Title, copy.Intro, BuildReference(copy.ReferenceLabel, date), copy.ButtonLabel, manageLink, copy.FooterNote));
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
        "it" => "Nessun elemento",
        "fr" => "Aucun élément",
        _ => "Brak pozycji"
    };

    private static string EmptyValue(string language) => language switch
    {
        "en" => "Not provided",
        "es" => "Sin datos",
        "de" => "Nicht angegeben",
        "it" => "Non indicato",
        "fr" => "Non renseigné",
        _ => "Brak danych"
    };

    private static string LinkFallback(string language) => language switch
    {
        "en" => "Use the button above or paste the secure link into your browser. Never share a one-time code.",
        "es" => "Usa el botón superior o pega el enlace seguro en tu navegador. No compartas nunca un código de un solo uso.",
        "de" => "Nutze die Schaltfläche oben oder füge den sicheren Link in deinen Browser ein. Teile niemals einen Einmalcode.",
        "it" => "Usa il pulsante qui sopra oppure incolla il link sicuro nel tuo browser. Non condividere mai un codice monouso.",
        "fr" => "Utilisez le bouton ci-dessus ou collez le lien sécurisé dans votre navigateur. Ne partagez jamais un code à usage unique.",
        _ => "Użyj przycisku powyżej albo wklej bezpieczny link do przeglądarki. Nigdy nie udostępniaj jednorazowego kodu."
    };

    private static string LinkFallbackLabel(string language) => language switch
    {
        "en" => "If the button does not work, paste this address into your browser:",
        "es" => "Si el botón no funciona, pega esta dirección en el navegador:",
        "de" => "Falls die Schaltfläche nicht funktioniert, füge diese Adresse in den Browser ein:",
        "it" => "Se il pulsante non funziona, incolla questo indirizzo nel browser:",
        "fr" => "Si le bouton ne fonctionne pas, collez cette adresse dans votre navigateur :",
        _ => "Jeśli przycisk nie działa, wklej ten adres do przeglądarki:"
    };

    private static string TransactionalLabel(string language) => language switch
    {
        "en" => "transactional message",
        "es" => "mensaje transaccional",
        "de" => "Transaktionsnachricht",
        "it" => "messaggio transazionale",
        "fr" => "message transactionnel",
        _ => "wiadomość transakcyjna"
    };

    private static string AutomatedMessage(string language) => language switch
    {
        "en" => "This message was generated automatically. Please do not reply.",
        "es" => "Este mensaje se ha generado automáticamente. No respondas.",
        "de" => "Diese Nachricht wurde automatisch erstellt. Bitte nicht antworten.",
        "it" => "Questo messaggio è stato generato automaticamente. Non rispondere.",
        "fr" => "Ce message a été généré automatiquement. Merci de ne pas y répondre.",
        _ => "Ta wiadomość została wygenerowana automatycznie. Nie odpowiadaj na nią."
    };

    private static string AlertFooter(string language) => language switch
    {
        "en" => "Open Tenebit to review the current status and take the appropriate action.",
        "es" => "Abre Tenebit para revisar el estado actual y realizar la acción adecuada.",
        "de" => "Öffne Tenebit, um den aktuellen Status zu prüfen und die passende Aktion auszuführen.",
        "it" => "Apri Tenebit per verificare lo stato attuale ed eseguire l'azione appropriata.",
        "fr" => "Ouvrez Tenebit pour consulter l'état actuel et effectuer l'action appropriée.",
        _ => "Otwórz Tenebit, sprawdź aktualny status i wykonaj odpowiednią akcję."
    };

    private sealed record EmailCopy(string Subject, string Eyebrow, string Title, string Intro, string CodeLabel, string CodeHint, string ButtonLabel, string SecurityNote, string SecurityLabel);
    private sealed record ActionCopy(string Subject, string Eyebrow, string Title, string Intro, string PrimaryListTitle, string SecondaryListTitle, string ReferenceLabel, string ButtonLabel, string FooterNote);
    private sealed record SimpleActionCopy(string Subject, string Eyebrow, string Title, string Intro, string ReferenceLabel, string ButtonLabel, string FooterNote);
    private sealed record AlertCopy(string Subject, string Eyebrow, string Title, string Intro, string ProcedureLabel, string PersonLabel, string ReferenceLabel);
}
