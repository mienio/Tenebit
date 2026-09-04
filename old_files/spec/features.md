# Specyfikacja rozwoju Tenebit: odpowiedzialność za sprzęt i samoobsługa pracownika

## 1. Cel dokumentu

Dokument opisuje pięć powiązanych funkcji rozwijających Tenebit:

1. ExitProof — kreator offboardingu.
2. Kampanie potwierdzenia aktywów.
3. Zdjęcia przy wydaniu i zwrocie.
4. Konfigurowalne alerty i digest.
5. Portal zamawiania i rezerwowania sprzętu.

Specyfikacja jest dopasowana do obecnego projektu: backend .NET z warstwami Domain/Application/Infrastructure/Api, frontend React, EF Core, SMTP, zadania w tle, QuestPDF, dziennik aktywności, publiczne linki do potwierdzeń i istniejący portal `MyWorkspace`.

Dokument definiuje docelowe zachowanie produktu, dane, API, ekrany, uprawnienia, integracje między modułami, przypadki brzegowe i kryteria akceptacji. Nie jest instrukcją jednoczesnego wdrożenia wszystkiego. Kolejność implementacji znajduje się w sekcji 11.

## 2. Stan obecny i założenia

### 2.1. Elementy projektu przeznaczone do ponownego użycia

Obecny projekt dostarcza już:

- osoby, zespoły, przełożonych, stanowiska i lokalizacje;
- aktywa z kategorią, tagiem, numerem seryjnym, statusem, właścicielem, lokalizacją, gwarancją i kodem QR;
- wydania sprzętu oraz pełny zwrot wydania;
- stan sprzętu przy wydaniu i zwrocie zapisany tekstowo;
- publiczny link do potwierdzenia odbioru;
- potwierdzenia procedur i generowanie protokołu PDF;
- miejsca licencyjne przypisane do osób;
- portal pracownika `MyWorkspace`;
- alerty e-mail o gwarancjach, zaległych zwrotach i opóźnionym onboardingu;
- dziennik aktywności;
- role organizacyjne i pojedyncze konfigurowalne uprawnienie;
- tłumaczenia polskie, angielskie, hiszpańskie i niemieckie;
- plan Free/Pro oraz rozliczenie Stripe.

Nowe funkcje powinny rozszerzać te mechanizmy, a nie budować drugi równoległy system aktywów, użytkowników, powiadomień albo dokumentów.

### 2.2. Wspólne zasady implementacyjne

- Każdy nowy rekord biznesowy musi zawierać `OrganizationId` i być pobierany wyłącznie w kontekście bieżącej organizacji.
- Operacje zmieniające stan zapisują wpis w istniejącym `ActivityLog`.
- Czas przechowywany jest jako UTC. Daty i godziny pokazywane użytkownikowi są przeliczane według `Organization.TimeZone`.
- Publiczne linki nowych modułów korzystają z losowego tokenu. W bazie przechowywany jest wyłącznie hash tokenu oraz termin ważności.
- Publiczne endpointy korzystają z istniejącego rate limitera `public`.
- Nie dodajemy brokera wiadomości, zewnętrznego storage ani nowego frameworka zadań. MVP używa obecnego zadania w tle, SMTP i bazy danych.
- Błędy domenowe są zwracane przez istniejący `Result` i mapowane na aktualny format HTTP.
- Listy administracyjne są stronicowane. Eksporty nie mogą polegać na pobraniu wszystkich rekordów do przeglądarki.
- Wszystkie nowe napisy trafiają do `translations.ts` dla czterech obsługiwanych języków.
- Nowe formularze i publiczne strony muszą działać na telefonie, ponieważ potwierdzenia i zdjęcia będą często wykonywane mobilnie.

### 2.3. Wspólne statusy i odpowiedzialność za dane

Źródłem prawdy o bieżącym właścicielu sprzętu pozostaje `Asset.AssignedPersonId`. Wydania, rezerwacje, audyty i offboarding zapisują historię procesu, ale nie mogą tworzyć konkurencyjnego pola właściciela.

Źródłem prawdy o dostępności w przedziale czasu jest rezerwacja. `Asset.Status` opisuje aktualny stan fizyczny, a nie wszystkie przyszłe rezerwacje.

Zakończenie zatrudnienia i rozliczenie sprzętu są dwoma niezależnymi procesami:

- osoba może zostać automatycznie zdezaktywowana w ostatnim dniu pracy, nawet jeżeli nie odpowiedziała i nie zwróciła sprzętu;
- fizyczne aktywo nie staje się dostępne tylko dlatego, że osoba została zdezaktywowana;
- do czasu potwierdzonego odbioru aktywo pozostaje przypisane do osoby i ma stan `PendingReturn`;
- po odbiorze aktywo trafia bezpośrednio do `InStock` albo do `InService` na kontrolę, zgodnie z polityką kategorii;
- dopiero `InStock` wraz z `IsReservable = true` oznacza, że sprzęt może zostać przydzielony lub zarezerwowany.

Automatyczna odpowiedź pracownika „brakuje” albo „uszkodzone” nie może samodzielnie zmieniać statusu aktywa na `Lost` lub `Damaged`. Wymaga zatwierdzenia przez administratora lub operatora aktywów.

### 2.4. Założenia dla klientów europejskich

Tenebit powinien realizować prywatność domyślnie, ale nie może obiecywać klientowi zgodności z każdym prawem krajowym wyłącznie przez włączenie funkcji. Klient jest administratorem danych, Tenebit co do zasady procesorem, a klient konfiguruje podstawę prawną, okresy przechowywania i treść informacji dla pracowników.

Wspólne wymagania produktu:

- minimalizacja danych: publiczne strony pokazują tylko informacje potrzebne do konkretnego zwrotu lub audytu;
- ograniczenie celu: kampanie służą kontroli ewidencji, nie monitorowaniu produktywności, zachowania ani lokalizacji pracownika;
- ograniczenie przechowywania: zdjęcia, adresy IP, odpowiedzi i protokoły mają konfigurowalne okresy retencji oraz proces usuwania albo anonimizacji;
- privacy by default: zapis pełnego adresu IP jest domyślnie wyłączony, metadane EXIF/GPS są usuwane ze zdjęć, a zdjęcia nie są obowiązkowe dla każdej kategorii;
- rozliczalność: każda zmiana statusu, ręczne odstępstwo, pobranie materiału dowodowego i zmiana polityki retencji trafiają do `ActivityLog`;
- lokalizacja: formaty dat, język, strefa czasowa, dni robocze i godziny ciszy wynikają z ustawień organizacji;
- język odbiorcy: e-mail i publiczna strona wybierają `Person.PreferredLanguage`, a następnie język organizacji; treść informacji o prywatności jest wersjonowana per język i zatwierdzana przez klienta;
- eksport danych osoby oraz anonimizacja po upływie retencji są dostępne administratorowi niezależnie od planu płatnego;
- e-maile nie zawierają w temacie numerów seryjnych, informacji o szkodzie ani innych niepotrzebnych danych osobowych.

Potwierdzenie przez link i hash dokumentu należy nazywać `potwierdzeniem elektronicznym` lub `zapisem akceptacji`, a nie kwalifikowanym podpisem elektronicznym. Tylko integracja z kwalifikowanym dostawcą usług zaufania może być opisywana jako QES. Hash SHA-256 zapewnia techniczną kontrolę integralności, lecz sam nie jest kwalifikowaną pieczęcią elektroniczną.

Dla klientów finansowych ExitProof może dostarczać dowody wspierające politykę zwrotu aktywów ICT i odbierania dostępu przy zakończeniu zatrudnienia, o których mówi [rozporządzenie delegowane 2024/1774 uzupełniające DORA](https://eur-lex.europa.eu/eli/reg_del/2024/1774/oj). Jest to argument sprzedażowy i materiał audytowy, nie deklaracja, że samo Tenebit zapewnia zgodność klienta z DORA.

Podstawa projektowa:

- [RODO, w szczególności zasady z art. 5 i privacy by design z art. 25](https://eur-lex.europa.eu/legal-content/PL/TXT/?uri=CELEX:32016R0679);
- [Komisja Europejska — dane pracowników i adres IP jako dane osobowe](https://commission.europa.eu/law/law-topic/data-protection/information-business-and-organisations/application-gdpr_en);
- [Europejska Rada Ochrony Danych — zgoda może nie być dobrowolna przy nierównowadze, np. pracodawca–pracownik](https://www.edpb.europa.eu/system/files/2026-04/edpb-summary-consent_en.pdf);
- [Komisja Europejska — informacja przekazywana osobie przy zbieraniu danych](https://commission.europa.eu/law/law-topic/data-protection/rules-business-and-organisations/principles-gdpr/what-information-must-be-given-individuals-whose-data-collected_en);
- [Komisja Europejska — przechowywanie danych tylko przez niezbędny okres](https://commission.europa.eu/law/law-topic/data-protection/rules-business-and-organisations/principles-gdpr/how-long-can-data-be-kept-and-it-necessary-update-it_en);
- [eIDAS — skutki podpisów i pieczęci elektronicznych](https://eur-lex.europa.eu/legal-content/PL/TXT/?uri=CELEX:02014R0910-20240520).

### 2.5. Elementy, których nie należy dodawać do tego zakresu

- GPS, śledzenie urządzenia, agent zbierający aktywność użytkownika lub ocenianie pracowników;
- rozpoznawanie twarzy, danych biometrycznych albo automatyczna analiza osób widocznych na zdjęciu;
- automatyczne kary, potrącenia lub decyzje dyscyplinarne na podstawie braku odpowiedzi;
- twierdzenia marketingowe typu `pełna zgodność z RODO`, `niepodważalny dowód` albo `kwalifikowany podpis`;
- automatyczne udostępnienie fizycznego sprzętu przed jego odbiorem i kontrolą;
- integracje kurierskie, MDM, Entra i Google Workspace w MVP — wymagają osobnego zakresu, uprawnień i analizy bezpieczeństwa.

### 2.6. Wnioski z aktualnych wzorców rynkowych

Przegląd aktualnej dokumentacji produktów ITAM potwierdza cztery decyzje dla Tenebit:

- zwrot powinien być liniowym zleceniem odzyskania sprzętu z możliwym wynikiem: magazyn, ponowne przypisanie, naprawa albo utylizacja; podobny przepływ opisuje [ServiceNow Asset Reclamation](https://www.servicenow.com/docs/r/it-asset-management/hardware-asset-management/reclaim-asset.html);
- przyjęcie urządzenia powinno automatycznie aktualizować stan magazynu i pozostawiać pełną historię statusów, co jest istotą [Salesforce Asset Reclaims](https://help.salesforce.com/s/articleView?id=service.it_srvcs_asset_mgmt_reclaims_parent.htm&type=5);
- odstąpienie od zwrotu musi być jawnym rozstrzygnięciem z powodem, a nie cichym pominięciem; taką regułę stosuje również [SAP SuccessFactors Offboarding](https://help.sap.com/docs/successfactors-onboarding/implementing-onboarding/listing-assets-that-employee-leaving-company-has-to-return);
- wniosek rezerwacyjny może wskazywać model lub kategorię, a konkretny egzemplarz dopiero przed wydaniem. Ogranicza to ekspozycję danych magazynowych i jest znanym wzorcem w [systemach rezerwowania wyposażenia](https://docs.wennsoft.com/__attachments/109609045/Equipment%20Management%202023%20%2818.6.8%29%C2%A0Guide.pdf?inst-v=acad7dbb-9001-4128-913f-f6c360399d2a).

Wynik dla zakresu: należy zachować automatyczny powrót do puli, ale tylko dla aktywów przeznaczonych do ponownego użycia. Sprzęt leasingowany, przeznaczony do zwrotu dostawcy lub utylizacji nie może trafić do katalogu rezerwacji.

## 3. Wspólna podstawa techniczna

Pięć funkcji współdzieli cztery mechanizmy, które należy wdrożyć tylko raz.

### 3.1. Zwroty częściowe

Obecny `Assignment.Return(...)` zwraca wszystkie aktywa jednocześnie. ExitProof i rezerwacje wymagają zwrotu pojedynczych elementów.

Należy rozszerzyć `AssignmentAsset` o:

- `ReturnedAt: DateTimeOffset?`;
- `ReturnCondition: string?` — pole już istnieje i pozostaje;
- `ReturnLocation: string?`;
- `ReturnedBy: string?`;
- `ReturnResolution: Returned | Missing | Damaged | Retained | WrittenOff`;
- opcjonalny `ReturnNotes: string?`.

Do `AssignmentStatus` należy dodać `PartiallyReturned`.

Reguły:

- zwrot pojedynczego aktywa jest idempotentny;
- aktywo zwrócone fizycznie traci `AssignedPersonId`, a następny status wynika z polityki kategorii: `InStock` dla prostego zwrotu albo `InService` do czasu kontroli technicznej;
- aktywo zgłoszone jako uszkodzone przechodzi do `Damaged` dopiero po zatwierdzeniu przez operatora;
- aktywo brakujące przechodzi do `Lost` dopiero po zatwierdzeniu przez operatora;
- wydanie otrzymuje `PartiallyReturned`, gdy co najmniej jedno aktywo jest rozliczone, ale pozostały pozycje otwarte;
- wydanie otrzymuje `Returned`, gdy wszystkie aktywa mają końcowe rozstrzygnięcie;
- istniejący endpoint pełnego zwrotu pozostaje kompatybilny i wewnętrznie wywołuje zwrot dla każdej pozycji.

Nowy endpoint:

```text
POST /api/assignments/{assignmentId}/assets/{assetId}/return
```

Body:

```json
{
  "resolution": "Returned",
  "returnCondition": "Sprawny, drobna rysa na obudowie",
  "returnLocation": "Magazyn główny",
  "notes": null
}
```

### 3.2. Bezpieczne publiczne tokeny

Nowe publiczne procesy nie powinny opierać dostępu wyłącznie na identyfikatorach GUID w adresie.

Wspólny komponent powinien:

- generować co najmniej 32 losowe bajty;
- zwracać token w formacie Base64URL;
- zapisywać SHA-256 tokenu w rekordzie procesu;
- weryfikować token w stałym czasie;
- obsługiwać `ExpiresAt`, `RevokedAt` i ponowne wygenerowanie linku;
- unieważniać poprzedni token po wygenerowaniu nowego;
- nie zapisywać pełnego tokenu w `ActivityLog` ani logach serwera.

Dotyczy to ExitProof i kampanii audytowych. Portal rezerwacji wymaga zalogowania i nie korzysta z publicznego tokenu.

### 3.3. Materiał dowodowy

Wspólna encja `AssetEvidence` obsługuje zdjęcia dla wydania, zwrotu, offboardingu i audytu.

Proponowane pola:

```text
AssetEvidence
- Id
- OrganizationId
- AssetId
- AssignmentId?
- OffboardingItemId?
- AssetAuditItemId?
- EvidencePhase: Issue | Return | Audit | Offboarding
- FileName
- ContentType
- Content
- SizeBytes
- Sha256
- Caption?
- UploadedAt
- UploadedBy
- UploadedVia: AuthenticatedUser | PublicToken
- LockedAt?
```

`Content` jest przechowywany w bazie tak jak obecne dokumenty procedur. Pozwala to wdrożyć MVP bez nowej usługi storage. Po przekroczeniu uzgodnionego progu danych można później przenieść zawartość do storage obiektowego bez zmiany kontraktów API.

Materiał z ustawionym `LockedAt` nie może być zastąpiony ani usunięty zwykłą operacją użytkownika. Korekta polega na dodaniu kolejnego zdjęcia i wpisu wyjaśniającego. Kontrolowane usunięcie po upływie retencji albo wykonaniu obowiązku prawnego jest osobną operacją systemową, pozostawiającą ślad audytowy bez treści pliku.

### 3.4. Cykl osoby i polityka ponownego udostępnienia aktywa

Do `Person` należy dodać jawny cykl życia, bez tworzenia drugiego źródła prawdy dla aktywności konta:

```text
EmploymentStatus: Active | Offboarding | Inactive
EmploymentEndsAt?
DeactivatedAt?
PreferredLanguage?
```

`IsActive` pozostaje polem kompatybilnym używanym przez istniejący kod. Dla `Active` i `Offboarding` ma wartość `true`; po wykonaniu zaplanowanej dezaktywacji przechodzi na `false`, a `EmploymentStatus` na `Inactive`.

Do `AssetStatus` należy dodać `PendingReturn`. Status oznacza, że aktywo nadal znajduje się poza magazynem, ale jest już objęte rozpoczętym zwrotem. Nie jest dostępne do wydania ani rezerwacji.

Na kategorii aktywa należy przechowywać:

```text
ReturnHandlingMode: DirectToStock | InspectionRequired
PostReturnDisposition: Reuse | ReturnToVendor | Dispose
ReturnChecklistTemplate?
PhotoOnIssue: Disabled | Optional | Required
PhotoOnReturn: Disabled | Optional | Required
```

Domyślne ustawienie dla laptopów, telefonów i pojazdów to `InspectionRequired + Reuse`. Dla prostych akcesoriów administrator może wybrać `DirectToStock + Reuse`. Sprzęt leasingowany otrzymuje `ReturnToVendor`, a przeznaczony do trwałego wycofania `Dispose`.

Przepływ fizycznego zwrotu:

1. Rozpoczęcie offboardingu ustawia przypisane aktywa na `PendingReturn`, ale zachowuje `AssignedPersonId`.
2. Administrator, operator albo technik potwierdza faktyczne przyjęcie każdego aktywa.
3. `DirectToStock + Reuse` i stan sprawny: system usuwa przypisanie, ustawia `InStock` i aktywo natychmiast pojawia się w katalogu, jeśli `IsReservable = true`.
4. `InspectionRequired + Reuse`: system usuwa przypisanie, ustawia `InService` i tworzy checklistę kontroli, np. zgodność numeru seryjnego, komplet akcesoriów, usunięcie danych, test działania i ocenę szkód.
5. Technik wybiera `Gotowe do ponownego użycia`, co ustawia `InStock`; albo kończy kontrolę jako `Damaged`, `Retired` lub `Disposed`.
6. `ReturnToVendor` po odbiorze przechodzi do `InTransit`, wymaga wskazania odbiorcy i potwierdzenia przekazania, a następnie kończy się jako `Retired`; nie pojawia się w katalogu.
7. `Dispose` przechodzi do `InService` do czasu potwierdzenia właściwej procedury utylizacji, a następnie do `Disposed`; nie pojawia się w katalogu.
8. Brak fizycznego zwrotu pozostawia aktywo jako `PendingReturn`; dopiero ręczne rozstrzygnięcie może ustawić `Lost` lub `Retained`.

Zmiany statusu są idempotentne. Zadanie w tle może ponawiać dezaktywację osoby i zwalnianie licencji, ale nigdy nie symuluje fizycznego odbioru aktywa.

## 4. ExitProof — kreator offboardingu

### 4.1. Cel biznesowy

ExitProof prowadzi administratora przez odejście pracownika i daje jednoznaczną odpowiedź na pytania:

- jaki sprzęt pracownik powinien zwrócić;
- które elementy zostały zwrócone, są uszkodzone albo zaginęły;
- które miejsca licencyjne zostały zwolnione;
- kto wykonał poszczególne działania i kiedy;
- czy proces można bezpiecznie zamknąć;
- jaki dokument potwierdza końcowy stan rozliczenia.

### 4.2. Zakres MVP

MVP obejmuje:

- utworzenie sprawy offboardingowej dla aktywnej osoby;
- automatyczne zebranie jej aktywów, otwartych wydań i licencji;
- utworzenie migawki checklisty;
- natychmiastową blokadę nowych wniosków i rezerwacji tej osoby;
- zaplanowaną, automatyczną dezaktywację osoby w ostatnim dniu pracy, niezależną od odpowiedzi pracownika;
- automatyczne anulowanie przyszłych rezerwacji i odrzucenie oczekujących wniosków;
- opcjonalne automatyczne zwolnienie miejsc licencyjnych w terminie zakończenia zatrudnienia;
- zmianę sprzętu na `PendingReturn` bez przedwczesnego udostępnienia go innym;
- publiczny link dla odchodzącego pracownika jako kanał pomocniczy, a nie warunek zakończenia procesu;
- opcjonalną odpowiedź pracownika dla każdego aktywa;
- częściowe przyjmowanie zwrotów przez administratora;
- kontrolę techniczną po zwrocie i automatyczne udostępnienie sprawnego sprzętu;
- przypomnienia przed i po terminie;
- blokadę zamknięcia przy nierozliczonych wymaganych pozycjach;
- końcowy protokół PDF;
- pełną historię w dzienniku audytowym.

Poza MVP pozostają:

- automatyczne etykiety kurierskie;
- zamawianie kartonów i odbioru przez kuriera;
- zdalne czyszczenie urządzeń;
- wyłączanie kont w Entra, Google Workspace i innych aplikacjach;
- rozliczenia finansowe z pracownikiem.

Ważne ograniczenie: zwolnienie miejsca licencyjnego w Tenebit aktualizuje ewidencję Tenebit. Bez osobnej integracji nie wyłącza konta ani subskrypcji w systemie zewnętrznym.

### 4.3. Model danych

#### OffboardingCase

```text
- Id
- OrganizationId
- PersonId
- Status: Draft | Active | WaitingForReturn | ReadyToClose | Completed | Cancelled
- EmploymentEndsAt
- ReturnDueDate
- DefaultReturnLocation?
- Notes?
- ProcessOwnerId?
- BlockNewReservations: bool
- CancelFutureReservations: bool
- AutoReleaseLicenses: bool
- PersonDeactivatedAt?
- ScheduledActionsCompletedAt?
- PublicTokenHash?
- PublicTokenExpiresAt?
- PublicTokenRevokedAt?
- CreatedAt
- CreatedBy
- StartedAt?
- CompletedAt?
- CompletedBy?
- CancelledAt?
- CancellationReason?
- FinalProtocolNumber?
```

#### OffboardingItem

```text
- Id
- OrganizationId
- OffboardingCaseId
- Type: AssetReturn | LicenseRelease | ManualTask
- AssetId?
- AssignmentId?
- LicenseId?
- Label
- Required
- Status: Pending | EmployeeAcknowledged | Received | Inspecting | Returned | Released | Missing | Damaged | Retained | Waived
- EmployeeResponse?
- EmployeeComment?
- AutomationMode: Manual | AtEmploymentEnd
- AutomationLastAttemptAt?
- AutomationError?
- ReceivedAt?
- ReceivedBy?
- InspectionCompletedAt?
- InspectionCompletedBy?
- ResolutionNotes?
- CompletedAt?
- CompletedBy?
- SortOrder
```

Pozycje są migawką z momentu uruchomienia sprawy. Późniejsza zmiana przypisania aktywa nie usuwa go automatycznie z checklisty. System pokazuje konflikt i wymaga jawnego rozstrzygnięcia.

`Waived` oznacza świadome odstąpienie organizacji od wymogu zwrotu lub wykonania zadania. Wymaga uprawnienia `offboarding.complete`, powodu i aktora; nie może powstać przez brak odpowiedzi albo automatyczny timeout.

W bazie obowiązuje unikalność jednej niezakończonej sprawy offboardingowej dla danej osoby i organizacji.

### 4.4. Statusy procesu

- `Draft` — konfiguracja nie została jeszcze uruchomiona.
- `Active` — proces został uruchomiony; pracownik może nadal być aktywny do daty zakończenia zatrudnienia.
- `WaitingForReturn` — osoba została zdezaktywowana albo minął termin zwrotu, ale pozostają nierozliczone pozycje. Status nie zależy od odpowiedzi pracownika.
- `ReadyToClose` — wszystkie wymagane pozycje mają końcowe rozstrzygnięcie.
- `Completed` — protokół został wygenerowany, token unieważniony, a każda pozycja ma końcowe rozstrzygnięcie.
- `Cancelled` — proces przerwany przed terminem zakończenia zatrudnienia. Anuluje przyszłe automatyzacje, przywraca `EmploymentStatus = Active` i status `Assigned` dla nieodebranych aktywów, ale nie odtwarza anulowanych wcześniej rezerwacji.

Status `ReadyToClose` powinien być wyliczany na podstawie pozycji, a nie ustawiany ręcznie. Sprawa może być gotowa operacyjnie wcześniej, ale `Complete` wymaga także `PersonDeactivatedAt` oraz rozstrzygnięcia wszystkich zaplanowanych działań. Dzięki temu wcześniejszy zwrot sprzętu nie powoduje pominięcia przyszłej dezaktywacji.

Dezaktywacja osoby nie jest statusem sprawy. Zaplanowane zadanie wykonuje ją przy `EmploymentEndsAt` w strefie czasowej organizacji. Dzięki temu brak kliknięcia pracownika ani opóźniony zwrot laptopa nie pozostawia osoby aktywnej w systemie.

Po wykonaniu dezaktywacji zwykłe `Anuluj offboarding` jest zablokowane. Pomyłkę obsługuje jawna akcja `Przywróć zatrudnienie`, która ponownie aktywuje osobę, ale nie przydziela automatycznie zwolnionych licencji ani zwróconych aktywów. Administrator widzi listę czynności wymagających ręcznego odtworzenia.

### 4.5. Przebieg administratora

1. Na stronie osoby administrator wybiera `Rozpocznij offboarding`.
2. System pokazuje podsumowanie:
   - aktywa przypisane bezpośrednio;
   - aktywa z otwartych wydań;
   - miejsca licencyjne;
   - przyszłe rezerwacje;
   - nierozstrzygnięte kampanie audytowe.
3. Administrator ustawia datę i godzinę zakończenia zatrudnienia, termin zwrotu, lokalizację zwrotu i notatkę.
4. Kreator domyślnie zaznacza: blokadę nowych rezerwacji od razu, anulowanie przyszłych rezerwacji i zwolnienie licencji w dniu odejścia. Administrator może wyłączyć automatyczne zwolnienie wybranej licencji, jeżeli wymaga ręcznej procedury.
5. Administrator może dodać ręczne zadanie, np. `Oddanie karty wejściowej`, jeśli karta nie jest osobnym aktywem.
6. Podgląd pokazuje planowane automatyczne działania, odbiorcę wiadomości i wszystkie pozycje.
7. `Uruchom offboarding` tworzy migawkę, ustawia osobę na `Offboarding`, ustawia aktywa na `PendingReturn`, blokuje nowe wnioski i wykonuje skonfigurowane anulowania.
8. Wiadomość i publiczny token są tworzone tylko, gdy osoba ma adres e-mail i administrator nie wyłączył kontaktu. Brak adresu nie blokuje uruchomienia.
9. W terminie `EmploymentEndsAt` zadanie w tle najpierw ustawia `Person.IsActive = false` i `EmploymentStatus = Inactive`, a następnie zwalnia wskazane licencje. Każda akcja ma osobną idempotentną próbę: błąd jednej licencji nie cofa dezaktywacji osoby ani udanych działań, lecz tworzy alert i pozostaje do ponowienia.
10. Administrator przyjmuje zwroty pojedynczo, rejestruje stan, lokalizację, kompletność i opcjonalne zdjęcia.
11. Sprzęt z polityką `Reuse + DirectToStock` trafia od razu do `InStock`. `Reuse + InspectionRequired` trafia do `InService`, a technik kończy checklistę przed ustawieniem `InStock`. `ReturnToVendor` i `Dispose` używają własnego wyniku i nigdy nie trafiają do katalogu.
12. Brakujący, zatrzymany lub uszkodzony sprzęt wymaga ręcznego rozstrzygnięcia, notatki i właściwego uprawnienia.
13. Po rozliczeniu wymaganych pozycji przycisk `Zamknij offboarding` staje się aktywny.
14. Zamknięcie generuje protokół i unieważnia link. Nie jest mechanizmem dezaktywacji osoby, ponieważ ta następuje według terminu zatrudnienia.

Jeżeli HR tworzy sprawę już po odejściu pracownika, `Uruchom offboarding` natychmiast wykonuje zaległą dezaktywację i działania cyfrowe, a sprzęt ustawia jako `PendingReturn`.

Każdy endpoint tworzący wniosek, rezerwację albo nowe wydanie dla osoby sprawdza `EmploymentStatus = Active`. Samo starsze pole `IsActive` nie wystarcza, ponieważ osoba w `Offboarding` może być jeszcze zalogowana do końca zatrudnienia, ale nie powinna zaciągać nowych zobowiązań sprzętowych.

### 4.6. Przebieg pracownika

Publiczna strona `/exit/{token}` jest opcjonalna. Pokazuje:

- nazwę organizacji;
- termin zwrotu i instrukcję;
- listę sprzętu z nazwą, tagiem, numerem seryjnym ograniczonym do bezpiecznego zakresu i zdjęciem wydania;
- listę elementów wymagających odpowiedzi;
- odpowiedź per aktywo: `Mam i zwrócę`, `Już zwrócone`, `Nie mam`, `Uszkodzone`;
- opcjonalny komentarz i możliwość dodania zdjęcia dla stanu `Uszkodzone`;
- zbiorcze potwierdzenie wysłania odpowiedzi.

Odpowiedź ma charakter informacyjny. Brak odpowiedzi, wygaśnięcie tokenu albo odmowa użycia strony nie blokują dezaktywacji osoby, odbioru przez administratora ani zamknięcia sprawy z udokumentowanym wyjątkiem.

Pracownik nie może:

- zmienić swojej odpowiedzi po fizycznym rozliczeniu pozycji;
- zobaczyć kosztu aktywa, klucza licencyjnego ani danych innych osób;
- oznaczyć aktywa jako zwrócone w magazynie bez potwierdzenia administratora.

Strona zawiera krótką informację o prywatności: administrator danych, cel procesu, zakres danych, link do pełnej informacji, okres przechowywania lub kryterium jego ustalenia oraz kontakt do realizacji praw. Przycisk nazywa się `Wyślij potwierdzenie`, nie `Wyrażam zgodę`; zgoda pracownika nie jest mechanizmem legalizującym obowiązkową ewidencję pracodawcy.

Zalogowany pracownik widzi tę samą sprawę również w `MyWorkspace` bez użycia tokenu.

### 4.7. Ekrany frontendowe

#### Nowa strona `/offboarding`

- liczniki: aktywne, po terminie, gotowe do zamknięcia;
- tabela: osoba, ostatni dzień, termin zwrotu, postęp, status, właściciel procesu;
- filtry: status, termin, zespół, lokalizacja;
- akcja `Nowy offboarding`;
- panel szczegółów lub osobny route `/offboarding/{id}`.

#### Widok szczegółów

- nagłówek ze statusem i paskiem postępu;
- osobne wskaźniki `Status osoby` oraz `Rozliczenie aktywów`, aby nie sugerować, że są tym samym;
- panel `Zaplanowane działania`: data dezaktywacji, licencje, rezerwacje, wynik ostatniej próby;
- sekcja `Sprzęt`;
- sekcja `Licencje`;
- sekcja `Zadania dodatkowe`;
- oś czasu zdarzeń z `ActivityLog`;
- kolejka `Odebrane — do kontroli` z akcją `Gotowe do ponownego użycia`;
- działania: kopiuj link, wyślij ponownie, unieważnij link, wykonaj zaplanowane działania teraz, anuluj, zamknij, pobierz PDF.

#### Zmiany w istniejących ekranach

- `PeoplePage`: akcja offboardingu i znacznik `W offboardingu`;
- `MyWorkspacePage`: karta aktywnego offboardingu;
- `AssignmentsPage`: obsługa zwrotów częściowych;
- `LicensesPage`: informacja, że miejsce oczekuje na zwolnienie w offboardingu;
- `DashboardPage`: widget `Offboarding wymagający uwagi`.

### 4.8. API

```text
GET    /api/offboarding
POST   /api/offboarding
GET    /api/offboarding/{id}
PUT    /api/offboarding/{id}
POST   /api/offboarding/{id}/start
POST   /api/offboarding/{id}/resend
POST   /api/offboarding/{id}/regenerate-link
POST   /api/offboarding/{id}/items
PUT    /api/offboarding/{id}/items/{itemId}
POST   /api/offboarding/{id}/items/{itemId}/release-license
POST   /api/offboarding/{id}/items/{itemId}/confirm-return
POST   /api/offboarding/{id}/items/{itemId}/complete-inspection
POST   /api/offboarding/{id}/execute-scheduled-actions
POST   /api/offboarding/{id}/complete
POST   /api/offboarding/{id}/cancel
POST   /api/offboarding/{id}/restore-employment
GET    /api/offboarding/{id}/protocol

GET    /api/public/offboarding/{token}
POST   /api/public/offboarding/{token}/response
POST   /api/public/offboarding/{token}/items/{itemId}/evidence
```

`POST /complete` musi być idempotentny. Ponowne wywołanie zwraca zakończoną sprawę i nie generuje drugiego protokołu.

### 4.9. Protokół końcowy

QuestPDF otrzymuje nową metodę `GenerateOffboardingProtocol`.

PDF zawiera:

- dane organizacji i osoby;
- numer sprawy i daty;
- tabelę aktywów z wynikiem oraz stanem przy zwrocie;
- tabelę zwolnionych licencji bez kluczy licencyjnych;
- listę nierozliczonych wyjątków i uzasadnienia;
- miniatury zdjęć w dodatku;
- osoby wykonujące czynności i daty;
- skróty SHA-256 materiału dowodowego;
- informację, czy odpowiedź pochodziła od pracownika, administratora czy automatyzacji;
- końcowy wynik: rozliczony albo rozliczony z wyjątkami.

PDF jest protokołem operacyjnym i zapisem integralności. Nie może być opisany jako dokument podpisany kwalifikowanym podpisem, jeśli nie zastosowano zewnętrznej usługi kwalifikowanej. Pełny adres IP, jeśli klient wyjątkowo włączył jego zapis, nie jest umieszczany w standardowym PDF.

### 4.10. Uprawnienia

Domyślne role zarządzające: `owner`, `admin`, `hr`, `asset_operator`.

Nowe klucze uprawnień:

- `offboarding.view`;
- `offboarding.manage`;
- `offboarding.complete`.

`hr` może prowadzić proces, ale zwrot fizyczny i zmianę statusu aktywa domyślnie wykonuje `asset_operator`, `admin` lub `owner`.

### 4.11. Zdarzenia audytowe

Minimum:

```text
offboarding.created
offboarding.started
offboarding.person_marked_offboarding
offboarding.person_deactivated
offboarding.scheduled_action_failed
offboarding.reservations_cancelled
offboarding.link_regenerated
offboarding.employee_responded
offboarding.asset_marked_pending_return
offboarding.asset_returned
offboarding.asset_inspection_completed
offboarding.asset_available
offboarding.asset_missing
offboarding.asset_damaged
offboarding.item_waived
offboarding.license_released
offboarding.completed
offboarding.cancelled
```

### 4.12. Kryteria akceptacji

- Nie można uruchomić drugiej aktywnej sprawy dla tej samej osoby.
- Lista startowa zawiera wszystkie aktywa z `AssignedPersonId` równym osobie oraz jej miejsca licencyjne.
- Pracownik może odpowiedzieć bez konta przez ważny token.
- Brak odpowiedzi pracownika nie blokuje żadnej czynności administracyjnej ani automatycznej dezaktywacji.
- Publiczna odpowiedź nie zmienia automatycznie statusu aktywa na `Lost` lub `Damaged`.
- Administrator może zwracać aktywa pojedynczo.
- W terminie zakończenia zatrudnienia osoba jest dezaktywowana, a skonfigurowane licencje zwalniane nawet przy nierozliczonym sprzęcie.
- Awaria zwolnienia jednej licencji nie cofa dezaktywacji osoby; błąd jest widoczny i ponawiany.
- Rozpoczęcie procesu blokuje nowe rezerwacje i anuluje przyszłe zgodnie z konfiguracją.
- Samo odejście pracownika nie ustawia fizycznego aktywa na `InStock`.
- Fizycznie odebrane aktywo z `DirectToStock` staje się dostępne od razu; aktywo z `InspectionRequired` dopiero po zakończeniu checklisty.
- Aktywo `ReturnToVendor` albo `Dispose` nigdy nie zwiększa dostępności katalogu.
- Nie można zamknąć sprawy z pozycją wymaganą bez końcowego statusu `Returned`, `Released`, `Missing`, `Damaged`, `Retained` albo `Waived`.
- Nie można zamknąć sprawy przed zaplanowaną dezaktywacją osoby; wcześniejsze rozliczenie aktywów pozostawia ją w `ReadyToClose` do daty odejścia.
- Każde działanie znajduje się w `ActivityLog`.
- Protokół po zamknięciu można pobrać ponownie i ma identyczną treść biznesową.

## 5. Kampanie potwierdzenia aktywów

### 5.1. Cel biznesowy

Kampania pozwala okresowo potwierdzić, czy ewidencja odpowiada rzeczywistości. Zamiast ręcznego arkusza każdy pracownik otrzymuje własną listę przypisanych aktywów i odpowiada, co faktycznie posiada.

### 5.2. Zakres MVP

- kampania dla aktywów przypisanych do osób;
- wybór zakresu według zespołu, lokalizacji, kategorii albo konkretnych osób;
- podgląd liczby odbiorców i aktywów przed uruchomieniem;
- osobny token dla każdego uczestnika;
- odpowiedź `Mam`, `Brakuje`, `Uszkodzone`, `To nie jest moje`;
- komentarz i zdjęcie dla wyjątku;
- przypomnienia;
- dashboard wyników;
- rozstrzyganie wyjątków przez administratora;
- eksport CSV i raport PDF;
- zakończenie kampanii i blokada odpowiedzi.

Poza MVP:

- audyt magazynu przez seryjne skanowanie QR;
- tryb offline;
- GPS skanu;
- cykliczne automatyczne tworzenie kampanii.

### 5.3. Model danych

#### AssetAuditCampaign

```text
- Id
- OrganizationId
- Name
- Description?
- Status: Draft | Active | Reviewing | Completed | Cancelled
- DueDate
- ScopeJson
- CreatedAt
- CreatedBy
- StartedAt?
- CompletedAt?
- CompletedBy?
```

`ScopeJson` przechowuje definicję wyboru używaną wyłącznie do podglądu historycznego. Faktyczny zakres jest utrwalony w pozycjach kampanii.

#### AssetAuditParticipant

```text
- Id
- OrganizationId
- CampaignId
- PersonId
- Email
- TokenHash
- TokenExpiresAt
- TokenRevokedAt?
- Status: Pending | InProgress | Submitted | Reviewed
- SubmittedAt?
- LastReminderAt?
```

#### AssetAuditItem

```text
- Id
- OrganizationId
- CampaignId
- ParticipantId
- AssetId
- ExpectedPersonId
- ExpectedLocation?
- Response: Pending | Confirmed | Missing | Damaged | WrongOwner
- Comment?
- RespondedAt?
- Resolution: None | Accepted | AssetMarkedLost | AssetMarkedDamaged | OwnershipCorrected | Dismissed
- ResolutionNotes?
- ResolvedAt?
- ResolvedBy?
```

Kampania przechowuje migawkę. Zmiana właściciela aktywa po rozpoczęciu kampanii nie przepisuje pozycji historycznej.

### 5.4. Tworzenie kampanii

Kreator ma cztery kroki:

1. Nazwa, opis i termin.
2. Zakres: cała organizacja, zespoły, lokalizacje, kategorie lub osoby.
3. Podgląd odbiorców i aktywów z ostrzeżeniami o osobach bez e-maila.
4. Treść wiadomości i harmonogram przypomnień.

Po uruchomieniu:

- tworzone są pozycje dla bieżących przypisań;
- każdy uczestnik otrzymuje osobny token;
- zakres kampanii jest zablokowany;
- można przedłużyć termin, ale nie usuwać już zebranych odpowiedzi;
- do aktywnej kampanii można opcjonalnie dodać nowego uczestnika ręcznie.

### 5.5. Widok pracownika

Publiczna strona `/asset-check/{token}` oraz karta w `MyWorkspace` pokazują:

- nazwę kampanii i termin;
- postęp odpowiedzi;
- każdą pozycję z nazwą, tagiem, modelem i bezpiecznym podglądem zdjęcia;
- cztery odpowiedzi;
- pole komentarza;
- zdjęcie wymagane konfiguracyjnie dla `Uszkodzone`;
- ekran podsumowania przed ostatecznym wysłaniem.

Do momentu `Wyślij odpowiedzi` pracownik może poprawiać wybory. Po wysłaniu uczestnik ma status `Submitted`. Ponowne otwarcie odpowiedzi jest możliwe wyłącznie przez administratora i zapisuje wpis audytowy.

Przed wysłaniem formularza strona pokazuje informację o prywatności właściwą dla organizacji. Akcja nazywa się `Potwierdź stan aktywów`, nie `Wyrażam zgodę`. Kampania nie może zbierać lokalizacji, czasu aktywności urządzenia ani wykorzystywać braku odpowiedzi do automatycznego oceniania pracownika.

### 5.6. Widok administratora

Nowa strona `/asset-audits` zawiera:

- listę kampanii;
- postęp odpowiedzi;
- liczbę potwierdzonych, brakujących, uszkodzonych i błędnie przypisanych aktywów;
- liczbę osób bez odpowiedzi;
- filtr po zespole, osobie, odpowiedzi i rozstrzygnięciu;
- masową akcję `Wyślij przypomnienie`;
- eksport CSV/PDF.

Widok szczegółów ma dwie zakładki:

- `Uczestnicy` — stan odpowiedzi per osoba;
- `Wyjątki` — wszystkie odpowiedzi inne niż `Confirmed` oraz działania naprawcze.

### 5.7. Rozstrzyganie wyjątków

- `Missing`: administrator może oznaczyć aktywo jako `Lost`, poprawić właściciela albo odrzucić zgłoszenie.
- `Damaged`: administrator może oznaczyć aktywo jako `Damaged`, utworzyć notatkę serwisową albo odrzucić zgłoszenie.
- `WrongOwner`: administrator wybiera poprawnego właściciela lub zwraca aktywo do magazynu.
- każde rozstrzygnięcie wymaga notatki, jeśli zmienia właściciela lub status.

Kampania może przejść do `Completed`, gdy wszyscy uczestnicy odpowiedzieli albo administrator jawnie zakończył ją z nieudzielonymi odpowiedziami. Raport końcowy pokazuje liczbę takich braków.

### 5.8. API

```text
GET    /api/asset-audits
POST   /api/asset-audits
GET    /api/asset-audits/{id}
PUT    /api/asset-audits/{id}
POST   /api/asset-audits/{id}/preview
POST   /api/asset-audits/{id}/start
POST   /api/asset-audits/{id}/remind
POST   /api/asset-audits/{id}/participants/{participantId}/reopen
POST   /api/asset-audits/{id}/items/{itemId}/resolve
POST   /api/asset-audits/{id}/complete
POST   /api/asset-audits/{id}/cancel
GET    /api/asset-audits/{id}/export.csv
GET    /api/asset-audits/{id}/report.pdf

GET    /api/public/asset-audits/{token}
PUT    /api/public/asset-audits/{token}/items/{itemId}
POST   /api/public/asset-audits/{token}/submit
POST   /api/public/asset-audits/{token}/items/{itemId}/evidence
```

### 5.9. Uprawnienia

Domyślne role zarządzające: `owner`, `admin`, `asset_operator`, `auditor`.

Nowe klucze:

- `assetAudits.view`;
- `assetAudits.manage`;
- `assetAudits.resolve`.

`auditor` domyślnie widzi i eksportuje wyniki, ale nie zmienia statusu ani właściciela aktywa.

### 5.10. Zdarzenia audytowe

```text
asset_audit.created
asset_audit.started
asset_audit.reminder_sent
asset_audit.participant_submitted
asset_audit.participant_reopened
asset_audit.exception_resolved
asset_audit.completed
asset_audit.cancelled
```

### 5.11. Kryteria akceptacji

- Podgląd kampanii zgadza się z aktywami przypisanymi w chwili uruchomienia.
- Każdy uczestnik otrzymuje inny token.
- Token jednego uczestnika nie pozwala zobaczyć aktywów innej osoby.
- Odpowiedź publiczna nie zmienia bezpośrednio właściciela ani statusu aktywa.
- Po wysłaniu odpowiedzi pracownik nie może ich zmienić bez ponownego otwarcia.
- Administrator widzi postęp oraz wszystkie wyjątki.
- Raport końcowy zachowuje historyczną migawkę nawet po późniejszych zmianach aktywów.

## 6. Zdjęcia przy wydaniu i zwrocie

### 6.1. Cel biznesowy

Zdjęcia mają udokumentować faktyczny stan sprzętu w konkretnym momencie. Nie są zwykłą galerią aktywa. Każde zdjęcie musi być powiązane z wydaniem, zwrotem, audytem albo offboardingiem i zachowywać kontekst wykonania.

### 6.2. Zakres MVP

- zdjęcia stanu przy wydaniu;
- zdjęcia stanu przy zwrocie;
- zdjęcia szkody w offboardingu i kampanii audytowej;
- podgląd miniatur;
- pobieranie oryginału przez uprawnionych użytkowników;
- pokazanie zdjęć wydania pracownikowi przed potwierdzeniem;
- dołączenie miniaturek i hashy do protokołu PDF;
- blokada usuwania po zaakceptowaniu wydania lub zamknięciu zwrotu;
- limity typu, rozmiaru i liczby plików.

### 6.3. Limity i walidacja

- dozwolone typy: JPEG, PNG, WebP;
- maksymalny rozmiar jednego pliku: 5 MB;
- maksymalnie 5 zdjęć na aktywo i fazę procesu;
- nazwa pliku nie jest używana jako ścieżka;
- backend weryfikuje MIME oraz sygnaturę pliku;
- backend dekoduje i ponownie zapisuje obraz do wspieranego formatu, usuwając EXIF, GPS i pozostałe metadane urządzenia;
- pliki SVG, PDF i formaty wykonywalne są odrzucane;
- publiczny upload wymaga ważnego tokenu oraz podlega rate limitingowi;
- aplikacja mobilna używa `accept="image/*"` i umożliwia wykonanie zdjęcia aparatem;
- frontend może zmniejszyć obraz do maksymalnego wymiaru 1600 px przed wysłaniem, ale backend nadal egzekwuje limit.

### 6.4. Wydanie sprzętu ze zdjęciami

W obecnym procesie wydanie jest tworzone od razu i może wysłać link do akceptacji. Aby zachować mały zakres zmian, proponowane jest:

- formularz wydania pozwala wybrać zdjęcia per aktywo;
- przy braku zdjęć używany jest obecny endpoint JSON;
- przy zdjęciach frontend wysyła multipart do nowego endpointu;
- backend w jednej operacji tworzy wydanie, zapisuje zdjęcia, a dopiero potem wysyła wiadomość z linkiem;
- błąd zapisu dowolnego zdjęcia wycofuje całą operację.

Endpoint:

```text
POST /api/assignments/with-evidence
Content-Type: multipart/form-data

request: JSON CreateAssignmentRequest
evidenceManifest: JSON mapujący nazwy części na AssetId i caption
files: zdjęcia
```

Taki sam wariant powinien otrzymać onboarding pracownika, aby zdjęcia nie omijały istniejącego kreatora pakietu.

### 6.5. Zwrot ze zdjęciami

Zdjęcia zwrotu są wysyłane razem z rozliczeniem pojedynczego `AssignmentAsset`:

```text
POST /api/assignments/{assignmentId}/assets/{assetId}/return-with-evidence
Content-Type: multipart/form-data
```

Operacja zapisuje zdjęcia i zwrot w jednej transakcji. Jeżeli zapis pliku się nie powiedzie, aktywo nie jest oznaczane jako zwrócone.

### 6.6. Integralność historycznych protokołów

Obecny hash wydania nie obejmuje zdjęć. Po wdrożeniu materiału dowodowego należy dodać:

- `IntegrityVersion` na `Assignment`;
- wersję 1 dla istniejących rekordów;
- wersję 2 obejmującą uporządkowaną listę `AssetEvidence.Id`, fazę i `Sha256`;
- osobny hash zamknięcia zwrotu albo sprawy offboardingowej.

Nie wolno przeliczać istniejących hashy wersji 1 nowym algorytmem. Weryfikacja wybiera algorytm na podstawie `IntegrityVersion`.

### 6.7. Ekrany

- `AssignmentsPage`: sekcja zdjęć w formularzu wydania i w szczegółach protokołu;
- `OnboardingPage`: przycisk aparatu przy każdym wybranym aktywie;
- modal zwrotu: zdjęcia per aktywo obok pola stanu;
- `PublicAssignmentPage`: zdjęcia wydania przed potwierdzeniem;
- `AssetsPage`: historia materiału dowodowego tylko do odczytu, pogrupowana według procesu;
- ExitProof i kampanie audytowe używają tego samego komponentu uploadu.

### 6.8. API dodatkowe

```text
GET    /api/assets/{assetId}/evidence
GET    /api/evidence/{id}
DELETE /api/evidence/{id}
GET    /api/public/assignments/{organizationId}/{assignmentId}/evidence/{id}
```

Usunięcie jest dostępne tylko przed `LockedAt`. Publiczny endpoint zwraca wyłącznie zdjęcia należące do konkretnego publicznego procesu.

### 6.9. Uprawnienia i prywatność

Domyślnie zdjęcia widzą role mające dostęp do aktywów i wydań. Usuwanie niezamkniętych zdjęć jest ograniczone do `owner`, `admin`, `asset_operator` i autora zdjęcia.

Nowy klucz:

- `evidence.view`;
- `evidence.manage`.

Przy polu uploadu system pokazuje instrukcję: `Fotografuj wyłącznie sprzęt. Nie fotografuj osób, dokumentów, identyfikatorów ani treści widocznej na ekranie`. Zdjęcia nie mogą służyć do rozpoznawania twarzy ani kontroli miejsca przebywania pracownika.

Widok publiczny nie ujawnia zdjęć innych aktywów, pełnych danych pracowników ani komentarzy wewnętrznych. Dostęp do oryginału wymaga `evidence.view`; role o dostępie wyłącznie do podsumowania widzą miniaturę albo sam wpis o istnieniu dowodu.

Ustawienia organizacji obejmują:

```text
EvidencePrivacySettings
- CapturePublicIp: Off | Truncated | Full       // domyślnie Off
- PublicIpRetentionDays?                       // wymagane dla Truncated/Full
- RemoveImageMetadata: bool                    // zawsze true w MVP
- DefaultEvidenceRetentionMonths?
- PrivacyNoticeUrl?
- PrivacyContactEmail?
```

Adres IP nie jest składnikiem koniecznym potwierdzenia. Podstawowy zapis dowodowy składa się z tokenu przypisanego do procesu, czasu, treści odpowiedzi, aktora i hasha. Pełny IP, jeśli klient świadomie go włączy, jest widoczny tylko dla uprawnionego audytora, ma krótki okres retencji i nie trafia do e-maila ani standardowego PDF.

### 6.10. Kryteria akceptacji

- Niedozwolony format i plik ponad limit są odrzucane przed zmianą stanu procesu.
- Zdjęcia wydania są widoczne na stronie akceptacji.
- Zdjęcia zwrotu są przypisane do konkretnego aktywa i zwrotu.
- Po elektronicznym potwierdzeniu wydania nie można zwykłą akcją usunąć zdjęcia wydania; nadal działa kontrolowany proces retencji.
- Po zapisie zdjęcie nie zawiera EXIF ani lokalizacji GPS.
- Protokół zawiera miniatury oraz skróty plików.
- Weryfikacja integralności starych wydań nadal działa.

## 7. Konfigurowalne alerty i digest

### 7.1. Cel biznesowy

Administrator sam określa, o czym, kiedy i kogo Tenebit informuje. System może wysyłać alert od razu albo grupować zdarzenia w jeden czytelny digest.

### 7.2. Typy alertów

MVP obsługuje:

1. Koniec gwarancji aktywa.
2. Koniec ważności licencji.
3. Termin przeglądu procedury.
4. Termin zwrotu wydania.
5. Brak potwierdzenia wydania/onboardingu.
6. Termin zwrotu w offboardingu.
7. Brak odpowiedzi w kampanii aktywów.
8. Rezerwacja oczekująca na akceptację.
9. Zbliżający się odbiór rezerwacji.
10. Przekroczony termin zwrotu rezerwacji.

### 7.3. Model danych

#### AlertRule

```text
- Id
- OrganizationId
- Type
- IsEnabled
- ThresholdDays: string, np. "90,30,7"
- DeliveryMode: Immediate | Digest | Both
- RecipientMode: OwnersAndAdmins | ResponsibleRoles | ResponsiblePerson | Custom
- CustomEmails?
- CooldownDays
- CreatedAt
- UpdatedAt
- UpdatedBy
```

W bazie istnieje unikalny rekord `OrganizationId + Type`.

#### AlertDigestSettings

```text
- OrganizationId
- Frequency: Off | Daily | Weekly
- DayOfWeek?
- LocalTime
- QuietHoursStart?
- QuietHoursEnd?
- BusinessDays: flags
- HolidayCalendarCountryCode?
- IncludeEmptyDigest: bool
- LastGeneratedAt?
```

#### Rozszerzenie rejestru wysyłki

Obecny `SentAlert` zapisuje jedynie fakt próby wysyłki. Docelowo rekord dostawy powinien dodatkowo przechowywać:

- odbiorcę;
- `Status: Pending | Sent | Failed | IncludedInDigest`;
- liczbę prób;
- `NextAttemptAt`;
- `LastError` ograniczony długością;
- `SentAt`;
- `DigestId?`.

Alert nie może zostać uznany za wysłany, jeśli SMTP zwrócił błąd. Nieudana dostawa jest ponawiana z ograniczeniem liczby prób.

### 7.4. Ustawienia administratora

Do `SettingsPage` dochodzi zakładka `Alerty` dostępna dla `owner` i `admin`.

Każdy typ alertu ma:

- przełącznik włącz/wyłącz;
- progi dni, np. 30 i 7 dni wcześniej;
- tryb natychmiastowy/digest;
- odbiorców;
- podgląd przykładowej wiadomości;
- przycisk `Wyślij wiadomość testową`.

Ustawienia digestu zawierają częstotliwość, dzień tygodnia i godzinę lokalną.

Walidacja:

- maksymalnie 5 progów dla reguły;
- próg 0 oznacza dzień terminu;
- zakres od 0 do 365 dni;
- adresy niestandardowe muszą być poprawnymi e-mailami;
- digest tygodniowy wymaga dnia tygodnia.
- godziny ciszy są liczone w `Organization.TimeZone` i nie dotyczą krytycznego błędu bezpieczeństwa;
- przypomnienia do byłego pracownika są osobnym przełącznikiem i domyślnie kończą się po wygaśnięciu tokenu; dalsze eskalacje trafiają do właściciela procesu.

### 7.5. Mechanizm wykrywania i wysyłki

Obecny `AlertBackgroundService` pozostaje jednym zadaniem w tle.

Cykl:

1. Pobiera aktywne reguły organizacji.
2. Wykrywa zdarzenia pasujące do progów.
3. Tworzy brakujące rekordy dostawy z unikalnym kluczem logicznym.
4. Wysyła alerty natychmiastowe.
5. Grupuje zdarzenia przeznaczone do digestu, gdy nadejdzie lokalny termin.
6. Ponawia nieudane dostawy.
7. Zapisuje wynik i wpis audytowy dla zmian ustawień, nie dla każdego technicznego retry.

Przykład klucza deduplikacji:

```text
organizationId | alertType | entityId | threshold | dueDate | recipient
```

Zmiana terminu encji tworzy nowy klucz, ale nie powoduje ponownego wysłania starego alertu.

### 7.6. Digest

Wiadomość digest zawiera sekcje:

- pilne i po terminie;
- nadchodzące zwroty;
- gwarancje i licencje;
- procedury do przeglądu;
- kampanie i offboardingi;
- rezerwacje oczekujące na działanie.

Każda pozycja ma krótki opis, termin i bezpośredni link do właściwego ekranu. Digest nie zawiera kluczy licencyjnych ani publicznych tokenów.

Temat wiadomości jest ogólny, np. `Tenebit — 4 działania wymagają uwagi`. Nazwisko pracownika, numer seryjny, szkoda i informacja o zakończeniu zatrudnienia mogą znaleźć się dopiero w treści widocznej właściwym odbiorcom. Odbiorcy są wyliczani według minimalnych uprawnień; menedżer nie otrzymuje pełnego materiału zdjęciowego, jeżeli potrzebuje tylko informacji o terminie.

### 7.7. API

```text
GET  /api/settings/alerts
PUT  /api/settings/alerts/{type}
GET  /api/settings/alert-digest
PUT  /api/settings/alert-digest
POST /api/settings/alerts/test
GET  /api/alerts/history
```

Historia jest stronicowana i pokazuje typ, encję, odbiorcę, status, datę oraz ostatni błąd w bezpiecznej formie.

### 7.8. Uprawnienia

Nowy klucz:

- `alerts.manage` — domyślnie `owner`, `admin`;
- `alerts.viewHistory` — domyślnie `owner`, `admin`, `auditor`.

### 7.9. Kryteria akceptacji

- Wyłączona reguła nie generuje nowych dostaw.
- Ten sam alert nie jest wysyłany dwukrotnie do tego samego odbiorcy.
- Błąd SMTP pozostawia dostawę w stanie `Failed` i umożliwia retry.
- Digest jest wysyłany według strefy czasowej organizacji.
- Alert o licencji nie ujawnia klucza licencyjnego.
- Linki w wiadomości prowadzą do odpowiednich rekordów aplikacji.
- Zmiana reguły jest widoczna w dzienniku audytowym.

## 8. Portal zamawiania i rezerwowania sprzętu

### 8.1. Cel biznesowy

Pracownik sam sprawdza dostępność sprzętu i składa wniosek. Administrator lub przełożony akceptuje go, a system pilnuje konfliktów terminów, wydania i zwrotu.

### 8.2. Zakres MVP

- portal wyłącznie dla zalogowanego pracownika powiązanego z `Person` przez e-mail;
- katalog rezerwowalnych kategorii i gotowych zestawów, np. `Laptop na podróż`, `Stanowisko pracy`, `Projektor`;
- pokazanie liczby dostępnych sztuk bez ujawniania bieżącego użytkownika, pełnego numeru seryjnego ani szczegółowej listy magazynowej;
- wybór wielu kategorii lub zestawów do jednego wniosku;
- termin od/do, cel i miejsce odbioru;
- akceptacja lub odrzucenie;
- przypisanie konkretnego aktywa przez zatwierdzającego albo operatora;
- możliwość zamiany konkretnego aktywa przed wydaniem;
- ponowna kontrola konfliktów przy akceptacji;
- utworzenie wydania z zatwierdzonej rezerwacji;
- śledzenie odbioru i zwrotu;
- anulowanie;
- powiadomienia;
- widok kalendarzowy dla administratora.

Poza MVP:

- rezerwowanie ilości materiałów eksploatacyjnych;
- cykliczne rezerwacje;
- synchronizacja z Outlook/Google Calendar;
- płatności i obciążenia kosztowe;
- rezerwacje pomieszczeń;
- kolejka oczekujących;
- automatyczne sugerowanie zamienników.

### 8.3. Oznaczenie aktywa jako rezerwowalne

Do `Asset` należy dodać:

- `IsReservable: bool`;
- opcjonalne `ReservationInstructions: string?`;
- opcjonalne `MaxReservationDays: int?`.

Domyślnie istniejące aktywa nie są rezerwowalne. Administrator włącza opcję w edycji aktywa albo masowo dla kategorii.

Kategoria otrzymuje ustawienia `VisibleInEmployeeCatalog`, nazwę katalogową, opis i opcjonalną ilustrację. Zestaw jest prostą listą kategorii i wymaganych ilości; nie przechowuje z góry konkretnych `AssetId`.

Domyślny tryb `RequestByCategory` jest prostszy i bezpieczniejszy niż wybór laptopa po tagu. Administrator może włączyć `SelectExactAsset` tylko dla jawnie współdzielonych urządzeń, np. projektorów lub pojazdów. Katalog nie pokazuje ceny zakupu, poufnych pól, pełnego numeru seryjnego ani bieżącego właściciela.

### 8.4. Model danych

#### EquipmentReservation

```text
- Id
- OrganizationId
- RequesterPersonId
- Status: Draft | PendingApproval | Approved | Rejected | Cancelled | ReadyForPickup | CheckedOut | Completed | Expired
- StartAt
- EndAt
- Purpose
- PickupLocation?
- Notes?
- RequestedAt?
- ApprovedAt?
- ApprovedBy?
- RejectedAt?
- RejectedBy?
- DecisionNotes?
- CancelledAt?
- CancelledBy?
- CancellationReason?
- AssignmentId?
- CreatedAt
- UpdatedAt
- RowVersion
```

#### EquipmentReservationItem

```text
- Id
- OrganizationId
- ReservationId
- RequestedCategoryId
- RequestedQuantity
- KitDefinitionId?
- AssetId?                 // wybierany przy zatwierdzeniu lub przygotowaniu
- OriginalAssetId?
- SubstitutionReason?
- Status: Requested | Allocated | Approved | Rejected | Substituted | CheckedOut | Returned
```

`OriginalAssetId` zachowuje informację o zamianie dokonanej przy zatwierdzeniu.

### 8.5. Reguły dostępności

Katalog pokazuje kategorię jako dostępną, gdy liczba aktywów spełniających poniższe reguły jest co najmniej równa żądanej ilości. Jest to informacja orientacyjna do chwili zatwierdzenia. Konkretne aktywo jest dostępne w przedziale, gdy:

- `IsReservable = true`;
- nie ma statusu `Damaged`, `Lost`, `Retired`, `Disposed` ani `InService`;
- nie jest przypisane długoterminowo do innej osoby w sposób kolidujący z terminem;
- nie występuje w zatwierdzonej rezerwacji o nachodzącym przedziale;
- nie jest w aktywnym offboardingu jako oczekujące na zwrot;
- nie ma otwartego wydania obejmującego żądany termin.

Aktywo `PendingReturn` nie zwiększa liczby dostępnych sztuk. Interfejs administratora może pokazać `oczekiwany zwrot: 18 sierpnia`, ale nie wolno zatwierdzić go dla następnej osoby do czasu rzeczywistego odbioru i, jeśli wymagana, kontroli. Po przejściu do `InStock` jest automatycznie uwzględniane w dostępności bez dodatkowego kliknięcia `aktywuj`.

Rezerwacja `PendingApproval` nie blokuje sprzętu. Przy zatwierdzeniu system ponownie sprawdza wszystkie pozycje.

Zapobieganie podwójnej rezerwacji:

- zatwierdzenie odbywa się w transakcji;
- rekord rezerwacji ma token współbieżności `RowVersion`;
- backend ponownie wykonuje zapytanie o nakładające się zatwierdzone rezerwacje;
- konflikt zwraca HTTP 409 wraz z listą niedostępnych pozycji;
- frontend proponuje powrót do edycji lub zamianę aktywa;
- `Asset.Status` nie jest jedynym mechanizmem blokady przyszłego terminu.

### 8.6. Przebieg pracownika

W `MyWorkspace` dochodzi zakładka `Zamów sprzęt`:

1. Pracownik wybiera daty.
2. System pokazuje dostępne kategorie i zestawy wraz z orientacyjną liczbą sztuk.
3. Pracownik dodaje kategorię lub zestaw; konkretny numer seryjny nie jest mu potrzebny.
4. Podaje cel, miejsce odbioru i notatkę.
5. Widzi podsumowanie i wysyła wniosek.
6. Po wysłaniu obserwuje status, decyzję i instrukcję odbioru.
7. Może anulować wniosek przed wydaniem.
8. Po zatwierdzeniu i fizycznym odbiorze potwierdza standardowe wydanie w Tenebit.

Jeśli konto nie jest powiązane z `Person`, portal pokazuje informację o konieczności kontaktu z administratorem i nie pozwala utworzyć rezerwacji.

### 8.7. Przebieg zatwierdzającego

Nowa strona `/reservations` pokazuje:

- kolejkę oczekujących;
- kalendarz rezerwacji;
- konflikty;
- rezerwacje do wydania dzisiaj;
- zwroty po terminie.

Zatwierdzający może:

- zatwierdzić cały wniosek;
- odrzucić cały wniosek z powodem;
- przydzielić konkretne aktywa spełniające kategorię i termin;
- zamienić niedostępne aktywo na inne z tej samej albo zaakceptowanej kategorii;
- zmienić miejsce odbioru;
- skrócić termin po podaniu uzasadnienia;
- utworzyć wydanie przy odbiorze;
- anulować zatwierdzoną rezerwację przed wydaniem.

### 8.8. Połączenie z wydaniami

Akcja `Wydaj sprzęt`:

- tworzy `Assignment` dla osoby i zatwierdzonych aktywów;
- kopiuje `EndAt` do terminu zwrotu;
- ustawia `Reservation.AssignmentId`;
- zmienia status na `CheckedOut`;
- pozwala dodać zdjęcia wydania;
- używa istniejącego publicznego potwierdzenia odbioru.

Jeżeli wniosek został zatwierdzony na poziomie kategorii, wszystkie konkretne aktywa muszą zostać przydzielone przed akcją `Wydaj sprzęt`. Backend ponownie weryfikuje ich `InStock`, `IsReservable` oraz brak konfliktu.

Gdy wszystkie pozycje powiązanego wydania zostaną rozliczone, rezerwacja przechodzi do `Completed`.

### 8.9. API

```text
GET    /api/reservation-catalog?from=&to=&search=&location=
GET    /api/reservable-assets?from=&to=&categoryId=&location=  // widok operatora do alokacji
GET    /api/my/reservations
POST   /api/my/reservations
GET    /api/my/reservations/{id}
PUT    /api/my/reservations/{id}
POST   /api/my/reservations/{id}/submit
POST   /api/my/reservations/{id}/cancel

GET    /api/reservations
GET    /api/reservations/{id}
POST   /api/reservations/{id}/approve
POST   /api/reservations/{id}/reject
POST   /api/reservations/{id}/substitute
POST   /api/reservations/{id}/checkout
POST   /api/reservations/{id}/cancel
GET    /api/reservations/calendar?from=&to=
```

### 8.10. Uprawnienia

- każdy zalogowany użytkownik powiązany z osobą może tworzyć własne rezerwacje;
- `manager` może zatwierdzać wnioski swoich bezpośrednich podwładnych, jeśli organizacja na to zezwala;
- `owner`, `admin` i `asset_operator` mogą zatwierdzać wszystkie wnioski;
- `technician` może wydać i przyjąć sprzęt, ale domyślnie nie zatwierdza wniosku.

Nowe klucze:

- `reservations.request`;
- `reservations.approve`;
- `reservations.checkout`;
- `reservations.viewAll`.

### 8.11. Zdarzenia audytowe

```text
reservation.created
reservation.submitted
reservation.approved
reservation.rejected
reservation.asset_substituted
reservation.cancelled
reservation.checked_out
reservation.completed
reservation.expired
```

### 8.12. Kryteria akceptacji

- Pracownik domyślnie widzi kategorie i zestawy, nie listę numerów seryjnych ani dane aktualnych posiadaczy.
- Dwie nachodzące zatwierdzone rezerwacje tego samego aktywa nie mogą powstać.
- Konflikt wykryty przy zatwierdzeniu zwraca 409 i nie zatwierdza części wniosku.
- Zatwierdzenie nie tworzy jeszcze wydania.
- Wydanie utworzone z rezerwacji zachowuje termin i listę zatwierdzonych aktywów.
- Zwrot wszystkich pozycji kończy rezerwację.
- Rozpoczęcie offboardingu automatycznie blokuje nowe wnioski, odrzuca oczekujące i anuluje przyszłe zatwierdzone rezerwacje zgodnie z konfiguracją sprawy.
- Aktywo oczekujące na zwrot od byłego pracownika nie jest dostępne; po potwierdzonym zwrocie i wymaganej kontroli pojawia się automatycznie w katalogu.

## 9. Integracja pięciu modułów

### 9.1. Przepływ pełnego cyklu

```text
Rezerwacja
  -> zatwierdzenie
  -> wydanie + zdjęcia stanu
  -> opcjonalne potwierdzenie przez pracownika
  -> okresowa kampania potwierdzenia aktywów
  -> alerty o terminach i wyjątkach
  -> ExitProof przy odejściu: automatyczna dezaktywacja osoby
  -> fizyczny zwrot lub udokumentowany wyjątek
  -> kontrola techniczna, jeśli wymagana
  -> InStock i automatyczny powrót do katalogu
  -> końcowy protokół
```

### 9.2. Wspólne reguły

- Offboarding pokazuje aktywne i przyszłe rezerwacje osoby.
- Uruchomienie offboardingu, a nie jego późniejsze zamknięcie, blokuje nowe wnioski i wykonuje skonfigurowane anulowania.
- Aktywna rezerwacja nie może użyć aktywa oczekującego na zwrot w offboardingu.
- Dezaktywacja osoby nie zmienia `PendingReturn` na `InStock`.
- Potwierdzony zwrot i kontrola techniczna są jedyną standardową drogą z `PendingReturn` do `InStock`.
- Kampania audytowa nie zmienia automatycznie danych aktywa; tworzy wyjątek do rozstrzygnięcia.
- Zdjęcia są przechowywane w jednym modelu `AssetEvidence`.
- Alerty obsługują terminy wszystkich pozostałych modułów.
- Każdy moduł korzysta z `ActivityLog`, nie tworzy własnej tabeli historii użytkownika.
- Wszystkie procesy operują na tej samej encji `Asset` i `Person`.

## 10. Umiejscowienie w projekcie

### 10.1. Backend Domain

Nowe katalogi i główne typy:

```text
Tenebit.Domain/Offboarding/
  OffboardingCase.cs
  OffboardingItem.cs
  OffboardingStatus.cs

Tenebit.Domain/AssetAudits/
  AssetAuditCampaign.cs
  AssetAuditParticipant.cs
  AssetAuditItem.cs

Tenebit.Domain/Evidence/
  AssetEvidence.cs
  EvidencePhase.cs

Tenebit.Domain/Alerts/
  AlertRule.cs
  AlertDigestSettings.cs

Tenebit.Domain/Reservations/
  EquipmentReservation.cs
  EquipmentReservationItem.cs
  EquipmentReservationStatus.cs
```

Zmiany w istniejących typach:

- `AssignmentAsset` — zwrot częściowy;
- `AssignmentStatus` — `PartiallyReturned`;
- `Assignment` — zwrot pojedynczej pozycji i wersjonowanie integralności;
- `Person` — `EmploymentStatus`, termin zakończenia i czas dezaktywacji;
- `Asset` — `PendingReturn` i pola rezerwowalności;
- `AssetCategory` — polityka zwrotu, kontroli i zdjęć;
- `Organization` lub ustawienia organizacji — prywatność materiału dowodowego i domyślna retencja;
- `SentAlert` — status dostawy i retry albo zastąpienie odpowiednikiem migracyjnym.

### 10.2. Backend Application

```text
Tenebit.Application/Offboarding/
  OffboardingService.cs
  OffboardingDtos.cs

Tenebit.Application/AssetAudits/
  AssetAuditService.cs
  AssetAuditDtos.cs

Tenebit.Application/Evidence/
  AssetEvidenceService.cs
  AssetEvidenceDtos.cs

Tenebit.Application/Alerts/
  AlertSettingsService.cs
  AlertDtos.cs

Tenebit.Application/Reservations/
  ReservationService.cs
  ReservationDtos.cs
```

Należy rozszerzyć:

- `IRepositories.cs` o pięć repozytoriów;
- `IPdfProtocolGenerator` o raport offboardingu i audytu;
- `DependencyInjection.cs` o nowe serwisy;
- `MyWorkspaceService` o offboarding, kampanie i rezerwacje;
- `AlertCheckService` o reguły i nowe typy terminów;
- istniejące zadanie w tle o idempotentne wykonywanie zaplanowanych działań offboardingowych;
- `AssignmentService` o zwroty częściowe i materiał dowodowy;
- `LicenseService` jedynie przez istniejące zwalnianie miejsca, bez duplikowania logiki.

### 10.3. Backend Infrastructure

Potrzebne elementy:

- `DbSet` i konfiguracja encji w `TenebitDbContext`;
- repozytorium dla każdego nowego agregatu;
- indeksy po `OrganizationId`, statusach, terminach i tokenach;
- unikalny indeks otwartej sprawy offboardingowej per osoba, jeśli baza pozwala na indeks filtrowany;
- indeks rezerwacji po `OrganizationId`, `AssetId`, `StartAt`, `EndAt` poprzez pozycje;
- nowe metody QuestPDF;
- rozbudowa obecnego `AlertBackgroundService`, bez tworzenia drugiego timera wykonującego ten sam rodzaj pracy.
- job retencji usuwający treść zdjęć i anonimizujący dane po terminie, z obsługą blokady prawnej `LegalHold`.

Zdjęcia w MVP są zapisane w bazie. Należy monitorować łączny rozmiar i czas backupu.

### 10.4. Backend API

Endpointy mogą zostać zarejestrowane przez kolejne prywatne metody w istniejącym `TenebitEndpoints`, zgodnie z aktualnym stylem. Nie ma potrzeby wykonywania niezależnego refaktoru wszystkich istniejących endpointów.

Publiczne endpointy wymagają:

- `AllowAnonymous()`;
- `RequireRateLimiting("public")`;
- walidacji tokenu i organizacji w serwisie;
- braku ujawniania, czy token należał kiedyś do innej organizacji;
- ogólnego komunikatu dla tokenu nieważnego, wygasłego i unieważnionego.

Wspólne endpointy prywatności:

```text
GET  /api/settings/privacy
PUT  /api/settings/privacy
GET  /api/people/{personId}/privacy-export
POST /api/people/{personId}/anonymize-expired-data
```

Eksport zawiera dane osoby, przypisania, odpowiedzi, sprawy i zdarzenia objęte zakresem eksportu klienta. Anonimizacja nie usuwa rekordów z aktywną blokadą prawną i nie może naruszać spójności ewidencji organizacji.

### 10.5. Frontend

Nowe strony:

```text
src/pages/OffboardingPage.tsx
src/pages/PublicOffboardingPage.tsx
src/pages/AssetAuditsPage.tsx
src/pages/PublicAssetAuditPage.tsx
src/pages/ReservationsPage.tsx
```

Nowe współdzielone komponenty są uzasadnione tylko tam, gdzie użyją ich co najmniej dwa moduły:

```text
src/components/AssetEvidenceUploader.tsx
src/components/AssetEvidenceGallery.tsx
src/components/ProgressSummary.tsx
```

Zmiany istniejących plików:

- `App.tsx` — nowe routes;
- `Layout.tsx` — nawigacja administracyjna;
- `MyWorkspacePage.tsx` — rezerwacje, kampanie i offboarding;
- `PeoplePage.tsx` — rozpoczęcie offboardingu;
- `AssignmentsPage.tsx` i `OnboardingPage.tsx` — zdjęcia i częściowe zwroty;
- `SettingsPage.tsx` — zakładki alertów oraz prywatności, retencji i informacji dla pracownika;
- `AssetsPage.tsx` — rezerwowalność i historia zdjęć;
- `types/domain.ts` — kontrakty frontendowe;
- `api/endpoints.ts` — wywołania API;
- `i18n/translations.ts` — komplet nowych kluczy;
- istniejące arkusze CSS — style nowych stron bez dokładania biblioteki UI.

### 10.6. Nawigacja

Proponowana nawigacja administracyjna:

```text
Dashboard
Moje
Aktywa
Licencje
Ludzie
Wydania
Rezerwacje
Onboarding
Offboarding
Procedury
Audyty aktywów
Raporty
Dziennik audytu
Ustawienia
```

Przy węższym sidebarze `Onboarding` i `Offboarding` mogą docelowo znaleźć się pod wspólną pozycją `Cykl pracownika`, ale nie jest to wymagane w MVP.

## 11. Kolejność implementacji

### Etap 1 — fundament

1. Cykl `Person`: `Active`, `Offboarding`, `Inactive`.
2. `PendingReturn`, polityka kontroli kategorii oraz zwroty częściowe.
3. Idempotentne zadanie zaplanowanej dezaktywacji, zwalniania licencji i anulowania rezerwacji.
4. Wspólny mechanizm tokenów publicznych.
5. `AssetEvidence`, bezpieczny upload, usuwanie metadanych i ustawienia prywatności.
6. Retencja, eksport danych osoby i kontrolowana anonimizacja.
7. Rozszerzenie mechanizmu dostawy alertów o status, retry i godziny ciszy.

Weryfikacja etapu:

- stare wydania nadal działają;
- pełny i częściowy zwrot działają;
- osoba jest dezaktywowana w terminie mimo braku odpowiedzi;
- sprzęt nie jest dostępny przed odbiorem, a po wymaganej kontroli automatycznie wraca do `InStock`;
- stare hashe zachowują poprawną weryfikację;
- zdjęcia można zapisać i zablokować;
- nieudana wiadomość nie jest oznaczona jako wysłana.

### Etap 2 — pierwszy produkt sprzedażowy

1. ExitProof niezależny od reakcji pracownika.
2. Automatyczny powrót odebranego i sprawnego sprzętu do katalogu.
3. Zdjęcia przy wydaniu i zwrocie.
4. Reguły alertów dla gwarancji, licencji i offboardingu.
5. Protokół końcowy.

Po tym etapie powstaje spójny pakiet, który można pokazać klientowi jako zarządzanie odpowiedzialnością za sprzęt od wydania do odejścia pracownika.

### Etap 3 — kampanie aktywów

1. Kreator kampanii.
2. Publiczne odpowiedzi.
3. Rozstrzyganie wyjątków.
4. Raport i digest.

### Etap 4 — rezerwacje

1. Oznaczanie aktywów jako rezerwowalne.
2. Katalog kategorii i zestawów z zagregowaną dostępnością.
3. Wniosek i zatwierdzanie.
4. Kalendarz oraz kontrola konfliktów.
5. Konwersja do wydania.

## 12. Testowanie

Projekt posiada testy usług i domeny z repozytoriami in-memory. Nowe testy powinny używać tego samego podejścia.

### 12.1. Testy domenowe

- częściowy zwrot nie zamyka całego wydania;
- ostatni zwrot ustawia `Returned`;
- zamkniętego zwrotu nie można zmienić;
- offboarding nie zamyka się z otwartą pozycją wymaganą;
- dezaktywacja osoby nie ustawia aktywa na `InStock`;
- `DirectToStock` kończy zwrot w `InStock`, a `InspectionRequired` w `InService`;
- tylko zakończona kontrola może przenieść sprawny sprzęt z `InService` do `InStock`;
- kampania blokuje odpowiedzi po zakończeniu;
- materiał dowodowy blokuje usunięcie po `LockedAt`;
- rezerwacja odrzuca niepoprawny przedział dat;
- anulowane i odrzucone rezerwacje nie blokują dostępności.

### 12.2. Testy usług

- każda operacja filtruje po `OrganizationId`;
- token publiczny daje dostęp tylko do właściwego uczestnika;
- start offboardingu tworzy poprawną migawkę aktywów i licencji;
- start offboardingu działa dla osoby bez adresu e-mail;
- zaplanowana dezaktywacja działa bez odpowiedzi pracownika i jest idempotentna;
- zaległa data zakończenia powoduje natychmiastowe wykonanie działań cyfrowych;
- przyszłe rezerwacje osoby są anulowane, a oczekujące wnioski odrzucone;
- zwolnienie licencji jest idempotentne;
- zwrot aktywa automatycznie aktualizuje dostępność katalogu dopiero po spełnieniu polityki kontroli;
- odpowiedź audytowa nie zmienia aktywa bez rozstrzygnięcia;
- konflikt rezerwacji zwraca błąd typu Conflict;
- nieudana wysyłka alertu jest ponawiana;
- digest nie zawiera tajnych pól.
- upload usuwa EXIF i GPS;
- job retencji pomija rekordy z `LegalHold` i usuwa tylko dane właściwej organizacji.

### 12.3. Testy frontendowe

Testy Vitest są wymagane dla logiki, która może być sprawdzona bez renderowania całej aplikacji:

- mapowanie statusów na dostępne akcje;
- walidacja przedziału rezerwacji;
- podsumowanie postępu kampanii i offboardingu;
- walidacja limitów zdjęć;
- budowanie parametrów filtrów API.

Nie należy dodawać nowego frameworka E2E wyłącznie dla tych funkcji.

### 12.4. Scenariusze manualne

Minimum przed wydaniem:

1. Onboarding z dwoma aktywami i zdjęciami, akceptacja publiczna, częściowy zwrot.
2. Offboarding osoby bez żadnej odpowiedzi: automatyczna dezaktywacja, zwolnienie licencji i anulowanie przyszłej rezerwacji.
3. Laptop po dacie odejścia nadal jest `PendingReturn`; po odbiorze trafia do `InService`, po kontroli do `InStock` i pojawia się w katalogu.
4. Kampania dla dwóch osób, jedna odpowiedź poprawna i jeden brak.
5. Błąd SMTP oraz udana ponowna wysyłka.
6. Dwie równoczesne próby zatwierdzenia tego samego aktywa.
7. Publiczny upload nieprawidłowego pliku, przekroczenie limitu i zdjęcie zawierające EXIF/GPS.
8. Próba odczytu danych innej organizacji dla każdego nowego modułu.
9. Upływ retencji dla zdjęcia zwykłego oraz zdjęcia objętego `LegalHold`.

## 13. Wymagania niefunkcjonalne

### Bezpieczeństwo

- Tokeny publiczne są jednorazowo widoczne i przechowywane jako hash.
- Endpointy uploadu sprawdzają faktyczny format pliku.
- Materiał dowodowy jest zawsze filtrowany przez organizację i kontekst procesu.
- Pobranie oryginału zdjęcia oraz eksport danych są rejestrowane w `ActivityLog`.
- Żaden e-mail nie zawiera klucza licencyjnego.
- Wyjątki publiczne nie zwracają stack trace ani identyfikatorów wewnętrznych poza wymaganymi.

### Wydajność

- Listy offboardingów, kampanii i rezerwacji są stronicowane.
- Dashboardy używają agregacji po stronie bazy.
- Widoki list pobierają miniatury, nie pełne zdjęcia.
- Raporty i digest unikają zapytania osobno dla każdego rekordu.

### Dostępność i urządzenia mobilne

- docelowy poziom nowych ekranów i publicznych formularzy to [WCAG 2.2 AA](https://www.w3.org/TR/WCAG22/);
- Każda odpowiedź ma etykietę tekstową, nie tylko kolor lub ikonę.
- Publiczne formularze są obsługiwalne klawiaturą.
- Przyciski zdjęć i odpowiedzi mają odpowiednio duży obszar dotykowy.
- Postęp ma tekstową wartość, np. `3 z 5`, obok paska.
- walidacja nie opiera się wyłącznie na kolorze, komunikat błędu jest powiązany z polem, a fokus po błędzie trafia do podsumowania;
- protokoły PDF zachowują poprawną kolejność czytania i znaczenie tabel; jeżeli pełna dostępność wygenerowanego PDF nie jest jeszcze zapewniona, te same dane są dostępne w HTML.

### Retencja

- Dezaktywacja osoby nie usuwa historii tego samego dnia, ponieważ historia może być nadal potrzebna do rozliczenia; uruchamia jednak wyliczenie terminów retencji.
- Organizacja ustawia osobne okresy dla protokołów, zdjęć, odpowiedzi kampanii, publicznego IP i danych operacyjnych. System nie narzuca jednego okresu wszystkim klientom ani krajom.
- Konfiguracja pokazuje cel każdej kategorii danych, datę najbliższego usunięcia i ostrzeżenie przy ustawieniu bezterminowym.
- Zadanie retencji okresowo usuwa treść pliku lub anonimizuje dane osoby. Pozostawia minimalny wpis, że operacja miała miejsce, bez zachowania usuniętych danych w komunikacie audytowym.
- `LegalHold` wstrzymuje usunięcie konkretnej sprawy lub dowodu, wymaga powodu, osoby ustawiającej i terminu przeglądu.
- Backupy mają własny udokumentowany okres rotacji; usunięte dane nie mogą wracać do aktywnej bazy przy zwykłym odtworzeniu.
- Klient przed uruchomieniem ustala okresy zgodnie z prawem właściwego państwa i swoją podstawą prawną. Tenebit dostarcza mechanizm, nie zastępuje tej decyzji.

### Gotowość operacyjna dla UE

- umowa powierzenia danych opisuje role administratora i procesora, zakres przetwarzania, usunięcie po zakończeniu usługi i pomoc w realizacji praw;
- lista podprocesorów oraz region przechowywania danych i backupów są jawne przed zakupem;
- preferowany wariant handlowy przechowuje dane klientów europejskich w UE/EOG; transfer poza EOG wymaga udokumentowanego mechanizmu prawnego;
- administrator może wyeksportować dane organizacji i dane konkretnej osoby oraz zlecić usunięcie organizacji po zakończeniu umowy;
- procedura incydentowa pozwala ustalić organizacje, rekordy i przedział czasu objęty naruszeniem;
- funkcje mogą wspierać obowiązki klienta, ale interfejs i materiały sprzedażowe nie deklarują automatycznej zgodności z RODO, DORA ani prawem pracy każdego państwa.

## 14. Proponowane pakietowanie produktu

Bez zmiany obecnego modelu planów funkcje można przypisać następująco:

### Free

- podstawowe wydania i zwroty bez zdjęć;
- podgląd jednej przykładowej reguły alertu;
- brak aktywnych kampanii i offboardingu.
- podstawowa informacja o prywatności, bezpieczne domyślne ustawienia IP, eksport danych osoby oraz usunięcie danych organizacji.

### Pro

- ExitProof;
- zdjęcia przy wydaniu i zwrocie;
- kampanie potwierdzenia aktywów;
- konfigurowalne alerty i digest;
- rezerwacje;
- raporty biznesowe i protokoły.

### Enterprise

- rozbudowane polityki retencji per rodzaj dowodu, `LegalHold` i akceptacja polityk na poziomie wielu organizacji;
- większe limity materiału dowodowego;
- własne reguły odbiorców;
- przyszłe integracje HR/MDM/SSO.

Limity planów powinny być egzekwowane w `SubscriptionService` lub osobnym prostym serwisie uprawnień planu, a nie tylko ukrywane w interfejsie.

Funkcji koniecznych do bezpiecznego przetwarzania danych — informacji o prywatności, usuwania EXIF/GPS, podstawowej retencji, eksportu danych osoby i usunięcia konta organizacji — nie wolno blokować wyłącznie w planie Enterprise.

## 15. Definicja ukończenia całego zakresu

Zakres można uznać za ukończony, gdy:

- pięć modułów działa w jednym cyklu aktywa i pracownika;
- nie istnieją dwa źródła prawdy o właścicielu albo dostępności aktywa;
- zwroty częściowe nie psują istniejących protokołów;
- publiczne linki są wygasające i unieważnialne;
- zdjęcia mają kontekst, hash i blokadę po zamknięciu;
- alerty obsługują retry i digest;
- podwójna zatwierdzona rezerwacja jest blokowana;
- brak reakcji pracownika nie blokuje dezaktywacji ani administracyjnego rozliczenia;
- aktywo nie wraca do puli przed fizycznym odbiorem i wymaganą kontrolą;
- sprawny zwrócony sprzęt automatycznie pojawia się w katalogu po przejściu do `InStock`;
- zdjęcia są pozbawione metadanych, a retencja i `LegalHold` działają automatycznie;
- każda operacja jest izolowana per organizacja;
- wszystkie nowe statusy i działania są przetłumaczone;
- testy istniejących modułów oraz nowe testy domenowe i usługowe przechodzą;
- migracje działają na istniejącej bazie bez utraty wydań, hashy, alertów i licencji.
