namespace Tenebit.Application.Protocols;

/// <summary>
/// Teksty stałe protokołu. Dokument jest materiałem dowodowym, więc jego język wynika z ustawień
/// organizacji, a nie z języka interfejsu osoby, która akurat klika "pobierz" - dwa egzemplarze tego
/// samego protokołu nie mogą różnić się treścią klauzuli.
///
/// Uwaga prawna (spec 2.4): potwierdzenie linkiem, hash i rysowany podpis to `potwierdzenie
/// elektroniczne`, a nie kwalifikowany podpis elektroniczny - stopka musi to mówić wprost.
/// </summary>
public sealed record ProtocolLabels(
    string HandoverTitle,
    string ReturnTitle,
    string Organization,
    string Employee,
    string EmployeeNumber,
    string JobTitle,
    string ProtocolNumber,
    string IssuedAt,
    string ConfirmedAt,
    string NotConfirmed,
    string Item,
    string AssetTag,
    string SerialNumber,
    string Condition,
    string Value,
    string Status,
    string Procedures,
    string Notes,
    string NoItems,
    string IntegrityHash,
    string LiabilityClause,
    string LegalNote,
    string Page)
{
    private static readonly ProtocolLabels Polish = new(
        HandoverTitle: "Protokół przekazania sprzętu",
        ReturnTitle: "Protokół zwrotu sprzętu",
        Organization: "Organizacja",
        Employee: "Pracownik",
        EmployeeNumber: "Nr pracownika",
        JobTitle: "Stanowisko",
        ProtocolNumber: "Numer protokołu",
        IssuedAt: "Data wystawienia",
        ConfirmedAt: "Data potwierdzenia",
        NotConfirmed: "Niepotwierdzony",
        Item: "Pozycja",
        AssetTag: "Tag",
        SerialNumber: "Nr seryjny",
        Condition: "Stan",
        Value: "Wartość",
        Status: "Status",
        Procedures: "Zaakceptowane procedury",
        Notes: "Uwagi",
        NoItems: "Brak pozycji.",
        IntegrityHash: "Suma kontrolna potwierdzenia (SHA-256)",
        LiabilityClause: "Pracownik potwierdza odbiór wymienionego wyżej mienia, przyjmuje je na swój stan i zobowiązuje się używać go zgodnie z przeznaczeniem oraz zwrócić na żądanie pracodawcy albo przy zakończeniu zatrudnienia.",
        LegalNote: "Dokument stanowi potwierdzenie elektroniczne (zapis akceptacji) złożone przez link wysłany na adres pracownika. Nie jest kwalifikowanym podpisem elektronicznym w rozumieniu rozporządzenia eIDAS. Suma kontrolna umożliwia wykrycie zmiany treści protokołu po jego potwierdzeniu.",
        Page: "Strona");

    private static readonly ProtocolLabels English = new(
        HandoverTitle: "Equipment handover protocol",
        ReturnTitle: "Equipment return protocol",
        Organization: "Organisation",
        Employee: "Employee",
        EmployeeNumber: "Employee no.",
        JobTitle: "Job title",
        ProtocolNumber: "Protocol number",
        IssuedAt: "Issued at",
        ConfirmedAt: "Confirmed at",
        NotConfirmed: "Not confirmed",
        Item: "Item",
        AssetTag: "Tag",
        SerialNumber: "Serial no.",
        Condition: "Condition",
        Value: "Value",
        Status: "Status",
        Procedures: "Accepted procedures",
        Notes: "Notes",
        NoItems: "No items.",
        IntegrityHash: "Confirmation checksum (SHA-256)",
        LiabilityClause: "The employee confirms receipt of the property listed above, takes responsibility for it, and undertakes to use it as intended and return it on the employer's request or when the employment ends.",
        LegalNote: "This document is an electronic acknowledgement recorded through a link sent to the employee's address. It is not a qualified electronic signature under the eIDAS Regulation. The checksum makes any change to the protocol after confirmation detectable.",
        Page: "Page");

    private static readonly ProtocolLabels Spanish = new(
        HandoverTitle: "Protocolo de entrega de equipo",
        ReturnTitle: "Protocolo de devolución de equipo",
        Organization: "Organización",
        Employee: "Empleado",
        EmployeeNumber: "N.º de empleado",
        JobTitle: "Puesto",
        ProtocolNumber: "Número de protocolo",
        IssuedAt: "Fecha de emisión",
        ConfirmedAt: "Fecha de confirmación",
        NotConfirmed: "Sin confirmar",
        Item: "Elemento",
        AssetTag: "Etiqueta",
        SerialNumber: "N.º de serie",
        Condition: "Estado",
        Value: "Valor",
        Status: "Situación",
        Procedures: "Procedimientos aceptados",
        Notes: "Observaciones",
        NoItems: "Sin elementos.",
        IntegrityHash: "Suma de verificación de la confirmación (SHA-256)",
        LiabilityClause: "El empleado confirma la recepción de los bienes indicados anteriormente, los asume bajo su custodia y se compromete a utilizarlos conforme a su finalidad y a devolverlos a requerimiento del empleador o al finalizar la relación laboral.",
        LegalNote: "Este documento constituye una confirmación electrónica (registro de aceptación) realizada mediante un enlace enviado a la dirección del empleado. No es una firma electrónica cualificada conforme al Reglamento eIDAS. La suma de verificación permite detectar cualquier modificación del protocolo posterior a su confirmación.",
        Page: "Página");

    private static readonly ProtocolLabels German = new(
        HandoverTitle: "Übergabeprotokoll für Arbeitsmittel",
        ReturnTitle: "Rückgabeprotokoll für Arbeitsmittel",
        Organization: "Organisation",
        Employee: "Mitarbeitende Person",
        EmployeeNumber: "Personalnummer",
        JobTitle: "Funktion",
        ProtocolNumber: "Protokollnummer",
        IssuedAt: "Ausstellungsdatum",
        ConfirmedAt: "Bestätigt am",
        NotConfirmed: "Nicht bestätigt",
        Item: "Gegenstand",
        AssetTag: "Kennung",
        SerialNumber: "Seriennummer",
        Condition: "Zustand",
        Value: "Wert",
        Status: "Status",
        Procedures: "Akzeptierte Richtlinien",
        Notes: "Anmerkungen",
        NoItems: "Keine Positionen.",
        IntegrityHash: "Prüfsumme der Bestätigung (SHA-256)",
        LiabilityClause: "Die mitarbeitende Person bestätigt den Empfang der oben aufgeführten Gegenstände, übernimmt sie in ihre Obhut und verpflichtet sich, sie bestimmungsgemäß zu verwenden und auf Verlangen des Arbeitgebers oder bei Beendigung des Arbeitsverhältnisses zurückzugeben.",
        LegalNote: "Dieses Dokument ist eine elektronische Bestätigung (Annahmenachweis), die über einen an die Adresse der mitarbeitenden Person gesendeten Link abgegeben wurde. Es handelt sich nicht um eine qualifizierte elektronische Signatur im Sinne der eIDAS-Verordnung. Die Prüfsumme ermöglicht es, nachträgliche Änderungen am Protokoll zu erkennen.",
        Page: "Seite");

    /// <summary>Cztery języki interfejsu mają własną klauzulę; nieznany język dostaje angielską.</summary>
    public static ProtocolLabels For(string? language) => (language?.Trim().ToLowerInvariant()) switch
    {
        "pl" => Polish,
        "es" => Spanish,
        "de" => German,
        _ => English
    };
}
