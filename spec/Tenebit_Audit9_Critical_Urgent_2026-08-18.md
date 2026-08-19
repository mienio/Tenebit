# Tenebit audit9 — audyt krytyczny i pilny

**Data audytu:** 2026-08-18  
**Audytowana paczka:** `Tenebit audit9.zip`  
**Zakres raportu:** wyłącznie błędy CRITICAL/P0 i HIGH/P1 oraz wady o realnym wpływie na bezpieczeństwo, dane klienta, rozliczenia lub dostępność.  
**Nie uwzględniam:** kosmetyki, nazewnictwa, drobnego Clean Code, opcjonalnych refaktorów ani starych uwag, których nie potwierdziłem w `audit9`.

---

# 1. Werdykt

## Ocena audit9: **81/100**

## Gotowość produkcyjna przy ~100 firmach: **72/100**

## Werdykt: **NO-GO do czasu zamknięcia P0 i najważniejszych P1**

Nie podtrzymuję oceny **98/100** dla tej konkretnej paczki.

Powód jest prosty: znalazłem jeden problem klasy **CRITICAL/P0**, w którym surowy sekret będący jedynym credentialem anonimowego dostępu trafia do logów, oraz kilka niezależnych problemów **HIGH/P1** w autoryzacji zasobowej, identity, billing/reliability i polityce prywatności.

To nie oznacza, że kod wrócił do jakości pierwszych wersji. W szczególności w tym audycie **nie znalazłem potwierdzonej standardowej ścieżki firma A → dane firmy B**. Warstwa tenantowa została istotnie wzmocniona. Problem polega na tym, że przy systemie dla realnych klientów pojedynczy P0 nadal blokuje bezpieczne wdrożenie.

### Zasada punktacji zastosowana w tym audycie

- **100** — brak znanych problemów P0/P1 w przebadanym zakresie, kompletne negatywne testy bezpieczeństwa.
- **95–99** — najwyżej drobne, dobrze ograniczone ryzyka bez istotnego wpływu na dane/autoryzację.
- **90–94** — pojedyncze P1, brak P0.
- **80–89** — system ogólnie dojrzały, ale istnieją istotne P1 lub pojedynczy P0 blokujący release.
- **<80** — kilka niezależnych blockerów bezpieczeństwa/reliability lub słaba izolacja tenantów.

Przy potwierdzonym P0 nie uważam oceny 98 za obronną.

---

# 2. Executive summary — tylko problemy wymagające działania

| ID | Severity | Obszar | Problem | Priorytet |
|---|---|---|---|---|
| **AUD9-001** | **CRITICAL / P0** | capability links / secrets / logging | Surowe publiczne bearer tokeny są częścią URL i trafiają do Serilog/nginx; dostarczone logi potwierdzają zapis tokenów | **Natychmiast** |
| **AUD9-002** | **HIGH / P1** | authorization / BOLA | Manager scope działa dla głównego AssetService, ale jest omijany przez evidence, service tickets i inspection | **Przed produkcją** |
| **AUD9-003** | **HIGH / P1** | privacy / compliance | `CapturePublicIp=Off` nie wyłącza zapisu IP; `PublicIpRetentionDays` nie jest egzekwowane | **Przed produkcją** |
| **AUD9-004** | **HIGH / P1** | identity / account pre-hijacking | Niezwerfikowany e-mail dostaje aktywnego Ownera, a późniejszy OAuth może automatycznie podpiąć prawdziwą tożsamość do wcześniej zajętego konta | **Przed produkcją** |
| **AUD9-005** | **HIGH / P1** | password reset | Reset token nie jest konsumowany atomowo; starsze tokeny pozostają ważne po nowszym resecie | **Przed produkcją** |
| **AUD9-006** | **HIGH / P1** | 2FA | Recovery code nie jest konsumowany atomowo; race może złamać gwarancję single-use | **Przed produkcją** |
| **AUD9-007** | **HIGH / P1** | availability / auth | Wszystkie endpointy auth, w tym refresh, współdzielą 10 req/min na publiczny IP; biuro za NAT może samo się zablokować | **Pilne** |
| **AUD9-008** | **HIGH / P1** | reliability / transactions | Linki/e-maile są wysyłane przed commit DB; job lock trzyma transakcję DB podczas network I/O | **Pilne** |
| **AUD9-009** | **HIGH / P1** | billing / Stripe | `PastDue/Unknown` ma istniejące StripeSubscriptionId, ale kod pozwala rozpocząć kolejny checkout | **Przed płatnym rolloutem** |
| **AUD9-010** | **HIGH / P1** | data integrity | Service ticket może wskazywać inspection innego assetu w tej samej organizacji | **Pilne** |

### Najważniejsze rozróżnienie

- **Nie potwierdziłem cross-tenant data read firma A → firma B w normalnym API.**
- Potwierdziłem jednak **BOLA wewnątrz tenanta** dla roli Manager.
- Potwierdziłem **wyciek sekretów capability do logów**, który nie wymaga przejścia przez tenant authorization, bo posiadanie tokenu samo w sobie daje publiczny dostęp.

---

# 3. AUD9-001 — surowe capability tokeny trafiają do logów

## Severity: **CRITICAL / P0**

**Kategoria:** secret exposure, capability links, anonymous authorization, logging  
**Status:** **release blocker**

## 3.1. Co jest nie tak

Publiczne flow dla:

- assignment acceptance,
- offboardingu,
- asset audit,

używa losowego tokenu jako **bearer credential**. Backend przechowuje jego hash, co jest poprawne. Problem zaczyna się wcześniej: **surowy token jest częścią ścieżki URL**.

### Linki generowane przez backend

`Tenebit.Backend/Tenebit.Infrastructure/Services/AppLinkBuilder.cs`

- linia 15: `/accept/{rawToken}`
- linia 27: `/reset-password?token={rawToken}`
- linia 33: `/verify-email?token={rawToken}`
- linia 39: `/exit/{rawToken}`
- linia 45: `/audit/{rawToken}`

### Publiczne API również używa raw tokenu w path

`Tenebit.Backend/Tenebit.Api/Endpoints/PublicAssignmentsEndpoints.cs`

- linia 44: `GET /public/assignments/{token}`
- linia 50: `POST /public/assignments/{token}/accept`
- linia 56: `GET /public/assignments/{token}/protocol`
- linia 67: dokumenty procedur pod tym samym tokenem
- linia 82: evidence pod tym samym tokenem

`Tenebit.Backend/Tenebit.Api/Endpoints/PublicOffboardingEndpoints.cs`

- linia 44: `GET /public/offboarding/{token}`
- linia 50: zapis odpowiedzi
- linia 56: upload evidence

`Tenebit.Backend/Tenebit.Api/Endpoints/PublicAssetAuditsEndpoints.cs`

- linia 44: GET kampanii uczestnika
- linia 50: update itemu
- linia 56: submit
- linia 62: upload evidence

## 3.2. Dlaczego loguje się sekret

`Tenebit.Backend/Tenebit.Api/Program.cs:179`

```csharp
app.UseSerilogRequestLogging();
```

Request logging zapisuje ścieżkę requestu. W tym systemie ścieżka zawiera token autoryzacyjny.

To nie jest wyłącznie teoretyczny zarzut.

## 3.3. Dowód z dostarczonej paczki

W paczce istnieją runtime logi:

`Tenebit.Backend/Tenebit.Api/logs/tenebit-20260817.log`

Przykład po **celowym zredagowaniu sekretu**:

```text
HTTP GET /api/public/offboarding/<REDACTED> responded 200 ...
```

Takie wpisy występują m.in. około linii:

- 2508,
- 2524,
- 3179,
- 3195,
- 4664,
- 4680.

W logu przed redakcją znajduje się cały raw token. Co istotne, część requestów zwróciła `200`, więc zapisano tokeny wyglądające na poprawnie autoryzujące publiczne flow w momencie wykonania requestu.

**Nie umieszczam tych tokenów w raporcie.**

## 3.4. Nginx powiększa powierzchnię problemu

`Tenebit.Frontend/nginx.conf`

Tylko healthcheck ma jawnie:

```nginx
location = /healthz { access_log off; ... }
```

Dla:

- `/accept/:token`,
- `/exit/:token`,
- `/audit/:token`,
- `/reset-password?token=...`,
- `/verify-email?token=...`,
- `/api/public/.../{token}`

nie ma analogicznej ochrony/redakcji.

Domyślne access logi reverse proxy mogą więc stać się drugim miejscem przechowywania credentiali.

## 3.5. Dlaczego to jest P0, a nie „tylko log hygiene”

Capability token **jest uprawnieniem**.

Jeśli operator, support, agregator logów, backup logów, support bundle, SIEM albo osoba mająca dostęp do archiwum zobaczy taki token, może potencjalnie użyć go bez:

- loginu,
- hasła,
- JWT,
- roli,
- 2FA.

W zależności od rodzaju tokenu skutkiem może być m.in.:

### Offboarding

- odczyt informacji publicznej sprawy,
- zapis odpowiedzi,
- upload evidence.

### Assignment

- odczyt wydania,
- dostęp do protokołu,
- dokumentów/evidence,
- akceptacja, jeśli token nadal spełnia warunki procesu.

### Asset audit

- odczyt przypisanych pozycji,
- modyfikacja odpowiedzi,
- upload,
- submit.

Czyli wyciek logu może stać się **wyciekiem danych lub nieautoryzowaną zmianą procesu biznesowego**.

## 3.6. Dodatkowe ryzyko: logi znajdują się w paczce źródłowej

`audit9` zawiera katalog runtime logs. To oznacza, że przynajmniej w obecnym procesie pakowania logi mogą podróżować razem z kodem.

Nie twierdzę, że ten ZIP jest produkcyjnym artifactem deployowym. Sam fakt, że logi z sekretami weszły do paczki audytowej, pokazuje jednak, że obecna granica dostępu do nich jest zbyt szeroka.

## 3.7. Minimalny hotfix

### Natychmiast

1. Wyłączyć logowanie raw `RequestPath` dla publicznych capability endpoints.
2. Redagować tokeny przed każdym structured logging.
3. Ustawić osobne zasady nginx dla public capability routes.
4. Usunąć runtime logs z paczek źródłowych/deployowych.
5. Jeśli te logi były kopiowane poza zaufane środowisko, potraktować występujące w nich aktywne tokeny jak ujawnione credentials:
   - revoke,
   - regenerate,
   - purge/restrict log archives.

## 3.8. Rozwiązanie docelowe

Najbezpieczniejszy model:

### Krok 1 — token tylko w URL fragment frontendowym

Zamiast:

```text
https://app.example/exit/RAW_SECRET
```

użyć np.:

```text
https://app.example/exit#RAW_SECRET
```

Fragment `#...` nie jest wysyłany w HTTP request target do serwera/proxy.

### Krok 2 — SPA natychmiast usuwa sekret z address bara

- odczytuje fragment,
- `history.replaceState(...)`,
- nie pozostawia tokenu w historii przeglądarki.

### Krok 3 — exchange endpoint

Frontend wysyła token w **body POST**, np.:

```text
POST /api/public/capability-session
```

Backend:

- hashuje raw token,
- sprawdza expiry/revocation/parent status,
- wystawia krótko żyjącą, scope'owaną sesję capability.

### Krok 4 — scoped HttpOnly cookie

Sesja publiczna powinna mieć:

- `HttpOnly`,
- `Secure`,
- sensowny `SameSite`,
- bardzo ograniczony TTL,
- scope wskazujący konkretny proces/uczestnika.

Dalsze requesty nie zawierają sekretu z e-maila w URL.

## 3.9. Test regresyjny wymagany do zamknięcia

Automatyczny test powinien:

1. wygenerować token o znanej wartości testowej,
2. wykonać wszystkie publiczne flow,
3. zebrać log API/reverse proxy,
4. asercja:

```text
RAW_TEST_SECRET nie występuje ani razu w logach.
```

Dodatkowo:

- reset password token,
- verification token,
- public assignment,
- offboarding,
- audit.

## 3.10. Kryterium DONE

AUD9-001 można zamknąć dopiero gdy:

- raw capability secret nie występuje w request path logowanym przez API,
- raw secret nie trafia do nginx access log,
- support bundle nie zawiera raw sekretu,
- history/address bar jest czyszczony,
- stare ujawnione tokeny zostały objęte planem revocation/purge.

---

# 4. AUD9-002 — Manager może ominąć row-level scope przez subresource aktywa

## Severity: **HIGH / P1**

**Kategoria:** BOLA / authorization / least privilege  
**Zasięg:** wewnątrz jednej organizacji, nie cross-company

## 4.1. Model uprawnień deklaruje ograniczenie Managera

`Tenebit.Backend/Tenebit.Application/Common/TenebitRoles.cs`

Manager jest opisywany jako rola widząca swój zespół i jego zasoby.

Główny `AssetService` faktycznie stosuje `ManagerScopeService` i filtruje aktywa.

Problem: kilka usług podrzędnych sprawdza tylko:

```csharp
EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers)
```

`AssetViewers` zawiera Managera, ale **nie jest wykonywany row-level scope**.

## 4.2. Asset evidence

`Tenebit.Backend/Tenebit.Application/Evidence/AssetEvidenceService.cs`

### ListByAssetAsync — linie 35–45

- linia 37: wystarczy rola z `AssetViewers`,
- linia 41: asset pobierany tylko po `(organizationId, assetId)`,
- brak `ManagerScopeService`,
- linia 44: evidence zostaje zwrócone.

### GetAsync — linie 48–54

- linia 50: tylko `AssetViewers`,
- linia 53: evidence pobierane po organizacji i ID,
- brak sprawdzenia, czy jego asset należy do scope Managera.

### Skutek

Manager znający GUID assetu/evidence innego zespołu może uzyskać materiały, których nie powinien widzieć.

## 4.3. Service tickets — jeszcze łatwiejsze do wykorzystania

`Tenebit.Backend/Tenebit.Application/Assets/ServiceTicketService.cs`

### ListByAssetAsync — linie 30–36

Brak ManagerScope.

### ListPagedAsync — linie 39–45

To najpoważniejszy wariant:

- Manager przechodzi przez `AssetViewers`,
- repozytorium dostaje tylko `OrganizationId`, status, page, pageSize,
- zwracana jest tenant-wide lista ticketów.

Tu Manager **nie musi nawet znać GUID** obcego assetu. Może enumerować zgłoszenia serwisowe z innych zespołów w tej samej firmie.

Dane ticketu mogą obejmować m.in.:

- opis awarii,
- vendor,
- koszt szacowany/rzeczywisty,
- SLA,
- resolution,
- powiązane asset/inspection IDs.

### GetAsync — linie 48–55

Ponownie brak scope.

## 4.4. Asset inspection

`Tenebit.Backend/Tenebit.Application/Assets/AssetInspectionService.cs:28–36`

`GetPendingForAssetAsync`:

- sprawdza `AssetViewers`,
- pobiera inspection po organizacji i asset ID,
- nie weryfikuje Manager scope.

Może ujawnić dane kontroli aktywa spoza zespołu Managera.

## 4.5. Dlaczego to jest realny błąd autoryzacji

To nie jest problem UI.

Nawet jeśli frontend nie pokazuje linku, użytkownik posiada ważny JWT Managera i może wywołać endpoint bezpośrednio.

Autoryzacja zasobowa musi być wymuszana przez backend dla **każdej ścieżki do tego samego zasobu**.

Obecnie istnieje sytuacja:

```text
GET głównego assetu -> poprawnie ograniczony
GET evidence/tickets/inspection assetu -> scope można ominąć
```

To klasyczny inconsistent authorization / BOLA.

## 4.6. Minimalna poprawka

Wprowadzić wspólny guard, np.:

```text
IAssetAuthorizationService.EnsureCanViewAssetAsync(assetId)
```

Guard powinien:

- Owner/Admin/AssetOperator/Technician — zgodnie z polityką organizacyjną,
- Manager — `ManagerScopeService.ContainsAsset(assetId)`,
- inne role — deny.

Każdy subresource musi przejść przez ten sam guard.

## 4.7. Lepsze rozwiązanie

Nie kopiować guardów do 15 usług.

Dla list:

- repozytorium powinno otrzymywać **scope query**, np. `allowedTeamIds/allowedPersonIds`,
- filtrowanie odbywa się w SQL,
- nie pobierać całej organizacji i filtrować w pamięci.

Dla obiektu po ID:

- query samo powinno zawierać dozwolony scope,
- jeśli obiekt istnieje, ale jest poza scope — zwracać 404, nie 403, żeby nie ujawniać istnienia.

## 4.8. Wymagane testy negatywne

Stworzyć:

- Team A + Manager A,
- Team B,
- Asset B,
- Evidence B,
- Inspection B,
- ServiceTicket B.

Następnie jako Manager A:

- `GET Asset B` -> 404,
- `GET Asset B evidence list` -> 404,
- `GET Evidence B by id` -> 404,
- `GET Asset B inspection` -> 404,
- `GET Asset B tickets` -> 404,
- `GET Ticket B by id` -> 404,
- global paged ticket list -> **nie może zawierać Ticket B**.

---

# 5. AUD9-003 — ustawienie `CapturePublicIp` nie steruje faktycznym zapisem IP

## Severity: **HIGH / P1**

**Kategoria:** privacy, compliance, contractual correctness

## 5.1. Konfiguracja mówi jedno, runtime robi drugie

`Tenebit.Backend/Tenebit.Domain/Organizations/Organization.cs`

- linia 34: `CapturePublicIp` domyślnie `Off`,
- linia 35: `PublicIpRetentionDays`.

Ustawienie jest dostępne przez settings service.

Natomiast w runtime brak centralnego użycia tych wartości podczas capture.

## 5.2. Assignment zapisuje pełne IP bez policy

`Tenebit.Backend/Tenebit.Application/Assignments/AssignmentService.cs`

- około linii 341: authenticated acceptance przekazuje `_currentUser.IpAddress`,
- około linii 602: public acceptance również przekazuje `_currentUser.IpAddress`.

Nie ma przed tym:

```text
CapturePublicIp == Off -> null
CapturePublicIp == Truncated -> truncate
CapturePublicIp == Full -> full
```

## 5.3. Public QR umieszcza IP w ActivityLog actor subject

`Tenebit.Backend/Tenebit.Application/Assets/AssetService.cs`

- linia 305: `reporterIp = _currentUser.IpAddress`,
- linia 306: `actorSubject = $"public-scan:{reporterIp}"`,
- później wartość jest zapisywana w ActivityLog.

To oznacza, że nawet jeśli klient wybierze „Off”, surowe IP może być utrwalone w logu aktywności.

## 5.4. Retention

`PublicIpRetentionDays` jest zapisywane w ustawieniach, ale w przebadanym runtime nie znalazłem mechanizmu, który rzeczywiście:

- usuwał,
- anonimizował,
- truncował

IP po wskazanym okresie.

## 5.5. Dlaczego to jest poważne

To nie jest spór o idealną interpretację RODO.

Techniczny fakt jest prosty:

> aplikacja wystawia klientowi ustawienie prywatności, ale backend go nie respektuje w miejscach capture.

Jeżeli firma ustawi `Off`, ma uzasadnione oczekiwanie, że raw IP nie będzie utrwalane.

W systemie B2B taki mismatch może stać się:

- problemem compliance,
- problemem umowy/DPA,
- problemem podczas DSAR/retention audit,
- niepotrzebnym źródłem danych osobowych przy incydencie.

## 5.6. Naprawa

Wprowadzić jeden serwis, np.:

```text
IPublicIpPrivacyPolicy
```

API:

```text
Capture(organization, rawIp) -> null / truncated / full
```

Zasady:

### Off

```text
null
```

### Truncated

Jawnie zdefiniować politykę, np.:

- IPv4: `/24`,
- IPv6: `/56` lub `/64`,

i trzymać ją konsekwentnie.

### Full

- parsowanie `IPAddress`,
- normalizacja,
- dopiero potem zapis.

## 5.7. Nie wkładać IP do tekstowego `ActorSubject`

`public-scan:<raw ip>` jest trudne do późniejszej retencji/redakcji.

Lepszy model:

- structured nullable `SourceIp`,
- `SourceIpExpiresAt`,
- osobny cleanup/anonymization job.

Do ochrony anty-abuse, gdy nie chcesz trzymać IP długo, można użyć krótkotrwałego keyed-HMAC fingerprintu zamiast trwałego raw address.

## 5.8. Uwaga o integrity hash

Jeżeli accepted IP jest częścią trwałego integrity hash protokołu, a klient ustawi retention wymagający późniejszego usunięcia IP, powstaje konflikt projektu danych.

Rozwiązanie:

- seal nie powinien wymagać przechowywania raw IP w nieskończoność,
- można zapisać HMAC/pseudonim do dowodu integralności i usunąć raw IP zgodnie z retention.

## 5.9. Wymagane testy

Dla każdego flow capture:

### Organization = Off

- DB: brak raw IP,
- ActivityLog: brak raw IP,
- response: brak raw IP.

### Organization = Truncated

- raw address nie występuje,
- tylko oczekiwany prefiks.

### Organization = Full

- zapis zgodny z policy.

### Retention

- rekord przed TTL pozostaje,
- po TTL jest redagowany/usuwany,
- integrity procesu pozostaje poprawne.

---

# 6. AUD9-004 — account pre-hijacking przez niezwerfikowaną rejestrację + automatyczne OAuth linking

## Severity: **HIGH / P1**

**Kategoria:** identity integrity / account pre-hijacking

## 6.1. Rejestracja daje pełne konto Owner przed potwierdzeniem mailboxa

`Tenebit.Backend/Tenebit.Application/Identity/AuthService.cs:75–119`

Istotne miejsca:

- linia 82: globalne sprawdzenie, czy email już istnieje,
- linie 94–96: tworzony jest aktywny użytkownik i rola `Owner`,
- linia 115: zapis,
- linia 117: dopiero potem wysyłany jest verification email.

`Tenebit.Backend/Tenebit.Api/Endpoints/AuthEndpoints.cs:44–52`

Po `RegisterAsync` API od razu:

- wystawia refresh token,
- ustawia cookie,
- wystawia access JWT.

Czyli **kontrola mailbox ownership nie jest bramką przed uzyskaniem workspace Owner session**.

## 6.2. Login hasłem również nie wymaga verified email

`AuthService.cs:127–150`

Login sprawdza:

- czy user istnieje,
- czy jest aktywny,
- czy hasło się zgadza,
- 2FA/trusted device,

ale nie wymaga `IsEmailVerified`.

## 6.3. OAuth automatycznie linkuje konto tylko po emailu

`AuthService.cs:361–382`

Jeśli dostawca zwróci verified email, a lokalny user z tym emailem już istnieje:

- external login jest dodawany,
- niezwerfikowany lokalny email jest oznaczany jako verified,
- użytkownik dostaje login outcome.

Nie ma wymogu wcześniejszego zalogowania się na istniejące konto ani potwierdzenia lokalnego password/session przed linkowaniem.

## 6.4. Scenariusz pre-hijacking

1. Atakujący zna służbowy adres ofiary: `victim@company.example`.
2. Rejestruje ten email zanim ofiara zacznie korzystać z Tenebit.
3. Nie ma dostępu do mailboxa, ale system i tak daje mu aktywne konto Owner i sesję.
4. Email jest globalnie zajęty.
5. Ofiara później wybiera „Sign in with Microsoft/Google”.
6. Provider potwierdza prawdziwy email ofiary.
7. Backend znajduje wcześniej utworzone konto i automatycznie dopina provider do niego.
8. Konto staje się verified, ale **hasło ustawione przez atakującego nadal istnieje**.

To jest niebezpieczny merge dwóch różnych dowodów tożsamości.

## 6.5. Skutki

- squatting służbowych adresów,
- onboarding DoS,
- możliwość wprowadzenia ofiary do organizacji utworzonej przez atakującego,
- równoległy dostęp atakującego przez hasło/sesję po tym, jak ofiara uwierzy, że zalogowała się „swoim Microsoftem”.

Nie jest to prosta droga do istniejącej firmy B, ale jest to realna wada lifecycle identity.

## 6.6. Minimalna poprawka

Rejestracja przed weryfikacją email powinna tworzyć **pending registration**, nie pełny aktywny workspace.

Przed verification:

- brak Owner JWT do aplikacji,
- brak normalnego refresh tokenu,
- ewentualnie osobna ograniczona verification session.

Normalne logowanie/workspace:

```text
IsEmailVerified == true
```

## 6.7. OAuth auto-linking

Nie łączyć automatycznie verified provider identity z **niezweryfikowanym lokalnym kontem** wyłącznie po emailu.

Bezpieczne warianty:

### Wariant A — explicit linking

- user loguje się lokalnie,
- potwierdza operację,
- dopiero wtedy link provider.

### Wariant B — secure recovery/claim

Jeżeli provider ma zweryfikowany email i ma przejąć pending account:

- unieważnić stare lokalne credentials/sesje,
- wymusić nowy password albo usunąć local password,
- rotate security stamp,
- revoke refresh/trusted devices,
- zapisać audyt claimu.

## 6.8. Test regresyjny

Test powinien dokładnie symulować:

1. attacker register `victim@example.com`,
2. attacker nie potwierdza mailboxa,
3. sprawdzić, że nie ma pełnego workspace access,
4. victim loguje się verified OAuth o tym samym emailu,
5. stare password/session atakującego **nie mogą** nadal działać.

---

# 7. AUD9-005 — password reset nie jest atomowo single-use

## Severity: **HIGH / P1**

**Kategoria:** authentication / recovery / concurrency

## 7.1. Obecny flow

`Tenebit.Backend/Tenebit.Infrastructure/Repositories/PasswordResetTokenRepository.cs:13–14`

Valid token jest pobierany zwykłym SELECT:

```csharp
FirstOrDefaultAsync(x =>
    x.TokenHash == tokenHash &&
    x.UsedAt == null &&
    x.ExpiresAt > now)
```

Nie ma:

- row lock,
- atomic update,
- compare-and-set,
- concurrency tokenu dla consume.

## 7.2. Reset

`Tenebit.Backend/Tenebit.Application/Identity/AuthService.cs:477–509`

Flow:

1. `FindValidAsync`,
2. pobierz usera,
3. ustaw password,
4. rotate security stamp,
5. `token.MarkUsed()`,
6. revoke sessions/devices,
7. `SaveChangesAsync`.

Dwa requesty uruchomione równolegle mogą oba przeczytać `UsedAt == null` przed commitem drugiego.

## 7.3. Drugi problem: wiele tokenów dla jednego usera pozostaje ważnych

`RequestPasswordResetAsync:459–462`

Każdy request:

- tworzy nowy token,
- dodaje go,
- nie odwołuje wcześniejszych unused reset tokens.

Przykład:

1. użytkownik prosi o reset A,
2. użytkownik prosi o reset B,
3. używa B i poprawnie zmienia hasło,
4. A nadal ma `UsedAt == null` i może być ważny aż do expiry.

Zmiana security stamp nie jest obecnie częścią warunku validacji PasswordResetToken, więc stary token nie zostaje automatycznie unieważniony przez sam udany reset.

## 7.4. Dlaczego to jest istotne

Reset hasła jest jednym z najbardziej uprzywilejowanych credentiali.

Po skutecznym resecie użytkownik ma prawo oczekiwać:

> wszystkie stare linki do resetu przestały działać.

Obecny model tego nie gwarantuje.

## 7.5. Poprawka

Repozytorium powinno mieć operację typu:

```text
TryConsumeAsync(tokenHash, now)
```

realizowaną jednym atomicznym SQL:

```sql
UPDATE password_reset_tokens
SET used_at = @now
WHERE token_hash = @hash
  AND used_at IS NULL
  AND expires_at > @now
RETURNING organization_user_id;
```

Sukces tylko gdy dokładnie jeden rekord został zaktualizowany.

## 7.6. Udany reset powinien w jednej transakcji

- atomowo consume bieżący token,
- zmienić password,
- rotate security stamp,
- revoke refresh tokens,
- revoke trusted devices,
- revoke **wszystkie pozostałe unused password reset tokens usera**.

## 7.7. Testy

### Concurrency

Uruchomić dwa równoległe resety tym samym tokenem.

Oczekiwane:

```text
1 success
1 invalid/used token
```

### Stary link

- issue A,
- issue B,
- reset B,
- reset A -> failure.

---

# 8. AUD9-006 — recovery code 2FA nie jest atomowo single-use

## Severity: **HIGH / P1**

**Kategoria:** 2FA / recovery / race condition

## 8.1. Kod

`Tenebit.Backend/Tenebit.Application/Identity/AuthService.cs:310–322`

Flow:

1. normalizacja kodu,
2. hash,
3. `_recoveryCodes.ListAsync(userId)`,
4. `FirstOrDefault(x => x.IsUnused && x.CodeHash == hash)`,
5. `match.MarkUsed(...)`,
6. `SaveChangesAsync`.

`Tenebit.Backend/Tenebit.Infrastructure/Repositories/TwoFactorRecoveryCodeRepository.cs:13–14`

Repository wykonuje zwykły SELECT listy kodów.

## 8.2. Problem

Dwa requesty mogą równocześnie zobaczyć ten sam recovery code jako unused.

Recovery code ma semantykę **jednorazowego drugiego czynnika**. Single-use nie powinno być właściwością „zwykle działa”, tylko gwarancją atomową.

## 8.3. Realny scenariusz race

Atakujący, który ma:

- poprawne hasło,
- jeden przejęty recovery code,

może zdobyć więcej niż jeden aktywny login challenge i próbować równolegle skonsumować ten sam recovery code.

Nie twierdzę, że jest to łatwe zdalne przejęcie bez credentials. Problem polega na tym, że mechanizm recovery nie zapewnia deklarowanej właściwości security.

## 8.4. Naprawa

Podobnie jak reset:

```sql
UPDATE two_factor_recovery_codes
SET used_at = @now
WHERE organization_user_id = @user
  AND code_hash = @hash
  AND used_at IS NULL;
```

Akceptacja tylko gdy affected rows == 1.

## 8.5. Test

`Task.WhenAll` / równoległe requesty:

- ten sam user,
- ten sam recovery code,
- kilka challenge tokens,
- dokładnie jedna operacja może zakończyć się sukcesem.

---

# 9. AUD9-007 — wspólny limiter auth może zablokować całe biuro za NAT

## Severity: **HIGH / P1**

**Kategoria:** availability / authentication / client incident

## 9.1. Konfiguracja

`Tenebit.Backend/Tenebit.Api/Program.cs:103–125`

Policy `auth`:

- partition key = publiczny IP,
- `PermitLimit = 10`,
- window = 1 minuta,
- queue = 0.

To jest poprawa względem globalnego limitera dla całego systemu, ale granularity nadal jest zbyt grube.

## 9.2. Wszystkie operacje auth współdzielą ten sam koszyk

Ta sama policy obejmuje m.in.:

- register,
- login,
- login/2fa,
- **refresh**,
- forgot password,
- reset password,
- verify/resend,
- OAuth start/callback.

`AuthEndpoints.cs:104–124` pokazuje, że `/auth/refresh` też ma `.RequireRateLimiting("auth")`.

## 9.3. Access token żyje domyślnie 10 minut

`Tenebit.Backend/Tenebit.Api/Auth/JwtIssuerOptions.cs:14–15`

Default access JWT TTL = 10 min.

Frontend wykorzystuje refresh przy odtwarzaniu sesji / po 401.

## 9.4. Scenariusz normalnego klienta

Firma ma 30 pracowników w biurze za jednym NAT.

Rano albo po deployu/reloadzie:

- 10 osób wykona refresh/login,
- 11. operacja w tej minucie dostaje 429,
- kolejne operacje auth również są blokowane.

To nie wymaga ataku.

Jeden agresywny user z tego samego NAT może również wyczerpać koszyk i blokować współpracowników.

## 9.5. Dlaczego to jest ważne przy 100 firmach

W B2B wiele firm korzysta z:

- jednego publicznego wyjścia internetowego,
- VPN,
- corporate proxy,
- VDI.

Per-IP auth limiter musi uwzględniać fakt, że jeden IP != jeden użytkownik.

## 9.6. Naprawa

Rozdzielić polityki:

### Login password attempts

Klucz złożony:

```text
IP + normalized account/email
```

Niski limit.

### Password reset

```text
IP + normalized email
```

plus anty-enumeration.

### Refresh

Limit po:

- refresh token family/user,
- ewentualnie wysokim per-IP safety ceiling.

Nie 10/min dla całego NAT.

### OAuth callback/start

Osobny bucket.

## 9.7. Multi-replica

Obecny in-memory limiter działa per instance. Jeśli rate limiting ma być kontrolą security przeciw brute force, przy wielu replikach skuteczny budżet może wzrosnąć wraz z liczbą instancji.

Dla krytycznych limitów auth warto rozważyć distributed limiter lub warstwę edge.

## 9.8. Test obciążeniowy

Minimum:

```text
50 różnych legalnych userów
1 public IP
równoczesny refresh/login burst
```

Oczekiwane:

- normalni userzy nie dostają masowo 429,
- pojedyncze konto/brute-force nadal jest skutecznie throttlowane.

---

# 10. AUD9-008 — side effect przed commit DB i długie transakcje jobów

## Severity: **HIGH / P1**

**Kategoria:** transactional reliability / messaging / HA

To są dwa objawy tego samego problemu: zewnętrzny side effect i stan DB nie są spięte niezawodnym delivery patternem.

## 10.1. Offboarding wysyła link przed zapisem token hash

`Tenebit.Backend/Tenebit.Application/Offboarding/OffboardingService.cs`

### StartAsync

- linia 315: `IssueTokenAndSendLinkAsync(...)`,
- linia 318: dopiero potem `SaveChangesAsync`.

### Helper

`IssueTokenAndSendLinkAsync`, linie 705–727:

- linia 712: obiekt otrzymuje nowy TokenHash,
- linia 713: budowany jest publiczny link z raw tokenem,
- linia 719: e-mail jest wysyłany,
- caller dopiero później zapisuje DB.

## 10.2. Awaria po SMTP success

Scenariusz:

1. raw token wygenerowany,
2. e-mail wysłany do pracownika,
3. `SaveChangesAsync` nie dochodzi do skutku / transaction rollback,
4. użytkownik otrzymuje link,
5. backend nie ma odpowiadającego mu zapisanego hash/stanu.

Klient ma „działający-looking” mail z martwym linkiem.

W resend/regenerate istnieje analogiczna kolejność.

## 10.3. Asset audit ma ten sam problem w większej skali

`Tenebit.Backend/Tenebit.Application/Audits/AssetAuditCampaignService.cs`

### Start

- linie 206–223: participant/item/token są tworzone, a e-mail jest wysyłany w pętli,
- linia 227: dopiero po całej pętli jest `SaveChangesAsync`.

Jeśli wysłano 40 wiadomości, a końcowy save się nie powiedzie, potencjalnie 40 osób dostało linki do stanu, który nie został zatwierdzony.

### Reminders

- linia 465: ustawienie nowego token hash,
- linia 468: wysyłka,
- linia 480: dopiero końcowy save.

## 10.4. Background job lock trzyma transaction podczas całego action

`Tenebit.Backend/Tenebit.Infrastructure/Services/PostgresJobLock.cs:24–46`

- linia 29: otwarcie DB transaction,
- linie 33–40: claim,
- linia 44: `await action(cancellationToken)`,
- linia 45: commit.

Czyli transaction pozostaje otwarta podczas wykonywania pełnego joba.

Jeśli job:

- wysyła SMTP,
- pyta Stripe,
- wykonuje inny network I/O,

to DB transaction żyje przez cały czas zewnętrznego I/O.

## 10.5. Skutki

- dłuższe transakcje,
- gorsza skalowalność przy 100 tenantach,
- retry po rollback może powtórzyć external side effect,
- „mail wysłany / baza mówi inaczej”,
- partial delivery kampanii,
- trudniejsze odtwarzanie błędów.

## 10.6. Rozwiązanie docelowe: transactional outbox

W jednej krótkiej transakcji zapisać:

- business change,
- token hash,
- outbox message z recipient/template/payload/idempotency key.

Commit.

Osobny dispatcher:

1. bierze pending outbox,
2. wysyła e-mail,
3. oznacza sent,
4. retry z backoff,
5. nie generuje nowego sekretu przy każdym retry.

## 10.7. Job coordination

Nie trzymać transakcji DB wokół zewnętrznego action.

Lepszy model:

- krótki claim/lease,
- commit claimu,
- wykonanie joba poza długą transakcją,
- krótkie transakcje biznesowe per chunk/tenant,
- heartbeat/lease expiry, jeśli potrzebne,
- idempotentny worker.

## 10.8. Test fault-injection

W testach zasymulować:

- SMTP success,
- DB failure przy commit,

oraz odwrotnie.

Po naprawie nie może istnieć sytuacja, gdzie klient otrzymuje raw capability link, którego backend nie uznaje z powodu rollbacku.

---

# 11. AUD9-009 — możliwa druga subskrypcja Stripe dla PastDue/Unknown

## Severity: **HIGH / P1**

**Kategoria:** billing / double subscription risk

## 11.1. Problem definicji „aktywnej subskrypcji”

`Tenebit.Backend/Tenebit.Domain/Subscriptions/OrganizationSubscription.cs:38–40`

```csharp
public bool HasActiveStripeSubscription =>
    !string.IsNullOrWhiteSpace(StripeSubscriptionId) &&
    Status == SubscriptionStatus.Active;
```

Nazwa sugeruje pytanie „czy za tym tenantem istnieje nadal live Stripe subscription”, ale implementacja pyta w praktyce:

> czy lokalny status jest dokładnie Active.

To nie jest to samo.

## 11.2. PastDue i Unknown zachowują StripeSubscriptionId

`SyncFromStripe`, linie 85–110:

- `StripeSubscriptionId` jest ustawiane,
- status `PastDue`/`Unknown` przełącza plan na Free,
- subscription ID nie jest usuwane.

Czyli organizacja może mieć nadal realny obiekt subskrypcji w Stripe, ale:

```text
HasActiveStripeSubscription == false
```

## 11.3. Checkout sprawdza tylko tę flagę

`Tenebit.Backend/Tenebit.Application/Subscriptions/SubscriptionService.cs:154–165`

Nowy Checkout Session jest blokowany tylko wtedy, gdy `HasActiveStripeSubscription` jest true.

Dla `PastDue`/`Unknown` false -> nowy checkout może zostać uruchomiony.

## 11.4. Scenariusz

1. Firma ma subscription S1.
2. Płatność wpada w `PastDue`.
3. Lokalny system zabiera entitlement i plan wraca do Free — to samo w sobie jest defensywne.
4. `StripeSubscriptionId = S1` nadal istnieje.
5. Owner uruchamia nowy checkout.
6. Powstaje subscription S2.
7. S1 później odzyskuje płatność / zostaje ręcznie naprawiona w Stripe.
8. Firma może mieć dwie subskrypcje.

To jest ryzyko podwójnego billing i bardzo nieprzyjemny incydent klienta.

## 11.5. Drugi problem: brak idempotency key dla Checkout Session

`Tenebit.Backend/Tenebit.Infrastructure/Services/StripePaymentGateway.cs:41–50`

POST do `checkout/sessions` nie ma Stripe idempotency key.

Dwa równoległe kliknięcia/retry mogą stworzyć więcej niż jedną Checkout Session.

Nie znaczy to automatycznie, że klient zapłaci dwa razy bez wykonania obu checkoutów, ale system powinien eliminować tę klasę race na poziomie integracji billingowej.

## 11.6. Poprawny model domenowy

Rozdzielić dwie koncepcje:

### `IsEntitledToPaidPlan`

Czy aplikacja ma obecnie przyznać funkcje płatne.

### `HasLiveStripeSubscription`

Czy w Stripe istnieje subskrypcja, którą trzeba:

- naprawić,
- opłacić,
- anulować,
- zarządzić w Billing Portal,

zamiast tworzyć drugą.

## 11.7. Przed nowym checkoutem

Jeśli istnieje StripeSubscriptionId i stan nie jest terminalny:

1. pobierz canonical state ze Stripe,
2. jeśli live/past_due/unpaid/incomplete w zależności od modelu — nie twórz drugiej,
3. kieruj do payment recovery / billing portal,
4. nowy checkout dopiero po terminalnym/cancelled/expired state zgodnie z polityką.

## 11.8. Stripe idempotency

Dodać stabilny idempotency key dla operacji tworzenia customer/checkout, np. na podstawie:

- OrganizationId,
- billing intent ID,
- app-generated checkout attempt ID.

Checkout attempt warto utrwalić lokalnie.

## 11.9. Testy

- `PastDue + StripeSubscriptionId` -> nowy checkout blocked/recovery flow,
- `Unknown + StripeSubscriptionId` -> canonical verification, nie blind create,
- `Cancelled` -> nowy checkout allowed,
- dwa równoległe `CreateCheckoutSession` -> jeden logical billing attempt.

---

# 12. AUD9-010 — ServiceTicket może wskazywać inspection innego assetu

## Severity: **HIGH / P1**

**Kategoria:** data integrity / audit trail correctness

## 12.1. Kod

`Tenebit.Backend/Tenebit.Application/Assets/ServiceTicketService.cs:58–82`

Flow otwarcia:

- linia 66: asset `request.AssetId` musi istnieć w organizacji,
- linia 69: jeśli podano `AssetInspectionId`, sprawdzane jest jedynie, czy taka inspection istnieje w organizacji,
- linia 74: tworzony jest ticket z osobno przekazanym AssetId i AssetInspectionId.

Brakuje warunku:

```text
inspection.AssetId == request.AssetId
```

## 12.2. Scenariusz

W tej samej organizacji:

- Asset A,
- Asset B,
- Inspection B.

Użytkownik mający uprawnienie do tworzenia ticketu wysyła:

```text
AssetId = A
AssetInspectionId = InspectionB
```

Oba GUID-y są legalne w organizacji, więc obecna walidacja przechodzi.

Powstaje ServiceTicket:

```text
asset = A
inspection = kontrola assetu B
```

## 12.3. Dlaczego HIGH

To nie jest wyciek bezpieczeństwa między firmami, ale w systemie zarządzania sprzętem i audytem taka niespójność może zanieczyścić:

- historię serwisową,
- raport kosztów,
- decyzje naprawa/wymiana,
- ślad audytowy,
- powiązanie procesu inspection → ticket.

Jeżeli klient podejmuje decyzje finansowe albo compliance na podstawie historii assetu, silent cross-link jest poważnym błędem danych.

## 12.4. Minimalna naprawa

```text
inspection = GetAsync(orgId, request.AssetInspectionId)
if inspection == null -> validation error
if inspection.AssetId != request.AssetId -> validation error
```

Nie wykonywać samego existence check.

## 12.5. Jeszcze lepszy model

Jeśli ticket powstaje **z inspection**, nie pozwalać callerowi podawać dwóch niezależnych źródeł prawdy.

Np.:

```text
OpenFromInspection(inspectionId)
```

backend sam bierze `AssetId` z inspection.

To eliminuje całą klasę niespójnych par.

## 12.6. Test

- Asset A + Inspection A -> success,
- Asset A + Inspection B -> validation failure,
- Asset A + inspection z innej organizacji -> not found/failure.

---

# 13. Werdykt multi-tenant

To jest najważniejsza część z punktu widzenia wdrożenia dla ~100 niezależnych firm.

## 13.1. Czego nie znalazłem

W statycznym przejściu `audit9` **nie znalazłem potwierdzonego standardowego endpointu**, który pozwala zalogowanemu userowi firmy A wykonać zwykły request i pobrać rekord firmy B przez podmianę GUID.

Nie będę wymyślał takiego problemu tylko dlatego, że był głównym ryzykiem wcześniejszych wersji.

## 13.2. Baza ma znacznie lepszy defense-in-depth

W bieżącym modelu znajduje się duża liczba tenant-aware composite relationships opartych o:

```text
(OrganizationId, TargetId)
```

W automatycznym przeglądzie konfiguracji znalazłem około **52 composite FK** tej klasy.

To istotnie zmniejsza możliwość utworzenia przypadkowej relacji:

```text
rekord organizacji A -> rekord organizacji B
```

nawet jeśli przyszły kod aplikacyjny zapomni o części walidacji.

## 13.3. Czego ten wniosek NIE oznacza

Nie oznacza:

> „multi-tenancy jest matematycznie udowodnione jako bezpieczne”.

Wciąż potrzebne są negatywne testy HTTP + realny PostgreSQL dla całej macierzy krytycznych zasobów.

I istnieje potwierdzony **same-tenant row-scope bypass Managera** opisany w AUD9-002.

## 13.4. Public capability token jest osobną granicą bezpieczeństwa

AUD9-001 jest szczególnie ważny dlatego, że capability route działa **poza zwykłym JWT tenant authorization**.

Kto ma raw token, ma ograniczone anonimowe uprawnienie do konkretnego procesu. Dlatego nawet przy poprawnym `OrganizationId` sekret w logach pozostaje poważnym incydentem.

---

# 14. Kolejność napraw

## P0 — natychmiast, przed release z realnymi danymi

### 1. AUD9-001 — capability token logging

Kolejność:

1. zatrzymać logowanie raw secretów,
2. poprawić nginx/API logging,
3. usunąć token z URL request target w docelowej architekturze,
4. przejrzeć istniejące logi,
5. revoke potencjalnie ujawnione aktywne tokeny,
6. dodać secret-not-in-logs test.

Bez tego release = **NO-GO**.

---

## P1-A — authorization / identity

### 2. AUD9-002 — Manager BOLA

Naprawić wszystkie asset subresources jednym centralnym scope guardem.

### 3. AUD9-004 — pre-hijacking

Nie wydawać pełnego Owner session przed mailbox verification i nie auto-linkować verified OAuth do unverified local account bez bezpiecznego claim flow.

### 4. AUD9-005 — reset password atomicity

Atomic consume + revoke all outstanding reset tokens.

### 5. AUD9-006 — recovery code atomicity

Atomic single-use SQL.

---

## P1-B — klient / dostępność / prywatność

### 6. AUD9-007 — auth limiter

Rozdzielić limiter login/refresh/recovery/OAuth i przetestować corporate NAT.

### 7. AUD9-003 — IP privacy

Centralna capture policy + retention implementation + cleanup istniejących danych zgodnie z konfiguracją.

---

## P1-C — billing / reliability / integralność

### 8. AUD9-009 — Stripe

Rozdzielić entitlement od live Stripe subscription + idempotent checkout.

### 9. AUD9-008 — outbox / transakcje jobów

Przenieść e-maile/linki na transactional outbox i skrócić transaction scope job gate.

### 10. AUD9-010 — inspection mismatch

Jedna prosta walidacja aplikacyjna plus test.

---

# 15. Minimalny security regression pack przed kolejnym audytem

Po poprawkach nie wystarczy „kod wygląda dobrze”. Oczekuję automatycznych testów, które próbują złamać dokładnie te granice.

## 15.1. Secret leakage suite

- wygeneruj capability secret,
- wykonaj public GET/POST/upload,
- przeszukaj API log,
- przeszukaj reverse proxy log,
- secret count musi wynosić **0**.

Osobno dla:

- assignment,
- offboarding,
- audit,
- password reset,
- email verification.

## 15.2. Manager authorization matrix

Manager Team A kontra zasoby Team B:

- asset,
- evidence list,
- evidence by id,
- service ticket list,
- service ticket by id,
- inspection,
- assignment,
- procedures.

Wszystkie ścieżki muszą być konsekwentne.

## 15.3. Tenant A/B matrix

Dla każdej głównej encji:

- GET,
- list/filter,
- update,
- delete,
- attach child,
- upload/download,
- public flow, jeśli istnieje.

A próbuje użyć GUID B.

Oczekiwane:

- 404/forbidden zgodnie z polityką,
- brak zmiany w DB B,
- brak existence leak.

## 15.4. Identity pre-hijacking

- attacker preregisters victim email,
- brak mailbox confirmation,
- attacker nie może wejść do normalnego workspace,
- victim verified OAuth claim,
- wszystkie stare attacker credentials invalid.

## 15.5. Password reset concurrency

- dwa równoległe consume jednego tokenu,
- dokładnie jeden success.

- token A,
- token B,
- success B,
- token A invalid.

## 15.6. 2FA recovery concurrency

Ten sam recovery code równolegle:

- dokładnie jeden success.

## 15.7. NAT auth load

Co najmniej:

- 50 użytkowników,
- jeden public IP,
- równoczesny session restore/refresh,
- brak masowego 429,
- brute-force jednego konta nadal blokowany.

## 15.8. Stripe state machine

Przetestować:

- active,
- trialing, jeśli obsługiwane,
- past_due,
- unpaid,
- incomplete,
- cancelled,
- unknown/out-of-order event,
- concurrent checkout.

## 15.9. Outbox fault tests

- DB commit failure,
- SMTP timeout,
- SMTP success + process crash,
- retry,
- duplicate worker.

Wymaganie:

- zero dead capability links wysłanych z niezapisanym stanem,
- zero niekontrolowanych duplikatów.

---

# 16. Kryteria GO

Dla tej konkretnej paczki dałbym status **GO** dopiero gdy wszystkie poniższe są spełnione.

## Security

- [ ] raw capability/reset/verification token **nigdy** nie trafia do logów,
- [ ] AUD9-001 zamknięty testem automatycznym,
- [ ] Manager nie może obejść scope przez żaden asset subresource,
- [ ] preregistration nie daje pełnego workspace bez email verification,
- [ ] OAuth claim nie zachowuje credentials osoby, która wcześniej zajęła email,
- [ ] reset token jest atomic single-use,
- [ ] recovery code jest atomic single-use.

## Multi-tenancy

- [ ] pełna tenant A/B HTTP integration matrix jest zielona,
- [ ] negative association tests są zielone,
- [ ] public capability tests nie ujawniają zasobów poza powiązanym parentem.

## Privacy

- [ ] `CapturePublicIp=Off` faktycznie oznacza brak raw IP,
- [ ] truncated działa zgodnie ze spec,
- [ ] retention jest wykonywany,
- [ ] stare dane są objęte migracją/redakcją, jeśli potrzeba.

## Reliability/billing

- [ ] e-mail/link delivery nie wyprzedza trwałego stanu DB,
- [ ] długie external I/O nie odbywa się w job transaction,
- [ ] PastDue/Unknown live subscription nie może utworzyć drugiej subskrypcji,
- [ ] checkout ma idempotency,
- [ ] mismatched AssetInspection/Asset ticket jest odrzucany.

## Availability

- [ ] test wielu użytkowników za jednym NAT nie powoduje auth outage.

---

# 17. Nowa punktacja szczegółowa

Ocena dotyczy **audit9**, nie historii projektu.

| Obszar | Ocena | Komentarz ograniczony do aktualnego ryzyka |
|---|---:|---|
| Izolacja tenantów firma A/B | **94/100** | Nie znalazłem aktualnej potwierdzonej ścieżki A→B; DB ma mocny composite-FK defense-in-depth. |
| Authorization / row-level access | **82/100** | Główne scope są poprawione, ale Manager nadal omija je przez asset subresources. |
| Authentication / sessions / 2FA | **83/100** | Security stamp i inne stare problemy są poprawione; zostają pre-hijack oraz atomicity reset/recovery. |
| Public capability security | **55/100** | Sam token model jest znacznie lepszy, ale raw credential w logach jest P0. |
| Privacy / retention | **68/100** | Konfiguracja capture IP nie steruje realnym zapisem i retention jest niewykonane. |
| Billing / Stripe | **82/100** | Webhook/state hardening jest lepszy; live subscription vs entitlement nadal ma groźną lukę biznesową. |
| Reliability / background processing | **84/100** | HA gate istnieje, ale external I/O w transakcji i e-mail-before-commit pozostają ryzykowne. |
| Data integrity | **90/100** | Ogólnie mocne FK, ale ServiceTicket↔Inspection ma niespójny invariant. |
| Frontend static quality | **96/100** | TypeScript i ESLint w przebadanej paczce przechodzą; nie znaleziono tu P0/P1 frontendowego poza token-in-URL architecture. |
| Production readiness | **72/100** | P0 secret leakage + kilka P1 blokuje bezpieczny rollout. |

## Ocena ogólna: **81/100**

### Dlaczego 81 mimo mocnego multi-tenancy?

Bo ocena ogólna nie jest średnią „ładności kodu”.

Przy systemie obsługującym dane 100 firm wagę dominującą mają:

- confidentiality,
- authorization,
- credential handling,
- identity recovery,
- billing correctness,
- client-facing reliability.

Jeden P0 z raw bearer credentialem w logach obniża ocenę dużo bardziej niż kilka dobrych wzorców architektonicznych ją podnosi.

---

# 18. Co musi się wydarzyć, żeby wrócić w okolice 95–98/100

Bez rewrite'u systemu jest to realne.

Po zamknięciu:

1. token/log P0,
2. Manager subresource BOLA,
3. account pre-hijacking,
4. atomic reset/recovery,
5. IP policy/retention,
6. auth limiter NAT,
7. Stripe duplicate-subscription state,
8. outbox/transaction ordering,
9. service-ticket invariant,

oraz po pokazaniu zielonych regression/integration tests, ten kod może wrócić w okolice **94–97/100**.

Żeby uczciwie napisać **98/100**, chciałbym dodatkowo zobaczyć:

- backend build bez warningów bezpieczeństwa,
- pełne testy .NET,
- realny PostgreSQL integration suite,
- tenant A/B matrix,
- authz role matrix,
- concurrency tests,
- production-like reverse proxy secret leakage test,
- billing fault/concurrency tests.

Nie daję 100/100 systemowi tej klasy na podstawie samego static audit, bo static review nie dowodzi braku wszystkich luk runtime/deployment.

---

# 19. Ograniczenia audytu

## Backend

W środowisku audytu **nie ma SDK `dotnet`**, dlatego nie będę udawał, że wykonałem:

- `dotnet build`,
- `dotnet test`,
- testy EF migrations na realnym PostgreSQL.

Wnioski backendowe są oparte na:

- pełnym statycznym przeglądzie kodu,
- konfiguracji EF/migracjach,
- endpointach,
- service/repository flows,
- dostarczonych runtime logs,
- analizie przepływu autoryzacji i transakcji.

## Frontend

Wykonane w `audit9`:

- **TypeScript `tsc -b`: PASS**,
- **ESLint: PASS bez błędów** w wykonanym sprawdzeniu.

Paczka nadal zawiera artefakty takie jak `node_modules`, `dist`, `test-results` oraz backendowe runtime logs. Nie traktuję tego jako osobnego P1 Clean Code, ale w przypadku logów jest to część AUD9-001, ponieważ pakowane logi zawierają sekrety capability.

## Brak fałszywych carry-overów

Nie przenosiłem automatycznie starych problemów z audit3/audit8.

Jeśli wcześniejsza wada została poprawiona i nie znalazłem regresji, **nie ma jej w tym raporcie**.

W trakcie audytu pojawiła się hipoteza dotycząca pierwszego tokenu email verification. Po prześledzeniu pełnego request flow została odrzucona: późniejszy `SaveChangesAsync` w tym samym requestcie flushuje token. Dlatego nie została wpisana jako finding.

---

# 20. Ostateczna decyzja

## Czy w obecnej wersji audit9 dopuściłbym 100 firm z realnymi danymi?

# **Nie jeszcze.**

Nie dlatego, że multi-tenancy nadal wygląda jak w pierwszej wersji. Ten obszar jest dziś znacznie lepszy i nie znalazłem potwierdzonego standardowego A→B read.

Powód `NO-GO` jest bardziej konkretny:

1. **P0: raw capability credentials są logowane i potwierdza to dostarczony log.**
2. **P1: Manager może ominąć row-level scope przez kilka subresource endpoints.**
3. **P1: rejestracja/OAuth mają pre-hijacking weakness.**
4. **P1: password reset i recovery code nie mają atomowego single-use.**
5. **P1: privacy settings IP nie są egzekwowane.**
6. **P1: auth limiter może stworzyć normalny outage za corporate NAT.**
7. **P1: mail/link side effects mogą rozjechać się z commitem DB.**
8. **P1: Stripe może dopuścić kolejny checkout przy nadal istniejącej live subscription.**
9. **P1: ServiceTicket może dostać inspection innego assetu.**

Po naprawieniu tych punktów nie widzę obecnie potrzeby przepisywania architektury od zera. Następny audyt powinien być przede wszystkim **audytorem regresji tych dokładnych scenariuszy**, a nie ponowną listą stylistycznych zaleceń.
