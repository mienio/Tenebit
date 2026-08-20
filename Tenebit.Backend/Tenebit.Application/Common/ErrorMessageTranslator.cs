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
    private static readonly Dictionary<string, (string En, string Es, string De)> Exact = new()
    {
        ["Aby przejść na plan Pro, użyj płatności Stripe (checkout)."] = ("To move to the Pro plan, use Stripe checkout.", "Para pasar al plan Pro, usa el pago con Stripe (checkout).", "Um auf den Pro-Plan zu wechseln, nutzen Sie den Stripe-Checkout."),
        ["Aktywo nie istnieje."] = ("The asset does not exist.", "El activo no existe.", "Das Asset existiert nicht."),
        ["Aktywo nie jest dostępne do wydania."] = ("The asset is not available for assignment.", "El activo no está disponible para su entrega.", "Das Asset steht nicht zur Übergabe zur Verfügung."),
        ["Akceptacja regulaminu i polityki prywatności jest wymagana."] = ("Acceptance of the terms and privacy policy is required.", "Debes aceptar los términos y la política de privacidad.", "Die Nutzungsbedingungen und die Datenschutzerklärung müssen akzeptiert werden."),
        ["Brak oczekującej kontroli dla tego aktywa."] = ("There is no pending inspection for this asset.", "No hay ninguna inspección pendiente para este activo.", "Für dieses Asset liegt keine ausstehende Kontrolle vor."),
        ["Brak uprawnień do tej operacji."] = ("You do not have permission to perform this operation.", "No tienes permiso para realizar esta operación.", "Sie haben keine Berechtigung für diesen Vorgang."),
        ["Brak wolnych miejsc w tej licencji."] = ("There are no free seats left on this license.", "No quedan plazas libres en esta licencia.", "Für diese Lizenz sind keine freien Plätze mehr verfügbar."),
        ["Cena zakupu nie może być ujemna."] = ("The purchase price cannot be negative.", "El precio de compra no puede ser negativo.", "Der Kaufpreis darf nicht negativ sein."),
        ["Co najmniej jedno aktywo nie jest dostępne do wydania."] = ("At least one asset is not available for assignment.", "Al menos un activo no está disponible para su entrega.", "Mindestens ein Asset steht nicht zur Übergabe zur Verfügung."),
        ["Dodaj co najmniej jedno aktywo do wydania."] = ("Add at least one asset to the assignment.", "Añade al menos un activo a la entrega.", "Fügen Sie der Übergabe mindestens ein Asset hinzu."),
        ["Dostawca logowania nie udostępnił adresu e-mail. Wyraź zgodę na udostępnienie e-maila i spróbuj ponownie."] = ("The login provider did not share an email address. Grant permission to share your email and try again.", "El proveedor de inicio de sesión no compartió una dirección de correo. Concede permiso para compartir tu correo e inténtalo de nuevo.", "Der Anmeldeanbieter hat keine E-Mail-Adresse übermittelt. Erteilen Sie die Freigabe Ihrer E-Mail und versuchen Sie es erneut."),
        ["Dozwolone są tylko zdjęcia w formacie JPEG, PNG lub WebP."] = ("Only JPEG, PNG or WebP photos are allowed.", "Solo se permiten fotos en formato JPEG, PNG o WebP.", "Es sind nur Fotos im Format JPEG, PNG oder WebP zulässig."),
        ["Dwuskładnikowe uwierzytelnianie nie jest włączone."] = ("Two-factor authentication is not enabled.", "La autenticación de dos factores no está activada.", "Die Zwei-Faktor-Authentifizierung ist nicht aktiviert."),
        ["E-mail z tego dostawcy nie jest zweryfikowany. Zaloguj się hasłem i połącz konto w ustawieniach."] = ("The email from this provider is not verified. Log in with your password and link the account in settings.", "El correo de este proveedor no está verificado. Inicia sesión con tu contraseña y vincula la cuenta en ajustes.", "Die E-Mail-Adresse dieses Anbieters ist nicht verifiziert. Melden Sie sich mit Ihrem Passwort an und verknüpfen Sie das Konto in den Einstellungen."),
        ["Etykieta pola własnego jest wymagana."] = ("The custom field label is required.", "La etiqueta del campo personalizado es obligatoria.", "Die Bezeichnung des benutzerdefinierten Felds ist erforderlich."),
        ["Etykieta statusu jest wymagana."] = ("The status label is required.", "La etiqueta del estado es obligatoria.", "Die Statusbezeichnung ist erforderlich."),
        ["Hasło musi mieć co najmniej 8 znaków."] = ("The password must be at least 8 characters long.", "La contraseña debe tener al menos 8 caracteres.", "Das Passwort muss mindestens 8 Zeichen lang sein."),
        ["Imię i nazwisko są wymagane."] = ("First and last name are required.", "El nombre y los apellidos son obligatorios.", "Vor- und Nachname sind erforderlich."),
        ["Kategoria nie istnieje."] = ("The category does not exist.", "La categoría no existe.", "Die Kategorie existiert nicht."),
        ["Kategoria o tej nazwie już istnieje."] = ("A category with this name already exists.", "Ya existe una categoría con este nombre.", "Eine Kategorie mit diesem Namen existiert bereits."),
        ["Klucz pola własnego jest wymagany."] = ("The custom field key is required.", "La clave del campo personalizado es obligatoria.", "Der Schlüssel des benutzerdefinierten Felds ist erforderlich."),
        ["Klucze pól własnych muszą być unikalne w obrębie kategorii."] = ("Custom field keys must be unique within the category.", "Las claves de los campos personalizados deben ser únicas dentro de la categoría.", "Die Schlüssel benutzerdefinierter Felder müssen innerhalb der Kategorie eindeutig sein."),
        ["Kolor tekstu statusu musi być w formacie szesnastkowym, np. #1d4ed8."] = ("The status text color must be in hex format, e.g. #1d4ed8.", "El color del texto del estado debe estar en formato hexadecimal, p. ej. #1d4ed8.", "Die Textfarbe des Status muss im Hex-Format angegeben werden, z. B. #1d4ed8."),
        ["Kolor tła statusu musi być w formacie szesnastkowym, np. #eff6ff."] = ("The status background color must be in hex format, e.g. #eff6ff.", "El color de fondo del estado debe estar en formato hexadecimal, p. ej. #eff6ff.", "Die Hintergrundfarbe des Status muss im Hex-Format angegeben werden, z. B. #eff6ff."),
        ["Kontrola nie istnieje."] = ("The inspection does not exist.", "La inspección no existe.", "Die Kontrolle existiert nicht."),
        ["Kontrola tego aktywa została już zakończona."] = ("This asset's inspection has already been completed.", "La inspección de este activo ya se ha completado.", "Die Kontrolle dieses Assets wurde bereits abgeschlossen."),
        ["Licencja nie istnieje."] = ("The license does not exist.", "La licencia no existe.", "Die Lizenz existiert nicht."),
        ["Liczba miejsc nie może być ujemna."] = ("The number of seats cannot be negative.", "El número de plazas no puede ser negativo.", "Die Anzahl der Plätze darf nicht negativ sein."),
        ["Kod resetujący jest nieprawidłowy lub wygasł."] = ("The password reset code is invalid or has expired.", "El código para restablecer la contraseña no es válido o ha caducado.", "Der Code zum Zurücksetzen des Passworts ist ungültig oder abgelaufen."),
        ["Kod weryfikacyjny jest nieprawidłowy lub wygasł."] = ("The verification code is invalid or has expired.", "El código de verificación no es válido o ha caducado.", "Der Bestätigungscode ist ungültig oder abgelaufen."),
        ["Materiał dowodowy nie istnieje."] = ("The evidence item does not exist.", "El material de evidencia no existe.", "Der Beweismaterial-Eintrag existiert nicht."),
        ["Najpierw wygeneruj sekret 2FA."] = ("First generate a 2FA secret.", "Primero genera un secreto de 2FA.", "Generieren Sie zuerst ein 2FA-Secret."),
        ["Nazwa aktywa jest wymagana."] = ("The asset name is required.", "El nombre del activo es obligatorio.", "Der Asset-Name ist erforderlich."),
        ["Nazwa firmy jest wymagana."] = ("The company name is required.", "El nombre de la empresa es obligatorio.", "Der Firmenname ist erforderlich."),
        ["Nazwa kategorii jest wymagana."] = ("The category name is required.", "El nombre de la categoría es obligatorio.", "Der Kategoriename ist erforderlich."),
        ["Nazwa licencji jest wymagana."] = ("The license name is required.", "El nombre de la licencia es obligatorio.", "Der Lizenzname ist erforderlich."),
        ["Nazwa pliku jest wymagana."] = ("The file name is required.", "El nombre del archivo es obligatorio.", "Der Dateiname ist erforderlich."),
        ["Nazwa typu relacji jest wymagana."] = ("The relationship type name is required.", "El nombre del tipo de relación es obligatorio.", "Der Name des Beziehungstyps ist erforderlich."),
        ["Nazwa zespołu jest wymagana."] = ("The team name is required.", "El nombre del equipo es obligatorio.", "Der Teamname ist erforderlich."),
        ["Nazwa zestawu stanowiskowego jest wymagana."] = ("The job profile name is required.", "El nombre del perfil de puesto es obligatorio.", "Der Name des Stellenprofils ist erforderlich."),
        ["Nie możesz odłączyć jedynego sposobu logowania. Ustaw najpierw hasło."] = ("You cannot unlink your only sign-in method. Set a password first.", "No puedes desvincular tu único método de inicio de sesión. Establece antes una contraseña.", "Sie können Ihre einzige Anmeldemethode nicht entfernen. Legen Sie zuerst ein Passwort fest."),
        ["Nie można edytować opublikowanej procedury - osoby już ją zaakceptowały. Zarchiwizuj ją i utwórz nową wersję."] = ("A published procedure cannot be edited - people have already accepted it. Archive it and create a new version.", "No se puede editar un procedimiento publicado - ya ha sido aceptado por personas. Archívalo y crea una nueva versión.", "Eine veröffentlichte Prozedur kann nicht bearbeitet werden - sie wurde bereits akzeptiert. Archivieren Sie sie und erstellen Sie eine neue Version."),
        ["Nie można modyfikować wydania, które zostało już podpisane lub zamknięte."] = ("An assignment that has already been signed or closed cannot be modified.", "No se puede modificar una entrega que ya ha sido firmada o cerrada.", "Eine bereits unterzeichnete oder abgeschlossene Übergabe kann nicht geändert werden."),
        ["Nie można opublikować procedury bez pliku."] = ("A procedure cannot be published without a file.", "No se puede publicar un procedimiento sin un archivo.", "Eine Prozedur kann nicht ohne Datei veröffentlicht werden."),
        ["Nie można opublikować zarchiwizowanej procedury."] = ("An archived procedure cannot be published.", "No se puede publicar un procedimiento archivado.", "Eine archivierte Prozedur kann nicht veröffentlicht werden."),
        ["Nie można ustawić liczby miejsc poniżej liczby już przypisanych."] = ("The number of seats cannot be set below the number already assigned.", "No se puede establecer un número de plazas inferior al ya asignado.", "Die Anzahl der Plätze darf nicht unter die Anzahl der bereits zugewiesenen Plätze gesetzt werden."),
        ["Nie można usunąć aktywa powiązanego z wydaniami, kontrolami, zgłoszeniami serwisowymi, rezerwacjami lub offboardingiem."] = ("An asset linked to assignments, inspections, service tickets, reservations, or offboarding cannot be deleted.", "No se puede eliminar un activo vinculado a entregas, inspecciones, tickets de servicio, reservas u offboarding.", "Ein Asset, das mit Übergaben, Kontrollen, Service-Tickets, Reservierungen oder Offboarding verknüpft ist, kann nicht gelöscht werden."),
        ["Tego rekordu nie można usunąć, ponieważ jest używany w innym miejscu."] = ("This record cannot be deleted because it is used elsewhere.", "Este registro no se puede eliminar porque se utiliza en otro lugar.", "Dieser Datensatz kann nicht gelöscht werden, da er an anderer Stelle verwendet wird."),
        ["Przesłany plik jest za duży."] = ("The uploaded file is too large.", "El archivo subido es demasiado grande.", "Die hochgeladene Datei ist zu groß."),
        ["Nieprawidłowe żądanie."] = ("Invalid request.", "Solicitud no válida.", "Ungültige Anfrage."),
        ["Wystąpił nieoczekiwany błąd aplikacji."] = ("An unexpected application error occurred.", "Se produjo un error inesperado en la aplicación.", "Es ist ein unerwarteter Anwendungsfehler aufgetreten."),
        ["Rekord z takimi danymi już istnieje."] = ("A record with this data already exists.", "Ya existe un registro con estos datos.", "Ein Datensatz mit diesen Daten existiert bereits."),
        ["Nie można usunąć kategorii używanej przez aktywa lub zestawy."] = ("A category used by assets or job profiles cannot be deleted.", "No se puede eliminar una categoría utilizada por activos o perfiles de puesto.", "Eine Kategorie, die von Assets oder Stellenprofilen verwendet wird, kann nicht gelöscht werden."),
        ["Nie można usunąć pracownika, bo ma przypisany sprzęt, historię wydań albo jest przełożonym. Najpierw zwróć sprzęt albo usuń powiązania."] = ("This employee cannot be deleted because they have assigned equipment, an assignment history, or are a manager. Return the equipment or remove the relations first.", "No se puede eliminar a este empleado porque tiene equipos asignados, historial de entregas o es responsable de otras personas. Devuelve el equipo o elimina las relaciones primero.", "Dieser Mitarbeiter kann nicht gelöscht werden, da ihm Geräte zugewiesen sind, eine Übergabehistorie besteht oder er eine Führungsposition hat. Geben Sie zuerst die Geräte zurück oder entfernen Sie die Beziehungen."),
        ["Nie można usunąć typu relacji przypisanego do osób."] = ("A relationship type assigned to people cannot be deleted.", "No se puede eliminar un tipo de relación asignado a personas.", "Ein Beziehungstyp, der Personen zugewiesen ist, kann nicht gelöscht werden."),
        ["Nie można usunąć zespołu przypisanego do osób lub aktywów."] = ("A team assigned to people or assets cannot be deleted.", "No se puede eliminar un equipo asignado a personas o activos.", "Ein Team, das Personen oder Assets zugewiesen ist, kann nicht gelöscht werden."),
        ["Nie można zwrócić wydania, które zostało już zamknięte lub anulowane."] = ("An assignment that has already been closed or cancelled cannot be returned.", "No se puede devolver una entrega que ya ha sido cerrada o cancelada.", "Eine bereits abgeschlossene oder stornierte Übergabe kann nicht zurückgegeben werden."),
        ["Nie znaleziono konta powiązanego z tym linkiem."] = ("No account linked to this link was found.", "No se encontró ninguna cuenta vinculada a este enlace.", "Es wurde kein mit diesem Link verknüpftes Konto gefunden."),
        ["Nie znaleziono konta."] = ("No account was found.", "No se encontró la cuenta.", "Es wurde kein Konto gefunden."),
        ["Nie znaleziono organizacji powiązanej z kontem."] = ("No organization linked to this account was found.", "No se encontró ninguna organización vinculada a la cuenta.", "Es wurde keine mit dem Konto verknüpfte Organisation gefunden."),
        ["Niektóre aktywa nie istnieją."] = ("Some assets do not exist.", "Algunos activos no existen.", "Einige Assets existieren nicht."),
        ["Niektóre procedury nie istnieją."] = ("Some procedures do not exist.", "Algunos procedimientos no existen.", "Einige Prozeduren existieren nicht."),
        ["Nieprawidłowy e-mail lub hasło."] = ("Invalid email or password.", "Correo electrónico o contraseña incorrectos.", "Ungültige E-Mail-Adresse oder ungültiges Passwort."),
        ["Nieprawidłowy kod uwierzytelniający."] = ("Invalid authentication code.", "Código de autenticación incorrecto.", "Ungültiger Authentifizierungscode."),
        ["Nieprawidłowy kod. Sprawdź godzinę na urządzeniu i spróbuj ponownie."] = ("Invalid code. Check the time on your device and try again.", "Código incorrecto. Comprueba la hora de tu dispositivo e inténtalo de nuevo.", "Ungültiger Code. Überprüfen Sie die Uhrzeit auf Ihrem Gerät und versuchen Sie es erneut."),
        ["Nieprawidłowy lub uszkodzony plik obrazu."] = ("The image file is invalid or corrupted.", "El archivo de imagen no es válido o está dañado.", "Die Bilddatei ist ungültig oder beschädigt."),
        ["Nieprawidłowy podpis webhooka Stripe."] = ("Invalid Stripe webhook signature.", "Firma de webhook de Stripe no válida.", "Ungültige Stripe-Webhook-Signatur."),
        ["Numer wydania jest wymagany."] = ("The assignment reference is required.", "La referencia de la entrega es obligatoria.", "Die Ausgabenummer ist erforderlich."),
        ["Organizacja ma już aktywną płatną subskrypcję."] = ("The organization already has an active paid subscription.", "La organización ya tiene una suscripción de pago activa.", "Die Organisation hat bereits ein aktives kostenpflichtiges Abonnement."),
        ["Organizacja nie istnieje."] = ("The organization does not exist.", "La organización no existe.", "Die Organisation existiert nicht."),
        ["Organizacja nie ma jeszcze konta rozliczeniowego Stripe."] = ("The organization does not yet have a Stripe billing account.", "La organización aún no tiene una cuenta de facturación de Stripe.", "Die Organisation verfügt noch über kein Stripe-Abrechnungskonto."),
        ["Osiągnięto limit 5 zdjęć dla tego aktywa i etapu."] = ("The limit of 5 photos for this asset and phase has been reached.", "Se ha alcanzado el límite de 5 fotos para este activo y esta fase.", "Das Limit von 5 Fotos für dieses Asset und diese Phase wurde erreicht."),
        ["Osoba z tym adresem e-mail już istnieje."] = ("A person with this email address already exists.", "Ya existe una persona con esta dirección de correo.", "Es existiert bereits eine Person mit dieser E-Mail-Adresse."),
        ["Plik nie jest prawidłowym obrazem JPEG/PNG/WebP."] = ("The file is not a valid JPEG/PNG/WebP image.", "El archivo no es una imagen JPEG/PNG/WebP válida.", "Die Datei ist kein gültiges JPEG-/PNG-/WebP-Bild."),
        ["Plik procedury jest pusty."] = ("The procedure file is empty.", "El archivo del procedimiento está vacío.", "Die Prozedurdatei ist leer."),
        ["Plik procedury może mieć maksymalnie 25 MB."] = ("The procedure file can be at most 25 MB.", "El archivo del procedimiento puede tener como máximo 25 MB.", "Die Prozedurdatei darf höchstens 25 MB groß sein."),
        ["Plik procedury nie istnieje."] = ("The procedure file does not exist.", "El archivo del procedimiento no existe.", "Die Prozedurdatei existiert nicht."),
        ["Pole nie jest polem wrażliwym."] = ("This is not a sensitive field.", "Este campo no es un campo sensible.", "Dies ist kein sensibles Feld."),
        ["Poprawny adres e-mail jest wymagany."] = ("A valid email address is required.", "Se requiere una dirección de correo válida.", "Eine gültige E-Mail-Adresse ist erforderlich."),
        ["Poprawny e-mail użytkownika jest wymagany."] = ("A valid user email is required.", "Se requiere un correo de usuario válido.", "Eine gültige Benutzer-E-Mail-Adresse ist erforderlich."),
        ["Pracownik nie istnieje."] = ("The employee does not exist.", "El empleado no existe.", "Der Mitarbeiter existiert nicht."),
        ["Pracownik z tym adresem e-mail już istnieje."] = ("An employee with this email address already exists.", "Ya existe un empleado con esta dirección de correo.", "Es existiert bereits ein Mitarbeiter mit dieser E-Mail-Adresse."),
        ["Procedura nie istnieje."] = ("The procedure does not exist.", "El procedimiento no existe.", "Die Prozedur existiert nicht."),
        ["Płatności Stripe nie są jeszcze skonfigurowane."] = ("Stripe payments are not configured yet.", "Los pagos con Stripe aún no están configurados.", "Stripe-Zahlungen sind noch nicht konfiguriert."),
        ["Sesja wygasła. Zaloguj się ponownie."] = ("Your session has expired. Please log in again.", "Tu sesión ha caducado. Inicia sesión de nuevo.", "Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an."),
        ["Suma kontrolna pliku jest wymagana."] = ("The file checksum is required.", "La suma de comprobación del archivo es obligatoria.", "Die Prüfsumme der Datei ist erforderlich."),
        ["Ta akceptacja procedury została już zarejestrowana i nie może zostać zmieniona."] = ("This procedure acceptance has already been recorded and cannot be changed.", "Esta aceptación del procedimiento ya ha sido registrada y no se puede modificar.", "Diese Prozedurakzeptanz wurde bereits erfasst und kann nicht mehr geändert werden."),
        ["Ta organizacja ma aktywną płatną subskrypcję. Zarządzaj nią (w tym anulowaniem) w portalu rozliczeń Stripe."] = ("This organization has an active paid subscription. Manage it (including cancellation) in the Stripe billing portal.", "Esta organización tiene una suscripción de pago activa. Gestiónala (incluida la cancelación) en el portal de facturación de Stripe.", "Diese Organisation hat ein aktives kostenpflichtiges Abonnement. Verwalten Sie es (einschließlich Kündigung) im Stripe-Abrechnungsportal."),
        ["Ta osoba ma już przypisane miejsce w tej licencji."] = ("This person already has an assigned seat on this license.", "Esta persona ya tiene una plaza asignada en esta licencia.", "Dieser Person ist bereits ein Platz in dieser Lizenz zugewiesen."),
        ["Tag aktywa jest już używany."] = ("This asset tag is already in use.", "Esta etiqueta de activo ya está en uso.", "Dieses Asset-Tag wird bereits verwendet."),
        ["Tag aktywa jest wymagany."] = ("The asset tag is required.", "La etiqueta del activo es obligatoria.", "Das Asset-Tag ist erforderlich."),
        ["Ten asset jest już dodany do wydania."] = ("This asset has already been added to the assignment.", "Este activo ya ha sido añadido a la entrega.", "Dieses Asset wurde der Übergabe bereits hinzugefügt."),
        ["To konto nie jest połączone z tym dostawcą."] = ("This account is not linked to this provider.", "Esta cuenta no está vinculada a este proveedor.", "Dieses Konto ist nicht mit diesem Anbieter verknüpft."),
        ["To samo aktywo nie może wystąpić dwa razy w jednym wydaniu."] = ("The same asset cannot appear twice in a single assignment.", "El mismo activo no puede aparecer dos veces en una misma entrega.", "Dasselbe Asset darf in einer Übergabe nicht zweimal vorkommen."),
        ["Treść zgłoszenia jest wymagana."] = ("The report content is required.", "El contenido del informe es obligatorio.", "Der Inhalt der Meldung ist erforderlich."),
        ["Tylko wydane aktywo można oznaczyć jako oczekujące na zwrot."] = ("Only an assigned asset can be marked as pending return.", "Solo un activo entregado se puede marcar como pendiente de devolución.", "Nur ein ausgegebenes Asset kann als rückgabeausstehend markiert werden."),
        ["Typ relacji nie istnieje."] = ("The relationship type does not exist.", "El tipo de relación no existe.", "Der Beziehungstyp existiert nicht."),
        ["Typ relacji o tej nazwie już istnieje."] = ("A relationship type with this name already exists.", "Ya existe un tipo de relación con este nombre.", "Ein Beziehungstyp mit diesem Namen existiert bereits."),
        ["Tytuł procedury jest wymagany."] = ("The procedure title is required.", "El título del procedimiento es obligatorio.", "Der Titel der Prozedur ist erforderlich."),
        ["Układ pulpitu nie może być pusty."] = ("The dashboard layout cannot be empty.", "El diseño del panel no puede estar vacío.", "Das Dashboard-Layout darf nicht leer sein."),
        ["Uzupełnij wymagane pola pakietu startowego."] = ("Fill in the required job profile fields.", "Completa los campos obligatorios del perfil de puesto.", "Füllen Sie die erforderlichen Felder des Stellenprofils aus."),
        ["Użytkownik nie istnieje."] = ("The user does not exist.", "El usuario no existe.", "Der Benutzer existiert nicht."),
        ["Użytkownik z tym adresem e-mail już istnieje."] = ("A user with this email address already exists.", "Ya existe un usuario con esta dirección de correo.", "Es existiert bereits ein Benutzer mit dieser E-Mail-Adresse."),
        ["Wersja procedury jest wymagana."] = ("The procedure version is required.", "La versión del procedimiento es obligatoria.", "Die Version der Prozedur ist erforderlich."),
        ["Wybrana kategoria nie istnieje."] = ("The selected category does not exist.", "La categoría seleccionada no existe.", "Die ausgewählte Kategorie existiert nicht."),
        ["Wybrana osoba nie istnieje."] = ("The selected person does not exist.", "La persona seleccionada no existe.", "Die ausgewählte Person existiert nicht."),
        ["Wybrany pracownik nie istnieje."] = ("The selected employee does not exist.", "El empleado seleccionado no existe.", "Der ausgewählte Mitarbeiter existiert nicht."),
        ["Wybrany zestaw stanowiskowy nie istnieje."] = ("The selected job profile does not exist.", "El perfil de puesto seleccionado no existe.", "Das ausgewählte Stellenprofil existiert nicht."),
        ["Wydanie można zaakceptować tylko wtedy, gdy oczekuje na akceptację albo jest po terminie."] = ("An assignment can only be accepted while it is pending acceptance or overdue.", "Una entrega solo se puede aceptar mientras está pendiente de aceptación o vencida.", "Eine Übergabe kann nur akzeptiert werden, solange sie noch aussteht oder überfällig ist."),
        ["Wydanie nie istnieje."] = ("The assignment does not exist.", "La entrega no existe.", "Die Übergabe existiert nicht."),
        ["Wymagane uwierzytelnienie."] = ("Authentication is required.", "Se requiere autenticación.", "Authentifizierung ist erforderlich."),
        ["Za mało danych historycznych do porównania - migawki zbierają się od teraz, spróbuj ponownie za kilka dni."] = ("Not enough historical data to compare yet - snapshots are being collected from now on, try again in a few days.", "Todavía no hay suficientes datos históricos para comparar - las instantáneas se están recopilando a partir de ahora, inténtalo de nuevo en unos días.", "Noch nicht genügend historische Daten zum Vergleich - ab jetzt werden Snapshots gesammelt, versuchen Sie es in ein paar Tagen erneut."),
        ["Zbyt wiele prób. Poproś o nowy kod lub spróbuj ponownie później."] = ("Too many attempts. Request a new code or try again later.", "Demasiados intentos. Solicita un código nuevo o inténtalo de nuevo más tarde.", "Zu viele Versuche. Fordern Sie einen neuen Code an oder versuchen Sie es später erneut."),
        ["Zablokowany materiał dowodowy nie może zostać usunięty."] = ("A locked evidence item cannot be deleted.", "No se puede eliminar un material de evidencia bloqueado.", "Ein gesperrter Beweismaterial-Eintrag kann nicht gelöscht werden."),
        ["Zdjęcie jest puste."] = ("The photo is empty.", "La foto está vacía.", "Das Foto ist leer."),
        ["Zdjęcie może mieć maksymalnie 5 MB."] = ("The photo can be at most 5 MB.", "La foto puede tener como máximo 5 MB.", "Das Foto darf höchstens 5 MB groß sein."),
        ["Zespół nie istnieje."] = ("The team does not exist.", "El equipo no existe.", "Das Team existiert nicht."),
        ["Zespół o tej nazwie już istnieje."] = ("A team with this name already exists.", "Ya existe un equipo con este nombre.", "Ein Team mit diesem Namen existiert bereits."),
        ["Zestaw stanowiskowy nie istnieje."] = ("The job profile does not exist.", "El perfil de puesto no existe.", "Das Stellenprofil existiert nicht."),
        ["Zestaw stanowiskowy nie zawiera żadnego dostępnego aktywa - dodaj aktywo ręcznie."] = ("The job profile does not contain any available asset - add an asset manually.", "El perfil de puesto no contiene ningún activo disponible - añade un activo manualmente.", "Das Stellenprofil enthält kein verfügbares Asset - fügen Sie manuell ein Asset hinzu."),
        ["Zestaw stanowiskowy o tej nazwie już istnieje."] = ("A job profile with this name already exists.", "Ya existe un perfil de puesto con este nombre.", "Ein Stellenprofil mit diesem Namen existiert bereits."),
        ["Zestaw zawiera kategorię, która nie istnieje."] = ("The job profile contains a category that does not exist.", "El perfil de puesto contiene una categoría que no existe.", "Das Stellenprofil enthält eine Kategorie, die nicht existiert."),
        ["Zestaw zawiera procedurę, która nie istnieje."] = ("The job profile contains a procedure that does not exist.", "El perfil de puesto contiene un procedimiento que no existe.", "Das Stellenprofil enthält eine Prozedur, die nicht existiert."),
        ["Zutylizowane aktywo nie może wrócić do obiegu."] = ("A disposed asset cannot return to circulation.", "Un activo dado de baja no puede volver a circulación.", "Ein entsorgtes Asset kann nicht wieder in den Umlauf zurückkehren."),
    };

    private sealed record TemplateRule(Regex Pattern, Func<Match, string, string?> Build);

    // Translations for the small set of messages built with interpolated runtime values (e.g. plan
    // name, role key). Regex captures the variable portion(s) so they can be re-inserted into the
    // translated template. Build returns null when no translation is needed for the requested
    // language (i.e. it matches the message's own source language), signalling the caller to fall
    // back to the original message unchanged.
    private static readonly List<TemplateRule> Templates = new()
    {
        new TemplateRule(
            new Regex(@"^Limit aktywów przekroczony\. Plan (?<plan>.+) pozwala na (?<limit>\d+) aktywów\. Przejdź na wyższy plan\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Asset limit exceeded. The {m.Groups["plan"].Value} plan allows {m.Groups["limit"].Value} assets. Upgrade your plan.",
                "es" => $"Límite de activos superado. El plan {m.Groups["plan"].Value} permite {m.Groups["limit"].Value} activos. Actualiza tu plan.",
                "de" => $"Asset-Limit überschritten. Der Plan {m.Groups["plan"].Value} erlaubt {m.Groups["limit"].Value} Assets. Aktualisieren Sie Ihren Plan.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Nieznana rola: (?<role>.+)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Unknown role: {m.Groups["role"].Value}.",
                "es" => $"Rol desconocido: {m.Groups["role"].Value}.",
                "de" => $"Unbekannte Rolle: {m.Groups["role"].Value}.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Nieznane uprawnienie: (?<perm>.+)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Unknown permission: {m.Groups["perm"].Value}.",
                "es" => $"Permiso desconocido: {m.Groups["perm"].Value}.",
                "de" => $"Unbekannte Berechtigung: {m.Groups["perm"].Value}.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Nieznany status aktywa: (?<status>.+)\.$"),
            (m, lang) => lang switch
            {
                "en" => $"Unknown asset status: {m.Groups["status"].Value}.",
                "es" => $"Estado de activo desconocido: {m.Groups["status"].Value}.",
                "de" => $"Unbekannter Asset-Status: {m.Groups["status"].Value}.",
                _ => null,
            }),
        new TemplateRule(
            new Regex(@"^Pole „(?<label>.+)” jest wymagane\.$"),
            (m, lang) => lang switch
            {
                "en" => $"The field \"{m.Groups["label"].Value}\" is required.",
                "es" => $"El campo «{m.Groups["label"].Value}» es obligatorio.",
                "de" => $"Das Feld „{m.Groups["label"].Value}“ ist erforderlich.",
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
                _ => null,
            }),
    };

    private static readonly string[] SupportedLanguages = { "pl", "en", "es", "de" };

    public static string Translate(string message, string? language)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        var lang = (language ?? "pl").Trim().ToLowerInvariant();
        if (Array.IndexOf(SupportedLanguages, lang) < 0)
        {
            lang = "pl";
        }

        if (Exact.TryGetValue(message, out var translations))
        {
            return lang switch
            {
                "pl" => message,
                "en" => translations.En,
                "es" => translations.Es,
                "de" => translations.De,
                _ => message,
            };
        }

        foreach (var rule in Templates)
        {
            var match = rule.Pattern.Match(message);
            if (match.Success)
            {
                return rule.Build(match, lang) ?? message;
            }
        }

        return message;
    }
}
