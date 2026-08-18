# Tenebit — audyt kodu, architektury, bezpieczeństwa i multi-tenancy

**Data audytu:** 2026-08-16  
**Zakres:** `Tenebit code.zip` — frontend React/Vite/TypeScript + backend .NET / EF Core / PostgreSQL  
**Cel biznesowy:** ocena gotowości systemu SaaS do obsługi ok. 100 niezależnych firm, ze szczególnym naciskiem na brak wycieku danych pomiędzy organizacjami.  
**Tryb oceny:** restrykcyjny, produkcyjny. Błędy bezpieczeństwa i izolacji tenantów mają większą wagę niż estetyka architektury.

---

## 1. Werdykt końcowy

# **OCENA KOŃCOWA: 39 / 100**

# **GOTOWOŚĆ PRODUKCYJNA DLA 100 FIRM: 23 / 100 — NO-GO**

Na obecnym etapie **nie rekomenduję uruchomienia Tenebit jako wielofirmowego systemu produkcyjnego z realnymi danymi klientów**.

Kod ma sensowny szkielet i kilka naprawdę dobrych rozwiązań, ale obecnie istnieje zbyt dużo pojedynczych punktów awarii bezpieczeństwa. Najgorsze z nich dotyczą dokładnie obszaru, który w SaaS multi-tenant musi być najmocniejszy: uwierzytelniania, autoryzacji, publicznych linków oraz technicznego wymuszenia granicy `OrganizationId`.

Najważniejsza obserwacja: **izolacja tenantów jest w dużej części konwencją programistyczną, a nie właściwością systemu wymuszaną warstwowo.** Wiele repozytoriów poprawnie filtruje po `OrganizationId`, ale baza i `DbContext` nie tworzą niezależnej bariery. Wystarczy jeden przyszły błąd w query/repository, import danych lub nowy endpoint bez filtra, żeby dane firmy A mogły zostać powiązane lub odczytane w kontekście firmy B.

Dodatkowo znalazłem luki, które nie są hipotetyczne:

- dostarczone logi zawierają rzeczywiste HTTP 500,
- logowanie z niepoprawnym/null e-mailem już kończyło się wyjątkiem,
- aktualizacja pól własnych kategorii już kończyła się `DbUpdateConcurrencyException`,
- background jobs uruchamiały się przed migracją i wykonywały zapytania do kolumny, której jeszcze nie było,
- aplikacja zaczyna nasłuchiwać HTTP przed ukończeniem migracji.

To oznacza, że audyt nie wykrył wyłącznie „teoretycznych edge-case'ów”. W paczce są ślady awarii, które już wystąpiły.

---

## 2. Punktacja 0–100

| Obszar | Ocena | Werdykt |
|---|---:|---|
| **Ocena końcowa** | **39/100** | **NO-GO** |
| Gotowość produkcyjna dla 100 tenantów | **23/100** | krytycznie niewystarczająca |
| Bezpieczeństwo ogólne | **28/100** | blokery |
| Izolacja multi-tenant | **31/100** | za dużo polegania na konwencji |
| Uwierzytelnianie i sesje | **42/100** | dobry fundament, krytyczne luki konfiguracyjne/OIDC |
| Autoryzacja | **32/100** | wykryte bypassy i brakujące kontrole ról |
| Ochrona danych wrażliwych | **43/100** | maskowanie w API, ale plaintext w DB |
| Clean Architecture | **63/100** | poprawny kierunek projektów, konkretne wyłomy |
| SOLID | **47/100** | za duże klasy i zależności |
| DRY | **64/100** | zasadniczo akceptowalne, widoczne duplikacje |
| YAGNI | **68/100** | bez nadmiernej abstrakcji, choć część ręcznych implementacji szkodzi |
| KISS | **56/100** | lokalnie prosto, globalnie za dużo odpowiedzialności w serwisach |
| Clean Code | **52/100** | czytelne nazwy, ale zbyt duże klasy i endpoint file |
| Backend — poprawność/reliability | **43/100** | zaobserwowane 500 i problem migracyjny |
| Frontend — jakość/poprawność | **61/100** | typecheck OK, ale są błędy funkcjonalne i ogromne komponenty |
| Testy i QA | **42/100** | sporo unit testów, prawie brak najważniejszej warstwy integracyjnej |
| Skalowalność / HA | **36/100** | problemy z wieloma instancjami i jobami |
| Deployment / konfiguracja produkcyjna | **30/100** | fail-open, sekrety domyślne, migracje po starcie |
| Observability | **50/100** | correlation ID i logowanie są plusem, ale logi są pakowane z kodem |

### Dlaczego 39/100, mimo że Clean Architecture ma 63/100?

Oceny bezpieczeństwa nie powinny działać jak średnia szkolna. Można mieć bardzo schludny `Domain`, ale jeżeli domyślny klucz JWT pozwala w razie błędu konfiguracji podpisać token `owner` dla dowolnego `organization_id`, to struktura katalogów nie ratuje systemu.

W audycie stosuję **security gate**: krytyczne problemy mogą obniżyć gotowość wdrożeniową niezależnie od średniej jakości kodu.

---

## 3. Skala ważności

- **CRITICAL / P0** — możliwość przejęcia kont, ominięcia autoryzacji, naruszenia granicy tenantów, nieautoryzowanej modyfikacji istotnych danych albo wada, która uniemożliwia bezpieczne wdrożenie.
- **HIGH / P1** — poważny błąd bezpieczeństwa/reliability, który powinien zostać naprawiony przed produkcją.
- **MEDIUM / P2** — ryzyko jakości, bezpieczeństwa, wydajności lub utrzymania, które nie zawsze samodzielnie blokuje wdrożenie, ale zwiększa prawdopodobieństwo incydentu.
- **LOW / P3** — hardening, clean code, dług techniczny i poprawa ergonomii.

---

# 4. BLOKERY PRODUKCYJNE — CRITICAL

## AUD-001 — Publiczne wydanie/akceptacja sprzętu jest autoryzowane wyłącznie identyfikatorami UUID

**Severity:** CRITICAL  
**Kategoria:** authorization / capability links / privacy / integrity  
**Wpływ:** odczyt danych wydania, zdjęć, dokumentów i wykonanie akceptacji bez konta

### Dowód

`Tenebit.Backend/Tenebit.Api/Endpoints/TenebitEndpoints.cs:1254-1312`

Publiczne, anonimowe endpointy przyjmują:

- `organizationId`
- `assignmentId`

między innymi dla:

- odczytu wydania,
- `POST .../accept`,
- pobierania protokołu PDF,
- pobierania dokumentów procedur,
- pobierania evidence.

`Tenebit.Backend/Tenebit.Application/Assignments/AssignmentService.cs:461-543`

`GetPublicAsync`, `AcceptPublicAsync`, `GetPublicProtocolPdfAsync` oraz dokumenty korzystają z `organizationId + assignmentId` bez osobnego tokenu bezpieczeństwa.

`Tenebit.Backend/Tenebit.Infrastructure/Services/AppLinkBuilder.cs:12-16`

Link ma postać logicznie równoważną:

`/accept/{organizationId}/{assignmentId}`

### Dlaczego to jest problem

UUID v4 ma dużą entropię i nie jest łatwy do brute-force, ale **ID domenowego nie powinno jednocześnie pełnić roli nieodwoływalnego credentiala**.

Brakuje:

- osobnego losowego tokenu,
- hasha tokenu w bazie,
- `ExpiresAt`,
- `RevokedAt`,
- rotacji/regeneracji,
- ograniczenia celu tokenu,
- możliwości natychmiastowego unieważnienia linku,
- separacji „identyfikator zasobu” od „sekret dający dostęp”.

Jeżeli URL trafi do historii przeglądarki, logów, komunikatora, screenshotu, forwarded maila lub innej osoby, dostęp może być utrzymany bez właściwego mechanizmu unieważnienia.

Co istotne, w tym samym projekcie istnieje **lepszy wzorzec** dla offboardingu i audytów: `PublicTokenService`, hash tokenu i ograniczony lifecycle. To oznacza, że wydania są niespójne z własnym bezpieczniejszym standardem aplikacji.

### Naprawa

1. Dodać do assignment osobny `PublicAccessTokenHash`, `PublicAccessExpiresAt`, `PublicAccessRevokedAt`.
2. Generować minimum 256-bit token CSPRNG.
3. W DB przechowywać wyłącznie hash.
4. Publiczne endpointy powinny przyjmować token, nie `organizationId` jako credential.
5. Tenant/assignment należy odnaleźć po zweryfikowanym tokenie.
6. Dodać regenerację/revoke.
7. Ustalić rozsądny TTL.
8. Osobno rozważyć token tylko do odczytu i token/flow potwierdzenia akceptacji.
9. Dodać testy: token A nie może odczytać/zaakceptować assignment B.

**Status go-live:** BLOCKER.

---

## AUD-002 — Domyślny, znany klucz podpisujący JWT + produkcja uruchamia się mimo krytycznej konfiguracji

**Severity:** CRITICAL  
**Kategoria:** authentication / tenant takeover  
**Wpływ:** potencjalne sfałszowanie tokenu dla dowolnej firmy i dowolnej roli

### Dowód

`Tenebit.Backend/Tenebit.Api/appsettings.json:17-19`

W repozytorium znajduje się znany klucz:

`tenebit-development-signing-key-change-me-32chars`

`Tenebit.Backend/Tenebit.Api/Auth/JwtSigningKey.cs:8-11`

Przy braku konfiguracji kod **wraca do tego samego klucza**.

`Tenebit.Backend/Tenebit.Api/Program.cs:101-107`

W `Production` aplikacja wykrywa problem, ale tylko wykonuje `LogCritical` i działa dalej.

`Tenebit.Backend/Tenebit.Api/Auth/TokenIssuer.cs:14-34`

JWT zawiera m.in.:

- `organization_id`,
- role,
- user id.

### Scenariusz

Jeżeli na jednym wdrożeniu zabraknie `Auth__SigningKey`, aplikacja użyje publicznie znanego klucza z kodu.

Atakujący może wtedy skonstruować token z:

- wybranym `organization_id`,
- rolą `owner`,
- dowolnym `sub`.

To oznacza potencjalny pełny cross-tenant compromise.

### Naprawa

- W produkcji **natychmiast rzucić wyjątek i przerwać start**, jeżeli klucz jest pusty/domyslny/za krótki.
- Nie stosować fallbacku produkcyjnego.
- Trzymać sekret wyłącznie w managerze sekretów/env.
- Obrócić wszystkie używane wcześniej klucze, jeżeli istnieje choć cień możliwości, że domyślny klucz był kiedyś aktywny.
- Dodać `issuer` i `audience`, a następnie `ValidateIssuer = true`, `ValidateAudience = true`.
- Rozważyć asymetryczne podpisywanie JWT w większym wdrożeniu.

**Status go-live:** BLOCKER.

---

## AUD-003 — Izolacja tenantów nie jest centralnie wymuszona w ORM ani bazie

**Severity:** CRITICAL  
**Kategoria:** multi-tenancy / data isolation  
**Wpływ:** przyszły pojedynczy błąd zapytania może spowodować wyciek lub cross-tenant association

### Co jest dobre

Większość przejrzanych repozytoriów rzeczywiście używa wzorca:

`WHERE OrganizationId == organizationId && ...`

To jest realny plus.

### Problem

W całym backendzie nie znalazłem `HasQueryFilter` dla tenant-aware entities.

Nie znalazłem również PostgreSQL Row Level Security (`CREATE POLICY`, `ENABLE ROW LEVEL SECURITY`).

Wiele relacji w DB jest zbudowanych tylko po zwykłym `Id`, mimo że child również posiada `OrganizationId`.

Przykłady z `TenebitDbContext.cs`:

- `AssetAuditParticipant -> AssetAuditCampaign`: FK tylko `CampaignId` (`~122`),
- `AssetAuditItem -> Campaign/Participant`: tylko ID (`~137-138`),
- reservation -> items: tylko `ReservationId` (`~162`),
- `OffboardingItem -> OffboardingCase`: tylko `OffboardingCaseId` (`~213`),
- `AssetEvidence -> Asset`: tylko `AssetId` (`~232`),
- `AssetEvidence -> Assignment`: tylko `AssignmentId` (`~233`),
- `Procedure -> Documents`: tylko `ProcedureId` (`~523`),
- owned assignment rows: relacja po `AssignmentId` (`~617-636`).

### Dlaczego indeks `(OrganizationId, XId)` nie wystarcza

Indeks zwiększa szybkość zapytania. **Nie jest constraintem zapewniającym zgodność tenantów.**

Baza może przyjąć rekord:

- `child.OrganizationId = firma_A`
- `child.ParentId = rekord_firmy_B`

jeżeli FK sprawdza jedynie `ParentId`.

### Wymagany standard dla SaaS

W systemie, którego największą obawą jest „firma A nigdy nie zobaczy firmy B”, tenant boundary powinien mieć minimum 2–3 warstwy ochrony:

1. Application/use-case.
2. ORM/repository.
3. DB constraint / RLS.

Obecnie dominują warstwy 1–2 i są zależne od dyscypliny każdego programisty.

### Naprawa

Rekomenduję:

- `ITenantContext`/request-scoped tenant context,
- global query filters dla tenant entities w request path,
- jawny i audytowalny bypass tylko dla background/admin/system jobs,
- PostgreSQL RLS jako dodatkową barierę, jeżeli model deploymentu na to pozwala,
- alternate key `(OrganizationId, Id)` na parentach,
- composite FK `(OrganizationId, ParentId)` w tenant-owned relacjach,
- automatyczne testy migracji sprawdzające obecność tenant constraints,
- suite integracyjną tenant A / tenant B.

**Status go-live:** BLOCKER dla systemu przechowującego dane wielu klientów w jednej bazie.

---

## AUD-004 — LocationEndpoints omija Application Layer i nie ma właściwej kontroli ról dla mutacji

**Severity:** CRITICAL/HIGH  
**Kategoria:** authorization / architecture  
**Wpływ:** zwykły zalogowany użytkownik może modyfikować lokalizacje organizacji

### Dowód

`Tenebit.Backend/Tenebit.Api/Endpoints/LocationEndpoints.cs:14-188`

Endpointy używają bezpośrednio:

- `TenebitDbContext`,
- `Database.GetDbConnection()`,
- ręcznie pisanego SQL.

`POST /locations`, `PUT /locations/{id}`, `DELETE /locations/{id}` nie mają własnej kontroli `AccessPolicy`.

Grupa `/api` wymaga tylko uwierzytelnienia (`TenebitEndpoints.cs:48-49`).

Frontend natomiast traktuje zakładkę locations jako `organizationOnly` i pokazuje ją tylko użytkownikowi uprawnionemu do zarządzania organizacją:

`Tenebit.Frontend/src/pages/SettingsPage.tsx:34,465`.

### Wniosek

Frontend zakłada ograniczenie, którego backend nie egzekwuje.

**UI nie jest security boundary.**

Dowolny authenticated user może ręcznie wywołać endpoint.

### Naprawa

- przenieść logikę do `LocationService` w Application,
- dodać interfejs repozytorium,
- wymusić role np. `Owner/Admin` zgodnie z wymaganiem produktu,
- read inventory również jawnie sklasyfikować,
- dodać HTTP integration test z rolą Employee -> `403` dla POST/PUT/DELETE.

**Status go-live:** BLOCKER, jeżeli employee nie powinien zarządzać strukturą firmy.

---

## AUD-005 — Pobranie protokołu assignment omija kontrolę ról

**Severity:** CRITICAL/HIGH  
**Kategoria:** authorization / privacy

`AssignmentService.ListAsync` i `GetAsync` sprawdzają `TenebitRoles.AssignmentViewers`:

`AssignmentService.cs:59-92`.

Natomiast:

`AssignmentService.cs:458-459`

`GetProtocolPdfAsync` wywołuje bezpośrednio `BuildProtocolPdfAsync(_currentUser.OrganizationId, id, ...)` bez `AccessPolicy`.

Endpoint jest authenticated przez grupę, ale nie ma role gate.

PDF zawiera m.in.:

- imię i nazwisko osoby,
- stanowisko/team,
- listę aktywów,
- asset tag,
- serial,
- condition,
- procedury,
- notatki.

Patrz `AssignmentService.cs:545-583`.

### Ryzyko

Zalogowany użytkownik, który nie ma prawa oglądać assignments, może próbować pobrać protokół po UUID.

### Naprawa

- identyczna kontrola `AssignmentViewers` przed generacją protokołu,
- jeżeli pracownik ma mieć dostęp tylko do swojego protokołu: osobny policy/use-case z weryfikacją ownership,
- integration tests dla wszystkich ról.

**Status go-live:** BLOCKER dla prywatności i poprawności autoryzacji.

---

## AUD-006 — Tokeny OIDC Google/Microsoft/Apple są tylko dekodowane, a nie kryptograficznie walidowane

**Severity:** CRITICAL/HIGH  
**Kategoria:** OAuth/OIDC / account linking

### Dowód

`Tenebit.Backend/Tenebit.Api/Auth/OAuth/ExternalAuthService.cs:113-147`

Po code exchange:

- pobierany jest `id_token`,
- `JwtSecurityTokenHandler.ReadJwtToken(idToken)` tylko go parsuje,
- kod odczytuje `sub`, `email`, `email_verified`, `name`.

Brakuje jawnej walidacji:

- podpisu przez JWKS providera,
- `iss`,
- `aud`,
- `exp/nbf`,
- nonce.

Dla Apple `emailVerified` jest ustawiane na `true` bez odczytu zweryfikowanej wartości (`ExternalAuthService.cs:144`).

`AuthService.ExternalLoginAsync:330-344` automatycznie podłącza provider do istniejącego konta, jeżeli e-mail pasuje i `EmailVerified == true`.

### Co obniża ryzyko

Kod:

- robi server-side authorization code exchange,
- używa PKCE,
- ma jednorazowy state,
- wiąże state z providerem,
- używa stałych endpointów providerów.

To są dobre elementy.

Nie wystarczają jednak jako substytut kompletnej walidacji OIDC tokenu.

### Naprawa

Nie implementować OIDC ręcznie. Użyć standardowej biblioteki/provider middleware albo pełnej walidacji na metadata + JWKS.

Obowiązkowo:

- signature validation,
- issuer,
- audience/client id,
- lifetime,
- nonce,
- poprawne semantics `email_verified`.

---

# 5. HIGH — poważne problemy wymagające naprawy przed produkcją

## AUD-007 — Brak centralnej walidacji requestów; potwierdzony HTTP 500 przy loginie

**Severity:** HIGH

W backendzie nie znalazłem:

- FluentValidation,
- `IValidator<T>`,
- spójnego endpoint filtera walidującego DTO,
- kompletnej DataAnnotations pipeline.

DTO z typem `string` nie gwarantuje, że klient HTTP nie prześle `null`.

`OrganizationUserRepository.cs:25-26` wykonuje:

`email.Trim().ToLowerInvariant()`

przed bezpieczną walidacją.

### Potwierdzenie w dostarczonych logach

`Tenebit.Backend/Tenebit.Api/logs/tenebit-20260726.log:19471-19502`

Wystąpiło:

- `NullReferenceException`,
- stack trace do `OrganizationUserRepository.FindByEmailAsync`,
- `POST /api/auth/login` -> **500**.

To nie jest potencjalny bug. On już wystąpił.

### Szerszy problem

Podobne `.Trim()` na wejściu istnieją w wielu miejscach, np. custom field keys.

Brak centralnej walidacji długości oznacza również możliwość dotarcia z nadmiernie długą wartością do constraintu DB i otrzymania 500 zamiast 400.

### Naprawa

- walidator dla każdego request DTO,
- required/null/empty,
- max length zgodne z DB,
- format e-mail/url,
- enum/range,
- page/pageSize,
- collection size,
- guid semantics,
- globalne mapowanie validation -> `400`.

---

## AUD-008 — Upload plików jest w całości buforowany w RAM przed sprawdzeniem limitu 5 MB

**Severity:** HIGH  
**Kategoria:** DoS / uploads

Przykłady:

`TenebitEndpoints.cs:1331-1350`  
`TenebitEndpoints.cs:1381-1400`

Kod:

1. `ReadFormAsync`,
2. otwiera stream,
3. `CopyToAsync(memory)`,
4. `memory.ToArray()`,
5. dopiero serwis sprawdza `content.LongLength > 5 MB`.

Nie znalazłem:

- `RequestSizeLimit`,
- `RequestFormLimits`,
- `MultipartBodyLengthLimit`.

### Problem

Limit biznesowy po zaalokowaniu całego payloadu nie chroni serwera przed dużym requestem.

Dodatkowo plik obrazu o małym rozmiarze skompresowanym może mieć ogromne dimensions/pixel count po dekodowaniu.

### Naprawa

- limity Kestrel/reverse proxy,
- endpoint-specific body limits,
- kontrola `IFormFile.Length` przed kopiowaniem,
- bounded streaming,
- limity dimensions/pixel count decodera,
- timeouts i cancellation,
- oddzielny rate limit uploadów.

---

## AUD-009 — Reset hasła nie unieważnia aktywnych refresh tokens i trusted devices

**Severity:** HIGH

`AuthService.ResetPasswordAsync:420-443`

Po zmianie hasła kod:

- ustawia nowy hash,
- oznacza reset token jako użyty,
- zapisuje.

Nie widać revoke:

- refresh tokens,
- device trust tokens.

Refresh token żyje do 30 dni (`AuthService.cs:534-569`).

### Scenariusz

Użytkownik zmienia hasło, bo podejrzewa przejęcie konta. Atakujący posiada refresh cookie. Po resecie hasła stara sesja może nadal być odnawiana.

### Naprawa

Przy resecie hasła:

- revoke wszystkie refresh tokens użytkownika,
- revoke trusted devices,
- opcjonalnie zwiększyć `SecurityStamp/SessionVersion` w userze,
- access token powinien uwzględniać version lub mieć bardzo krótki TTL.

---

## AUD-010 — Stripe webhook: brak ochrony przed replay i brak idempotencji/event ordering

**Severity:** HIGH

`StripePaymentGateway.cs:143-178` sprawdza HMAC poprawnie i używa fixed-time comparison — to plus.

Brakuje jednak sprawdzenia świeżości `t=` z `Stripe-Signature`.

`SubscriptionService.cs:194-228` nie zapisuje identyfikatora eventu jako przetworzonego.

Brakuje:

- tolerance timestamp,
- unique event id,
- idempotency,
- ochrony przed out-of-order stale event.

### Ryzyko

Poprawnie podpisany, stary event może zostać odtworzony.

Duplicate/out-of-order event może nadpisać nowszy stan subskrypcji.

### Dodatkowy problem

`CreateCheckoutSessionAsync(successUrl, cancelUrl)` i billing portal przyjmują return URL z requestu. URL powinien być allowlistowany do oficjalnego frontendu.

---

## AUD-011 — Rate limiting auth/public jest globalny, a nie partycjonowany per caller

**Severity:** HIGH/MEDIUM

`Program.cs:63-78`

Zdefiniowane są named fixed-window limiters:

- auth: 10/min,
- public: 60/min.

Nie ma `PartitionedRateLimiter` z kluczem klienta.

### Ryzyko

- jedna osoba może zużyć wspólny limit,
- użytkownicy różnych firm mogą wpływać na siebie,
- łatwy denial-of-service na auth/public w skali całej instancji.

### Naprawa

- anonymous: trusted client IP + endpoint family,
- auth: IP + normalized identity where appropriate,
- authenticated: `(OrganizationId, UserId)` / endpoint,
- dodatkowe zasady dla forgot password, public reporting, uploads.

---

## AUD-012 — Aplikacja ufa surowemu `X-Forwarded-For`

**Severity:** HIGH/MEDIUM

`CurrentUser.cs:32-44`

Kod ręcznie czyta pierwszy `X-Forwarded-For` i traktuje jako IP klienta.

Nie znalazłem `UseForwardedHeaders` ani konfiguracji trusted proxies/networks.

### Wpływ

Klient, który może dotrzeć do backendu lub proxy przekazującego nagłówek bez sanitacji, może spoofować IP zapisywany jako część recordu potwierdzenia.

Komentarz w kodzie określa te rekordy jako „tamper-evident confirmation records”, więc jakość źródła IP ma znaczenie.

### Naprawa

- `ForwardedHeadersOptions`,
- `KnownProxies/KnownNetworks`,
- dopiero potem `RemoteIpAddress`,
- nie czytać XFF ręcznie jako źródła zaufanego.

---

## AUD-013 — `/health/ready` ujawnia wewnętrzny tekst wyjątku anonimowemu klientowi

**Severity:** HIGH/MEDIUM

`TenebitEndpoints.cs:56-70`

Catch zwraca:

`detail = ex.Message`

### Ryzyko

Możliwe ujawnienie:

- nazwy hosta DB,
- nazwy tabeli/kolumny,
- błędu uwierzytelnienia,
- elementów topologii infrastruktury.

### Naprawa

Publicznie tylko:

- `ready` / `unready`,
- generic code.

Szczegóły wyłącznie w chronionym logu z correlation id.

---

## AUD-014 — Aplikacja zaczyna nasłuchiwać przed migracją bazy

**Severity:** HIGH

`Program.cs:145-153`

Kolejność:

1. `await app.StartAsync()`
2. dopiero potem `InitializeDatabaseAsync()`
3. wyjątek migracji tylko logowany, proces działa dalej.

### Potwierdzony incydent w paczce

`logs/tenebit-20260816.log`

- linie ~6004-6006: serwer zaczyna słuchać 8080 i 5000,
- linie ~6014+: background services zapytują o `QrLabelShowName`,
- linie ~6026+: PostgreSQL: kolumna nie istnieje,
- linie ~6233/~6262/~6291/~6320: błędy kilku background jobs,
- dopiero ~6343 zostaje zaaplikowana migracja `20260816141723_AddQrLabelSettings`.

### To jest błąd projektu startupu

Nie jest poprawne „utrzymywanie aplikacji online, żeby health i logi działały”, jeżeli proces obsługuje jednocześnie ruch biznesowy i uruchamia joby na niezgodnym schemacie.

### Naprawa

Najlepiej:

- migracja jako osobny deployment step/job przed startem aplikacji.

Alternatywnie dla mniejszego systemu:

- migrate przed udostępnieniem listenera,
- fail startup po błędzie krytycznym,
- readiness nigdy nie może być true, dopóki expected migration set nie jest zastosowany.

---

## AUD-015 — Domyślne hasło PostgreSQL i ponownie fail-open w Production

**Severity:** HIGH

`appsettings.json:2-4`:

`Username=postgres;Password=postgres`

`Program.cs:109-113` tylko loguje `Critical`, ale aplikacja działa.

### Naprawa

- brak produkcyjnego fallbacku,
- fail startup,
- dedicated DB user z minimalnymi uprawnieniami,
- osobna rola migration i runtime, jeśli możliwe,
- secret manager,
- zakaz używania superusera aplikacyjnego.

---

## AUD-016 — Stan OAuth i 2FA jest tylko w pamięci pojedynczej instancji

**Severity:** HIGH dla HA / MEDIUM dla jednej repliki

`OAuthStateStore.cs:7-33` -> `IMemoryCache`  
`TwoFactorChallengeStore.cs:6-32` -> `IMemoryCache`

### Problem

Przy dwóch replikach:

1. `/start` trafi na replica A,
2. callback może trafić na B,
3. state nie istnieje -> login fail.

To samo dla challenge 2FA.

### Naprawa

Redis/`IDistributedCache` lub inny współdzielony ephemeral store z atomic consume.

---

## AUD-017 — Background jobs uruchamiają się na każdej replice bez leader lock

**Severity:** HIGH dla multi-instance

`Tenebit.Infrastructure/DependencyInjection.cs:67-70`

Zarejestrowane są m.in.:

- `AlertBackgroundService`,
- `DashboardSnapshotBackgroundService`,
- `OffboardingBackgroundService`,
- `EvidenceRetentionBackgroundService`.

Nie znalazłem distributed lock / scheduler coordination.

### Wpływ

Przy 2–3 instancjach API każda wykona ten sam cykl.

Potencjalne skutki:

- wielokrotne e-maile,
- duplicate actions,
- race przy retencji,
- konkurencyjne snapshoty,
- większy load DB.

### Naprawa

- osobny worker,
- durable job scheduler,
- leader election/distributed lock,
- idempotent job steps.

---

## AUD-018 — Evidence może wskazywać assignment, do którego podany asset nie należy

**Severity:** HIGH/MEDIUM

`AssetEvidenceService.UploadAsync:73-103`

Kod sprawdza:

- czy asset istnieje w organizacji,
- czy `AssignmentId` istnieje w organizacji.

Nie sprawdza, czy **ten asset jest elementem tego assignmentu**.

### Wpływ

Można utworzyć logicznie fałszywy chain of evidence:

- asset A,
- assignment B,
- oba z jednego tenanta,
- ale niezwiązane biznesowo.

Dla protokołów/integrity/audit trail jest to wada integralności.

### Naprawa

`assignment.Assets.Any(x => x.AssetId == assetId)` oraz kontrola phase/status.

---

## AUD-019 — Lista użytkowników organizacji nie ma server-side role gate

**Severity:** HIGH/MEDIUM

`UserAccessService.cs:49-53`

`ListAsync` zwraca listę users bez `AccessPolicy`.

`TenebitEndpoints.cs:704-713`

`GET /organization-users` i `GET /settings/users` wymagają tylko ogólnego authentication.

Frontend pokazuje zakładkę users tylko w management context (`SettingsPage.tsx:34,634+`).

### Ryzyko

Authenticated employee może pobrać katalog kont firmy, e-maile, display names, roles, jeśli endpoint zostanie wywołany ręcznie.

### Naprawa

Jeżeli katalog nie jest funkcją dla każdego pracownika: `Owner/Admin` na backendzie.

---

## AUD-020 — „Kopiuj publiczny link” offboardingu generuje zły link

**Severity:** HIGH funkcjonalny

`Tenebit.Frontend/src/pages/OffboardingPage.tsx:379-389`

Frontend kopiuje:

`/exit/${caseItem.id}`

Tymczasem publiczny offboarding działa na **raw tokenie**, nie case id.

Backend celowo przechowuje tylko hash tokenu, więc raw token nie jest możliwy do późniejszego odczytania.

### Skutek

Przycisk może generować link, który nie działa.

### Poprawny model

- „Wygeneruj/regeneruj link” -> backend tworzy nowy raw token,
- zapisuje hash,
- zwraca raw token **jednorazowo**,
- poprzedni link zostaje unieważniony.

---

## AUD-021 — Aktualizacja custom fields kategorii już powodowała `DbUpdateConcurrencyException` / HTTP 500

**Severity:** HIGH

Dostarczony log `tenebit-20260726.log`:

- ~1747-1762: `DbUpdateConcurrencyException`,
- ~2676: `PUT /api/asset-categories/{id}/fields responded 500`.

Aktualny kod:

`AssetCategoryService.cs:98-120`

wywołuje:

`category.ReplaceFieldDefinitions(...)`.

Domain:

`AssetCategory.cs:58-66`

robi:

- `FieldDefinitions.Clear()`,
- dodaje nowy zestaw elementów.

EF mapuje owned collection z composite key `(CategoryId, Id)`.

### Wniosek

Mechanizm replace owned collection musi zostać zweryfikowany z realnym PostgreSQL. Dostarczony log pokazuje, że przynajmniej jedna wersja tej ścieżki była wadliwa.

### Naprawa

- explicit diff existing/new,
- jawne remove/add,
- albo osobna encja/repository,
- integration test na PostgreSQL:
  - dodanie pól,
  - zmiana kolejności,
  - edycja,
  - usunięcie,
  - replace wszystkich,
  - dwa równoległe requesty.

---

## AUD-022 — Runtime logi zostały dostarczone razem z kodem

**Severity:** HIGH/MEDIUM operational security

Paczka zawiera:

`Tenebit.Backend/Tenebit.Api/logs/`

W logach znajdują się:

- stack traces,
- SQL,
- nazwy schematów,
- lokalne ścieżki `D:\Tenebit\...`,
- request paths,
- correlation IDs,
- szczegóły migracji i błędów DB.

Parametry SQL były zwykle zamaskowane jako `?`, co jest plusem.

Mimo tego **runtime logs nie powinny być częścią paczki źródłowej**.

Nie znalazłem `.gitignore` w dostarczonym drzewie.

### Naprawa

Ignorować/przestać pakować:

- `logs/`,
- `node_modules/`,
- `dist/`,
- `bin/`,
- `obj/`,
- secret/env files.

---

## AUD-023 — OpenAPI schema jest wystawiana również poza Development

**Severity:** MEDIUM

`Program.cs:130`

`app.MapOpenApi()` wykonywane zawsze.

Tylko UI Scalar jest ograniczone do Development.

Nie jest to sama w sobie krytyczna luka, ale zwiększa powierzchnię informacyjną produkcji i ułatwia enumerację endpointów.

### Rekomendacja

Jeżeli publiczne API docs nie są funkcją produktu, wyłączyć schema endpoint w produkcji lub chronić go.

---

## AUD-024 — Brak aplikacyjnego TLS/HSTS/forwarded headers i `AllowedHosts = *`

**Severity:** HIGH/MEDIUM zależnie od deploymentu

`Program.cs:29-31`

Backend binduje:

- `http://0.0.0.0:8080`
- `http://0.0.0.0:5000`

Nie znalazłem:

- `UseHttpsRedirection`,
- `UseHsts`,
- `UseForwardedHeaders`.

`appsettings.json:55`:

`AllowedHosts = *`.

### Interpretacja

Jeżeli TLS terminowany jest na nginx/reverse proxy i backend jest dostępny wyłącznie w prywatnej sieci Docker, HTTP wewnątrz może być świadomą decyzją.

Ale **repozytorium nie egzekwuje tego założenia**.

### Wymagania produkcyjne

- backend nie może być publicznie wystawiony po HTTP,
- trusted proxy config,
- host allowlist,
- HSTS na warstwie ingress/proxy,
- security headers.

---

## AUD-025 — Access token OAuth trafia do URL fragmentu

**Severity:** MEDIUM

`ExternalAuthEndpoints.cs:117-118`

Redirect:

`/auth/callback#token=...`

Fragment jest lepszy od query string, ponieważ nie jest standardowo wysyłany do serwera jako część request target/referrer.

Nadal jednak bearer token:

- jest dostępny dla JS strony callback,
- pozostaje elementem URL/history przez pewien czas,
- jest bardziej podatny na wyciek przy XSS/extension/telemetrii niż one-time code.

### Lepszy wzorzec

Backend powinien wydać krótko żyjący jednorazowy authorization code, a frontend wymienić go na token/session.

---

## AUD-026 — Sekret TOTP jest przechowywany plaintext w DB

**Severity:** HIGH/MEDIUM

`TenebitDbContext.cs:322`

`TotpSecret` jest zwykłym stringiem.

### Problem

Hashowanie TOTP secret nie jest możliwe, bo serwer potrzebuje sekretu do weryfikacji kodu. Powinien jednak być **szyfrowany odwracalnie** kluczem przechowywanym poza bazą.

W razie wycieku DB plaintext TOTP secret pozwala generować prawidłowe kody 2FA.

### Naprawa

- envelope encryption / KMS,
- application-level encryption,
- key rotation,
- nigdy nie logować sekretu.

**Uwaga:** użycie HMAC-SHA1 w samym TOTP nie jest tu uznane za błąd — jest zgodne z powszechnym standardem TOTP. Problemem jest storage secretu.

---

## AUD-027 — „Sensitive” custom fields i license keys są maskowane w API, ale leżą plaintext w bazie

**Severity:** HIGH/MEDIUM

Custom fields:

`TenebitDbContext.cs:434-440` -> `asset_field_values.Value` plaintext.

`AssetService.cs:317-347` poprawnie:

- maskuje pola `Sensitive` w normalnej odpowiedzi,
- ma osobny reveal,
- sprawdza role,
- loguje reveal.

To jest **dobry model aplikacyjny**.

Jednak DB nadal zawiera wartość plaintext.

License:

`TenebitDbContext.cs:593` -> `LicenseKey` plaintext.

`LicenseService.cs:124-152` dobrze kontroluje, kto może dostać key w response, ale storage pozostaje plaintext.

### Rekomendacja

Dla danych rzeczywiście „Sensitive” oraz kluczy licencyjnych zastosować field-level encryption at rest z kluczem poza DB.

---

## AUD-028 — Concurrency jest mapowana do wyjątku, ale nie do kontrolowanej odpowiedzi 409

**Severity:** MEDIUM/HIGH

`TenebitDbContext.cs:28-37`

`DbUpdateConcurrencyException` jest zamieniany na własny `ConcurrencyException`.

Nie znalazłem handlera tego wyjątku.

Globalny exception handler w `Program.cs:119-127` zamienia każdy nieobsłużony wyjątek na `500`.

### Skutek

Konflikt równoległej edycji to przewidywalny przypadek biznesowy, a klient otrzymuje INTERNAL_ERROR.

### Naprawa

- `ConcurrencyException` -> HTTP 409,
- stabilny error code,
- UI może pokazać „rekord się zmienił, odśwież”,
- row version/etag dla obiektów wymagających konkurencyjnej edycji.

---

## AUD-029 — Ręczna implementacja Stripe zwiększyła ryzyko i omija `IHttpClientFactory`

**Severity:** MEDIUM

Projekt rejestruje `AddHttpClient()`, ale `StripePaymentGateway` używa własnego `HttpClient` zamiast typed client/factory.

Singleton `HttpClient` sam w sobie nie jest katastrofalnym socket leak, ale:

- omija configured handlers,
- trudniej centralnie ustawić timeout/retry/telemetry,
- trudniej testować,
- ręczna obsługa podpisu już pominęła replay tolerance/idempotency.

### KISS/YAGNI

Tutaj „mniej zależności” nie oznacza prostszego systemu. Kod ręcznie implementujący protokół płatności jest bardziej ryzykowny niż mała, sprawdzona abstrakcja/SDK.

---

## AUD-030 — Publiczne zgłoszenie problemu z QR można wykorzystać do spamu powiadomień

**Severity:** MEDIUM

`/public/assets/{organizationId}/{assetId}/report`

`AssetService.ReportPublicIssueAsync:240-272`

Plusy:

- wiadomość jest HTML-encode'owana,
- docelowi odbiorcy są ograniczeni do active owner/admin.

Minus:

- endpoint jest anonimowy,
- bazuje na wspólnym public rate limiterze,
- brak per-IP/per-asset cooldown,
- każde zgłoszenie może wysłać e-maile do wielu administratorów.

### Naprawa

- partitioned limit,
- cooldown per asset/IP,
- abuse detection,
- opcjonalnie CAPTCHA po przekroczeniu progu.

---

## AUD-031 — Globalna unikalność e-maila uniemożliwia jednemu człowiekowi członkostwo w wielu organizacjach

**Severity:** MEDIUM — ograniczenie modelu, nie luka

`TenebitDbContext.cs:323-324`

Istnieją jednocześnie:

- unique `(OrganizationId, Email)`,
- unique `Email` globalnie.

Auth robi globalne `FindByEmailAsync`.

### Konsekwencja

Jedna osoba nie może mieć konta z tym samym e-mailem w dwóch firmach.

Dla zwykłego SaaS może to być zamierzone. Dla:

- konsultantów,
- MSP,
- administratorów grupy kapitałowej,
- księgowych/outsourcingu

może być poważnym ograniczeniem.

Wymaga decyzji produktowej teraz, zanim dane użytkowników zaczną rosnąć.

---

## AUD-032 — Race condition przy limicie liczby aktywów planu

**Severity:** MEDIUM/HIGH biznesowy

`AssetService.cs:122-156`

Flow:

1. pobierz subscription,
2. `ListAsync` wszystkie aktywa,
3. `Count`,
4. jeżeli poniżej limitu -> utwórz.

Dwa równoległe requesty mogą oba zobaczyć np. 99/100 i oba utworzyć rekord -> 101/100.

### Dodatkowo

Do sprawdzenia limitu pobierana jest pełna lista assets zamiast `COUNT(*)`.

### Naprawa

- repozytoryjne `CountAsync`,
- atomiczny model enforcementu,
- odpowiednia transakcja/lock/counter lub mechanizm z gwarancją przy concurrency.

---

## AUD-033 — Background jobs robią organizacyjne skany, co będzie rosnąć liniowo wraz z tenantami

**Severity:** MEDIUM

Background services iterują przez organizacje i wykonują cykle aplikacyjne.

Dla 100 firm może to jeszcze działać, ale model:

- jedna instancja,
- cykliczny pełny scan,
- brak kolejki,
- brak partitioningu,
- brak leader lock

jest słabą bazą do dalszego wzrostu.

### Rekomendacja

- paging tenants,
- osobne job messages per tenant,
- idempotency,
- metrics czasu per tenant,
- circuit breaker / error isolation per tenant.

---

## AUD-034 — Location list/inventory ładuje wszystkie aktywa i wszystkie osoby tenanta do pamięci

**Severity:** MEDIUM

`LocationEndpoints.cs:14-19`  
`LocationEndpoints.cs:173-187`

Dla zwykłego GET locations/inventory pobierane są pełne listy assets i people, a liczenie/filtering odbywa się in-memory.

### Skutek

Koszt jednego requestu rośnie z całą firmą, nie z jedną lokalizacją.

### Naprawa

- SQL `GROUP BY` / count per path/location,
- inventory query po konkretnej location,
- paginacja inventory.

---

## AUD-035 — Ograniczenie głębokości lokalizacji do 20 jest niejawne i może osłabiać cycle detection

**Severity:** MEDIUM

`LocationEndpoints.cs:81-91` oraz `224-238`.

Ancestor traversal i `FullPath` mają guard `20`.

Jeżeli drzewo przekroczy 20 poziomów:

- cycle check może nie dojść do starszego przodka,
- path zostanie obcięty logicznie,
- zachowanie staje się zależne od magicznej liczby.

### Naprawa

Albo:

- jawnie ograniczyć depth w domain/DB do np. 10 i zwracać validation error,

albo:

- poprawnie wykrywać visited IDs bez arbitralnego guardu.

---

## AUD-036 — Token JWT nie waliduje issuer ani audience

**Severity:** MEDIUM/HIGH hardening

`Program.cs:85-93`

- `ValidateIssuer = false`
- `ValidateAudience = false`

Sam signing key zapewnia główną kryptograficzną barierę, ale brak issuer/audience zwiększa ryzyko token confusion przy rozbudowie systemu i integracji.

### Naprawa

Ustawić i walidować:

- `ValidIssuer`,
- `ValidAudience`,
- lifetime,
- clock skew świadomie.

---

## AUD-037 — Role/deaktywacja użytkownika nie unieważniają natychmiast istniejącego access tokenu

**Severity:** MEDIUM

Access token ma 30 minut (`TokenIssuer.cs:29-32`).

Autoryzacja opiera się na rolach z JWT.

Po:

- odebraniu roli,
- dezaktywacji usera,

stary access token zachowuje claims do expiry.

Refresh sprawdza `user.IsActive`, co jest poprawne, ale nie cofa już wystawionego access tokenu.

### Rekomendacja

Dla panelu biznesowego 30 min może być akceptowalnym kompromisem, ale należy świadomie określić SLA revocation.

Opcje:

- krótszy access token,
- security/session version,
- token introspection dla bardzo wrażliwych akcji.

---

## AUD-038 — Rejestracja i inne check-then-insert ścieżki nie obsługują poprawnie konfliktu DB przy concurrency

**Severity:** MEDIUM

`AuthService.RegisterAsync`:

1. `FindByEmailAsync`,
2. później insert,
3. catch obejmuje `DomainException`, nie unique constraint exception.

Dwa równoległe requesty z tym samym e-mailem mogą oba przejść pre-check, a jeden zakończy się DB error -> 500.

Analogiczny wzorzec istnieje w różnych unique-name/tag flow.

### Naprawa

- constraints w DB zostawić — są konieczne,
- catch/mapować expected unique violation -> `409 CONFLICT`,
- pre-check traktować wyłącznie jako UX optimization.

---

## AUD-039 — Konfiguracja `AutoCreate=true` i `Seed.Enabled=true` znajduje się w bazowym appsettings

**Severity:** MEDIUM/HIGH operational

`appsettings.json:5-10`

Base config jest współdzielony przez środowiska. Produkcja zależy od tego, czy operator pamięta o override.

### Naprawa

Bezpieczne defaulty powinny być produkcyjne:

- `AutoCreate=false`,
- `Seed.Enabled=false`,
- development opt-in w `appsettings.Development.json`.

Zasada: **bezpieczne zachowanie ma być defaultem**, nie specjalną konfiguracją.

---

## AUD-040 — Brak realnej warstwy HTTP/integration tests dla autoryzacji i tenant isolation

**Severity:** HIGH dla projektu multi-tenant

W repozytorium są **41 pliki backendowych testów** — to istotny plus.

Nie znalazłem jednak:

- `WebApplicationFactory`,
- `TestServer`,
- real PostgreSQL/Testcontainers setup.

Większość logiki testowana jest z `InMemoryRepositories`.

### Co to oznacza

Unit test service może być zielony, a aplikacja nadal mieć:

- endpoint bez policy,
- zły `.AllowAnonymous()`,
- złą konfigurację auth middleware,
- zły query filter,
- wadliwy FK,
- błąd migracji,
- realny EF tracking/concurrency bug.

Dokładnie takie klasy problemów zostały znalezione w tym audycie.

### Minimalny wymagany zestaw

Dla KAŻDEGO zasobu chronionego tenantem:

- create jako tenant A,
- read przez A -> 200,
- read po ID jako B -> 404/403,
- update jako B -> 404/403,
- delete jako B -> 404/403,
- list jako B nie zawiera A,
- export B nie zawiera A,
- download B nie zawiera A,
- nested resource A+B mismatch -> fail,
- public token A nie działa na B.

Test musi działać na PostgreSQL, nie tylko fake repository.

---

# 6. MEDIUM / LOW — jakość kodu, utrzymanie, spójność

## AUD-041 — Zbyt duże klasy łamią SRP i zwiększają blast radius zmian

Największe istotne pliki produkcyjne:

Backend:

- `TenebitEndpoints.cs` — **1499 linii**,
- `OffboardingService.cs` — ok. **1025 linii**,
- `AssetAuditCampaignService.cs` — ok. **734**,
- `AlertCheckService.cs` — ok. **676**,
- `TenebitDbContext.cs` — ok. **668**,
- `AssignmentService.cs` — ok. **609**,
- `AuthService.cs` — ok. **583**,
- `PdfProtocolGenerator.cs` — ok. **459**,
- `AssetService.cs` — ok. **429**.

Frontend:

- `AssetsPage.tsx` — **1309 linii**,
- `PeoplePage.tsx` — **755**,
- `SettingsPage.tsx` — **740**,
- `OffboardingPage.tsx` — **698**,
- `AssetAuditsPage.tsx` — **593**,
- `api/endpoints.ts` — **397**.

### Ocena SOLID

To nie jest automatycznie „zły kod”, ale liczba odpowiedzialności jest za duża.

`OffboardingService` obsługuje kilkanaście różnych przypadków użycia, integracje, public token flow, PDF/evidence i lifecycle.

### Rekomendacja

Nie przechodzić w skrajny CQRS/architecture astronautics. Wystarczy rozbić na feature/use-case services, np.:

- `CreateOffboardingCase`,
- `ManageOffboardingItem`,
- `PublicOffboarding`,
- `OffboardingProtocol`,
- `OffboardingAutomation`.

---

## AUD-042 — Ukryta zależność przez `new AssetReturnDispositionService(...)` wewnątrz AssignmentService

**Severity:** MEDIUM / SOLID-DIP

`AssignmentService.cs:56`

Serwis tworzy konkretny `AssetReturnDispositionService` ręcznie zamiast otrzymać zależność.

### Problem

- hidden dependency,
- trudniejsze testy,
- `AssignmentService` zna detal implementacji,
- DI graph nie odzwierciedla realnych zależności.

### Naprawa

Wstrzyknąć interfejs lub konkretny application service, jeżeli abstrakcja nie daje wartości.

---

## AUD-043 — Duplikacja kodu CSV

**Severity:** LOW/MEDIUM / DRY

Podobny/identyczny mechanizm RFC4180 escaping i formula-injection protection występuje w więcej niż jednym serwisie, m.in.:

- `AssetService.cs`,
- `AssetAuditCampaignService.cs`.

Sam mechanizm jest dobry — szczególnie ochrona przed wartościami zaczynającymi się od `=`, `+`, `-`, `@`, tab/newline.

Powinien jednak być jednym, przetestowanym utility `CsvWriter/CsvCellEscaper`.

---

## AUD-044 — ErrorMessageTranslator opiera lokalizację na dokładnym tekście komunikatu

**Severity:** LOW/MEDIUM

To jest brittle coupling: zmiana polskiej treści w Domain/Application może złamać tłumaczenie bez błędu kompilacji.

### Lepszy model

- stabilny `ErrorCode`,
- parameters,
- tłumaczenie po kodzie po stronie API/frontendu.

---

## AUD-045 — Paczka zawiera `node_modules` i `dist`

**Severity:** MEDIUM operational / supply-chain hygiene

W dostarczonym ZIP znajdują się:

- `Tenebit.Frontend/node_modules/`,
- `Tenebit.Frontend/dist/`,
- backend `logs/`.

### Skutek praktyczny audytu

Dołączone `node_modules` były platform-specific. Próba normalnego Vite build w Linux nie mogła wystartować, ponieważ brakowało linuxowego optional package Rollupa.

Źródło TypeScript przeszło jednak osobny typecheck.

### Rekomendacja

Do repo/paczki źródłowej:

- package manifest,
- lockfile,
- source.

W CI:

`npm ci` od zera na target platformie.

Nie przenosić `node_modules` między Windows/Linux.

---

# 7. Deep dive — multi-tenancy

## 7.1. Co obecnie chroni tenantów

Najczęściej przepływ jest następujący:

1. JWT zawiera `organization_id`.
2. `CurrentUser.OrganizationId` odczytuje ten claim.
3. Application service bierze `_currentUser.OrganizationId`.
4. Repository przyjmuje `organizationId`.
5. EF query dodaje `x.OrganizationId == organizationId`.

To jest rozsądny podstawowy wzorzec.

## 7.2. Dlaczego to nadal nie jest wystarczające

Każdy poziom ufa poprzedniemu i nie ma końcowego constraintu.

### Słabość A: JWT

Jeżeli signing key jest zły/domyslny, `organization_id` można sfałszować.

### Słabość B: Application

Nie wszystkie use-case'y mają jednakowe role checks.

### Słabość C: Repository

Nie ma global filter, więc nowa metoda może przypadkiem zrobić `FirstOrDefault(x => x.Id == id)`.

### Słabość D: DB

Nie ma tenant-aware FK/RLS, więc baza sama nie odrzuci cross-tenant association.

## 7.3. Wzorzec, który rekomenduję

### Warstwa 1 — request tenant context

Tenant powinien pochodzić tylko z poprawnie zweryfikowanego principal/session.

Nigdy nie ufać `organizationId` z request body/path dla authenticated tenant API.

Publiczne flow powinno uzyskiwać tenant z capability tokenu.

### Warstwa 2 — Application authorization

Każdy use-case:

- wymagana rola/policy,
- ownership/relationship validation,
- brak polegania na frontendzie.

### Warstwa 3 — EF

Global query filter:

`entity.OrganizationId == TenantContext.OrganizationId`

z wyraźnym mechanizmem bypass dla system jobs.

### Warstwa 4 — PostgreSQL

Najbardziej wrażliwe tenant-owned tables:

- RLS,
- composite tenant FKs.

### Warstwa 5 — tests

Test „red team tenant isolation” musi być obowiązkowym CI gate.

---

# 8. Deep dive — Clean Architecture

## Ocena: 63 / 100

### Co jest dobrze

Struktura projektów jest sensowna:

- `Tenebit.Domain` — brak zewnętrznych package dependencies,
- `Tenebit.Application` -> Domain,
- `Tenebit.Infrastructure` -> Application + Domain,
- `Tenebit.Api` -> Application + Infrastructure.

To jest prawidłowy kierunek zależności.

Domain nie jest ewidentnie zależny od EF/AspNet.

Application korzysta z interfejsów repozytoriów i abstractions.

Infrastructure implementuje persistence i integracje.

### Co obniża ocenę

1. `LocationEndpoints` bezpośrednio używa Infrastructure `TenebitDbContext`.
2. API posiada 1499-liniowy endpoint registry.
3. application services mają po kilkanaście/kilkadziesiąt zależności.
4. `AssignmentService` tworzy concrete service ręcznie.
5. część logiki technicznej i use-case orchestration jest skupiona w ogromnych klasach.

### Czy projekt „spełnia Clean Architecture”?

**Częściowo tak, strukturalnie. Nie w pełni wykonawczo.**

Nie dałbym odpowiedzi „tak, jest Clean Architecture” bez zastrzeżeń.

---

# 9. SOLID

## Ocena: 47 / 100

### S — Single Responsibility: 35/100

Największy problem SOLID.

Serwisy typu `OffboardingService`, `AssignmentService`, `AuthService`, `AssetAuditCampaignService` mają za dużo przypadków użycia i zależności.

Skutek:

- wysoki koszt zmiany,
- łatwiejsze regresje,
- test setup staje się rozbudowany,
- security checks łatwo pominąć w jednej metodzie.

### O — Open/Closed: 55/100

Są interfejsy dla repozytoriów i integracji, co pomaga.

Ale część logiki ma duże switch/if flows, a giant services wymagają edycji przy rozszerzaniu funkcji.

### L — Liskov: 75/100

Nie znalazłem istotnych nadużyć dziedziczenia. Model jest głównie composition-based.

### I — Interface Segregation: 55/100

Repozytoria są raczej domenowo skupione. Natomiast application services jako concrete dependencies nie zawsze mają wąskie kontrakty.

### D — Dependency Inversion: 52/100

Generalnie dobre repo abstractions.

Minusy:

- Location API -> DbContext,
- ręczne tworzenie disposition service,
- część infra protocol logic ręcznie implementowana zamiast stabilnej abstractions/library.

---

# 10. DRY

## Ocena: 64 / 100

Kod nie jest zasypany bezmyślnymi helperami i nie widzę masowej duplikacji.

Problemy:

- CSV escaping,
- podobne flow map/list/load across giant services,
- powtarzalne role guard patterns bez centralnego declarative policy system,
- manual upload buffering w kilku endpointach,
- część validation rozsiana po serwisach/domainie.

### Ważna uwaga

Nie rekomenduję tworzenia „UniversalService”, „BaseRepository<T>” czy globalnych mega-helperów tylko po to, by formalnie podnieść DRY. To pogorszyłoby system.

Najlepsze miejsca do ekstrakcji to tylko stabilne, powtarzalne zasady:

- CSV,
- upload constraints,
- validation,
- tenant constraints,
- error mapping,
- authorization policies.

---

# 11. YAGNI

## Ocena: 68 / 100

Ogólnie system nie wygląda na przesadnie przeabstrahowany.

Największym problemem nie jest zbyt dużo abstrakcji, lecz miejscami **zbyt ręczne implementowanie security-sensitive protocol logic**.

Przykład Stripe/OIDC pokazuje, że „nie dodawaj dependency” może przestać być KISS/YAGNI i zacząć tworzyć własny niedokończony framework bezpieczeństwa.

---

# 12. KISS

## Ocena: 56 / 100

Lokalnie wiele metod jest czytelnych i używa prostych wzorców.

Globalnie komplikacja rośnie przez:

- giant services,
- 1499-liniowy endpoint file,
- ręczne SQL tylko dla jednego feature,
- ręczne OIDC parsing,
- ręczne Stripe protocol,
- joby działające w procesie API,
- brak centralnej walidacji/autoryzacji.

Prosty system to nie system z najmniejszą liczbą klas. Prosty system to system, w którym security rules są w jednym oczywistym miejscu i trudno je przypadkiem ominąć.

---

# 13. Clean Code

## Ocena: 52 / 100

### Plusy

- nazwy klas/metod zwykle opisowe,
- async/cancellation token są powszechnie używane,
- większość service methods ma czytelny flow,
- nullable w projektach .NET jest włączone,
- TypeScript typecheck przechodzi,
- enumy/domain objects są używane zamiast magicznych stringów w wielu miejscach,
- security comments dokumentują część intencji.

### Minusy

- gigantyczne pliki,
- magic guard `20` dla lokalizacji,
- role checks nierównomiernie rozłożone,
- DTO validation nie jest systemowa,
- część wyjątków leci do globalnego 500,
- zbyt wiele dependency parameters w konstruktorach,
- API layer ma wyjątkowy raw-SQL feature.

---

# 14. Frontend — szczegółowa ocena

## Ocena: 61 / 100

### Co jest dobre

1. Access token jest trzymany **w pamięci**, a nie w localStorage.
   - `authConfig.ts` usuwa legacy token z localStorage.
2. Refresh cookie jest HttpOnly po backendzie.
3. API client ma pojedynczy in-flight refresh promise, ograniczający refresh storm.
4. Fetch używa `credentials: include` świadomie.
5. React domyślnie escape'uje tekst.
6. `dangerouslySetInnerHTML` występuje tylko dla generowanych SVG QR.
   - backend HTML-encode'uje label text w SVG,
   - nie znalazłem bezpośredniego user HTML injection w tych sinkach.
7. `window.open` używa `noopener`.
8. TypeScript source **przeszedł `tsc -b`** w środowisku audytu.

### Problemy

1. `AssetsPage.tsx` 1309 linii — trudna regresja i utrzymanie.
2. `PeoplePage`, `SettingsPage`, `OffboardingPage` również są za duże.
3. Tylko **4 frontendowe pliki testowe**.
4. Brak E2E testów krytycznych flow.
5. Offboarding copy public link jest błędny.
6. UI hide nie jest odpowiednikiem backend authorization; przykład locations/users pokazuje rozdźwięk.
7. API endpoint definitions w dużym `endpoints.ts` będą trudne w dalszym skalowaniu.

### Nie uznaję za lukę

Samo `dangerouslySetInnerHTML` dla QR nie dostaje minusów krytycznych, ponieważ źródło SVG jest kontrolowane, a custom label strings są HTML-encode'owane po stronie backendu.

---

# 15. Backend — szczegółowa ocena

## Ocena: 43 / 100

### Plusy

- sensowny project layering,
- dużo repozytoriów poprawnie filtruje tenant,
- cancellation tokens,
- domain exceptions dla walidacji,
- CORS jest origin allowlist, nie wildcard z credentials,
- refresh token rotation,
- hashed reset/public tokens,
- PBKDF2 + per-password salt + fixed-time compare,
- TOTP + recovery codes,
- bezpieczny image signature check i metadata stripping,
- CSV formula injection protection,
- correlation ID,
- generic production 500 body dla zwykłych endpointów,
- public offboarding/audit mają lepszy token pattern.

### Minusy

- krytyczny JWT fail-open,
- ręczne OIDC token parsing,
- authz omissions,
- brak tenant safety net,
- startup/migrations,
- validation,
- upload memory DoS,
- multi-instance state/job issues,
- real 500 z logów.

---

# 16. Bezpieczeństwo pozytywne — czego NIE należy bezmyślnie przepisywać

Audyt ma być krytyczny, ale nie powinien karać poprawnego kodu.

## 16.1. Password hashing

`PasswordHasher.cs`

- losowa sól,
- PBKDF2-SHA256,
- fixed-time compare.

100k iteracji warto ponownie zbenchmarkować i zwiększyć na współczesnym sprzęcie albo rozważyć Argon2id, ale obecny mechanizm nie jest „plaintext/fast hash”.

## 16.2. Token hashing

`TokenHasher.NewRawToken()` używa `RandomNumberGenerator.GetBytes(32)`.

Tokeny reset/public są hashowane przed storage.

To jest dobry wzorzec.

## 16.3. Refresh tokens

Refresh token:

- raw token w HttpOnly cookie,
- w DB hash,
- rotation przy refresh,
- revoke starego tokenu.

To jest dobry fundament.

## 16.4. Cookies

`RefreshTokenCookie`:

- `HttpOnly = true`,
- `Secure = true` poza Development,
- `SameSite=Lax`,
- ograniczony path `/api/auth`.

Dobrze.

## 16.5. PKCE/state

OAuth ma:

- PKCE,
- state,
- jednorazowe consume,
- provider binding,
- walidację local return path.

To jest poprawne i powinno zostać.

Problem dotyczy późniejszej walidacji `id_token`, nie PKCE/state.

## 16.6. Image evidence

`AssetEvidenceService`:

- allowlist JPEG/PNG/WebP,
- signature detection,
- limit 5 MB w serwisie,
- sanitization/re-encode,
- stripping metadata/EXIF,
- hash SHA256.

To jest bardzo dobry kierunek.

Problem: limit musi zadziałać **przed** pełnym bufferingiem oraz trzeba ograniczyć dimensions.

## 16.7. CSV export

Ochrona przed spreadsheet formula injection jest cenna i powinna pozostać po refaktorze do wspólnej implementacji.

---

# 17. Testy

## Ocena: 42 / 100

### Stan

- ok. **310** plików C# produkcja+testy w backendzie,
- **41** plików `*Tests.cs`,
- ok. **88** plików TS/TSX w `src`,
- tylko **4** frontendowe test files.

### Dobre testy, które zauważyłem

Istnieją m.in. testy:

- password hasher,
- token hasher,
- TOTP,
- PKCE,
- public token service,
- image signature/sanitizer,
- evidence,
- offboarding,
- asset audits,
- tenant isolation w części serwisów,
- evidence retention tenant isolation.

To jest znacznie lepsze niż brak testów.

### Najważniejsza luka testowa

Brak prawdziwej integracji HTTP + real EF/Postgres.

W systemie multi-tenant **unit tests repository fake nie są wystarczającym dowodem izolacji**.

### Obowiązkowy nowy test project

Proponuję `Tenebit.IntegrationTests` z Testcontainers PostgreSQL + `WebApplicationFactory`.

#### Każdy test tworzy minimum:

- Organization A + Owner A + Employee A,
- Organization B + Owner B + Employee B,
- osobne assets/people/assignments/procedures/licenses/offboarding/audits.

#### Następnie wykonuje cross-tenant attacks

| Operacja | Oczekiwany wynik |
|---|---|
| A GET B asset ID | 404/403 |
| A PUT B asset ID | 404/403 + B unchanged |
| A DELETE B asset ID | 404/403 + B unchanged |
| A export assets | zero danych B |
| A evidence B id | 404/403 |
| A assignment B id | 404/403 |
| A protocol B id | 404/403 |
| A procedure document B id | 404/403 |
| A offboarding B id | 404/403 |
| public token A + item B | 404/403 |
| nested entity with OrganizationId A + parent B | DB reject |
| Employee calls admin settings mutation | 403 |
| forged tenant path id | ignored/rejected |

#### Dodatkowe concurrency tests

- dwa simultaneous asset creates na granicy plan limit,
- dwa reservation approvals,
- custom field replace,
- refresh token reuse,
- double public acceptance.

---

# 18. Uruchomienie/build w audycie

## Frontend

### TypeScript

`node node_modules/typescript/bin/tsc -b`

**Wynik: PASS / TYPECHECK_OK.**

### Vite build

Standardowy build z dołączonego `node_modules` nie był miarodajny, ponieważ paczka dependencies była przygotowana na inną platformę i brakowało Linux optional package Rollupa (`@rollup/rollup-linux-x64-gnu`).

To jest również argument, żeby nie dostarczać `node_modules` w archiwum źródłowym.

## Backend

W środowisku audytowym nie był zainstalowany .NET SDK, więc **nie oznaczam backend build ani `dotnet test` jako wykonanych**.

Nie będę wpisywał fałszywego „tests pass”.

### Co z tego wynika

Ocena backendu opiera się na:

- statycznym przeglądzie źródeł,
- migracjach,
- strukturze dependencies,
- logach runtime dostarczonych w ZIP,
- test source review.

### Dependency vulnerability scan

`npm audit` nie mógł pobrać advisory z registry z powodu braku dostępu sieciowego w środowisku. Dlatego **nie certyfikuję wersji npm/NuGet jako wolnych od CVE**.

To musi być CI gate w normalnym środowisku sieciowym.

---

# 19. Operacje / deployment / HA

## Ocena: 30 / 100

Najważniejsze braki:

1. fail-open dla JWT secret,
2. fail-open dla DB password,
3. migracja po `StartAsync`,
4. background jobs przed schema readiness,
5. `AutoCreate=true` / seed w base config,
6. memory-only auth state,
7. brak distributed job coordination,
8. runtime logs w source package,
9. brak widocznego security header policy,
10. brak jawnego trusted proxy/TLS contract.

### Minimalny produkcyjny deployment contract

- ingress HTTPS only,
- backend private network only,
- dedicated DB runtime credentials,
- migration job przed deployment rollout,
- secret validation przed startem,
- immutable image,
- read-only filesystem tam, gdzie możliwe,
- centralized logs,
- metrics + traces,
- readiness sprawdza schema version,
- graceful shutdown,
- backup + restore drill,
- Redis/distributed state przy >1 replica,
- worker/scheduler coordination.

---

# 20. Priorytety napraw

## P0 — przed jakimkolwiek wdrożeniem z realnymi klientami

1. **JWT secret fail-closed.**
2. **Public assignment secure tokens** z expiry/revoke/hash.
3. **OIDC id_token pełna walidacja.**
4. **Location write role authorization** + przeniesienie z API/DbContext do Application.
5. **Assignment protocol role authorization.**
6. **Tenant isolation safety net**: co najmniej global query filters + plan composite FK/RLS.
7. **Testcontainers/WebApplicationFactory tenant attack suite.**
8. **Migracje przed startem/ruchem**, nie po `StartAsync`.
9. Naprawić **potwierdzony custom fields concurrency 500**.
10. Centralna walidacja requestów, zaczynając od auth.

Dopóki te punkty nie są zamknięte, moja rekomendacja pozostaje **NO-GO**.

## P1 — również przed pełnym rolloutem 100 firm

11. Upload request limits przed MemoryStream.
12. Revoke sesji/trusted devices po resecie hasła.
13. Stripe replay/idempotency/url allowlist.
14. Partitioned rate limiting.
15. Trusted forwarded headers.
16. Nie ujawniać exception text w readiness.
17. Redis/distributed OAuth+2FA state dla HA.
18. Distributed lock/job queue.
19. Evidence assignment-asset invariant.
20. User directory role policy.
21. Naprawić offboarding copy link.
22. Encrypt TOTP secret / sensitive fields / license keys at rest.
23. Expected DB conflicts -> 409, nie 500.
24. Atomic subscription limit.

## P2 — przed zwiększaniem skali zespołu/funkcji

25. Rozbić giant services/use cases.
26. Rozbić `TenebitEndpoints.cs` per feature.
27. Rozbić największe frontend pages.
28. Usprawnić location queries.
29. Wspólny CSV writer.
30. Error codes zamiast text-based translation.
31. Security headers.
32. OpenAPI production policy.
33. Bezpieczne production defaults w appsettings.
34. Dependency CVE scan w CI.
35. E2E frontend na krytyczne flow.

---

# 21. Proponowany docelowy model tenant isolation

Poniżej konkretny model, który uznałbym za odpowiedni dla Tenebit.

## 21.1. TenantContext

Jedna request-scoped abstrakcja:

- `OrganizationId`,
- `UserId`,
- roles,
- isSystemContext.

Authenticated requests nie mogą przyjmować arbitralnego tenant ID jako źródła prawdy.

## 21.2. EF global filters

Każda encja implementuje np. `ITenantEntity`.

Query filter bazuje na current tenant.

Background job tworzy jawny tenant scope dla jednej organizacji zamiast robić globalne query w zwykłym context.

## 21.3. Tenant-aware FKs

Parent:

unique alternate key `(OrganizationId, Id)`.

Child:

FK `(OrganizationId, ParentId) -> (OrganizationId, Id)`.

Wtedy nawet błędny kod nie zapisze relacji A->B.

## 21.4. PostgreSQL RLS

Jeżeli architektura deploymentu pozwala, aplikacyjna sesja/transaction ustawia current tenant, a policy wymusza:

`organization_id = current_setting(...)`

To nie zastępuje application auth, ale daje najlepszy defense-in-depth przed przypadkowym query bez filtra.

## 21.5. Public flows

Nie przekazuj `organizationId` jako credential.

Token -> hash lookup -> entity -> OrganizationId.

Token musi mieć:

- entropy,
- purpose,
- expiry,
- revoke,
- rotation,
- audit.

---

# 22. Proponowana architektura autoryzacji

Obecne `AccessPolicy.EnsureAnyRole` jest czytelne, ale łatwo o nim zapomnieć, czego przykłady już są w kodzie.

Rekomenduję dwa poziomy:

## Poziom endpoint

Declarative policy dla coarse-grained permission, np.:

- `assets.read`,
- `assets.manage`,
- `organization.users.manage`,
- `locations.manage`,
- `assignments.read`,
- `assignments.protocol.read`.

## Poziom use-case

Relationship/ownership rules:

- czy user może działać na tej osobie,
- czy asset należy do assignment,
- czy public token ma właściwy purpose,
- czy resource należy do active tenant.

### Dlaczego oba?

Endpoint policy chroni przed pominięciem kontroli w routing layer.

Use-case policy chroni przed wywołaniem serwisu z innego entrypointu.

---

# 23. Proponowany CI quality gate

Każdy merge do main powinien blokować się, jeżeli którykolwiek krok nie przejdzie.

## Backend

- restore z lock/controlled dependencies,
- build `Release`, warnings as errors dla kluczowych analyzerów,
- unit tests,
- PostgreSQL integration tests,
- migration-from-previous-version test,
- tenant isolation suite,
- secret scan,
- dependency vulnerability scan,
- formatter/analyzers,
- coverage report.

## Frontend

- `npm ci`,
- typecheck,
- ESLint,
- unit/component tests,
- production build,
- dependency audit,
- E2E critical paths.

## Security tests

Automatycznie:

- role matrix,
- tenant matrix,
- anonymous matrix,
- file upload malformed/oversize,
- public token expired/revoked/wrong purpose,
- refresh reuse,
- OAuth invalid issuer/audience/signature.

---

# 24. Lista kontrolna przed pierwszym klientem

Nie uznałbym systemu za gotowy, dopóki wszystkie poniższe pozycje nie są `YES`.

### Tenant isolation

- [ ] Czy DB odrzuca cross-tenant FK?
- [ ] Czy query filter automatycznie ogranicza tenant?
- [ ] Czy bypass filter jest jawny i zarezerwowany dla system jobs?
- [ ] Czy integration tests próbują każdego endpointu z ID drugiego tenanta?
- [ ] Czy export/download też są objęte testami cross-tenant?

### Authentication

- [ ] Czy produkcja NIE uruchomi się z default JWT key?
- [ ] Czy issuer/audience są walidowane?
- [ ] Czy OIDC podpis/issuer/audience/lifetime/nonce są walidowane?
- [ ] Czy password reset revokuje sesje?
- [ ] Czy zmiana roli/deaktywacja ma określone SLA revocation?

### Authorization

- [ ] Czy każdy management endpoint ma server-side policy?
- [ ] Czy public assignment używa osobnego capability tokenu?
- [ ] Czy protocol/download endpoints mają te same prawa co resource view?
- [ ] Czy user directory jest chroniony zgodnie z wymaganiem produktu?

### Uploads

- [ ] Czy request ma limit przed bufferingiem?
- [ ] Czy image dimensions są limitowane?
- [ ] Czy format jest sniffowany?
- [ ] Czy metadata jest stripped?
- [ ] Czy evidence relation jest walidowana?

### Database

- [ ] Czy migrations run before traffic?
- [ ] Czy aplikacja failuje po migration failure?
- [ ] Czy runtime DB user nie jest superuserem?
- [ ] Czy secrets nie mają defaults?
- [ ] Czy backup restore był realnie przetestowany?

### HA

- [ ] Czy OAuth/2FA state działa przy 2 replicas?
- [ ] Czy joby nie wykonują się N razy przy N replicas?
- [ ] Czy Stripe webhook jest idempotentny?
- [ ] Czy rate limiter nie pozwala jednemu tenantowi blokować wszystkich?

### QA

- [ ] Czy backend build/test wykonuje się w CI od czystego checkoutu?
- [ ] Czy frontend robi `npm ci` od zera?
- [ ] Czy migration test startuje ze schematu poprzedniego release?
- [ ] Czy dependency vulnerability scan jest zielony?

---

# 25. Rekomendowana kolejność refaktoru bez „przepisywania wszystkiego”

Nie rekomenduję rewrite'u.

Projekt ma wystarczająco dobry szkielet, żeby go naprawić iteracyjnie.

## Faza 1 — bezpieczeństwo, bez dużej przebudowy

- fail-closed secrets,
- assignment token,
- OIDC library/validation,
- missing role checks,
- input validators,
- migration order,
- upload limits,
- session revoke.

## Faza 2 — tenant safety net

- `ITenantEntity`,
- TenantContext,
- global query filters,
- composite DB keys/FKs,
- tenant integration suite.

To jest najważniejszy refaktor całego projektu.

## Faza 3 — HA/reliability

- distributed state,
- job locking/worker,
- Stripe idempotency,
- expected conflict handling,
- metrics.

## Faza 4 — maintainability

- split endpoint modules,
- split giant services by use case,
- split frontend pages,
- shared CSV, validators/error codes.

---

# 26. Co powinno zostać jako pierwsze zabezpieczenie regresji

Po naprawieniu krytycznych błędów najważniejsze nie jest kolejne 500 linii refaktoru. Najważniejsze jest **zamrożenie poprawnego zachowania testami**.

Pierwszy zestaw regression tests powinien dokładnie odtwarzać znalezione problemy:

1. production startup z default JWT key -> proces ma odmówić startu,
2. Employee -> POST location -> 403,
3. Employee bez AssignmentViewers -> protocol -> 403,
4. public assignment bez poprawnego tokenu -> 401/404,
5. expired/revoked token -> fail,
6. token assignment A -> assignment B -> fail,
7. null email login -> 400, nigdy 500,
8. oversized image -> reject przed full buffering,
9. custom field replace -> success w Postgres,
10. schema old -> migrate before hosted jobs,
11. password reset -> old refresh token fails,
12. stale Stripe webhook -> reject/ignore,
13. duplicate Stripe event -> exactly-once logical effect,
14. tenant A ID użyte w tokenie tenant B -> fail na każdym resource type,
15. DB direct insert child Organization A -> parent Organization B -> constraint fail.

---

# 27. Najważniejsze mocne strony projektu

Żeby ocena była precyzyjna: projekt nie jest „cały zły”. Ma kilka elementów, które warto zachować.

1. **Dobry kierunek dependencies** Domain/Application/Infrastructure/API.
2. Duża część repozytoriów konsekwentnie przyjmuje `OrganizationId`.
3. **41 backend test files**, czyli testowanie nie jest ignorowane.
4. CSPRNG dla public/reset tokens.
5. Hashowanie tokenów w storage.
6. PBKDF2 z salt i fixed-time compare.
7. Refresh token rotation.
8. HttpOnly/Secure refresh cookie.
9. PKCE i one-time OAuth state.
10. CORS exact origins + credentials, nie `*`.
11. Image signature detection.
12. Re-encode/strip metadata EXIF/GPS.
13. Evidence hashing.
14. CSV formula injection protection.
15. HTML encoding danych w QR labels i public-report emailu.
16. Correlation IDs i ujednolicony generic 500 response.
17. Public offboarding/audit token design jest dobrym wzorcem do użycia przy assignments.
18. TypeScript typecheck przechodzi.
19. Brak wykrytych `eval` / `new Function` w frontendzie.
20. Access token nie jest utrwalany w localStorage.

Te elementy są powodem, dla którego ocena jakości konstrukcji jest wyższa niż gotowość produkcyjna.

---

# 28. Najważniejsze słabe strony w jednym zdaniu

Jeżeli miałbym streścić całość technicznie:

> **Tenebit ma sensowną strukturę aplikacji, ale bezpieczeństwo wielofirmowe jest zbyt mocno oparte na tym, że każdy kolejny programista „pamięta o OrganizationId i roli”, zamiast na mechanizmach, które czynią pomyłkę trudną lub niemożliwą.**

To trzeba odwrócić przed skalowaniem do realnych klientów.

---

# 29. Ostateczna decyzja audytowa

## Czy kod jest „OK” jako projekt developerski?

**Częściowo.** Jest lepszy niż typowy prototyp: ma warstwy, domenę, repozytoria, testy, token hashing, refresh rotation i rozsądne security primitives.

## Czy spełnia Clean Architecture?

**W dużej części strukturalnie tak, praktycznie nie w pełni.** `LocationEndpoints` jest wyraźnym naruszeniem, a giant services osłabiają granice odpowiedzialności.

## Czy spełnia SOLID?

**Częściowo.** Najbardziej łamane są SRP i miejscami DIP.

## Czy spełnia DRY?

**W miarę.** Nie jest to główne ryzyko projektu.

## Czy spełnia YAGNI/KISS?

**Średnio-dobrze na poziomie lokalnym, średnio na poziomie systemu.** Ręczne implementowanie security-sensitive protokołów nie jest dobrym KISS.

## Czy spełnia Clean Code?

**Średnio.** Nazewnictwo i lokalna czytelność są często dobre, ale rozmiar klas/pliku endpointów i niespójne cross-cutting concerns znacząco obniżają jakość.

## Czy posiada błędy?

**Tak.** Co najmniej dwa HTTP 500 są potwierdzone dostarczonymi logami, a startup/migration race także jest potwierdzony logiem.

## Czy posiada potencjalne bugi?

**Tak.** W raporcie opisano m.in. race limitu aktywów, evidence relationship, location depth/cycle, multi-instance state/jobs i concurrency mapping.

## Czy posiada luki bezpieczeństwa?

**Tak.** Najpoważniejsze: domyślny JWT key fail-open, public assignment capability, niepełna walidacja OIDC, brakujące role gates i zbyt słaba warstwowa izolacja tenantów.

## Czy w tym stanie dopuściłbym 100 firm?

# **Nie.**

Nie dopuściłbym realnych danych 100 niezależnych klientów do obecnej wersji bez zamknięcia P0 i wykonania realnych testów integracyjnych tenant A/B na PostgreSQL.

Po naprawieniu P0 + P1 i dodaniu warstwy tenant integration tests projekt ma sensowną bazę do podniesienia w okolice **75–85/100** bez rewrite'u całego systemu.

---

# 30. Ograniczenia tego audytu

Ten dokument jest **statycznym audytem dostarczonego źródła i dostarczonych logów**, a nie formalnym pentestem uruchomionego środowiska produkcyjnego.

Nie mogę uczciwie zagwarantować, że lista zawiera absolutnie każdy możliwy błąd. Taka gwarancja nie istnieje w profesjonalnym audycie bezpieczeństwa.

W szczególności:

- backend nie został zbudowany/uruchomiony w tym środowisku z powodu braku .NET SDK,
- nie wykonano dynamicznego pentestu działającego API,
- nie wykonano dependency CVE audit online,
- nie zweryfikowano nginx/Docker/firewall/TLS/secrets na docelowym serwerze,
- nie wykonano load testów,
- nie wykonano realnego restore backupu,
- nie wykonano multi-replica chaos testu.

Dlatego **po naprawie kodu powinien istnieć drugi etap: dynamic security/integration review uruchomionej wersji**.

---

# 31. Minimalne kryterium PASS dla ponownego audytu

Ponowny audyt może dostać status „GO” dopiero gdy:

1. nie ma żadnego CRITICAL,
2. wszystkie tenant-owned relacje mają defense-in-depth,
3. tenant integration suite jest zielona,
4. auth/authz matrix jest zielona,
5. production startup jest fail-closed,
6. schema migration jest ukończona przed ruchem/jobami,
7. public assignment używa prawdziwych tokenów,
8. OIDC token validation jest standardowa,
9. reset hasła revokuje sesje,
10. uploady mają pre-buffer limits,
11. zaobserwowane 500 mają regression tests,
12. czysty checkout przechodzi pełne CI build/test/audit.

---

**Koniec raportu.**
