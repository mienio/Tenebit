using System.Text.RegularExpressions;

namespace Tenebit.Application.Common;

/// <summary>
/// Translates the hardcoded Polish error/validation messages produced across the application layer
/// (via <see cref="Error"/> factory methods and <see cref="DomainException"/>) into the requested UI
/// language before they are returned to the client. Polish remains the canonical/source language for
/// these messages; this translator is a lookup layer, not a source of truth, so callers keep writing
/// plain Polish messages as before.
/// </summary>
public static class ErrorMessageTranslator
{
    /// <summary>
    /// Jeden komunikat we wszystkich językach docelowych. Angielski, hiszpański i niemiecki są
    /// wymagane (mają pełne pokrycie od początku), włoski i francuski są opcjonalne - brakujący
    /// przekład spada na angielski, dokładnie tak jak <c>t()</c> na froncie. Dzięki temu nowy język
    /// można uzupełniać partiami, bez etapu, w którym aplikacja pokazuje puste napisy.
    /// </summary>
    private sealed record Localized(string En, string Es, string De, string? It = null, string? Fr = null)
    {
        public string For(string language) => language switch
        {
            "en" => En,
            "es" => Es,
            "de" => De,
            "it" => It ?? En,
            "fr" => Fr ?? En,
            _ => En,
        };
    }

    private static readonly Dictionary<string, Localized> Exact = new()
    {
        ["Aby przejść na plan Pro, użyj płatności Stripe (checkout)."] = new("To move to the Pro plan, use Stripe checkout.", "Para pasar al plan Pro, usa el pago con Stripe (checkout).", "Um auf den Pro-Plan zu wechseln, nutzen Sie den Stripe-Checkout.", "Per passare al piano Pro, usa il checkout Stripe.", "Pour passer au forfait Pro, utilisez le paiement Stripe (checkout)."),
        ["Aktywo nie istnieje."] = new("The asset does not exist.", "El activo no existe.", "Das Asset existiert nicht.", "L'asset non esiste.", "L'actif n'existe pas."),
        ["Aktywo nie jest dostępne do wydania."] = new("The asset is not available for assignment.", "El activo no está disponible para su entrega.", "Das Asset steht nicht zur Übergabe zur Verfügung.", "L'asset non è disponibile per l'assegnazione.", "L'actif n'est pas disponible pour la remise."),
        ["Akceptacja regulaminu i polityki prywatności jest wymagana."] = new("Acceptance of the terms and privacy policy is required.", "Debes aceptar los términos y la política de privacidad.", "Die Nutzungsbedingungen und die Datenschutzerklärung müssen akzeptiert werden.", "È necessario accettare i termini e l'informativa sulla privacy.", "L'acceptation des conditions générales et de la politique de confidentialité est requise."),
        ["Brak oczekującej kontroli dla tego aktywa."] = new("There is no pending inspection for this asset.", "No hay ninguna inspección pendiente para este activo.", "Für dieses Asset liegt keine ausstehende Kontrolle vor.", "Non ci sono controlli in sospeso per questo asset.", "Aucun contrôle en attente pour cet actif."),
        ["Brak uprawnień do tej operacji."] = new("You do not have permission to perform this operation.", "No tienes permiso para realizar esta operación.", "Sie haben keine Berechtigung für diesen Vorgang.", "Non hai i permessi per eseguire questa operazione.", "Vous n'avez pas les droits pour effectuer cette opération."),
        ["Brak wolnych miejsc w tej licencji."] = new("There are no free seats left on this license.", "No quedan plazas libres en esta licencia.", "Für diese Lizenz sind keine freien Plätze mehr verfügbar.", "Non ci sono posti liberi disponibili per questa licenza.", "Il ne reste aucune place disponible sur cette licence."),
        ["Cena zakupu nie może być ujemna."] = new("The purchase price cannot be negative.", "El precio de compra no puede ser negativo.", "Der Kaufpreis darf nicht negativ sein.", "Il prezzo di acquisto non può essere negativo.", "Le prix d'achat ne peut pas être négatif."),
        ["Co najmniej jedno aktywo nie jest dostępne do wydania."] = new("At least one asset is not available for assignment.", "Al menos un activo no está disponible para su entrega.", "Mindestens ein Asset steht nicht zur Übergabe zur Verfügung.", "Almeno un asset non è disponibile per l'assegnazione.", "Au moins un actif n'est pas disponible pour la remise."),
        ["Dodaj co najmniej jedno aktywo do wydania."] = new("Add at least one asset to the assignment.", "Añade al menos un activo a la entrega.", "Fügen Sie der Übergabe mindestens ein Asset hinzu.", "Aggiungi almeno un asset all'assegnazione.", "Ajoutez au moins un actif à la remise."),
        ["Dostawca logowania nie udostępnił adresu e-mail. Wyraź zgodę na udostępnienie e-maila i spróbuj ponownie."] = new("The login provider did not share an email address. Grant permission to share your email and try again.", "El proveedor de inicio de sesión no compartió una dirección de correo. Concede permiso para compartir tu correo e inténtalo de nuevo.", "Der Anmeldeanbieter hat keine E-Mail-Adresse übermittelt. Erteilen Sie die Freigabe Ihrer E-Mail und versuchen Sie es erneut.", "Il provider di accesso non ha condiviso un indirizzo e-mail. Concedi il permesso di condividere la tua e-mail e riprova.", "Le fournisseur de connexion n'a pas partagé d'adresse e-mail. Autorisez le partage de votre e-mail et réessayez."),
        ["Dozwolone są tylko zdjęcia w formacie JPEG, PNG lub WebP."] = new("Only JPEG, PNG or WebP photos are allowed.", "Solo se permiten fotos en formato JPEG, PNG o WebP.", "Es sind nur Fotos im Format JPEG, PNG oder WebP zulässig.", "Sono ammesse solo foto in formato JPEG, PNG o WebP.", "Seules les photos aux formats JPEG, PNG ou WebP sont autorisées."),
        ["Dozwolone są tylko obrazy w formacie JPEG, PNG lub WebP."] = new("Only JPEG, PNG or WebP images are allowed.", "Solo se permiten imágenes en formato JPEG, PNG o WebP.", "Es sind nur Bilder im Format JPEG, PNG oder WebP zulässig.", "Sono ammesse solo immagini in formato JPEG, PNG o WebP.", "Seules les images aux formats JPEG, PNG ou WebP sont autorisées."),
        ["Logo może mieć maksymalnie 512 KB."] = new("The logo may be at most 512 KB.", "El logotipo puede pesar como máximo 512 KB.", "Das Logo darf höchstens 512 KB groß sein.", "Il logo può pesare al massimo 512 KB.", "Le logo ne doit pas dépasser 512 Ko."),
        ["Dwuskładnikowe uwierzytelnianie nie jest włączone."] = new("Two-factor authentication is not enabled.", "La autenticación de dos factores no está activada.", "Die Zwei-Faktor-Authentifizierung ist nicht aktiviert.", "L'autenticazione a due fattori non è attiva.", "L'authentification à deux facteurs n'est pas activée."),
        ["E-mail z tego dostawcy nie jest zweryfikowany. Zaloguj się hasłem i połącz konto w ustawieniach."] = new("The email from this provider is not verified. Log in with your password and link the account in settings.", "El correo de este proveedor no está verificado. Inicia sesión con tu contraseña y vincula la cuenta en ajustes.", "Die E-Mail-Adresse dieses Anbieters ist nicht verifiziert. Melden Sie sich mit Ihrem Passwort an und verknüpfen Sie das Konto in den Einstellungen.", "L'e-mail fornita da questo provider non è verificata. Accedi con la password e collega l'account nelle impostazioni.", "L'e-mail de ce fournisseur n'est pas vérifiée. Connectez-vous avec votre mot de passe et associez le compte dans les paramètres."),
        ["Etykieta pola własnego jest wymagana."] = new("The custom field label is required.", "La etiqueta del campo personalizado es obligatoria.", "Die Bezeichnung des benutzerdefinierten Felds ist erforderlich.", "L'etichetta del campo personalizzato è obbligatoria.", "Le libellé du champ personnalisé est obligatoire."),
        ["Etykieta statusu jest wymagana."] = new("The status label is required.", "La etiqueta del estado es obligatoria.", "Die Statusbezeichnung ist erforderlich.", "L'etichetta dello stato è obbligatoria.", "Le libellé du statut est obligatoire."),
        ["Hasło musi mieć co najmniej 8 znaków."] = new("The password must be at least 8 characters long.", "La contraseña debe tener al menos 8 caracteres.", "Das Passwort muss mindestens 8 Zeichen lang sein.", "La password deve contenere almeno 8 caratteri.", "Le mot de passe doit comporter au moins 8 caractères."),
        ["Imię i nazwisko są wymagane."] = new("First and last name are required.", "El nombre y los apellidos son obligatorios.", "Vor- und Nachname sind erforderlich.", "Nome e cognome sono obbligatori.", "Le prénom et le nom sont obligatoires."),
        ["Kategoria nie istnieje."] = new("The category does not exist.", "La categoría no existe.", "Die Kategorie existiert nicht.", "La categoria non esiste.", "La catégorie n'existe pas."),
        ["Kategoria o tej nazwie już istnieje."] = new("A category with this name already exists.", "Ya existe una categoría con este nombre.", "Eine Kategorie mit diesem Namen existiert bereits.", "Esiste già una categoria con questo nome.", "Une catégorie portant ce nom existe déjà."),
        ["Klucz pola własnego jest wymagany."] = new("The custom field key is required.", "La clave del campo personalizado es obligatoria.", "Der Schlüssel des benutzerdefinierten Felds ist erforderlich.", "La chiave del campo personalizzato è obbligatoria.", "La clé du champ personnalisé est obligatoire."),
        ["Klucze pól własnych muszą być unikalne w obrębie kategorii."] = new("Custom field keys must be unique within the category.", "Las claves de los campos personalizados deben ser únicas dentro de la categoría.", "Die Schlüssel benutzerdefinierter Felder müssen innerhalb der Kategorie eindeutig sein.", "Le chiavi dei campi personalizzati devono essere univoche all'interno della categoria.", "Les clés des champs personnalisés doivent être uniques au sein de la catégorie."),
        ["Kolor tekstu statusu musi być w formacie szesnastkowym, np. #1d4ed8."] = new("The status text color must be in hex format, e.g. #1d4ed8.", "El color del texto del estado debe estar en formato hexadecimal, p. ej. #1d4ed8.", "Die Textfarbe des Status muss im Hex-Format angegeben werden, z. B. #1d4ed8.", "Il colore del testo dello stato deve essere in formato esadecimale, ad es. #1d4ed8.", "La couleur du texte du statut doit être au format hexadécimal, par ex. #1d4ed8."),
        ["Kolor tła statusu musi być w formacie szesnastkowym, np. #eff6ff."] = new("The status background color must be in hex format, e.g. #eff6ff.", "El color de fondo del estado debe estar en formato hexadecimal, p. ej. #eff6ff.", "Die Hintergrundfarbe des Status muss im Hex-Format angegeben werden, z. B. #eff6ff.", "Il colore di sfondo dello stato deve essere in formato esadecimale, ad es. #eff6ff.", "La couleur de fond du statut doit être au format hexadécimal, par ex. #eff6ff."),
        ["Kontrola nie istnieje."] = new("The inspection does not exist.", "La inspección no existe.", "Die Kontrolle existiert nicht.", "Il controllo non esiste.", "Le contrôle n'existe pas."),
        ["Kontrola tego aktywa została już zakończona."] = new("This asset's inspection has already been completed.", "La inspección de este activo ya se ha completado.", "Die Kontrolle dieses Assets wurde bereits abgeschlossen.", "Il controllo di questo asset è già stato completato.", "Le contrôle de cet actif est déjà terminé."),
        ["Licencja nie istnieje."] = new("The license does not exist.", "La licencia no existe.", "Die Lizenz existiert nicht.", "La licenza non esiste.", "La licence n'existe pas."),
        ["Liczba miejsc nie może być ujemna."] = new("The number of seats cannot be negative.", "El número de plazas no puede ser negativo.", "Die Anzahl der Plätze darf nicht negativ sein.", "Il numero di posti non può essere negativo.", "Le nombre de places ne peut pas être négatif."),
        ["Kod resetujący jest nieprawidłowy lub wygasł."] = new("The password reset code is invalid or has expired.", "El código para restablecer la contraseña no es válido o ha caducado.", "Der Code zum Zurücksetzen des Passworts ist ungültig oder abgelaufen.", "Il codice di reimpostazione della password non è valido o è scaduto.", "Le code de réinitialisation du mot de passe est invalide ou a expiré."),
        ["Kod weryfikacyjny jest nieprawidłowy lub wygasł."] = new("The verification code is invalid or has expired.", "El código de verificación no es válido o ha caducado.", "Der Bestätigungscode ist ungültig oder abgelaufen.", "Il codice di verifica non è valido o è scaduto.", "Le code de vérification est invalide ou a expiré."),
        ["Materiał dowodowy nie istnieje."] = new("The evidence item does not exist.", "El material de evidencia no existe.", "Der Beweismaterial-Eintrag existiert nicht.", "L'elemento probatorio non esiste.", "L'élément de preuve n'existe pas."),
        ["Najpierw wygeneruj sekret 2FA."] = new("First generate a 2FA secret.", "Primero genera un secreto de 2FA.", "Generieren Sie zuerst ein 2FA-Secret.", "Genera prima un segreto 2FA.", "Générez d'abord un secret 2FA."),
        ["Nazwa aktywa jest wymagana."] = new("The asset name is required.", "El nombre del activo es obligatorio.", "Der Asset-Name ist erforderlich.", "Il nome dell'asset è obbligatorio.", "Le nom de l'actif est obligatoire."),
        ["Nazwa firmy jest wymagana."] = new("The company name is required.", "El nombre de la empresa es obligatorio.", "Der Firmenname ist erforderlich.", "Il nome dell'azienda è obbligatorio.", "Le nom de l'entreprise est obligatoire."),
        ["Najpierw wgraj własne logo, aby użyć go na etykiecie."] = new("Upload your own logo first to use it on the label.", "Sube primero tu propio logotipo para usarlo en la etiqueta.", "Laden Sie zuerst Ihr eigenes Logo hoch, um es auf dem Etikett zu verwenden.", "Carica prima il tuo logo per usarlo sull'etichetta.", "Téléversez d'abord votre propre logo pour l'utiliser sur l'étiquette."),
        ["Nie udało się wygenerować unikalnego kodu etykiety. Spróbuj ponownie."] = new("Could not generate a unique label code. Please try again.", "No se pudo generar un código de etiqueta único. Inténtalo de nuevo.", "Es konnte kein eindeutiger Etikettencode erzeugt werden. Bitte versuchen Sie es erneut.", "Non è stato possibile generare un codice etichetta univoco. Riprova.", "Impossible de générer un code d'étiquette unique. Veuillez réessayer."),
        ["Nazwa kategorii jest wymagana."] = new("The category name is required.", "El nombre de la categoría es obligatorio.", "Der Kategoriename ist erforderlich.", "Il nome della categoria è obbligatorio.", "Le nom de la catégorie est obligatoire."),
        ["Nazwa licencji jest wymagana."] = new("The license name is required.", "El nombre de la licencia es obligatorio.", "Der Lizenzname ist erforderlich.", "Il nome della licenza è obbligatorio.", "Le nom de la licence est obligatoire."),
        ["Nazwa pliku jest wymagana."] = new("The file name is required.", "El nombre del archivo es obligatorio.", "Der Dateiname ist erforderlich.", "Il nome del file è obbligatorio.", "Le nom du fichier est obligatoire."),
        ["Nazwa typu relacji jest wymagana."] = new("The relationship type name is required.", "El nombre del tipo de relación es obligatorio.", "Der Name des Beziehungstyps ist erforderlich.", "Il nome del tipo di relazione è obbligatorio.", "Le nom du type de relation est obligatoire."),
        ["Nazwa zespołu jest wymagana."] = new("The team name is required.", "El nombre del equipo es obligatorio.", "Der Teamname ist erforderlich.", "Il nome del team è obbligatorio.", "Le nom de l'équipe est obligatoire."),
        ["Nazwa zestawu stanowiskowego jest wymagana."] = new("The job profile name is required.", "El nombre del perfil de puesto es obligatorio.", "Der Name des Stellenprofils ist erforderlich.", "Il nome del profilo professionale è obbligatorio.", "Le nom du profil de poste est obligatoire."),
        ["Nie możesz odłączyć jedynego sposobu logowania. Ustaw najpierw hasło."] = new("You cannot unlink your only sign-in method. Set a password first.", "No puedes desvincular tu único método de inicio de sesión. Establece antes una contraseña.", "Sie können Ihre einzige Anmeldemethode nicht entfernen. Legen Sie zuerst ein Passwort fest.", "Non puoi scollegare l'unico metodo di accesso. Imposta prima una password.", "Vous ne pouvez pas dissocier votre unique méthode de connexion. Définissez d'abord un mot de passe."),
        ["Nie można edytować opublikowanej procedury - osoby już ją zaakceptowały. Zarchiwizuj ją i utwórz nową wersję."] = new("A published procedure cannot be edited - people have already accepted it. Archive it and create a new version.", "No se puede editar un procedimiento publicado - ya ha sido aceptado por personas. Archívalo y crea una nueva versión.", "Eine veröffentlichte Prozedur kann nicht bearbeitet werden - sie wurde bereits akzeptiert. Archivieren Sie sie und erstellen Sie eine neue Version.", "Una procedura pubblicata non può essere modificata - è già stata accettata da alcune persone. Archiviala e crea una nuova versione.", "Une procédure publiée ne peut pas être modifiée - des personnes l'ont déjà acceptée. Archivez-la et créez une nouvelle version."),
        ["Nie można modyfikować wydania, które zostało już podpisane lub zamknięte."] = new("An assignment that has already been signed or closed cannot be modified.", "No se puede modificar una entrega que ya ha sido firmada o cerrada.", "Eine bereits unterzeichnete oder abgeschlossene Übergabe kann nicht geändert werden.", "Un'assegnazione già firmata o chiusa non può essere modificata.", "Une remise déjà signée ou clôturée ne peut pas être modifiée."),
        ["Nie można opublikować procedury bez pliku."] = new("A procedure cannot be published without a file.", "No se puede publicar un procedimiento sin un archivo.", "Eine Prozedur kann nicht ohne Datei veröffentlicht werden.", "Una procedura non può essere pubblicata senza un file.", "Une procédure ne peut pas être publiée sans fichier."),
        ["Nie można opublikować zarchiwizowanej procedury."] = new("An archived procedure cannot be published.", "No se puede publicar un procedimiento archivado.", "Eine archivierte Prozedur kann nicht veröffentlicht werden.", "Una procedura archiviata non può essere pubblicata.", "Une procédure archivée ne peut pas être publiée."),
        ["Nie można ustawić liczby miejsc poniżej liczby już przypisanych."] = new("The number of seats cannot be set below the number already assigned.", "No se puede establecer un número de plazas inferior al ya asignado.", "Die Anzahl der Plätze darf nicht unter die Anzahl der bereits zugewiesenen Plätze gesetzt werden.", "Il numero di posti non può essere impostato al di sotto del numero già assegnato.", "Le nombre de places ne peut pas être inférieur au nombre déjà attribué."),
        ["Nie można usunąć aktywa powiązanego z wydaniami, kontrolami, zgłoszeniami serwisowymi, rezerwacjami lub offboardingiem."] = new("An asset linked to assignments, inspections, service tickets, reservations, or offboarding cannot be deleted.", "No se puede eliminar un activo vinculado a entregas, inspecciones, tickets de servicio, reservas u offboarding.", "Ein Asset, das mit Übergaben, Kontrollen, Service-Tickets, Reservierungen oder Offboarding verknüpft ist, kann nicht gelöscht werden.", "Un asset collegato ad assegnazioni, controlli, ticket di assistenza, prenotazioni o offboarding non può essere eliminato.", "Un actif lié à des remises, des contrôles, des tickets de service, des réservations ou un offboarding ne peut pas être supprimé."),
        ["Tego rekordu nie można usunąć, ponieważ jest używany w innym miejscu."] = new("This record cannot be deleted because it is used elsewhere.", "Este registro no se puede eliminar porque se utiliza en otro lugar.", "Dieser Datensatz kann nicht gelöscht werden, da er an anderer Stelle verwendet wird.", "Questo record non può essere eliminato perché è utilizzato altrove.", "Cet enregistrement ne peut pas être supprimé car il est utilisé ailleurs."),
        ["Przesłany plik jest za duży."] = new("The uploaded file is too large.", "El archivo subido es demasiado grande.", "Die hochgeladene Datei ist zu groß.", "Il file caricato è troppo grande.", "Le fichier envoyé est trop volumineux."),
        ["Nieprawidłowe żądanie."] = new("Invalid request.", "Solicitud no válida.", "Ungültige Anfrage.", "Richiesta non valida.", "Requête invalide."),
        ["Wystąpił nieoczekiwany błąd aplikacji."] = new("An unexpected application error occurred.", "Se produjo un error inesperado en la aplicación.", "Es ist ein unerwarteter Anwendungsfehler aufgetreten.", "Si è verificato un errore imprevisto dell'applicazione.", "Une erreur inattendue de l'application s'est produite."),
        ["Rekord z takimi danymi już istnieje."] = new("A record with this data already exists.", "Ya existe un registro con estos datos.", "Ein Datensatz mit diesen Daten existiert bereits.", "Esiste già un record con questi dati.", "Un enregistrement avec ces données existe déjà."),
        ["Nie można usunąć kategorii używanej przez aktywa lub zestawy."] = new("A category used by assets or job profiles cannot be deleted.", "No se puede eliminar una categoría utilizada por activos o perfiles de puesto.", "Eine Kategorie, die von Assets oder Stellenprofilen verwendet wird, kann nicht gelöscht werden.", "Una categoria utilizzata da asset o profili professionali non può essere eliminata.", "Une catégorie utilisée par des actifs ou des profils de poste ne peut pas être supprimée."),
        ["Nie można usunąć pracownika, bo ma przypisany sprzęt, historię wydań albo jest przełożonym. Najpierw zwróć sprzęt albo usuń powiązania."] = new("This employee cannot be deleted because they have assigned equipment, an assignment history, or are a manager. Return the equipment or remove the relations first.", "No se puede eliminar a este empleado porque tiene equipos asignados, historial de entregas o es responsable de otras personas. Devuelve el equipo o elimina las relaciones primero.", "Dieser Mitarbeiter kann nicht gelöscht werden, da ihm Geräte zugewiesen sind, eine Übergabehistorie besteht oder er eine Führungsposition hat. Geben Sie zuerst die Geräte zurück oder entfernen Sie die Beziehungen.", "Questo dipendente non può essere eliminato perché ha attrezzature assegnate, uno storico di assegnazioni oppure è un responsabile. Restituisci prima l'attrezzatura o rimuovi le relazioni.", "Cet employé ne peut pas être supprimé car il a du matériel attribué, un historique de remises, ou il est responsable d'autres personnes. Restituez d'abord le matériel ou supprimez les relations."),
        ["Nie można usunąć typu relacji przypisanego do osób."] = new("A relationship type assigned to people cannot be deleted.", "No se puede eliminar un tipo de relación asignado a personas.", "Ein Beziehungstyp, der Personen zugewiesen ist, kann nicht gelöscht werden.", "Un tipo di relazione assegnato a delle persone non può essere eliminato.", "Un type de relation attribué à des personnes ne peut pas être supprimé."),
        ["Nie można usunąć zespołu przypisanego do osób lub aktywów."] = new("A team assigned to people or assets cannot be deleted.", "No se puede eliminar un equipo asignado a personas o activos.", "Ein Team, das Personen oder Assets zugewiesen ist, kann nicht gelöscht werden.", "Un team assegnato a persone o asset non può essere eliminato.", "Une équipe attribuée à des personnes ou des actifs ne peut pas être supprimée."),
        ["Nie można zwrócić wydania, które zostało już zamknięte lub anulowane."] = new("An assignment that has already been closed or cancelled cannot be returned.", "No se puede devolver una entrega que ya ha sido cerrada o cancelada.", "Eine bereits abgeschlossene oder stornierte Übergabe kann nicht zurückgegeben werden.", "Un'assegnazione già chiusa o annullata non può essere restituita.", "Une remise déjà clôturée ou annulée ne peut pas être retournée."),
        ["Nie znaleziono konta powiązanego z tym linkiem."] = new("No account linked to this link was found.", "No se encontró ninguna cuenta vinculada a este enlace.", "Es wurde kein mit diesem Link verknüpftes Konto gefunden.", "Non è stato trovato alcun account collegato a questo link.", "Aucun compte lié à ce lien n'a été trouvé."),
        ["Nie znaleziono konta."] = new("No account was found.", "No se encontró la cuenta.", "Es wurde kein Konto gefunden.", "Non è stato trovato alcun account.", "Aucun compte n'a été trouvé."),
        ["Nie znaleziono organizacji powiązanej z kontem."] = new("No organization linked to this account was found.", "No se encontró ninguna organización vinculada a la cuenta.", "Es wurde keine mit dem Konto verknüpfte Organisation gefunden.", "Non è stata trovata alcuna organizzazione collegata all'account.", "Aucune organisation liée à ce compte n'a été trouvée."),
        ["Niektóre aktywa nie istnieją."] = new("Some assets do not exist.", "Algunos activos no existen.", "Einige Assets existieren nicht.", "Alcuni asset non esistono.", "Certains actifs n'existent pas."),
        ["Niektóre procedury nie istnieją."] = new("Some procedures do not exist.", "Algunos procedimientos no existen.", "Einige Prozeduren existieren nicht.", "Alcune procedure non esistono.", "Certaines procédures n'existent pas."),
        ["Nieprawidłowy e-mail lub hasło."] = new("Invalid email or password.", "Correo electrónico o contraseña incorrectos.", "Ungültige E-Mail-Adresse oder ungültiges Passwort.", "E-mail o password non validi.", "E-mail ou mot de passe invalide."),
        ["Nieprawidłowy kod uwierzytelniający."] = new("Invalid authentication code.", "Código de autenticación incorrecto.", "Ungültiger Authentifizierungscode.", "Codice di autenticazione non valido.", "Code d'authentification invalide."),
        ["Nieprawidłowy kod. Sprawdź godzinę na urządzeniu i spróbuj ponownie."] = new("Invalid code. Check the time on your device and try again.", "Código incorrecto. Comprueba la hora de tu dispositivo e inténtalo de nuevo.", "Ungültiger Code. Überprüfen Sie die Uhrzeit auf Ihrem Gerät und versuchen Sie es erneut.", "Codice non valido. Controlla l'ora sul tuo dispositivo e riprova.", "Code invalide. Vérifiez l'heure de votre appareil et réessayez."),
        ["Nieprawidłowy lub uszkodzony plik obrazu."] = new("The image file is invalid or corrupted.", "El archivo de imagen no es válido o está dañado.", "Die Bilddatei ist ungültig oder beschädigt.", "Il file immagine non è valido o è danneggiato.", "Le fichier image est invalide ou corrompu."),
        ["Nieprawidłowy podpis webhooka Stripe."] = new("Invalid Stripe webhook signature.", "Firma de webhook de Stripe no válida.", "Ungültige Stripe-Webhook-Signatur.", "Firma del webhook Stripe non valida.", "Signature du webhook Stripe invalide."),
        ["Numer wydania jest wymagany."] = new("The assignment reference is required.", "La referencia de la entrega es obligatoria.", "Die Ausgabenummer ist erforderlich.", "Il numero di assegnazione è obbligatorio.", "La référence de remise est obligatoire."),
        ["Organizacja ma już aktywną płatną subskrypcję."] = new("The organization already has an active paid subscription.", "La organización ya tiene una suscripción de pago activa.", "Die Organisation hat bereits ein aktives kostenpflichtiges Abonnement.", "L'organizzazione ha già un abbonamento a pagamento attivo.", "L'organisation dispose déjà d'un abonnement payant actif."),
        ["Organizacja nie istnieje."] = new("The organization does not exist.", "La organización no existe.", "Die Organisation existiert nicht.", "L'organizzazione non esiste.", "L'organisation n'existe pas."),
        ["Organizacja nie ma jeszcze konta rozliczeniowego Stripe."] = new("The organization does not yet have a Stripe billing account.", "La organización aún no tiene una cuenta de facturación de Stripe.", "Die Organisation verfügt noch über kein Stripe-Abrechnungskonto.", "L'organizzazione non dispone ancora di un account di fatturazione Stripe.", "L'organisation ne dispose pas encore de compte de facturation Stripe."),
        ["Osiągnięto limit 5 zdjęć dla tego aktywa i etapu."] = new("The limit of 5 photos for this asset and phase has been reached.", "Se ha alcanzado el límite de 5 fotos para este activo y esta fase.", "Das Limit von 5 Fotos für dieses Asset und diese Phase wurde erreicht.", "È stato raggiunto il limite di 5 foto per questo asset e questa fase.", "La limite de 5 photos pour cet actif et cette phase est atteinte."),
        ["Osoba z tym adresem e-mail już istnieje."] = new("A person with this email address already exists.", "Ya existe una persona con esta dirección de correo.", "Es existiert bereits eine Person mit dieser E-Mail-Adresse.", "Esiste già una persona con questo indirizzo e-mail.", "Une personne avec cette adresse e-mail existe déjà."),
        ["Plik nie jest prawidłowym obrazem JPEG/PNG/WebP."] = new("The file is not a valid JPEG/PNG/WebP image.", "El archivo no es una imagen JPEG/PNG/WebP válida.", "Die Datei ist kein gültiges JPEG-/PNG-/WebP-Bild.", "Il file non è un'immagine JPEG/PNG/WebP valida.", "Le fichier n'est pas une image JPEG/PNG/WebP valide."),
        ["Plik logo jest pusty."] = new("The logo file is empty.", "El archivo del logotipo está vacío.", "Die Logo-Datei ist leer.", "Il file del logo è vuoto.", "Le fichier du logo est vide."),
        ["Podano więcej numerów seryjnych niż sztuk w partii."] = new("There are more serial numbers than units in the batch.", "Hay más números de serie que unidades en el lote.", "Es gibt mehr Seriennummern als Einheiten in der Charge.", "Ci sono più numeri di serie che unità nel lotto.", "Il y a plus de numéros de série que d'unités dans le lot."),
        ["Prefiks tagu jest wymagany."] = new("The tag prefix is required.", "El prefijo de etiqueta es obligatorio.", "Das Tag-Präfix ist erforderlich.", "Il prefisso del tag è obbligatorio.", "Le préfixe du tag est obligatoire."),
        ["Plik procedury jest pusty."] = new("The procedure file is empty.", "El archivo del procedimiento está vacío.", "Die Prozedurdatei ist leer.", "Il file della procedura è vuoto.", "Le fichier de la procédure est vide."),
        ["Plik procedury może mieć maksymalnie 25 MB."] = new("The procedure file can be at most 25 MB.", "El archivo del procedimiento puede tener como máximo 25 MB.", "Die Prozedurdatei darf höchstens 25 MB groß sein.", "Il file della procedura può avere una dimensione massima di 25 MB.", "Le fichier de la procédure peut peser au maximum 25 Mo."),
        ["Plik procedury nie istnieje."] = new("The procedure file does not exist.", "El archivo del procedimiento no existe.", "Die Prozedurdatei existiert nicht.", "Il file della procedura non esiste.", "Le fichier de la procédure n'existe pas."),
        ["Pole nie jest polem wrażliwym."] = new("This is not a sensitive field.", "Este campo no es un campo sensible.", "Dies ist kein sensibles Feld.", "Questo non è un campo sensibile.", "Ce champ n'est pas un champ sensible."),
        ["Poprawny adres e-mail jest wymagany."] = new("A valid email address is required.", "Se requiere una dirección de correo válida.", "Eine gültige E-Mail-Adresse ist erforderlich.", "È richiesto un indirizzo e-mail valido.", "Une adresse e-mail valide est requise."),
        ["Poprawny e-mail użytkownika jest wymagany."] = new("A valid user email is required.", "Se requiere un correo de usuario válido.", "Eine gültige Benutzer-E-Mail-Adresse ist erforderlich.", "È richiesta un'e-mail utente valida.", "Une adresse e-mail utilisateur valide est requise."),
        ["Pracownik nie istnieje."] = new("The employee does not exist.", "El empleado no existe.", "Der Mitarbeiter existiert nicht.", "Il dipendente non esiste.", "L'employé n'existe pas."),
        ["Pracownik z tym adresem e-mail już istnieje."] = new("An employee with this email address already exists.", "Ya existe un empleado con esta dirección de correo.", "Es existiert bereits ein Mitarbeiter mit dieser E-Mail-Adresse.", "Esiste già un dipendente con questo indirizzo e-mail.", "Un employé avec cette adresse e-mail existe déjà."),
        ["Procedura nie istnieje."] = new("The procedure does not exist.", "El procedimiento no existe.", "Die Prozedur existiert nicht.", "La procedura non esiste.", "La procédure n'existe pas."),
        ["Płatności Stripe nie są jeszcze skonfigurowane."] = new("Stripe payments are not configured yet.", "Los pagos con Stripe aún no están configurados.", "Stripe-Zahlungen sind noch nicht konfiguriert.", "I pagamenti Stripe non sono ancora configurati.", "Les paiements Stripe ne sont pas encore configurés."),
        ["Sesja wygasła. Zaloguj się ponownie."] = new("Your session has expired. Please log in again.", "Tu sesión ha caducado. Inicia sesión de nuevo.", "Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an.", "La sessione è scaduta. Accedi di nuovo.", "Votre session a expiré. Veuillez vous reconnecter."),
        ["Suma kontrolna pliku jest wymagana."] = new("The file checksum is required.", "La suma de comprobación del archivo es obligatoria.", "Die Prüfsumme der Datei ist erforderlich.", "La checksum del file è obbligatoria.", "La somme de contrôle du fichier est obligatoire."),
        ["Ta akceptacja procedury została już zarejestrowana i nie może zostać zmieniona."] = new("This procedure acceptance has already been recorded and cannot be changed.", "Esta aceptación del procedimiento ya ha sido registrada y no se puede modificar.", "Diese Prozedurakzeptanz wurde bereits erfasst und kann nicht mehr geändert werden.", "Questa accettazione della procedura è già stata registrata e non può essere modificata.", "Cette acceptation de procédure a déjà été enregistrée et ne peut pas être modifiée."),
        ["Ta organizacja ma aktywną płatną subskrypcję. Zarządzaj nią (w tym anulowaniem) w portalu rozliczeń Stripe."] = new("This organization has an active paid subscription. Manage it (including cancellation) in the Stripe billing portal.", "Esta organización tiene una suscripción de pago activa. Gestiónala (incluida la cancelación) en el portal de facturación de Stripe.", "Diese Organisation hat ein aktives kostenpflichtiges Abonnement. Verwalten Sie es (einschließlich Kündigung) im Stripe-Abrechnungsportal.", "Questa organizzazione ha un abbonamento a pagamento attivo. Gestiscilo (anche per la cancellazione) nel portale di fatturazione Stripe.", "Cette organisation dispose d'un abonnement payant actif. Gérez-le (y compris son annulation) dans le portail de facturation Stripe."),
        ["Ta osoba ma już przypisane miejsce w tej licencji."] = new("This person already has an assigned seat on this license.", "Esta persona ya tiene una plaza asignada en esta licencia.", "Dieser Person ist bereits ein Platz in dieser Lizenz zugewiesen.", "A questa persona è già assegnato un posto in questa licenza.", "Cette personne dispose déjà d'une place attribuée sur cette licence."),
        ["Tag aktywa jest już używany."] = new("This asset tag is already in use.", "Esta etiqueta de activo ya está en uso.", "Dieses Asset-Tag wird bereits verwendet.", "Questo tag asset è già in uso.", "Cette étiquette d'actif est déjà utilisée."),
        ["Tag aktywa jest wymagany."] = new("The asset tag is required.", "La etiqueta del activo es obligatoria.", "Das Asset-Tag ist erforderlich.", "Il tag dell'asset è obbligatorio.", "L'étiquette de l'actif est obligatoire."),
        ["Ten asset jest już dodany do wydania."] = new("This asset has already been added to the assignment.", "Este activo ya ha sido añadido a la entrega.", "Dieses Asset wurde der Übergabe bereits hinzugefügt.", "Questo asset è già stato aggiunto all'assegnazione.", "Cet actif a déjà été ajouté à la remise."),
        ["To konto nie jest połączone z tym dostawcą."] = new("This account is not linked to this provider.", "Esta cuenta no está vinculada a este proveedor.", "Dieses Konto ist nicht mit diesem Anbieter verknüpft.", "Questo account non è collegato a questo provider.", "Ce compte n'est pas lié à ce fournisseur."),
        ["To samo aktywo nie może wystąpić dwa razy w jednym wydaniu."] = new("The same asset cannot appear twice in a single assignment.", "El mismo activo no puede aparecer dos veces en una misma entrega.", "Dasselbe Asset darf in einer Übergabe nicht zweimal vorkommen.", "Lo stesso asset non può comparire due volte in un'unica assegnazione.", "Le même actif ne peut pas apparaître deux fois dans une même remise."),
        ["Treść zgłoszenia jest wymagana."] = new("The report content is required.", "El contenido del informe es obligatorio.", "Der Inhalt der Meldung ist erforderlich.", "Il contenuto della segnalazione è obbligatorio.", "Le contenu du signalement est obligatoire."),
        ["Tylko wydane aktywo można oznaczyć jako oczekujące na zwrot."] = new("Only an assigned asset can be marked as pending return.", "Solo un activo entregado se puede marcar como pendiente de devolución.", "Nur ein ausgegebenes Asset kann als rückgabeausstehend markiert werden.", "Solo un asset assegnato può essere contrassegnato come in attesa di restituzione.", "Seul un actif remis peut être marqué comme en attente de restitution."),
        ["Typ relacji nie istnieje."] = new("The relationship type does not exist.", "El tipo de relación no existe.", "Der Beziehungstyp existiert nicht.", "Il tipo di relazione non esiste.", "Le type de relation n'existe pas."),
        ["Typ relacji o tej nazwie już istnieje."] = new("A relationship type with this name already exists.", "Ya existe un tipo de relación con este nombre.", "Ein Beziehungstyp mit diesem Namen existiert bereits.", "Esiste già un tipo di relazione con questo nome.", "Un type de relation portant ce nom existe déjà."),
        ["Tytuł procedury jest wymagany."] = new("The procedure title is required.", "El título del procedimiento es obligatorio.", "Der Titel der Prozedur ist erforderlich.", "Il titolo della procedura è obbligatorio.", "Le titre de la procédure est obligatoire."),
        ["Układ pulpitu nie może być pusty."] = new("The dashboard layout cannot be empty.", "El diseño del panel no puede estar vacío.", "Das Dashboard-Layout darf nicht leer sein.", "Il layout della dashboard non può essere vuoto.", "La disposition du tableau de bord ne peut pas être vide."),
        ["Uzupełnij wymagane pola pakietu startowego."] = new("Fill in the required job profile fields.", "Completa los campos obligatorios del perfil de puesto.", "Füllen Sie die erforderlichen Felder des Stellenprofils aus.", "Compila i campi obbligatori del profilo professionale.", "Renseignez les champs obligatoires du profil de poste."),
        ["Użytkownik nie istnieje."] = new("The user does not exist.", "El usuario no existe.", "Der Benutzer existiert nicht.", "L'utente non esiste.", "L'utilisateur n'existe pas."),
        ["Użytkownik z tym adresem e-mail już istnieje."] = new("A user with this email address already exists.", "Ya existe un usuario con esta dirección de correo.", "Es existiert bereits ein Benutzer mit dieser E-Mail-Adresse.", "Esiste già un utente con questo indirizzo e-mail.", "Un utilisateur avec cette adresse e-mail existe déjà."),
        ["Wersja procedury jest wymagana."] = new("The procedure version is required.", "La versión del procedimiento es obligatoria.", "Die Version der Prozedur ist erforderlich.", "La versione della procedura è obbligatoria.", "La version de la procédure est obligatoire."),
        ["Wybrana kategoria nie istnieje."] = new("The selected category does not exist.", "La categoría seleccionada no existe.", "Die ausgewählte Kategorie existiert nicht.", "La categoria selezionata non esiste.", "La catégorie sélectionnée n'existe pas."),
        ["Wybrana osoba nie istnieje."] = new("The selected person does not exist.", "La persona seleccionada no existe.", "Die ausgewählte Person existiert nicht.", "La persona selezionata non esiste.", "La personne sélectionnée n'existe pas."),
        ["Wybrany pracownik nie istnieje."] = new("The selected employee does not exist.", "El empleado seleccionado no existe.", "Der ausgewählte Mitarbeiter existiert nicht.", "Il dipendente selezionato non esiste.", "L'employé sélectionné n'existe pas."),
        ["Wybrany zestaw stanowiskowy nie istnieje."] = new("The selected job profile does not exist.", "El perfil de puesto seleccionado no existe.", "Das ausgewählte Stellenprofil existiert nicht.", "Il profilo professionale selezionato non esiste.", "Le profil de poste sélectionné n'existe pas."),
        ["Wydanie można zaakceptować tylko wtedy, gdy oczekuje na akceptację albo jest po terminie."] = new("An assignment can only be accepted while it is pending acceptance or overdue.", "Una entrega solo se puede aceptar mientras está pendiente de aceptación o vencida.", "Eine Übergabe kann nur akzeptiert werden, solange sie noch aussteht oder überfällig ist.", "Un'assegnazione può essere accettata solo se è in attesa di accettazione o scaduta.", "Une remise ne peut être acceptée que si elle est en attente d'acceptation ou en retard."),
        ["Wydanie nie istnieje."] = new("The assignment does not exist.", "La entrega no existe.", "Die Übergabe existiert nicht.", "L'assegnazione non esiste.", "La remise n'existe pas."),
        ["Wymagane uwierzytelnienie."] = new("Authentication is required.", "Se requiere autenticación.", "Authentifizierung ist erforderlich.", "È richiesta l'autenticazione.", "L'authentification est requise."),
        ["Za mało danych historycznych do porównania - migawki zbierają się od teraz, spróbuj ponownie za kilka dni."] = new("Not enough historical data to compare yet - snapshots are being collected from now on, try again in a few days.", "Todavía no hay suficientes datos históricos para comparar - las instantáneas se están recopilando a partir de ahora, inténtalo de nuevo en unos días.", "Noch nicht genügend historische Daten zum Vergleich - ab jetzt werden Snapshots gesammelt, versuchen Sie es in ein paar Tagen erneut.", "Non ci sono ancora abbastanza dati storici per il confronto - da ora vengono raccolti degli snapshot, riprova tra qualche giorno.", "Pas encore assez de données historiques pour comparer - des instantanés sont désormais collectés, réessayez dans quelques jours."),
        ["Zbyt wiele prób. Poproś o nowy kod lub spróbuj ponownie później."] = new("Too many attempts. Request a new code or try again later.", "Demasiados intentos. Solicita un código nuevo o inténtalo de nuevo más tarde.", "Zu viele Versuche. Fordern Sie einen neuen Code an oder versuchen Sie es später erneut.", "Troppi tentativi. Richiedi un nuovo codice o riprova più tardi.", "Trop de tentatives. Demandez un nouveau code ou réessayez plus tard."),
        ["Zablokowany materiał dowodowy nie może zostać usunięty."] = new("A locked evidence item cannot be deleted.", "No se puede eliminar un material de evidencia bloqueado.", "Ein gesperrter Beweismaterial-Eintrag kann nicht gelöscht werden.", "Un elemento probatorio bloccato non può essere eliminato.", "Un élément de preuve verrouillé ne peut pas être supprimé."),
        ["Zdjęcie jest puste."] = new("The photo is empty.", "La foto está vacía.", "Das Foto ist leer.", "La foto è vuota.", "La photo est vide."),
        ["Zdjęcie może mieć maksymalnie 5 MB."] = new("The photo can be at most 5 MB.", "La foto puede tener como máximo 5 MB.", "Das Foto darf höchstens 5 MB groß sein.", "La foto può avere una dimensione massima di 5 MB.", "La photo peut peser au maximum 5 Mo."),
        ["Zespół nie istnieje."] = new("The team does not exist.", "El equipo no existe.", "Das Team existiert nicht.", "Il team non esiste.", "L'équipe n'existe pas."),
        ["Zespół o tej nazwie już istnieje."] = new("A team with this name already exists.", "Ya existe un equipo con este nombre.", "Ein Team mit diesem Namen existiert bereits.", "Esiste già un team con questo nome.", "Une équipe portant ce nom existe déjà."),
        ["Zestaw stanowiskowy nie istnieje."] = new("The job profile does not exist.", "El perfil de puesto no existe.", "Das Stellenprofil existiert nicht.", "Il profilo professionale non esiste.", "Le profil de poste n'existe pas."),
        ["Zestaw stanowiskowy nie zawiera żadnego dostępnego aktywa - dodaj aktywo ręcznie."] = new("The job profile does not contain any available asset - add an asset manually.", "El perfil de puesto no contiene ningún activo disponible - añade un activo manualmente.", "Das Stellenprofil enthält kein verfügbares Asset - fügen Sie manuell ein Asset hinzu.", "Il profilo professionale non contiene alcun asset disponibile - aggiungi un asset manualmente.", "Le profil de poste ne contient aucun actif disponible - ajoutez un actif manuellement."),
        ["Zestaw stanowiskowy o tej nazwie już istnieje."] = new("A job profile with this name already exists.", "Ya existe un perfil de puesto con este nombre.", "Ein Stellenprofil mit diesem Namen existiert bereits.", "Esiste già un profilo professionale con questo nome.", "Un profil de poste portant ce nom existe déjà."),
        ["Zestaw zawiera kategorię, która nie istnieje."] = new("The job profile contains a category that does not exist.", "El perfil de puesto contiene una categoría que no existe.", "Das Stellenprofil enthält eine Kategorie, die nicht existiert.", "Il profilo professionale contiene una categoria che non esiste.", "Le profil de poste contient une catégorie qui n'existe pas."),
        ["Zestaw zawiera procedurę, która nie istnieje."] = new("The job profile contains a procedure that does not exist.", "El perfil de puesto contiene un procedimiento que no existe.", "Das Stellenprofil enthält eine Prozedur, die nicht existiert.", "Il profilo professionale contiene una procedura che non esiste.", "Le profil de poste contient une procédure qui n'existe pas."),
        ["Zutylizowane aktywo nie może wrócić do obiegu."] = new("A disposed asset cannot return to circulation.", "Un activo dado de baja no puede volver a circulación.", "Ein entsorgtes Asset kann nicht wieder in den Umlauf zurückkehren.", "Un asset dismesso non può tornare in circolazione.", "Un actif mis au rebut ne peut pas revenir en circulation."),

        // --- Moderacja organizacji ---
        ["Organizacja jest już zawieszona."] = new("The organization is already suspended.", "La organización ya está suspendida.", "Die Organisation ist bereits gesperrt.", "L'organizzazione è già sospesa.", "L'organisation est déjà suspendue."),
        ["Organizacja nie jest zawieszona."] = new("The organization is not suspended.", "La organización no está suspendida.", "Die Organisation ist nicht gesperrt.", "L'organizzazione non è sospesa.", "L'organisation n'est pas suspendue."),
        ["Powód zawieszenia jest wymagany."] = new("A suspension reason is required.", "Se requiere un motivo de suspensión.", "Ein Sperrgrund ist erforderlich.", "È richiesta una motivazione per la sospensione.", "Un motif de suspension est obligatoire."),
        ["Dostęp do tej organizacji został zawieszony. Skontaktuj się z pomocą techniczną."] = new("Access to this organization has been suspended. Please contact support.", "El acceso a esta organización ha sido suspendido. Ponte en contacto con el soporte.", "Der Zugang zu dieser Organisation wurde gesperrt. Bitte wenden Sie sich an den Support.", "L'accesso a questa organizzazione è stato sospeso. Contatta l'assistenza.", "L'accès à cette organisation a été suspendu. Veuillez contacter le support."),

        // --- Alerty i powiadomienia ---
        ["Cooldown musi być w zakresie 0–14 dni."] = new("The cooldown must be between 0 and 14 days.", "El tiempo de espera debe estar entre 0 y 14 días.", "Die Abklingzeit muss zwischen 0 und 14 Tagen liegen.", "L'intervallo di attesa deve essere compreso tra 0 e 14 giorni.", "Le délai d'attente doit être compris entre 0 et 14 jours."),
        ["Dla digestu tygodniowego konieczny jest dzień tygodnia."] = new("A day of the week is required for the weekly digest.", "Se requiere un día de la semana para el resumen semanal.", "Für die wöchentliche Zusammenfassung ist ein Wochentag erforderlich.", "Per il riepilogo settimanale è necessario indicare un giorno della settimana.", "Un jour de la semaine est requis pour le résumé hebdomadaire."),
        ["Godziny cichego trybu muszą być ustawione razem."] = new("The quiet hours must be set together.", "Las horas de silencio deben configurarse juntas.", "Die Ruhezeiten müssen gemeinsam festgelegt werden.", "Le ore di silenzio devono essere impostate insieme.", "Les heures silencieuses doivent être définies ensemble."),
        ["Godziny ciszy wymagają obu wartości: początku i końca."] = new("Quiet hours require both a start and an end value.", "Las horas de silencio requieren tanto un valor de inicio como de fin.", "Ruhezeiten erfordern sowohl einen Start- als auch einen Endwert.", "Le ore di silenzio richiedono sia un valore di inizio sia uno di fine.", "Les heures silencieuses nécessitent une valeur de début et une de fin."),
        ["Brak adresu e-mail dla zalogowanego użytkownika."] = new("The signed-in user has no email address.", "El usuario que ha iniciado sesión no tiene dirección de correo.", "Der angemeldete Benutzer hat keine E-Mail-Adresse.", "L'utente connesso non ha un indirizzo e-mail.", "L'utilisateur connecté n'a pas d'adresse e-mail."),
        ["Identyfikator organizacji jest wymagany."] = new("The organization identifier is required.", "El identificador de la organización es obligatorio.", "Die Organisations-ID ist erforderlich.", "L'identificativo dell'organizzazione è obbligatorio.", "L'identifiant de l'organisation est obligatoire."),
        ["Użytkownik tworzący jest wymagany."] = new("The creating user is required.", "El usuario creador es obligatorio.", "Der erstellende Benutzer ist erforderlich.", "L'utente creatore è obbligatorio.", "L'utilisateur créateur est obligatoire."),

        // --- Lokalizacje ---
        ["Wybrany zespół nie istnieje."] = new("The selected team does not exist.", "El equipo seleccionado no existe.", "Das ausgewählte Team existiert nicht.", "Il team selezionato non esiste.", "L'équipe sélectionnée n'existe pas."),
        ["Wybrana lokalizacja nie istnieje."] = new("The selected location does not exist.", "La ubicación seleccionada no existe.", "Der ausgewählte Standort existiert nicht.", "L'ubicazione selezionata non esiste.", "L'emplacement sélectionné n'existe pas."),
        ["Ścieżka lokalizacji jest niejednoznaczna."] = new("The location path is ambiguous.", "La ruta de la ubicación es ambigua.", "Der Standortpfad ist mehrdeutig.", "Il percorso dell'ubicazione è ambiguo.", "Le chemin de l'emplacement est ambigu."),
        ["Wybrana lokalizacja nie istnieje w tej organizacji."] = new("The selected location does not exist in this organization.", "La ubicación seleccionada no existe en esta organización.", "Der ausgewählte Standort existiert in dieser Organisation nicht.", "L'ubicazione selezionata non esiste in questa organizzazione.", "L'emplacement sélectionné n'existe pas dans cette organisation."),
        ["Nazwa lokalizacji jest wymagana."] = new("The location name is required.", "El nombre de la ubicación es obligatorio.", "Der Standortname ist erforderlich.", "Il nome dell'ubicazione è obbligatorio.", "Le nom de l'emplacement est obligatoire."),
        ["Lokalizacja o tej nazwie już istnieje na tym poziomie."] = new("A location with this name already exists at this level.", "Ya existe una ubicación con este nombre en este nivel.", "Ein Standort mit diesem Namen existiert auf dieser Ebene bereits.", "Esiste già un'ubicazione con questo nome a questo livello.", "Un emplacement portant ce nom existe déjà à ce niveau."),
        ["Lokalizacja nadrzędna nie istnieje."] = new("The parent location does not exist.", "La ubicación superior no existe.", "Der übergeordnete Standort existiert nicht.", "L'ubicazione principale non esiste.", "L'emplacement parent n'existe pas."),
        ["Lokalizacja nie może być nadrzędna sama dla siebie."] = new("A location cannot be its own parent.", "Una ubicación no puede ser su propia ubicación superior.", "Ein Standort kann sich nicht selbst übergeordnet sein.", "Un'ubicazione non può essere principale di sé stessa.", "Un emplacement ne peut pas être son propre parent."),
        ["Lokalizacja nie istnieje."] = new("The location does not exist.", "La ubicación no existe.", "Der Standort existiert nicht.", "L'ubicazione non esiste.", "L'emplacement n'existe pas."),
        ["Nie można ustawić lokalizacji podrzędnej jako nadrzędnej - utworzyłoby to cykl."] = new("A child location cannot be set as the parent - that would create a cycle.", "No se puede establecer una ubicación subordinada como superior - eso crearía un ciclo.", "Ein untergeordneter Standort kann nicht als übergeordneter Standort festgelegt werden - das würde einen Zyklus erzeugen.", "Un'ubicazione subordinata non può essere impostata come principale - si creerebbe un ciclo.", "Un emplacement enfant ne peut pas devenir le parent - cela créerait un cycle."),
        ["Najpierw usuń podlokalizacje tej pozycji."] = new("Delete this item's sub-locations first.", "Elimina primero las sububicaciones de este elemento.", "Löschen Sie zuerst die untergeordneten Standorte dieses Eintrags.", "Elimina prima le sotto-ubicazioni di questa voce.", "Supprimez d'abord les sous-emplacements de cet élément."),
        ["Nie można usunąć lokalizacji z przypisanymi aktywami albo osobami."] = new("A location with assigned assets or people cannot be deleted.", "No se puede eliminar una ubicación con activos o personas asignados.", "Ein Standort mit zugewiesenen Assets oder Personen kann nicht gelöscht werden.", "Un'ubicazione con asset o persone assegnate non può essere eliminata.", "Un emplacement auquel des actifs ou des personnes sont affectés ne peut pas être supprimé."),

        // --- Przeglądy i zgłoszenia serwisowe ---
        ["Przegląd nie istnieje."] = new("The maintenance schedule does not exist.", "La revisión no existe.", "Die Wartung existiert nicht.", "La manutenzione non esiste.", "La maintenance n'existe pas."),
        ["Data wykonania nie może być w przyszłości."] = new("The completion date cannot be in the future.", "La fecha de realización no puede ser futura.", "Das Durchführungsdatum darf nicht in der Zukunft liegen.", "La data di esecuzione non può essere futura.", "La date de réalisation ne peut pas être dans le futur."),
        ["Nazwa przeglądu jest wymagana."] = new("The maintenance name is required.", "El nombre de la revisión es obligatorio.", "Der Name der Wartung ist erforderlich.", "Il nome della manutenzione è obbligatorio.", "Le nom de la maintenance est obligatoire."),
        ["Częstotliwość przeglądu musi mieścić się w zakresie 1-120 miesięcy."] = new("The maintenance interval must be between 1 and 120 months.", "La frecuencia de la revisión debe estar entre 1 y 120 meses.", "Das Wartungsintervall muss zwischen 1 und 120 Monaten liegen.", "La frequenza della manutenzione deve essere compresa tra 1 e 120 mesi.", "La fréquence de maintenance doit être comprise entre 1 et 120 mois."),
        ["Zgłoszenie serwisowe nie istnieje."] = new("The service ticket does not exist.", "El ticket de servicio no existe.", "Das Service-Ticket existiert nicht.", "Il ticket di assistenza non esiste.", "Le ticket de service n'existe pas."),
        ["Wybrana inspekcja aktywa nie istnieje."] = new("The selected asset inspection does not exist.", "La inspección de activo seleccionada no existe.", "Die ausgewählte Asset-Kontrolle existiert nicht.", "Il controllo dell'asset selezionato non esiste.", "Le contrôle d'actif sélectionné n'existe pas."),
        ["Wybrana inspekcja dotyczy innego aktywa."] = new("The selected inspection belongs to a different asset.", "La inspección seleccionada pertenece a otro activo.", "Die ausgewählte Kontrolle gehört zu einem anderen Asset.", "Il controllo selezionato riguarda un altro asset.", "Le contrôle sélectionné concerne un autre actif."),
        ["Nieprawidłowy status docelowy aktywa po zamknięciu zgłoszenia serwisowego."] = new("Invalid target asset status after closing the service ticket.", "Estado de activo de destino no válido tras cerrar el ticket de servicio.", "Ungültiger Ziel-Asset-Status nach dem Schließen des Service-Tickets.", "Stato di destinazione dell'asset non valido dopo la chiusura del ticket di assistenza.", "Statut d'actif cible invalide après la clôture du ticket de service."),
        ["Vendor zgłoszenia serwisowego jest wymagany."] = new("The service ticket vendor is required.", "El proveedor del ticket de servicio es obligatorio.", "Der Dienstleister des Service-Tickets ist erforderlich.", "Il fornitore del ticket di assistenza è obbligatorio.", "Le prestataire du ticket de service est obligatoire."),
        ["Szacowany koszt nie może być ujemny."] = new("The estimated cost cannot be negative.", "El coste estimado no puede ser negativo.", "Die geschätzten Kosten dürfen nicht negativ sein.", "Il costo stimato non può essere negativo.", "Le coût estimé ne peut pas être négatif."),
        ["Koszt końcowy nie może być ujemny."] = new("The final cost cannot be negative.", "El coste final no puede ser negativo.", "Die Endkosten dürfen nicht negativ sein.", "Il costo finale non può essere negativo.", "Le coût final ne peut pas être négatif."),
        ["Zamkniętego zgłoszenia serwisowego nie można edytować."] = new("A closed service ticket cannot be edited.", "No se puede editar un ticket de servicio cerrado.", "Ein geschlossenes Service-Ticket kann nicht bearbeitet werden.", "Un ticket di assistenza chiuso non può essere modificato.", "Un ticket de service clôturé ne peut pas être modifié."),
        ["Przejście do statusu Completed lub Cancelled wymaga metody Complete() lub Cancel()."] = new("Moving to the Completed or Cancelled status requires the Complete() or Cancel() method.", "Pasar al estado Completed o Cancelled requiere el método Complete() o Cancel().", "Der Wechsel in den Status Completed oder Cancelled erfordert die Methode Complete() oder Cancel().", "Il passaggio allo stato Completed o Cancelled richiede il metodo Complete() o Cancel().", "Le passage au statut Completed ou Cancelled nécessite la méthode Complete() ou Cancel()."),
        ["Nie można zmienić statusu zamkniętego zgłoszenia serwisowego."] = new("The status of a closed service ticket cannot be changed.", "No se puede cambiar el estado de un ticket de servicio cerrado.", "Der Status eines geschlossenen Service-Tickets kann nicht geändert werden.", "Lo stato di un ticket di assistenza chiuso non può essere modificato.", "Le statut d'un ticket de service clôturé ne peut pas être modifié."),
        ["Zgłoszenie serwisowe zostało już zamknięte."] = new("The service ticket has already been closed.", "El ticket de servicio ya ha sido cerrado.", "Das Service-Ticket wurde bereits geschlossen.", "Il ticket di assistenza è già stato chiuso.", "Le ticket de service a déjà été clôturé."),

        // --- Wydania ---
        ["Dodaj co najmniej jedno zdjęcie."] = new("Add at least one photo.", "Añade al menos una foto.", "Fügen Sie mindestens ein Foto hinzu.", "Aggiungi almeno una foto.", "Ajoutez au moins une photo."),
        ["Brak wpisu manifestu dla przesłanego pliku."] = new("There is no manifest entry for the uploaded file.", "No hay ninguna entrada de manifiesto para el archivo subido.", "Für die hochgeladene Datei gibt es keinen Manifest-Eintrag.", "Non esiste una voce di manifest per il file caricato.", "Aucune entrée de manifeste pour le fichier envoyé."),
        ["Zdjęcie dotyczy aktywa spoza wydania."] = new("The photo belongs to an asset outside this assignment.", "La foto corresponde a un activo ajeno a esta entrega.", "Das Foto gehört zu einem Asset außerhalb dieser Übergabe.", "La foto riguarda un asset esterno a questa assegnazione.", "La photo concerne un actif extérieur à cette remise."),
        ["Nowe wydanie można utworzyć tylko dla aktywnej osoby."] = new("A new assignment can only be created for an active person.", "Solo se puede crear una nueva entrega para una persona activa.", "Eine neue Übergabe kann nur für eine aktive Person erstellt werden.", "Una nuova assegnazione può essere creata solo per una persona attiva.", "Une nouvelle remise ne peut être créée que pour une personne active."),
        ["Nie możesz zaakceptować cudzego wydania."] = new("You cannot accept someone else's assignment.", "No puedes aceptar la entrega de otra persona.", "Sie können die Übergabe einer anderen Person nicht akzeptieren.", "Non puoi accettare l'assegnazione di un'altra persona.", "Vous ne pouvez pas accepter la remise d'une autre personne."),
        ["To aktywo nie należy do tego wydania."] = new("This asset does not belong to this assignment.", "Este activo no pertenece a esta entrega.", "Dieses Asset gehört nicht zu dieser Übergabe.", "Questo asset non appartiene a questa assegnazione.", "Cet actif n'appartient pas à cette remise."),
        ["Hash tokenu jest wymagany."] = new("The token hash is required.", "El hash del token es obligatorio.", "Der Token-Hash ist erforderlich.", "L'hash del token è obbligatorio.", "Le hachage du jeton est obligatoire."),
        ["Nie można zwrócić aktywa dla anulowanego wydania."] = new("An asset cannot be returned for a cancelled assignment.", "No se puede devolver un activo de una entrega cancelada.", "Für eine stornierte Übergabe kann kein Asset zurückgegeben werden.", "Non è possibile restituire un asset per un'assegnazione annullata.", "Un actif ne peut pas être retourné pour une remise annulée."),
        ["Zutylizowanego aktywa nie można przypisać."] = new("A disposed asset cannot be assigned.", "No se puede asignar un activo dado de baja.", "Ein entsorgtes Asset kann nicht zugewiesen werden.", "Un asset dismesso non può essere assegnato.", "Un actif mis au rebut ne peut pas être attribué."),

        // --- Kampanie inwentaryzacyjne ---
        ["Kampania nie istnieje."] = new("The campaign does not exist.", "La campaña no existe.", "Die Kampagne existiert nicht.", "La campagna non esiste.", "La campagne n'existe pas."),
        ["Kampanię można edytować tylko w statusie roboczym."] = new("A campaign can only be edited while in draft status.", "Una campaña solo se puede editar en estado borrador.", "Eine Kampagne kann nur im Entwurfsstatus bearbeitet werden.", "Una campagna può essere modificata solo nello stato di bozza.", "Une campagne ne peut être modifiée qu'à l'état de brouillon."),
        ["Nieprawidłowy zakres kampanii."] = new("Invalid campaign scope.", "Alcance de campaña no válido.", "Ungültiger Kampagnenumfang.", "Ambito della campagna non valido.", "Périmètre de campagne invalide."),
        ["Link jest nieprawidłowy lub wygasł."] = new("The link is invalid or has expired.", "El enlace no es válido o ha caducado.", "Der Link ist ungültig oder abgelaufen.", "Il link non è valido o è scaduto.", "Le lien est invalide ou a expiré."),
        ["Odpowiedzi zostały już wysłane i nie można ich zmienić."] = new("The answers have already been submitted and cannot be changed.", "Las respuestas ya se han enviado y no se pueden modificar.", "Die Antworten wurden bereits übermittelt und können nicht geändert werden.", "Le risposte sono già state inviate e non possono essere modificate.", "Les réponses ont déjà été envoyées et ne peuvent pas être modifiées."),
        ["Odpowiedzi zostały już wysłane."] = new("The answers have already been submitted.", "Las respuestas ya se han enviado.", "Die Antworten wurden bereits übermittelt.", "Le risposte sono già state inviate.", "Les réponses ont déjà été envoyées."),
        ["Pozycja nie istnieje."] = new("The item does not exist.", "El elemento no existe.", "Der Eintrag existiert nicht.", "La voce non esiste.", "L'élément n'existe pas."),
        ["Uczestnik nie istnieje."] = new("The participant does not exist.", "El participante no existe.", "Der Teilnehmer existiert nicht.", "Il partecipante non esiste.", "Le participant n'existe pas."),
        ["Nowy właściciel jest wymagany dla tego rozstrzygnięcia."] = new("A new owner is required for this resolution.", "Se requiere un nuevo responsable para esta resolución.", "Für diese Entscheidung ist ein neuer Eigentümer erforderlich.", "Per questa risoluzione è richiesto un nuovo responsabile.", "Un nouveau responsable est requis pour cette résolution."),
        ["Wybrany nowy właściciel nie istnieje."] = new("The selected new owner does not exist.", "El nuevo responsable seleccionado no existe.", "Der ausgewählte neue Eigentümer existiert nicht.", "Il nuovo responsabile selezionato non esiste.", "Le nouveau responsable sélectionné n'existe pas."),
        ["Nazwa kampanii jest wymagana."] = new("The campaign name is required.", "El nombre de la campaña es obligatorio.", "Der Kampagnenname ist erforderlich.", "Il nome della campagna è obbligatorio.", "Le nom de la campagne est obligatoire."),
        ["Kampanię można uruchomić tylko ze statusu roboczego."] = new("A campaign can only be started from draft status.", "Una campaña solo se puede iniciar desde el estado borrador.", "Eine Kampagne kann nur aus dem Entwurfsstatus gestartet werden.", "Una campagna può essere avviata solo dallo stato di bozza.", "Une campagne ne peut être lancée qu'à partir de l'état de brouillon."),
        ["Kampanię można zakończyć tylko ze statusu aktywnego albo w przeglądzie."] = new("A campaign can only be closed from the active or in-review status.", "Una campaña solo se puede cerrar desde el estado activo o en revisión.", "Eine Kampagne kann nur aus dem Status aktiv oder in Prüfung abgeschlossen werden.", "Una campagna può essere chiusa solo dallo stato attivo o in revisione.", "Une campagne ne peut être clôturée qu'à partir de l'état actif ou en révision."),
        ["Nie można anulować zakończonej kampanii."] = new("A completed campaign cannot be cancelled.", "No se puede cancelar una campaña finalizada.", "Eine abgeschlossene Kampagne kann nicht storniert werden.", "Una campagna conclusa non può essere annullata.", "Une campagne terminée ne peut pas être annulée."),
        ["Nowy termin musi być późniejszy niż obecny."] = new("The new deadline must be later than the current one.", "El nuevo plazo debe ser posterior al actual.", "Die neue Frist muss später als die aktuelle sein.", "La nuova scadenza deve essere successiva a quella attuale.", "La nouvelle échéance doit être postérieure à l'actuelle."),
        ["Rozstrzygnięcie musi być inne niż None."] = new("The resolution must be other than None.", "La resolución debe ser distinta de None.", "Die Entscheidung muss von None abweichen.", "La risoluzione deve essere diversa da None.", "La résolution doit être différente de None."),
        ["Pozycja jest już rozstrzygnięta."] = new("The item has already been resolved.", "El elemento ya está resuelto.", "Der Eintrag wurde bereits entschieden.", "La voce è già stata risolta.", "L'élément est déjà résolu."),
        ["Uzasadnienie jest wymagane dla tego rozstrzygnięcia."] = new("A justification is required for this resolution.", "Se requiere una justificación para esta resolución.", "Für diese Entscheidung ist eine Begründung erforderlich.", "Per questa risoluzione è richiesta una motivazione.", "Une justification est requise pour cette résolution."),
        ["Osoba rozstrzygająca jest wymagana."] = new("The resolving person is required.", "La persona que resuelve es obligatoria.", "Die entscheidende Person ist erforderlich.", "La persona che risolve è obbligatoria.", "La personne qui résout est obligatoire."),
        ["Adres e-mail uczestnika jest wymagany."] = new("The participant's email address is required.", "La dirección de correo del participante es obligatoria.", "Die E-Mail-Adresse des Teilnehmers ist erforderlich.", "L'indirizzo e-mail del partecipante è obbligatorio.", "L'adresse e-mail du participant est obligatoire."),
        ["Ponowne otwarcie jest możliwe tylko dla wysłanych odpowiedzi."] = new("Only submitted answers can be reopened.", "Solo se pueden reabrir las respuestas enviadas.", "Nur übermittelte Antworten können wieder geöffnet werden.", "Solo le risposte inviate possono essere riaperte.", "Seules les réponses envoyées peuvent être rouvertes."),

        // --- Materiał dowodowy ---
        ["Można przesłać maksymalnie 25 zdjęć w jednym żądaniu."] = new("At most 25 photos can be uploaded in a single request.", "Se pueden subir como máximo 25 fotos en una sola solicitud.", "Pro Anfrage können höchstens 25 Fotos hochgeladen werden.", "È possibile caricare al massimo 25 foto in un'unica richiesta.", "Au maximum 25 photos peuvent être envoyées en une seule requête."),
        ["Łączny rozmiar zdjęć w jednym żądaniu może wynosić maksymalnie 25 MB."] = new("The total size of photos in a single request can be at most 25 MB.", "El tamaño total de las fotos en una sola solicitud puede ser como máximo de 25 MB.", "Die Gesamtgröße der Fotos pro Anfrage darf höchstens 25 MB betragen.", "La dimensione totale delle foto in un'unica richiesta può essere al massimo di 25 MB.", "La taille totale des photos dans une seule requête ne peut dépasser 25 Mo."),
        ["Obraz ma zbyt duże wymiary."] = new("The image dimensions are too large.", "Las dimensiones de la imagen son demasiado grandes.", "Die Bildabmessungen sind zu groß.", "Le dimensioni dell'immagine sono eccessive.", "Les dimensions de l'image sont trop grandes."),

        // --- Konta i uprawnienia ---
        ["Nie można dokończyć rejestracji."] = new("Registration cannot be completed.", "No se puede completar el registro.", "Die Registrierung kann nicht abgeschlossen werden.", "Non è possibile completare la registrazione.", "L'inscription ne peut pas être finalisée."),
        ["Tylko właściciel może nadać rolę Właściciela."] = new("Only an owner can grant the Owner role.", "Solo un propietario puede otorgar el rol de Propietario.", "Nur ein Eigentümer kann die Rolle Eigentümer vergeben.", "Solo un proprietario può assegnare il ruolo di Proprietario.", "Seul un propriétaire peut attribuer le rôle Propriétaire."),
        ["Tylko właściciel może modyfikować konto innego właściciela."] = new("Only an owner can modify another owner's account.", "Solo un propietario puede modificar la cuenta de otro propietario.", "Nur ein Eigentümer kann das Konto eines anderen Eigentümers ändern.", "Solo un proprietario può modificare l'account di un altro proprietario.", "Seul un propriétaire peut modifier le compte d'un autre propriétaire."),
        ["W firmie musi pozostać co najmniej jeden aktywny właściciel."] = new("At least one active owner must remain in the company.", "Debe permanecer al menos un propietario activo en la empresa.", "Im Unternehmen muss mindestens ein aktiver Eigentümer verbleiben.", "Nell'azienda deve rimanere almeno un proprietario attivo.", "Au moins un propriétaire actif doit rester dans l'entreprise."),
        ["Powiązany pracownik nie istnieje w tej firmie."] = new("The linked employee does not exist in this company.", "El empleado vinculado no existe en esta empresa.", "Der verknüpfte Mitarbeiter existiert in diesem Unternehmen nicht.", "Il dipendente collegato non esiste in questa azienda.", "L'employé associé n'existe pas dans cette entreprise."),
        ["Ten pracownik jest już powiązany z innym loginem."] = new("This employee is already linked to another sign-in.", "Este empleado ya está vinculado a otro inicio de sesión.", "Dieser Mitarbeiter ist bereits mit einer anderen Anmeldung verknüpft.", "Questo dipendente è già collegato a un altro accesso.", "Cet employé est déjà associé à un autre identifiant."),
        ["Imię i nazwisko jest wymagane."] = new("First and last name are required.", "El nombre y los apellidos son obligatorios.", "Vor- und Nachname sind erforderlich.", "Nome e cognome sono obbligatori.", "Le prénom et le nom sont obligatoires."),
        ["Kod uwierzytelniający został już użyty."] = new("The authentication code has already been used.", "El código de autenticación ya ha sido utilizado.", "Der Authentifizierungscode wurde bereits verwendet.", "Il codice di autenticazione è già stato utilizzato.", "Le code d'authentification a déjà été utilisé."),

        // --- Offboarding ---
        ["Sprawa offboardingowa nie istnieje."] = new("The offboarding case does not exist.", "El caso de offboarding no existe.", "Der Offboarding-Vorgang existiert nicht.", "La pratica di offboarding non esiste.", "Le dossier d'offboarding n'existe pas."),
        ["Osoba nie istnieje."] = new("The person does not exist.", "La persona no existe.", "Die Person existiert nicht.", "La persona non esiste.", "La personne n'existe pas."),
        ["Sprawę offboardingową można utworzyć tylko dla aktywnej osoby."] = new("An offboarding case can only be created for an active person.", "Solo se puede crear un caso de offboarding para una persona activa.", "Ein Offboarding-Vorgang kann nur für eine aktive Person erstellt werden.", "Una pratica di offboarding può essere creata solo per una persona attiva.", "Un dossier d'offboarding ne peut être créé que pour une personne active."),
        ["Dla tej osoby istnieje już aktywna sprawa offboardingowa."] = new("An active offboarding case already exists for this person.", "Ya existe un caso de offboarding activo para esta persona.", "Für diese Person existiert bereits ein aktiver Offboarding-Vorgang.", "Per questa persona esiste già una pratica di offboarding attiva.", "Un dossier d'offboarding actif existe déjà pour cette personne."),
        ["Wybrany właściciel procesu nie istnieje."] = new("The selected process owner does not exist.", "El responsable del proceso seleccionado no existe.", "Der ausgewählte Prozessverantwortliche existiert nicht.", "Il responsabile del processo selezionato non esiste.", "Le responsable du processus sélectionné n'existe pas."),
        ["Osoba przypisana do sprawy nie istnieje."] = new("The person assigned to the case does not exist.", "La persona asignada al caso no existe.", "Die dem Vorgang zugewiesene Person existiert nicht.", "La persona assegnata alla pratica non esiste.", "La personne affectée au dossier n'existe pas."),
        ["Ta pozycja nie dotyczy zwrotu aktywa."] = new("This item is not about an asset return.", "Este elemento no corresponde a la devolución de un activo.", "Bei diesem Eintrag geht es nicht um eine Asset-Rückgabe.", "Questa voce non riguarda la restituzione di un asset.", "Cet élément ne concerne pas la restitution d'un actif."),
        ["Ta pozycja nie dotyczy aktywa."] = new("This item is not about an asset.", "Este elemento no corresponde a un activo.", "Bei diesem Eintrag geht es nicht um ein Asset.", "Questa voce non riguarda un asset.", "Cet élément ne concerne pas un actif."),
        ["Ta pozycja nie dotyczy zwolnienia licencji."] = new("This item is not about releasing a license.", "Este elemento no corresponde a la liberación de una licencia.", "Bei diesem Eintrag geht es nicht um die Freigabe einer Lizenz.", "Questa voce non riguarda il rilascio di una licenza.", "Cet élément ne concerne pas la libération d'une licence."),
        ["Osoba nie ma adresu e-mail - nie można wysłać linku."] = new("The person has no email address - the link cannot be sent.", "La persona no tiene dirección de correo - no se puede enviar el enlace.", "Die Person hat keine E-Mail-Adresse - der Link kann nicht gesendet werden.", "La persona non ha un indirizzo e-mail - non è possibile inviare il link.", "La personne n'a pas d'adresse e-mail - le lien ne peut pas être envoyé."),
        ["Sprawę offboardingową można edytować tylko w statusie roboczym."] = new("An offboarding case can only be edited while in draft status.", "Un caso de offboarding solo se puede editar en estado borrador.", "Ein Offboarding-Vorgang kann nur im Entwurfsstatus bearbeitet werden.", "Una pratica di offboarding può essere modificata solo nello stato di bozza.", "Un dossier d'offboarding ne peut être modifié qu'à l'état de brouillon."),
        ["Offboarding można uruchomić tylko ze statusu roboczego."] = new("Offboarding can only be started from draft status.", "El offboarding solo se puede iniciar desde el estado borrador.", "Das Offboarding kann nur aus dem Entwurfsstatus gestartet werden.", "L'offboarding può essere avviato solo dallo stato di bozza.", "L'offboarding ne peut être lancé qu'à partir de l'état de brouillon."),
        ["Nie można zamknąć sprawy z nierozliczonymi wymaganymi pozycjami."] = new("A case with unsettled required items cannot be closed.", "No se puede cerrar un caso con elementos obligatorios sin liquidar.", "Ein Vorgang mit offenen Pflichteinträgen kann nicht abgeschlossen werden.", "Non è possibile chiudere una pratica con voci obbligatorie non regolarizzate.", "Un dossier comportant des éléments obligatoires non réglés ne peut pas être clôturé."),
        ["Nie można zamknąć sprawy przed zaplanowaną dezaktywacją osoby."] = new("A case cannot be closed before the person's scheduled deactivation.", "No se puede cerrar un caso antes de la desactivación programada de la persona.", "Ein Vorgang kann nicht vor der geplanten Deaktivierung der Person abgeschlossen werden.", "Non è possibile chiudere una pratica prima della disattivazione programmata della persona.", "Un dossier ne peut pas être clôturé avant la désactivation planifiée de la personne."),
        ["Nie można anulować zakończonej sprawy."] = new("A completed case cannot be cancelled.", "No se puede cancelar un caso finalizado.", "Ein abgeschlossener Vorgang kann nicht storniert werden.", "Una pratica conclusa non può essere annullata.", "Un dossier terminé ne peut pas être annulé."),
        ["Nie można anulować offboardingu po dezaktywacji osoby."] = new("Offboarding cannot be cancelled after the person has been deactivated.", "No se puede cancelar el offboarding después de desactivar a la persona.", "Das Offboarding kann nach der Deaktivierung der Person nicht storniert werden.", "L'offboarding non può essere annullato dopo la disattivazione della persona.", "L'offboarding ne peut pas être annulé après la désactivation de la personne."),
        ["Powód anulowania jest wymagany."] = new("A cancellation reason is required.", "Se requiere un motivo de cancelación.", "Ein Stornierungsgrund ist erforderlich.", "È richiesta una motivazione per l'annullamento.", "Un motif d'annulation est obligatoire."),
        ["Przywrócenie zatrudnienia dotyczy tylko spraw po dezaktywacji osoby."] = new("Reinstatement applies only to cases after the person has been deactivated.", "La readmisión solo se aplica a casos posteriores a la desactivación de la persona.", "Die Wiedereinstellung gilt nur für Vorgänge nach der Deaktivierung der Person.", "La reintegrazione riguarda solo le pratiche successive alla disattivazione della persona.", "La réintégration ne concerne que les dossiers postérieurs à la désactivation de la personne."),
        ["Nazwa pozycji jest wymagana."] = new("The item name is required.", "El nombre del elemento es obligatorio.", "Der Name des Eintrags ist erforderlich.", "Il nome della voce è obbligatorio.", "Le nom de l'élément est obligatoire."),
        ["Nie można zmienić odpowiedzi pracownika po rozliczeniu pozycji."] = new("The employee's answer cannot be changed after the item has been settled.", "No se puede cambiar la respuesta del empleado tras liquidar el elemento.", "Die Antwort des Mitarbeiters kann nach Abrechnung des Eintrags nicht geändert werden.", "La risposta del dipendente non può essere modificata dopo la regolarizzazione della voce.", "La réponse de l'employé ne peut pas être modifiée après le règlement de l'élément."),
        ["Pozycja jest już rozliczona."] = new("The item has already been settled.", "El elemento ya está liquidado.", "Der Eintrag wurde bereits abgerechnet.", "La voce è già stata regolarizzata.", "L'élément est déjà réglé."),
        ["Ten status wymaga jawnego rozstrzygnięcia (Missing, Damaged albo Retained)."] = new("This status requires an explicit resolution (Missing, Damaged or Retained).", "Este estado requiere una resolución explícita (Missing, Damaged o Retained).", "Dieser Status erfordert eine ausdrückliche Entscheidung (Missing, Damaged oder Retained).", "Questo stato richiede una risoluzione esplicita (Missing, Damaged o Retained).", "Ce statut nécessite une résolution explicite (Missing, Damaged ou Retained)."),
        ["Uzasadnienie jest wymagane."] = new("A justification is required.", "Se requiere una justificación.", "Eine Begründung ist erforderlich.", "È richiesta una motivazione.", "Une justification est obligatoire."),
        ["Powód odstąpienia jest wymagany."] = new("A waiver reason is required.", "Se requiere un motivo de exención.", "Ein Verzichtsgrund ist erforderlich.", "È richiesta una motivazione per la rinuncia.", "Un motif de renonciation est obligatoire."),
        ["Osoba odstępująca jest wymagana."] = new("The waiving person is required.", "La persona que concede la exención es obligatoria.", "Die verzichtende Person ist erforderlich.", "La persona che rinuncia è obbligatoria.", "La personne qui renonce est obligatoire."),

        // --- Onboarding i osoby ---
        ["Pakiet onboardingowy można utworzyć tylko dla aktywnej osoby."] = new("An onboarding package can only be created for an active person.", "Solo se puede crear un paquete de onboarding para una persona activa.", "Ein Onboarding-Paket kann nur für eine aktive Person erstellt werden.", "Un pacchetto di onboarding può essere creato solo per una persona attiva.", "Un pack d'onboarding ne peut être créé que pour une personne active."),
        ["Osoba nie może być swoim własnym przełożonym."] = new("A person cannot be their own manager.", "Una persona no puede ser su propio responsable.", "Eine Person kann nicht ihr eigener Vorgesetzter sein.", "Una persona non può essere responsabile di sé stessa.", "Une personne ne peut pas être son propre responsable."),
        ["Wybrany przełożony nie istnieje."] = new("The selected manager does not exist.", "El responsable seleccionado no existe.", "Der ausgewählte Vorgesetzte existiert nicht.", "Il responsabile selezionato non esiste.", "Le responsable sélectionné n'existe pas."),
        ["Wybrany domyślny przełożony nie istnieje."] = new("The selected default manager does not exist.", "El responsable predeterminado seleccionado no existe.", "Der ausgewählte Standard-Vorgesetzte existiert nicht.", "Il responsabile predefinito selezionato non esiste.", "Le responsable par défaut sélectionné n'existe pas."),
        ["Nieobsługiwany preferowany język."] = new("Unsupported preferred language.", "Idioma preferido no admitido.", "Nicht unterstützte bevorzugte Sprache.", "Lingua preferita non supportata.", "Langue préférée non prise en charge."),
        ["Offboarding można rozpocząć tylko dla aktywnej osoby."] = new("Offboarding can only be started for an active person.", "El offboarding solo se puede iniciar para una persona activa.", "Das Offboarding kann nur für eine aktive Person gestartet werden.", "L'offboarding può essere avviato solo per una persona attiva.", "L'offboarding ne peut être lancé que pour une personne active."),

        // --- Procedury ---
        ["Plik PDF ma nieprawidłową sygnaturę."] = new("The PDF file has an invalid signature.", "El archivo PDF tiene una firma no válida.", "Die PDF-Datei hat eine ungültige Signatur.", "Il file PDF ha una firma non valida.", "Le fichier PDF a une signature invalide."),
        ["Plik DOCX ma nieprawidłową strukturę."] = new("The DOCX file has an invalid structure.", "El archivo DOCX tiene una estructura no válida.", "Die DOCX-Datei hat eine ungültige Struktur.", "Il file DOCX ha una struttura non valida.", "Le fichier DOCX a une structure invalide."),
        ["Plik TXT musi być poprawnym tekstem UTF-8 bez danych binarnych."] = new("The TXT file must be valid UTF-8 text without binary data.", "El archivo TXT debe ser texto UTF-8 válido sin datos binarios.", "Die TXT-Datei muss gültiger UTF-8-Text ohne Binärdaten sein.", "Il file TXT deve essere testo UTF-8 valido senza dati binari.", "Le fichier TXT doit être du texte UTF-8 valide sans données binaires."),
        ["Dozwolone formaty dokumentów procedur to PDF, DOCX i TXT."] = new("The allowed procedure document formats are PDF, DOCX and TXT.", "Los formatos permitidos para documentos de procedimiento son PDF, DOCX y TXT.", "Zulässige Formate für Prozedurdokumente sind PDF, DOCX und TXT.", "I formati consentiti per i documenti di procedura sono PDF, DOCX e TXT.", "Les formats autorisés pour les documents de procédure sont PDF, DOCX et TXT."),
        ["Nazwa pliku nie może zawierać ścieżki katalogów."] = new("The file name cannot contain a directory path.", "El nombre del archivo no puede contener una ruta de directorios.", "Der Dateiname darf keinen Verzeichnispfad enthalten.", "Il nome del file non può contenere un percorso di directory.", "Le nom du fichier ne peut pas contenir de chemin de répertoire."),
        ["Nazwa pliku zawiera niedozwolone znaki."] = new("The file name contains disallowed characters.", "El nombre del archivo contiene caracteres no permitidos.", "Der Dateiname enthält unzulässige Zeichen.", "Il nome del file contiene caratteri non consentiti.", "Le nom du fichier contient des caractères non autorisés."),
        ["Nie można edytować zarchiwizowanej procedury. Utwórz nową wersję."] = new("An archived procedure cannot be edited. Create a new version.", "No se puede editar un procedimiento archivado. Crea una nueva versión.", "Eine archivierte Prozedur kann nicht bearbeitet werden. Erstellen Sie eine neue Version.", "Una procedura archiviata non può essere modificata. Crea una nuova versione.", "Une procédure archivée ne peut pas être modifiée. Créez une nouvelle version."),

        // --- Subskrypcje ---
        ["Płatności Stripe nie są jeszcze skonfigurowane dla tego planu."] = new("Stripe payments are not configured for this plan yet.", "Los pagos con Stripe aún no están configurados para este plan.", "Stripe-Zahlungen sind für diesen Plan noch nicht konfiguriert.", "I pagamenti Stripe non sono ancora configurati per questo piano.", "Les paiements Stripe ne sont pas encore configurés pour ce forfait."),
        ["Istnieje subskrypcja Stripe wymagająca naprawy lub zarządzania. Użyj portalu rozliczeniowego zamiast tworzyć drugą subskrypcję."] = new("There is a Stripe subscription that needs repair or management. Use the billing portal instead of creating a second subscription.", "Existe una suscripción de Stripe que requiere reparación o gestión. Usa el portal de facturación en lugar de crear una segunda suscripción.", "Es besteht ein Stripe-Abonnement, das repariert oder verwaltet werden muss. Nutzen Sie das Abrechnungsportal, statt ein zweites Abonnement anzulegen.", "Esiste un abbonamento Stripe che richiede correzione o gestione. Usa il portale di fatturazione invece di creare un secondo abbonamento.", "Un abonnement Stripe nécessite une réparation ou une gestion. Utilisez le portail de facturation au lieu de créer un second abonnement."),
        ["Nieprawidłowy webhook Stripe."] = new("Invalid Stripe webhook.", "Webhook de Stripe no válido.", "Ungültiger Stripe-Webhook.", "Webhook Stripe non valido.", "Webhook Stripe invalide."),

        // --- Organizacja i kategorie ---
        ["Okres przechowywania adresu IP jest wymagany, gdy przechwytywanie adresu IP jest włączone."] = new("An IP address retention period is required when IP address capture is enabled.", "Se requiere un periodo de conservación de la dirección IP cuando la captura de IP está activada.", "Bei aktivierter IP-Adress-Erfassung ist eine Aufbewahrungsfrist für die IP-Adresse erforderlich.", "Quando la registrazione dell'indirizzo IP è attiva è richiesto un periodo di conservazione dell'IP.", "Une durée de conservation de l'adresse IP est requise lorsque la capture d'IP est activée."),
        ["Okres przechowywania materiału dowodowego musi być większy od zera."] = new("The evidence retention period must be greater than zero.", "El periodo de conservación del material probatorio debe ser mayor que cero.", "Die Aufbewahrungsfrist für Beweismaterial muss größer als null sein.", "Il periodo di conservazione del materiale probatorio deve essere maggiore di zero.", "La durée de conservation des preuves doit être supérieure à zéro."),
        ["Okres amortyzacji musi być większy od zera."] = new("The depreciation period must be greater than zero.", "El periodo de amortización debe ser mayor que cero.", "Der Abschreibungszeitraum muss größer als null sein.", "Il periodo di ammortamento deve essere maggiore di zero.", "La durée d'amortissement doit être supérieure à zéro."),
        ["Okres amortyzacji nie może przekraczać 1200 miesięcy."] = new("The depreciation period cannot exceed 1200 months.", "El periodo de amortización no puede superar los 1200 meses.", "Der Abschreibungszeitraum darf 1200 Monate nicht überschreiten.", "Il periodo di ammortamento non può superare i 1200 mesi.", "La durée d'amortissement ne peut pas dépasser 1200 mois."),

        // --- Rezerwacje sprzętu ---
        ["Cel rezerwacji jest wymagany."] = new("The reservation purpose is required.", "El propósito de la reserva es obligatorio.", "Der Zweck der Reservierung ist erforderlich.", "Lo scopo della prenotazione è obbligatorio.", "L'objet de la réservation est obligatoire."),
        ["Data zakończenia musi być późniejsza niż data rozpoczęcia."] = new("The end date must be later than the start date.", "La fecha de fin debe ser posterior a la de inicio.", "Das Enddatum muss nach dem Startdatum liegen.", "La data di fine deve essere successiva alla data di inizio.", "La date de fin doit être postérieure à la date de début."),
        ["Wniosek można złożyć tylko ze statusu roboczego."] = new("A request can only be submitted from draft status.", "Una solicitud solo se puede enviar desde el estado borrador.", "Ein Antrag kann nur aus dem Entwurfsstatus eingereicht werden.", "Una richiesta può essere inviata solo dallo stato di bozza.", "Une demande ne peut être soumise qu'à partir de l'état de brouillon."),
        ["Data rozpoczęcia rezerwacji nie może być w przeszłości."] = new("The reservation start date cannot be in the past.", "La fecha de inicio de la reserva no puede ser pasada.", "Das Startdatum der Reservierung darf nicht in der Vergangenheit liegen.", "La data di inizio della prenotazione non può essere passata.", "La date de début de la réservation ne peut pas être dans le passé."),
        ["Wniosek musi zawierać co najmniej jedną pozycję."] = new("A request must contain at least one item.", "Una solicitud debe contener al menos un elemento.", "Ein Antrag muss mindestens einen Eintrag enthalten.", "Una richiesta deve contenere almeno una voce.", "Une demande doit contenir au moins un élément."),
        ["Zatwierdzić można tylko wniosek oczekujący na akceptację."] = new("Only a request pending approval can be approved.", "Solo se puede aprobar una solicitud pendiente de aprobación.", "Nur ein Antrag, der auf Genehmigung wartet, kann genehmigt werden.", "Può essere approvata solo una richiesta in attesa di approvazione.", "Seule une demande en attente d'approbation peut être approuvée."),
        ["Odrzucić można tylko wniosek oczekujący na akceptację."] = new("Only a request pending approval can be rejected.", "Solo se puede rechazar una solicitud pendiente de aprobación.", "Nur ein Antrag, der auf Genehmigung wartet, kann abgelehnt werden.", "Può essere respinta solo una richiesta in attesa di approvazione.", "Seule une demande en attente d'approbation peut être rejetée."),
        ["Powód odrzucenia jest wymagany."] = new("A rejection reason is required.", "Se requiere un motivo de rechazo.", "Ein Ablehnungsgrund ist erforderlich.", "È richiesta una motivazione per il rifiuto.", "Un motif de rejet est obligatoire."),
        ["Anulować można tylko wniosek przed wydaniem sprzętu."] = new("Only a request can be cancelled before the equipment is handed over.", "Solo se puede cancelar una solicitud antes de entregar el equipo.", "Ein Antrag kann nur vor der Übergabe der Ausrüstung storniert werden.", "Una richiesta può essere annullata solo prima della consegna dell'attrezzatura.", "Une demande ne peut être annulée qu'avant la remise du matériel."),
        ["Wniosek można edytować tylko w statusie roboczym."] = new("A request can only be edited while in draft status.", "Una solicitud solo se puede editar en estado borrador.", "Ein Antrag kann nur im Entwurfsstatus bearbeitet werden.", "Una richiesta può essere modificata solo nello stato di bozza.", "Une demande ne peut être modifiée qu'à l'état de brouillon."),
        ["Pozycje wniosku można edytować tylko w statusie roboczym."] = new("Request items can only be edited while in draft status.", "Los elementos de la solicitud solo se pueden editar en estado borrador.", "Antragspositionen können nur im Entwurfsstatus bearbeitet werden.", "Le voci della richiesta possono essere modificate solo nello stato di bozza.", "Les éléments de la demande ne peuvent être modifiés qu'à l'état de brouillon."),
        ["Wydać sprzęt można tylko z zatwierdzonego wniosku gotowego do odbioru."] = new("Equipment can only be handed over from an approved request ready for pickup.", "El equipo solo se puede entregar desde una solicitud aprobada lista para su recogida.", "Ausrüstung kann nur aus einem genehmigten, abholbereiten Antrag übergeben werden.", "L'attrezzatura può essere consegnata solo da una richiesta approvata e pronta per il ritiro.", "Le matériel ne peut être remis qu'à partir d'une demande approuvée et prête pour le retrait."),
        ["Wszystkie pozycje muszą mieć przydzielone aktywo przed wydaniem."] = new("All items must have an asset allocated before handover.", "Todos los elementos deben tener un activo asignado antes de la entrega.", "Allen Positionen muss vor der Übergabe ein Asset zugeordnet sein.", "Tutte le voci devono avere un asset assegnato prima della consegna.", "Tous les éléments doivent avoir un actif attribué avant la remise."),
        ["Zakończyć można tylko wydaną rezerwację."] = new("Only a handed-over reservation can be completed.", "Solo se puede finalizar una reserva ya entregada.", "Nur eine übergebene Reservierung kann abgeschlossen werden.", "Solo una prenotazione consegnata può essere conclusa.", "Seule une réservation remise peut être clôturée."),
        ["Wymagana ilość musi być większa od zera."] = new("The required quantity must be greater than zero.", "La cantidad requerida debe ser mayor que cero.", "Die benötigte Menge muss größer als null sein.", "La quantità richiesta deve essere maggiore di zero.", "La quantité requise doit être supérieure à zéro."),
        ["Przydzielić można tylko pozycję oczekującą."] = new("Only a pending item can be allocated.", "Solo se puede asignar un elemento pendiente.", "Nur ein ausstehender Eintrag kann zugeordnet werden.", "Può essere assegnata solo una voce in attesa.", "Seul un élément en attente peut être attribué."),
        ["Powód zamiany jest wymagany."] = new("A substitution reason is required.", "Se requiere un motivo de sustitución.", "Ein Austauschgrund ist erforderlich.", "È richiesta una motivazione per la sostituzione.", "Un motif de remplacement est obligatoire."),
        ["Nie można zamienić pozycji bez przydzielonego aktywa."] = new("An item without an allocated asset cannot be substituted.", "No se puede sustituir un elemento sin activo asignado.", "Ein Eintrag ohne zugeordnetes Asset kann nicht ausgetauscht werden.", "Una voce senza asset assegnato non può essere sostituita.", "Un élément sans actif attribué ne peut pas être remplacé."),
        ["Pozycja jest już rozliczona i nie można jej zamienić."] = new("The item has already been settled and cannot be substituted.", "El elemento ya está liquidado y no se puede sustituir.", "Der Eintrag wurde bereits abgerechnet und kann nicht ausgetauscht werden.", "La voce è già stata regolarizzata e non può essere sostituita.", "L'élément est déjà réglé et ne peut pas être remplacé."),
        ["Nie można wydać pozycji bez przydzielonego aktywa."] = new("An item without an allocated asset cannot be handed over.", "No se puede entregar un elemento sin activo asignado.", "Ein Eintrag ohne zugeordnetes Asset kann nicht übergeben werden.", "Una voce senza asset assegnato non può essere consegnata.", "Un élément sans actif attribué ne peut pas être remis."),

        // --- Warstwa API: logowanie i sesja ---
        // Endpointy zwracaja te komunikaty same, z pominieciem Result/Error, wiec musza wolac
        // ResultExtensions.Localize - patrz komentarz przy tej metodzie.
        ["Zbyt wiele prób. Spróbuj ponownie później."] = new("Too many attempts. Try again later.", "Demasiados intentos. Inténtalo de nuevo más tarde.", "Zu viele Versuche. Versuchen Sie es später erneut.", "Troppi tentativi. Riprova più tardi.", "Trop de tentatives. Réessayez plus tard."),
        ["Zbyt wiele prób logowania. Spróbuj ponownie później."] = new("Too many sign-in attempts. Try again later.", "Demasiados intentos de inicio de sesión. Inténtalo de nuevo más tarde.", "Zu viele Anmeldeversuche. Versuchen Sie es später erneut.", "Troppi tentativi di accesso. Riprova più tardi.", "Trop de tentatives de connexion. Réessayez plus tard."),
        ["Sesja logowania wygasła. Zaloguj się ponownie."] = new("The sign-in session has expired. Please log in again.", "La sesión de inicio de sesión ha caducado. Inicia sesión de nuevo.", "Die Anmeldesitzung ist abgelaufen. Bitte melden Sie sich erneut an.", "La sessione di accesso è scaduta. Accedi di nuovo.", "La session de connexion a expiré. Veuillez vous reconnecter."),
        ["Nieprawidłowa sesja."] = new("Invalid session.", "Sesión no válida.", "Ungültige Sitzung.", "Sessione non valida.", "Session invalide."),
        ["Hasło zostało zmienione."] = new("The password has been changed.", "La contraseña ha sido cambiada.", "Das Passwort wurde geändert.", "La password è stata modificata.", "Le mot de passe a été modifié."),
        ["E-mail został potwierdzony."] = new("The email has been confirmed.", "El correo electrónico ha sido confirmado.", "Die E-Mail-Adresse wurde bestätigt.", "L'e-mail è stata confermata.", "L'e-mail a été confirmée."),
        ["Konto zostało odłączone."] = new("The account has been unlinked.", "La cuenta ha sido desvinculada.", "Das Konto wurde getrennt.", "L'account è stato scollegato.", "Le compte a été dissocié."),
        ["Dwuskładnikowe uwierzytelnianie zostało wyłączone."] = new("Two-factor authentication has been disabled.", "La autenticación de dos factores ha sido desactivada.", "Die Zwei-Faktor-Authentifizierung wurde deaktiviert.", "L'autenticazione a due fattori è stata disattivata.", "L'authentification à deux facteurs a été désactivée."),
        ["Jeśli podany adres e-mail istnieje w systemie, wysłaliśmy kod do resetu hasła."] = new("If the given email address exists in the system, we have sent a password reset code.", "Si la dirección de correo indicada existe en el sistema, hemos enviado un código para restablecer la contraseña.", "Wenn die angegebene E-Mail-Adresse im System existiert, haben wir einen Code zum Zurücksetzen des Passworts gesendet.", "Se l'indirizzo e-mail indicato esiste nel sistema, abbiamo inviato un codice per reimpostare la password.", "Si l'adresse e-mail indiquée existe dans le système, nous avons envoyé un code de réinitialisation du mot de passe."),
        ["Jeśli konto oczekuje na potwierdzenie, wysłaliśmy nowy kod."] = new("If the account is awaiting confirmation, we have sent a new code.", "Si la cuenta está pendiente de confirmación, hemos enviado un código nuevo.", "Wenn das Konto auf die Bestätigung wartet, haben wir einen neuen Code gesendet.", "Se l'account è in attesa di conferma, abbiamo inviato un nuovo codice.", "Si le compte est en attente de confirmation, nous avons envoyé un nouveau code."),

        // --- Warstwa API: przesyłanie plików (multipart) ---
        ["Wyślij plik jako multipart/form-data."] = new("Send the file as multipart/form-data.", "Envía el archivo como multipart/form-data.", "Senden Sie die Datei als multipart/form-data.", "Invia il file come multipart/form-data.", "Envoyez le fichier en multipart/form-data."),
        ["Wyślij żądanie jako multipart/form-data."] = new("Send the request as multipart/form-data.", "Envía la solicitud como multipart/form-data.", "Senden Sie die Anfrage als multipart/form-data.", "Invia la richiesta come multipart/form-data.", "Envoyez la requête en multipart/form-data."),
        ["Wybierz zdjęcie."] = new("Select a photo.", "Selecciona una foto.", "Wählen Sie ein Foto aus.", "Seleziona una foto.", "Sélectionnez une photo."),
        ["Wybierz plik logo."] = new("Choose a logo file.", "Elige un archivo de logotipo.", "Wählen Sie eine Logo-Datei aus.", "Scegli un file di logo.", "Choisissez un fichier de logo."),
        ["Nieprawidłowy etap materiału dowodowego."] = new("Invalid evidence phase.", "Fase del material probatorio no válida.", "Ungültige Beweismaterial-Phase.", "Fase del materiale probatorio non valida.", "Phase de preuve invalide."),
        ["Nieprawidłowy identyfikator wydania."] = new("Invalid assignment identifier.", "Identificador de entrega no válido.", "Ungültige Übergabe-ID.", "Identificativo dell'assegnazione non valido.", "Identifiant de remise invalide."),
        ["Nieprawidłowe dane wydania."] = new("Invalid assignment data.", "Datos de la entrega no válidos.", "Ungültige Übergabedaten.", "Dati dell'assegnazione non validi.", "Données de remise invalides."),
        ["Nieprawidłowe dane zwrotu."] = new("Invalid return data.", "Datos de la devolución no válidos.", "Ungültige Rückgabedaten.", "Dati della restituzione non validi.", "Données de restitution invalides."),
        ["Nieprawidłowe dane pakietu pracownika."] = new("Invalid employee package data.", "Datos del paquete del empleado no válidos.", "Ungültige Daten des Mitarbeiterpakets.", "Dati del pacchetto del dipendente non validi.", "Données du pack collaborateur invalides."),
        ["Nieprawidłowy manifest zdjęć."] = new("Invalid photo manifest.", "Manifiesto de fotos no válido.", "Ungültiges Foto-Manifest.", "Manifest delle foto non valido.", "Manifeste de photos invalide."),
        ["Manifest zdjęć jest za duży."] = new("The photo manifest is too large.", "El manifiesto de fotos es demasiado grande.", "Das Foto-Manifest ist zu groß.", "Il manifest delle foto è troppo grande.", "Le manifeste de photos est trop volumineux."),
        ["Manifest zawiera nieprawidłową nazwę pola pliku."] = new("The manifest contains an invalid file field name.", "El manifiesto contiene un nombre de campo de archivo no válido.", "Das Manifest enthält einen ungültigen Dateifeldnamen.", "Il manifest contiene un nome di campo file non valido.", "Le manifeste contient un nom de champ de fichier invalide."),
        ["Żądanie jest za duże."] = new("The request is too large.", "La solicitud es demasiado grande.", "Die Anfrage ist zu groß.", "La richiesta è troppo grande.", "La requête est trop volumineuse."),
        ["Dane wejściowe są zbyt głęboko zagnieżdżone."] = new("The input data is nested too deeply.", "Los datos de entrada están anidados a demasiada profundidad.", "Die Eingabedaten sind zu tief verschachtelt.", "I dati in ingresso sono annidati troppo in profondità.", "Les données d'entrée sont imbriquées trop profondément."),
        ["Nieprawidłowe dane wejściowe."] = new("Invalid input data.", "Datos de entrada no válidos.", "Ungültige Eingabedaten.", "Dati in ingresso non validi.", "Données d'entrée invalides."),
    };

    private sealed record TemplateRule(Regex Pattern, Func<Match, string, string?> Build);

    /// <summary>
    /// Buduje regułę dla komunikatów walidacji kontraktu żądania (<c>RequestObjectValidator</c>),
    /// które różnią się wyłącznie nazwą pola i ewentualnym limitem. Nazwa pola celowo zostaje
    /// nieprzetłumaczona - to identyfikator z kontraktu API, po którym klient podświetla właściwy
    /// input; przetłumaczenie go zerwałoby to powiązanie.
    ///
    /// <paramref name="suffixPattern"/> to reszta polskiego zdania jako wzorzec regex; może zawierać
    /// grupę <c>limit</c>. W tłumaczeniach <c>{0}</c> to nazwa pola, <c>{1}</c> to limit.
    /// </summary>
    private static TemplateRule FieldRule(string suffixPattern, string en, string es, string de, string it, string fr)
    {
        var pattern = new Regex($@"^Pole (?<name>.+?) {suffixPattern}$");
        return new TemplateRule(pattern, (match, language) =>
        {
            var template = language switch
            {
                "en" => en,
                "es" => es,
                "de" => de,
                "it" => it,
                "fr" => fr,
                _ => null,
            };

            if (template is null) return null;

            var limit = match.Groups["limit"].Success ? match.Groups["limit"].Value : string.Empty;
            return string.Format(template, match.Groups["name"].Value, limit);
        });
    }

    /// <summary>Elizja: po francusku „de" przed samogłoską staje się „d'" (de licences, ale d'actifs).</summary>
    private static string FrenchOf(string noun) =>
        "aeiouéèêà".Contains(char.ToLowerInvariant(noun[0])) ? $"d'{noun}" : $"de {noun}";

    // Nazwa zasobu wstawiana w komunikat o przekroczonym limicie planu. Klucz to polski dopełniacz
    // liczby mnogiej użyty w komunikacie źródłowym.
    private static readonly Dictionary<string, (string EnSingular, string EnPlural, string EsPlural, string DeHead, string DePlural, string ItPlural, string FrPlural)> LimitResourceNouns = new()
    {
        ["aktywów"] = ("Asset", "assets", "activos", "Asset", "Assets", "asset", "actifs"),
        ["pracowników"] = ("Employee", "employees", "empleados", "Mitarbeiter", "Mitarbeiter", "dipendenti", "collaborateurs"),
        ["licencji"] = ("License", "licenses", "licencias", "Lizenz", "Lizenzen", "licenze", "licences"),
        ["lokalizacji"] = ("Location", "locations", "ubicaciones", "Standort", "Standorte", "ubicazioni", "emplacements"),
        ["procedur"] = ("Procedure", "procedures", "procedimientos", "Prozedur", "Prozeduren", "procedure", "procédures"),
        ["zespołów"] = ("Team", "teams", "equipos", "Team", "Teams", "team", "équipes"),
        ["zestawów stanowiskowych"] = ("Job profile", "job profiles", "perfiles de puesto", "Stellenprofil", "Stellenprofile", "profili professionali", "profils de poste"),
        ["kategorii"] = ("Category", "categories", "categorías", "Kategorie", "Kategorien", "categorie", "catégories"),
    };

    // Translations for the small set of messages built with interpolated runtime values (e.g. plan
    // name, role key). Regex captures the variable portion(s) so they can be re-inserted into the
    // translated template. Build returns null when no translation is needed for the requested
    // language (i.e. it matches the message's own source language), signalling the caller to fall
    // back to the original message unchanged.
    /// <summary>Podmioty komunikatów o dozwolonym zakresie liczbowym - jeden szablon, kilka pól formularza partii.</summary>
    private static readonly Dictionary<string, Localized> RangeSubjects = new()
    {
        ["Liczba sztuk"] = new("The quantity", "La cantidad", "Die Stückzahl", "La quantità", "La quantité"),
        ["Liczba cyfr numeracji"] = new("The number of digits", "El número de dígitos", "Die Stellenzahl", "Il numero di cifre", "Le nombre de chiffres"),
        ["Numer początkowy"] = new("The start number", "El número inicial", "Die Startnummer", "Il numero iniziale", "Le numéro de départ"),
    };

    private static readonly List<TemplateRule> Templates = new()
    {
        // Zakresy dla pól partii. Sformułowania celowo omijają uzgodnienie rodzaju ("rientrare",
        // "se situer"), żeby jeden szablon obsłużył wszystkie podmioty bez odmiany.
        new TemplateRule(
            new Regex(@"^(?<what>[\p{L} ]+) musi mieścić się w zakresie (?<range>\d+-\d+)\.$"),
            (m, lang) =>
            {
                if (!RangeSubjects.TryGetValue(m.Groups["what"].Value, out var subject)) return null;
                var range = m.Groups["range"].Value;
                var noun = subject.For(lang);
                return lang switch
                {
                    "en" => $"{noun} must be within the range {range}.",
                    "es" => $"{noun} debe estar en el rango {range}.",
                    "de" => $"{noun} muss im Bereich {range} liegen.",
                    "it" => $"{noun} deve rientrare nell'intervallo {range}.",
                    "fr" => $"{noun} doit se situer dans la plage {range}.",
                    _ => null,
                };
            }),
        new TemplateRule(
            new Regex(@"^Tag '(?<tag>[^']+)' przekracza 80 znaków\. Skróć prefiks\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Tag '{m.Groups["tag"].Value}' exceeds 80 characters. Shorten the prefix.",
                "es" => $"La etiqueta '{m.Groups["tag"].Value}' supera los 80 caracteres. Acorta el prefijo.",
                "de" => $"Der Tag '{m.Groups["tag"].Value}' überschreitet 80 Zeichen. Kürzen Sie das Präfix.",
                "it" => $"Il tag '{m.Groups["tag"].Value}' supera gli 80 caratteri. Accorcia il prefisso.",
                "fr" => $"Le tag « {m.Groups["tag"].Value} » dépasse 80 caractères. Raccourcissez le préfixe.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Te tagi są już używane: (?<tags>.+)\. Zmień numer początkowy lub prefiks\.$"),
            (m, lang) => lang switch
            {
                "en" => $"These tags are already in use: {m.Groups["tags"].Value}. Change the start number or the prefix.",
                "es" => $"Estas etiquetas ya están en uso: {m.Groups["tags"].Value}. Cambia el número inicial o el prefijo.",
                "de" => $"Diese Tags werden bereits verwendet: {m.Groups["tags"].Value}. Ändern Sie die Startnummer oder das Präfix.",
                "it" => $"Questi tag sono già in uso: {m.Groups["tags"].Value}. Cambia il numero iniziale o il prefisso.",
                "fr" => $"Ces tags sont déjà utilisés : {m.Groups["tags"].Value}. Changez le numéro de départ ou le préfixe.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Limit aktywów przekroczony\. Plan (?<plan>.+) pozwala na (?<limit>\d+) aktywów, zostało wolnych: (?<free>\d+)\. Przejdź na wyższy plan lub zmniejsz partię\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Asset limit exceeded. The {m.Groups["plan"].Value} plan allows {m.Groups["limit"].Value} assets and {m.Groups["free"].Value} are still free. Upgrade your plan or reduce the batch.",
                "es" => $"Límite de activos superado. El plan {m.Groups["plan"].Value} permite {m.Groups["limit"].Value} activos y quedan {m.Groups["free"].Value} libres. Actualiza tu plan o reduce el lote.",
                "de" => $"Asset-Limit überschritten. Der Plan {m.Groups["plan"].Value} erlaubt {m.Groups["limit"].Value} Assets, davon sind noch {m.Groups["free"].Value} frei. Aktualisieren Sie Ihren Plan oder verkleinern Sie die Charge.",
                "it" => $"Limite di asset superato. Il piano {m.Groups["plan"].Value} consente {m.Groups["limit"].Value} asset e ne restano {m.Groups["free"].Value} liberi. Passa a un piano superiore o riduci il lotto.",
                "fr" => $"Limite d'actifs dépassée. Le forfait {m.Groups["plan"].Value} autorise {m.Groups["limit"].Value} actifs et il en reste {m.Groups["free"].Value} de libres. Passez à un forfait supérieur ou réduisez le lot.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Tekst na etykiecie może mieć maksymalnie (?<max>\d+) znaków\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The label text may be at most {m.Groups["max"].Value} characters long.",
                "es" => $"El texto de la etiqueta puede tener como máximo {m.Groups["max"].Value} caracteres.",
                "de" => $"Der Etikettentext darf höchstens {m.Groups["max"].Value} Zeichen lang sein.",
                "it" => $"Il testo dell'etichetta può contenere al massimo {m.Groups["max"].Value} caratteri.",
                "fr" => $"Le texte de l'étiquette ne peut pas dépasser {m.Groups["max"].Value} caractères.",
                _ => null,
            }),
        // Jeden szablon dla całej rodziny komunikatów o limicie planu - wszystkie zasoby dzielą ten sam
        // limit (OrganizationSubscription.GetResourceLimit), więc różni je tylko nazwa zasobu.
        new TemplateRule(
            new Regex(@"^Limit (?<what>[\p{L} ]+) przekroczony\. Plan (?<plan>.+) pozwala na (?<limit>\d+) [\p{L} ]+\. Przejdź na wyższy plan\.$"),
            (m, lang) =>
            {
                if (!LimitResourceNouns.TryGetValue(m.Groups["what"].Value, out var noun)) return null;
                var plan = m.Groups["plan"].Value;
                var limit = m.Groups["limit"].Value;
                return lang switch
                {
                    "en" => $"{noun.EnSingular} limit exceeded. The {plan} plan allows {limit} {noun.EnPlural}. Upgrade your plan.",
                    "es" => $"Límite de {noun.EsPlural} superado. El plan {plan} permite {limit} {noun.EsPlural}. Actualiza tu plan.",
                    "de" => $"{noun.DeHead}-Limit überschritten. Der Plan {plan} erlaubt {limit} {noun.DePlural}. Aktualisieren Sie Ihren Plan.",
                    "it" => $"Limite di {noun.ItPlural} superato. Il piano {plan} consente {limit} {noun.ItPlural}. Passa a un piano superiore.",
                    "fr" => $"Limite {FrenchOf(noun.FrPlural)} dépassée. Le forfait {plan} autorise {limit} {noun.FrPlural}. Passez à un forfait supérieur.",
                    _ => null,
                };
            }),
        new TemplateRule(
            new Regex(@"^Limit planu (?<plan>.+) \((?<limit>\d+)\) został osiągnięty dla pracowników, aktywów lub procedur\. Przejdź na wyższy plan\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The {m.Groups["plan"].Value} plan limit ({m.Groups["limit"].Value}) has been reached for employees, assets or procedures. Upgrade your plan.",
                "es" => $"Se ha alcanzado el límite del plan {m.Groups["plan"].Value} ({m.Groups["limit"].Value}) para empleados, activos o procedimientos. Actualiza tu plan.",
                "de" => $"Das Limit des Plans {m.Groups["plan"].Value} ({m.Groups["limit"].Value}) ist für Mitarbeiter, Assets oder Prozeduren erreicht. Aktualisieren Sie Ihren Plan.",
                "it" => $"Il limite del piano {m.Groups["plan"].Value} ({m.Groups["limit"].Value}) è stato raggiunto per dipendenti, asset o procedure. Passa a un piano superiore.",
                "fr" => $"La limite du forfait {m.Groups["plan"].Value} ({m.Groups["limit"].Value}) est atteinte pour les collaborateurs, les actifs ou les procédures. Passez à un forfait supérieur.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Nieznana rola: (?<role>.+)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Unknown role: {m.Groups["role"].Value}.",
                "es" => $"Rol desconocido: {m.Groups["role"].Value}.",
                "de" => $"Unbekannte Rolle: {m.Groups["role"].Value}.",
                "it" => $"Ruolo sconosciuto: {m.Groups["role"].Value}.",
                "fr" => $"Rôle inconnu : {m.Groups["role"].Value}.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Nieznane uprawnienie: (?<perm>.+)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Unknown permission: {m.Groups["perm"].Value}.",
                "es" => $"Permiso desconocido: {m.Groups["perm"].Value}.",
                "de" => $"Unbekannte Berechtigung: {m.Groups["perm"].Value}.",
                "it" => $"Autorizzazione sconosciuta: {m.Groups["perm"].Value}.",
                "fr" => $"Autorisation inconnue : {m.Groups["perm"].Value}.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Nieznany status aktywa: (?<status>.+)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Unknown asset status: {m.Groups["status"].Value}.",
                "es" => $"Estado de activo desconocido: {m.Groups["status"].Value}.",
                "de" => $"Unbekannter Asset-Status: {m.Groups["status"].Value}.",
                "it" => $"Stato asset sconosciuto: {m.Groups["status"].Value}.",
                "fr" => $"Statut d'actif inconnu : {m.Groups["status"].Value}.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Pole „(?<label>.+)” jest wymagane\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The field \"{m.Groups["label"].Value}\" is required.",
                "es" => $"El campo «{m.Groups["label"].Value}» es obligatorio.",
                "de" => $"Das Feld „{m.Groups["label"].Value}“ ist erforderlich.",
                "it" => $"Il campo «{m.Groups["label"].Value}» è obbligatorio.",
                "fr" => $"Le champ « {m.Groups["label"].Value} » est obligatoire.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Aby przejść na plan (?<plan>.+), użyj płatności Stripe \(checkout\)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"To move to the {m.Groups["plan"].Value} plan, use Stripe checkout.",
                "es" => $"Para pasar al plan {m.Groups["plan"].Value}, usa el pago con Stripe (checkout).",
                "de" => $"Um auf den {m.Groups["plan"].Value}-Plan zu wechseln, nutzen Sie den Stripe-Checkout.",
                "it" => $"Per passare al piano {m.Groups["plan"].Value}, usa il checkout Stripe.",
                "fr" => $"Pour passer au forfait {m.Groups["plan"].Value}, utilisez le paiement Stripe (checkout).",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Przekroczono limit (?<limit>\d+) akcji moderacyjnych na godzinę\. To zabezpieczenie przed masowym działaniem z przejętego konta — odczekaj i spróbuj ponownie\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The limit of {m.Groups["limit"].Value} moderation actions per hour has been exceeded. This guards against bulk activity from a compromised account - wait and try again.",
                "es" => $"Se ha superado el límite de {m.Groups["limit"].Value} acciones de moderación por hora. Es una protección frente a la actividad masiva desde una cuenta comprometida: espera e inténtalo de nuevo.",
                "de" => $"Das Limit von {m.Groups["limit"].Value} Moderationsaktionen pro Stunde wurde überschritten. Dies schützt vor Massenaktionen aus einem kompromittierten Konto - warten Sie und versuchen Sie es erneut.",
                "it" => $"È stato superato il limite di {m.Groups["limit"].Value} azioni di moderazione all'ora. È una protezione contro le attività di massa da un account compromesso: attendi e riprova.",
                "fr" => $"La limite de {m.Groups["limit"].Value} actions de modération par heure est dépassée. Il s'agit d'une protection contre une activité massive depuis un compte compromis : patientez et réessayez.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Maksymalna liczba progów to (?<count>\d+)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The maximum number of thresholds is {m.Groups["count"].Value}.",
                "es" => $"El número máximo de umbrales es {m.Groups["count"].Value}.",
                "de" => $"Die maximale Anzahl an Schwellenwerten beträgt {m.Groups["count"].Value}.",
                "it" => $"Il numero massimo di soglie è {m.Groups["count"].Value}.",
                "fr" => $"Le nombre maximal de seuils est {m.Groups["count"].Value}.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Próg musi być w zakresie 0–(?<days>\d+) dni\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The threshold must be between 0 and {m.Groups["days"].Value} days.",
                "es" => $"El umbral debe estar entre 0 y {m.Groups["days"].Value} días.",
                "de" => $"Der Schwellenwert muss zwischen 0 und {m.Groups["days"].Value} Tagen liegen.",
                "it" => $"La soglia deve essere compresa tra 0 e {m.Groups["days"].Value} giorni.",
                "fr" => $"Le seuil doit être compris entre 0 et {m.Groups["days"].Value} jours.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Kategoria może mieć maksymalnie (?<count>\d+) pól własnych\.$"),
            (m, lang) => lang switch
            {
                "en" => $"A category can have at most {m.Groups["count"].Value} custom fields.",
                "es" => $"Una categoría puede tener como máximo {m.Groups["count"].Value} campos personalizados.",
                "de" => $"Eine Kategorie kann höchstens {m.Groups["count"].Value} benutzerdefinierte Felder haben.",
                "it" => $"Una categoria può avere al massimo {m.Groups["count"].Value} campi personalizzati.",
                "fr" => $"Une catégorie peut comporter au maximum {m.Groups["count"].Value} champs personnalisés.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Można przesłać maksymalnie (?<count>\d+) plików\.$"),
            (m, lang) => lang switch
            {
                "en" => $"At most {m.Groups["count"].Value} files can be uploaded.",
                "es" => $"Se pueden subir como máximo {m.Groups["count"].Value} archivos.",
                "de" => $"Es können höchstens {m.Groups["count"].Value} Dateien hochgeladen werden.",
                "it" => $"È possibile caricare al massimo {m.Groups["count"].Value} file.",
                "fr" => $"Au maximum {m.Groups["count"].Value} fichiers peuvent être envoyés.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Nazwa pliku może mieć maksymalnie (?<count>\d+) znaków\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The file name can be at most {m.Groups["count"].Value} characters long.",
                "es" => $"El nombre del archivo puede tener como máximo {m.Groups["count"].Value} caracteres.",
                "de" => $"Der Dateiname darf höchstens {m.Groups["count"].Value} Zeichen lang sein.",
                "it" => $"Il nome del file può contenere al massimo {m.Groups["count"].Value} caratteri.",
                "fr" => $"Le nom du fichier peut comporter au maximum {m.Groups["count"].Value} caractères.",
                _ => null,
            }),
        // Walidacja multipart w warstwie API. Nazwa pola jest techniczna (np. 'request',
        // 'evidenceManifest') i celowo zostaje nieprzetlumaczona - klient dopasowuje ja do wlasnego
        // formularza. Osobne reguly od wariantu z cudzyslowem drukarskim („...") dla pol wlasnych
        // aktywa, bo tamten niesie etykiete widoczna dla uzytkownika, a ten - nazwe techniczna.
        new TemplateRule(
            new Regex(@"^Pole '(?<name>[^']+)' jest wymagane\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The '{m.Groups["name"].Value}' field is required.",
                "es" => $"El campo '{m.Groups["name"].Value}' es obligatorio.",
                "de" => $"Das Feld '{m.Groups["name"].Value}' ist erforderlich.",
                "it" => $"Il campo '{m.Groups["name"].Value}' è obbligatorio.",
                "fr" => $"Le champ '{m.Groups["name"].Value}' est obligatoire.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Pole '(?<name>[^']+)' jest za duże\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The '{m.Groups["name"].Value}' field is too large.",
                "es" => $"El campo '{m.Groups["name"].Value}' es demasiado grande.",
                "de" => $"Das Feld '{m.Groups["name"].Value}' ist zu groß.",
                "it" => $"Il campo '{m.Groups["name"].Value}' è troppo grande.",
                "fr" => $"Le champ '{m.Groups["name"].Value}' est trop volumineux.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Pole '(?<name>[^']+)' ma nieprawidłowy format\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The '{m.Groups["name"].Value}' field has an invalid format.",
                "es" => $"El campo '{m.Groups["name"].Value}' tiene un formato no válido.",
                "de" => $"Das Feld '{m.Groups["name"].Value}' hat ein ungültiges Format.",
                "it" => $"Il campo '{m.Groups["name"].Value}' ha un formato non valido.",
                "fr" => $"Le champ '{m.Groups["name"].Value}' a un format invalide.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Pole '(?<name>[^']+)' ma nieprawidłowy JSON\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The '{m.Groups["name"].Value}' field contains invalid JSON.",
                "es" => $"El campo '{m.Groups["name"].Value}' contiene JSON no válido.",
                "de" => $"Das Feld '{m.Groups["name"].Value}' enthält ungültiges JSON.",
                "it" => $"Il campo '{m.Groups["name"].Value}' contiene JSON non valido.",
                "fr" => $"Le champ '{m.Groups["name"].Value}' contient du JSON invalide.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Manifest może zawierać maksymalnie (?<count>\d+) pozycji\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The manifest can contain at most {m.Groups["count"].Value} entries.",
                "es" => $"El manifiesto puede contener como máximo {m.Groups["count"].Value} entradas.",
                "de" => $"Das Manifest darf höchstens {m.Groups["count"].Value} Einträge enthalten.",
                "it" => $"Il manifest può contenere al massimo {m.Groups["count"].Value} voci.",
                "fr" => $"Le manifeste peut contenir au maximum {m.Groups["count"].Value} entrées.",
                _ => null,
            }),
        // Walidacja kontraktu żądania. Filtr ValidationEndpointFilter biegnie przed KAŻDYM handlerem
        // Minimal API, więc bez tych reguł dowolny błąd walidacji wracał po polsku niezależnie od
        // języka interfejsu. Reguły muszą stać po wzorcach z apostrofami ('nazwa') wyżej - tamte są
        // bardziej szczegółowe, a pierwsze dopasowanie wygrywa.
        FieldRule(@"może mieć maksymalnie (?<limit>\d+) znaków\.",
            "The {0} field can be at most {1} characters long.",
            "El campo {0} puede tener como máximo {1} caracteres.",
            "Das Feld {0} darf höchstens {1} Zeichen lang sein.",
            "Il campo {0} può contenere al massimo {1} caratteri.",
            "Le champ {0} peut comporter au maximum {1} caractères."),
        FieldRule(@"może zawierać maksymalnie (?<limit>\d+) elementów\.",
            "The {0} field can contain at most {1} items.",
            "El campo {0} puede contener como máximo {1} elementos.",
            "Das Feld {0} darf höchstens {1} Elemente enthalten.",
            "Il campo {0} può contenere al massimo {1} elementi.",
            "Le champ {0} peut contenir au maximum {1} éléments."),
        FieldRule(@"nie może być puste\.",
            "The {0} field cannot be empty.",
            "El campo {0} no puede estar vacío.",
            "Das Feld {0} darf nicht leer sein.",
            "Il campo {0} non può essere vuoto.",
            "Le champ {0} ne peut pas être vide."),
        FieldRule(@"nie zawiera prawidłowego adresu e-mail\.",
            "The {0} field does not contain a valid email address.",
            "El campo {0} no contiene una dirección de correo válida.",
            "Das Feld {0} enthält keine gültige E-Mail-Adresse.",
            "Il campo {0} non contiene un indirizzo e-mail valido.",
            "Le champ {0} ne contient pas d'adresse e-mail valide."),
        FieldRule(@"musi być względną ścieżką aplikacji\.",
            "The {0} field must be a relative application path.",
            "El campo {0} debe ser una ruta relativa de la aplicación.",
            "Das Feld {0} muss ein relativer Anwendungspfad sein.",
            "Il campo {0} deve essere un percorso relativo dell'applicazione.",
            "Le champ {0} doit être un chemin d'application relatif."),
        FieldRule(@"musi zawierać prawidłowy identyfikator\.",
            "The {0} field must contain a valid identifier.",
            "El campo {0} debe contener un identificador válido.",
            "Das Feld {0} muss eine gültige Kennung enthalten.",
            "Il campo {0} deve contenere un identificativo valido.",
            "Le champ {0} doit contenir un identifiant valide."),
        FieldRule(@"ma nieprawidłową wartość\.",
            "The {0} field has an invalid value.",
            "El campo {0} tiene un valor no válido.",
            "Das Feld {0} hat einen ungültigen Wert.",
            "Il campo {0} ha un valore non valido.",
            "Le champ {0} a une valeur invalide."),
        FieldRule(@"ma wartość poza dozwolonym zakresem\.",
            "The {0} field has a value outside the allowed range.",
            "El campo {0} tiene un valor fuera del rango permitido.",
            "Das Feld {0} hat einen Wert außerhalb des zulässigen Bereichs.",
            "Il campo {0} ha un valore fuori dall'intervallo consentito.",
            "Le champ {0} a une valeur en dehors de la plage autorisée."),
        FieldRule(@"nie może być ujemne\.",
            "The {0} field cannot be negative.",
            "El campo {0} no puede ser negativo.",
            "Das Feld {0} darf nicht negativ sein.",
            "Il campo {0} non può essere negativo.",
            "Le champ {0} ne peut pas être négatif."),
        FieldRule(@"ma datę poza dozwolonym zakresem\.",
            "The {0} field has a date outside the allowed range.",
            "El campo {0} tiene una fecha fuera del rango permitido.",
            "Das Feld {0} hat ein Datum außerhalb des zulässigen Bereichs.",
            "Il campo {0} ha una data fuori dall'intervallo consentito.",
            "Le champ {0} a une date en dehors de la plage autorisée."),
        FieldRule(@"zawiera nieprawidłową wartość tekstową\.",
            "The {0} field contains an invalid text value.",
            "El campo {0} contiene un valor de texto no válido.",
            "Das Feld {0} enthält einen ungültigen Textwert.",
            "Il campo {0} contiene un valore testuale non valido.",
            "Le champ {0} contient une valeur textuelle invalide."),
        FieldRule(@"zawiera wartość poza dozwolonym zakresem 0-3650\.",
            "The {0} field contains a value outside the allowed range 0-3650.",
            "El campo {0} contiene un valor fuera del rango permitido 0-3650.",
            "Das Feld {0} enthält einen Wert außerhalb des zulässigen Bereichs 0-3650.",
            "Il campo {0} contiene un valore fuori dall'intervallo consentito 0-3650.",
            "Le champ {0} contient une valeur en dehors de la plage autorisée 0-3650."),
        FieldRule(@"nie jest kolekcją\.",
            "The {0} field is not a collection.",
            "El campo {0} no es una colección.",
            "Das Feld {0} ist keine Sammlung.",
            "Il campo {0} non è una collezione.",
            "Le champ {0} n'est pas une collection."),
        new TemplateRule(
            new Regex(@"^Klucz w polu (?<name>.+?) może mieć maksymalnie (?<limit>\d+) znaków\.$"),
            (m, lang) => lang switch
            {
                "en" => $"A key in the {m.Groups["name"].Value} field can be at most {m.Groups["limit"].Value} characters long.",
                "es" => $"Una clave del campo {m.Groups["name"].Value} puede tener como máximo {m.Groups["limit"].Value} caracteres.",
                "de" => $"Ein Schlüssel im Feld {m.Groups["name"].Value} darf höchstens {m.Groups["limit"].Value} Zeichen lang sein.",
                "it" => $"Una chiave nel campo {m.Groups["name"].Value} può contenere al massimo {m.Groups["limit"].Value} caratteri.",
                "fr" => $"Une clé du champ {m.Groups["name"].Value} peut comporter au maximum {m.Groups["limit"].Value} caractères.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Wartość w polu (?<name>.+?) może mieć maksymalnie (?<limit>\d+) znaków\.$"),
            (m, lang) => lang switch
            {
                "en" => $"A value in the {m.Groups["name"].Value} field can be at most {m.Groups["limit"].Value} characters long.",
                "es" => $"Un valor del campo {m.Groups["name"].Value} puede tener como máximo {m.Groups["limit"].Value} caracteres.",
                "de" => $"Ein Wert im Feld {m.Groups["name"].Value} darf höchstens {m.Groups["limit"].Value} Zeichen lang sein.",
                "it" => $"Un valore nel campo {m.Groups["name"].Value} può contenere al massimo {m.Groups["limit"].Value} caratteri.",
                "fr" => $"Une valeur du champ {m.Groups["name"].Value} peut comporter au maximum {m.Groups["limit"].Value} caractères.",
                _ => null,
            }),
        // Wbudowane komunikaty DataAnnotations ([Required], [EmailAddress], [StringLength]) sa po
        // ANGIELSKU i nie da sie ich znalezc, szukajac polskiego tekstu - dlatego przetrwaly audyt
        // pokrycia. Siedza na rejestracji i logowaniu, wiec przeciekaly na najbardziej widocznym
        // ekranie, i to w obie strony: polski uzytkownik tez dostawal angielski.
        // Zrodlem jest tu angielski, wiec - inaczej niz reszta pliku - te reguly maja galaz "pl".
        new TemplateRule(
            new Regex(@"^The (?<name>.+) field is required\.$"),
            (m, lang) => lang switch
            {
                "pl" => $"Pole {m.Groups["name"].Value} jest wymagane.",
                "es" => $"El campo {m.Groups["name"].Value} es obligatorio.",
                "de" => $"Das Feld {m.Groups["name"].Value} ist erforderlich.",
                "it" => $"Il campo {m.Groups["name"].Value} è obbligatorio.",
                "fr" => $"Le champ {m.Groups["name"].Value} est obligatoire.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^The (?<name>.+) field is not a valid e-mail address\.$"),
            (m, lang) => lang switch
            {
                "pl" => $"Pole {m.Groups["name"].Value} nie zawiera prawidłowego adresu e-mail.",
                "es" => $"El campo {m.Groups["name"].Value} no contiene una dirección de correo válida.",
                "de" => $"Das Feld {m.Groups["name"].Value} enthält keine gültige E-Mail-Adresse.",
                "it" => $"Il campo {m.Groups["name"].Value} non contiene un indirizzo e-mail valido.",
                "fr" => $"Le champ {m.Groups["name"].Value} ne contient pas d'adresse e-mail valide.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^The field (?<name>.+) must be a string with a minimum length of (?<min>\d+) and a maximum length of (?<max>\d+)\.$"),
            (m, lang) =>
            {
                var (name, min, max) = (m.Groups["name"].Value, m.Groups["min"].Value, m.Groups["max"].Value);
                return lang switch
                {
                    "pl" => $"Pole {name} musi mieć od {min} do {max} znaków.",
                    "es" => $"El campo {name} debe tener entre {min} y {max} caracteres.",
                    "de" => $"Das Feld {name} muss zwischen {min} und {max} Zeichen lang sein.",
                    "it" => $"Il campo {name} deve contenere da {min} a {max} caratteri.",
                    "fr" => $"Le champ {name} doit comporter entre {min} et {max} caractères.",
                    _ => null,
                };
            }),
        new TemplateRule(
            new Regex(@"^The field (?<name>.+) must match the regular expression '(?<pattern>.+)'\.$"),
            (m, lang) =>
            {
                var name = m.Groups["name"].Value;
                // Surowe wyrazenie regularne nic uzytkownikowi nie mowi. Jedyne uzycie w projekcie to
                // 6-cyfrowy kod weryfikacyjny, wiec dostaje czytelny warunek; kazdy inny wzorzec
                // dostaje ogolny komunikat o formacie zamiast wycieku wyrazenia na ekran.
                var isSixDigitCode = m.Groups["pattern"].Value == @"^\d{6}$";
                return lang switch
                {
                    "pl" => isSixDigitCode ? $"Pole {name} musi być 6-cyfrowym kodem." : $"Pole {name} ma nieprawidłowy format.",
                    "en" => isSixDigitCode ? $"The {name} field must be a 6-digit code." : $"The {name} field has an invalid format.",
                    "es" => isSixDigitCode ? $"El campo {name} debe ser un código de 6 dígitos." : $"El campo {name} tiene un formato no válido.",
                    "de" => isSixDigitCode ? $"Das Feld {name} muss ein 6-stelliger Code sein." : $"Das Feld {name} hat ein ungültiges Format.",
                    "it" => isSixDigitCode ? $"Il campo {name} deve essere un codice di 6 cifre." : $"Il campo {name} ha un formato non valido.",
                    "fr" => isSixDigitCode ? $"Le champ {name} doit être un code à 6 chiffres." : $"Le champ {name} a un format invalide.",
                    _ => null,
                };
            }),
        new TemplateRule(
            new Regex(@"^The field (?<name>.+) must be between (?<min>[^ ]+) and (?<max>[^ ]+)\.$"),
            (m, lang) =>
            {
                var (name, min, max) = (m.Groups["name"].Value, m.Groups["min"].Value, m.Groups["max"].Value);
                return lang switch
                {
                    "pl" => $"Pole {name} musi mieścić się w zakresie od {min} do {max}.",
                    "es" => $"El campo {name} debe estar entre {min} y {max}.",
                    "de" => $"Das Feld {name} muss zwischen {min} und {max} liegen.",
                    "it" => $"Il campo {name} deve essere compreso tra {min} e {max}.",
                    "fr" => $"Le champ {name} doit être compris entre {min} et {max}.",
                    _ => null,
                };
            }),
        new TemplateRule(
            new Regex(@"^The field (?<name>.+) must be a string with a maximum length of (?<max>\d+)\.$"),
            (m, lang) => lang switch
            {
                "pl" => $"Pole {m.Groups["name"].Value} może mieć maksymalnie {m.Groups["max"].Value} znaków.",
                "es" => $"El campo {m.Groups["name"].Value} puede tener como máximo {m.Groups["max"].Value} caracteres.",
                "de" => $"Das Feld {m.Groups["name"].Value} darf höchstens {m.Groups["max"].Value} Zeichen lang sein.",
                "it" => $"Il campo {m.Groups["name"].Value} può contenere al massimo {m.Groups["max"].Value} caratteri.",
                "fr" => $"Le champ {m.Groups["name"].Value} peut comporter au maximum {m.Groups["max"].Value} caractères.",
                _ => null,
            }),
        // Source message itself is English (a small reverse-leak bug in the otherwise Polish-default
        // backend) so it is translated for pl/es/de and left unchanged for en.
        new TemplateRule(
            new Regex(@"^Unknown plan: (?<plan>.+)$"),
            (m, lang) => lang switch
            {
                "pl" => $"Nieznany plan: {m.Groups["plan"].Value}",
                "es" => $"Plan desconocido: {m.Groups["plan"].Value}",
                "de" => $"Unbekannter Plan: {m.Groups["plan"].Value}",
                "it" => $"Piano sconosciuto: {m.Groups["plan"].Value}",
                "fr" => $"Forfait inconnu : {m.Groups["plan"].Value}",
                _ => null,
            }),
    };

    public static string Translate(string message, string? language)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        var lang = AppLanguages.Normalize(language);

        if (Exact.TryGetValue(message, out var translations))
        {
            return lang == AppLanguages.Source ? message : translations.For(lang);
        }

        foreach (var rule in Templates)
        {
            var match = rule.Pattern.Match(message);
            if (!match.Success) continue;

            var translated = rule.Build(match, lang);
            if (translated is not null) return translated;

            // Szablon bez wariantu dla nowego języka spada na angielski. Dla polskiego nie - polski jest
            // językiem źródłowym, więc brak wariantu oznacza "komunikat jest już po polsku".
            return lang == AppLanguages.Source ? message : rule.Build(match, AppLanguages.Fallback) ?? message;
        }

        return message;
    }
}
