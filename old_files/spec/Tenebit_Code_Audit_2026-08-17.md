# Tenebit - ponowny audyt kodu po poprawkach krytycznych

**Data audytu:** 2026-08-17  
**Audytowana paczka:** `Tenebit_audit2.zip`  
**Poprzedni audyt:** `Tenebit_Code_Audit_2026-08-16.md`  
**Zakres:** backend .NET, frontend React/Vite/TypeScript, Clean Architecture, SOLID, DRY, YAGNI, KISS, Clean Code, poprawnosci, security, multi-tenancy, testy, reliability, scalability, deployment, observability.  
**Najwazniejsze zalozenie biznesowe:** system ma obslugiwac ok. 100 niezaleznych firm i dane jednej organizacji nie moga przedostac sie do drugiej.

---

# 1. Werdykt

## Wynik ogolny: **56/100**

## Gotowosc produkcyjna dla 100 firm: **41/100 - NO-GO**

Ta wersja jest **wyraznie lepsza** od poprzedniej. Najbardziej niebezpieczne problemy z pierwszego audytu nie zostaly jedynie zamaskowane w UI. Kilka z nich zostalo rzeczywiscie poprawionych na poziomie backendu i modelu danych.

Jednoczesnie nie moge uznac systemu za gotowy do bezpiecznej obslugi 100 niezaleznych tenantow. Powod jest konkretny: mimo dodania czesci tenant-aware foreign keys nadal istnieja rzeczywiste sciezki zapisu, w ktorych tenant A moze zapisac GUID obiektu nalezacego do tenant B, jezeli ten GUID pozna. Warstwa Application nie waliduje wszystkich takich referencji, a baza danych nie wymusza `OrganizationId` dla wszystkich relacji.

To oznacza, ze granica tenantow jest **lepsza niz w poprzedniej wersji, ale nadal nie jest zamknieta**.

W statycznym audycie aktualnej paczki **nie znalazlem potwierdzonej bezposredniej sciezki odczytu danych tenant B przez zalogowanego tenant A**. Nie bede wiec twierdzil, ze wykazalem obecny direct data exfiltration. Wykazalem natomiast mozliwosc tworzenia cross-tenant references w kilku produkcyjnych use-case'ach. Dla systemu, ktorego glownym wymaganiem jest scisla separacja firm, jest to blocker release.

---

# 2. Zmiana punktacji wzgledem pierwszego audytu

| Obszar | Poprzednio | Teraz | Ocena |
|---|---:|---:|---|
| Wynik ogolny | 39/100 | **56/100** | duza poprawa, ale nadal NO-GO |
| Gotowosc produkcyjna | 23/100 | **41/100** | nadal niedopuszczalne dla realnych 100 firm |
| Security | 28/100 | **58/100** | najgrozniejsze auth luki mocno poprawione |
| Multi-tenancy | 31/100 | **39/100** | poprawiono czesc FK, ale granica nadal niepelna |
| Authentication / session | 42/100 | **66/100** | JWT/OIDC mocno lepiej, pozostaja session lifecycle issues |
| Authorization | 32/100 | **71/100** | role gate'y poprawione, znacznie lepiej |
| Ochrona danych wrazliwych | 43/100 | **44/100** | niewielki postep; plaintext secrets/PII pozostaja |
| Clean Architecture | 63/100 | **65/100** | dobry szkielet, nadal wycieki Infrastructure do API |
| SOLID | 47/100 | **49/100** | duze klasy i reczne zaleznosci nadal sa |
| DRY | 64/100 | **63/100** | bez istotnej poprawy, miejscami nadal duplikacja |
| YAGNI | 68/100 | **69/100** | generalnie rozsadnie |
| KISS | 56/100 | **55/100** | kilka rozwiazan nadal zbyt rozproszonych/posrednich |
| Clean Code | 52/100 | **55/100** | poprawa obslugi bledow, ale duze pliki pozostaja |
| Backend reliability | 43/100 | **59/100** | migracje/concurrency lepiej; inne race conditions zostaja |
| Frontend | 61/100 | **62/100** | typecheck OK, malo testow i pozostaje blad linku offboarding |
| Testy / QA | 42/100 | **59/100** | duzy plus za WebApplicationFactory i tenant tests |
| Scalability / HA | 36/100 | **39/100** | nadal problemy z jobs, MemoryCache i global limiterami |
| Deployment / config | 30/100 | **48/100** | production fail-closed znacznie lepiej |
| Observability | 50/100 | **47/100** | logowanie nadal moze przenosic PII, logs sa w paczce |

**Wazne:** wynik 56/100 nie jest srednia arytmetyczna tabeli. Jest to wynik wazony ryzykiem. Dla systemu multi-tenant jeden powazny blad granicy organizacji ma wieksza wage niz kilka dobrze napisanych serwisow czy poprawny frontend typecheck.

---

# 3. Co zostalo rzeczywiscie naprawione

## 3.1. Publiczne assignmenty nie sa juz chronione samym UUID - NAPRAWIONE

W poprzedniej wersji publiczny URL uzywal w praktyce `organizationId + assignmentId` jako credentialu. To byl powazny blad capability authorization.

W aktualnej wersji:

- `Assignment` posiada `PublicTokenHash`, expiry i revoke timestamp,
- generowany jest losowy token,
- w bazie przechowywany jest hash zamiast raw tokenu,
- publiczne endpointy dzialaja po tokenie,
- porownanie tokenu jest realizowane przez dedykowany `PublicTokenService`,
- mozliwe jest wygaszenie/odwolanie tokenu.

To jest realna poprawa projektu security.

### Pozostaly problem

`AssignmentRepository.ListWithPublicTokenAsync()` nadal pobiera wszystkie aktywne publiczne assignmenty wszystkich organizacji do pamieci, a `AssignmentService.ResolveByTokenAsync()` iteruje po nich i sprawdza hash.

Dowod:

- `Tenebit.Infrastructure/Repositories/AssignmentRepository.cs:56-61`
- `Tenebit.Application/Assignments/AssignmentService.cs:498-510`

Konsekwencje:

1. anonimowy request ma koszt O(N) wzgledem wszystkich aktywnych tokenow w systemie,
2. materializowane sa dane cross-tenant w jednym procesie aplikacji bez potrzeby,
3. brak indeksowanego lookupu po hash tokenu,
4. wraz ze wzrostem liczby assignmentow rosnie koszt kazdego publicznego requestu.

### Rekomendacja

Hashowac token wejscia jeden raz i wykonywac:

`WHERE PublicTokenHash = @hash AND PublicTokenRevokedAt IS NULL AND PublicTokenExpiresAt > now`

Na `PublicTokenHash` dodac indeks, najlepiej unikalny dla nie-NULL tokenow.

---

## 3.2. Domyslny JWT secret w Production - NAPRAWIONE

`Program.cs:101-115` zatrzymuje teraz start aplikacji w `Production`, jezeli:

- signing key jest pusty,
- jest wartoscia developerska z repo,
- ma mniej niz 32 znaki,
- connection string nadal zawiera `Password=postgres`.

To zamyka poprzedni fail-open, gdzie aplikacja logowala `Critical`, ale startowala dalej.

### Pozostale hardening issues

- `ValidateIssuer = false`,
- `ValidateAudience = false`,
- fallback developerskiego signing key nadal istnieje poza `Production`.

Jesli staging lub inne publicznie osiagalne srodowisko nie ma `Environment=Production`, moze uruchomic sie z konfiguracja, ktorej nie dopuscilbym do Internetu.

Rekomendacja: fail-closed dla wszystkich srodowisk poza jawnie oznaczonym Development/Test.

---

## 3.3. OIDC Google/Microsoft/Apple - rdzen problemu NAPRAWIONY

Poprzednio `id_token` byl tylko dekodowany. Aktualnie `ExternalAuthService` waliduje:

- podpis,
- klucze providera,
- issuer,
- audience,
- lifetime,
- expiration,
- wymagany podpis.

Dowod: `ExternalAuthService.cs:169-216`.

To jest duza i poprawna zmiana.

### Pozostale uwagi

1. nie widze pelnego lifecycle `nonce` OIDC,
2. Microsoft issuer validator akceptuje issuer z dowolnym GUID tenantem pasujacym do regexu; jesli konfiguracja ma oznaczac single-tenant Azure AD, walidacja jest zbyt szeroka,
3. Facebook flow traktuje zwrocony e-mail jako verified w sposob bardziej ufny niz flow OIDC. Przy automatycznym linkowaniu kont przez e-mail ta decyzja musi byc swiadoma i potwierdzona dokumentacja providera.

---

## 3.4. Assignment protocol authorization - NAPRAWIONE

Pobranie protokolu nie omija juz role policy. `AssignmentService` stosuje `AssignmentViewers` przed generowaniem prywatnego protokolu.

To zamyka jeden z najwazniejszych starych brakow authz.

---

## 3.5. Location write authorization - CZESCIOWO NAPRAWIONE

Mutacje lokalizacji dostaly kontrole owner/admin. To jest poprawa security.

Nie zostal jednak rozwiazany problem architektoniczny: `LocationEndpoints` nadal pracuje bezposrednio z `TenebitDbContext` oraz wykonuje logike/SQL w warstwie API.

Czyli security gate jest lepszy, ale Clean Architecture nadal jest naruszona.

---

## 3.6. Health endpoint - NAPRAWIONE

Klient nie dostaje juz surowego `exception.Message`. Szczegoly sa logowane po stronie serwera, a odpowiedz jest generyczna.

To jest poprawny wzorzec.

---

## 3.7. Start przed migracja - DUZA POPRAWA

`InitializeDatabaseAsync()` jest wykonywane przed `RunAsync()`, a blad inicjalizacji przerywa start.

To usuwa poprzedni race, w ktorym aplikacja mogla zaczac obslugiwac ruch zanim schemat byl gotowy.

### Residual

Samo `CanConnect`/gotowosc DB nie dowodzi, ze wszystkie wymagane migracje sa zastosowane, a `InitializeDatabaseAsync` jest zalezny od konfiguracji AutoCreate. W procesie produkcyjnym migracje powinny byc jawnie zarzadzane i kontrolowane przez deployment.

---

## 3.8. Evidence -> assignment/asset tenant integrity - NAPRAWIONE

Dodano walidacje, ze assignment nalezy do organizacji oraz dotyczy wskazanego assetu. Dodano rowniez composite FK dla wybranych relacji.

To jest dobry kierunek i pokazuje, ze problem z pierwszego audytu zostal zrozumiany.

---

## 3.9. Organization user list role gate - NAPRAWIONE

Lista uzytkownikow organizacji zostala ograniczona do odpowiednich rol, a test integracyjny obejmuje przypadek zwyklego employee.

---

## 3.10. Concurrency exception -> 409 - NAPRAWIONE CZESCIOWO

`DbUpdateConcurrencyException` jest mapowane na domenowy konflikt i API zwraca 409 zamiast anonimowego 500.

To jest dobra poprawa reliability.

Nie rozwiazuje jednak wszystkich race conditions wynikajacych z `check then insert` i unikalnych indeksow.

---

# 4. Najwazniejszy blocker: multi-tenancy nadal nie jest domkniete

## Ocena: **39/100**

To pozostaje najslabszy obszar projektu i glowny powod `NO-GO`.

### Co jest lepsze

Migracja `20260817062322_AddTenantCompositeForeignKeys` dodaje alternate keys `(OrganizationId, Id)` oraz composite foreign keys dla kilku waznych relacji, m.in.:

- asset audit item -> campaign,
- asset audit item -> participant,
- audit participant -> campaign,
- asset evidence -> asset,
- asset evidence -> assignment,
- equipment reservation item -> reservation,
- offboarding item -> offboarding case.

To jest zdecydowanie lepsze od wersji pierwszej.

### Dlaczego to nadal nie wystarcza

W kodzie nadal nie ma:

- globalnych `HasQueryFilter` dla tenant-owned entities,
- PostgreSQL Row Level Security,
- kompletnego zestawu composite FK `(OrganizationId, ForeignId)` dla wszystkich relacji tenantowych.

Co wazniejsze: istnieja aktualne use-case'y zapisujace obcy GUID bez sprawdzenia jego `OrganizationId`.

---

# 5. P0-TENANT-001 - Asset.TeamId moze wskazywac Team z innej organizacji

**Severity:** HIGH / release blocker dla multi-tenant  
**Status:** OTWARTE

W `AssetService.CreateAsync` walidowana jest kategoria:

- `AssetService.cs:140-141`

ale `request.TeamId` jest przekazywany bez walidacji tenanta:

- `AssetService.cs:150-151`.

To samo przy update:

- `AssetService.cs:173-190`.

Scenariusz:

1. uzytkownik organizacji A ma prawo tworzyc/edytowac asset,
2. wysyla `TeamId` nalezacy do organizacji B,
3. serwis nie wykonuje `_teams.GetAsync(organizationA, teamId)`,
4. model przyjmuje GUID,
5. baza nie ma pelnego tenant-aware FK dla tej relacji.

Nie jest to jeszcze bezposredni odczyt danych B, ale jest to **naruszenie integralnosci granicy tenantow**.

### Naprawa

Przed zapisem kazdego `TeamId`:

- jezeli null -> OK,
- jezeli ma wartosc -> lookup przez `organizationId + teamId`,
- brak -> 400/404,
- DB FK `(OrganizationId, TeamId) -> Teams(OrganizationId, Id)`.

---

# 6. P0-TENANT-002 - Person.TeamId i Person.ManagerId bez tenant validation

**Severity:** HIGH / release blocker  
**Status:** OTWARTE

`PeopleService.CreateAsync`:

- pobiera `OrganizationId`,
- sprawdza email wewnatrz organizacji,
- tworzy `Person`,
- przekazuje `request.TeamId` i `request.ManagerId` bez sprawdzenia ich organizacji.

Dowod:

- `PeopleService.cs:62-81`, szczegolnie linia 76.

Update ma ten sam problem:

- `PeopleService.cs:89-118`, szczegolnie linia 105.

### Ryzyko

- cross-tenant manager relationship,
- cross-tenant team relationship,
- pozniejsze joiny/reporting/UI moga zaczac ujawniac niespodziewane dane albo zachowywac sie blednie,
- dane tenant A moga zostac logicznie powiazane z tenant B mimo tego, ze kazdy prosty repository lookup osobno filtruje `OrganizationId`.

### Wymagana naprawa

Walidacja obu GUID po `(organizationId, id)` oraz composite FK w DB.

---

# 7. P0-TENANT-003 - Team.ManagerId bez tenant validation

**Severity:** HIGH / release blocker  
**Status:** OTWARTE

`TeamService.CreateAsync` tworzy Team z `request.ManagerId` bez sprawdzenia osoby:

- `TeamService.cs:32-44`, linia 40.

`UpdateAsync` robi to samo:

- `TeamService.cs:49-62`, linia 59.

To jest kolejna bezposrednia sciezka cross-tenant foreign reference.

---

# 8. P0-TENANT-004 - JobProfile.DefaultManagerId bez tenant validation

**Severity:** HIGH / release blocker  
**Status:** OTWARTE

`JobProfileService.ValidateReferencesAsync()` waliduje referencje do kategorii i procedur, ale `DefaultManagerId` jest przekazywany do modelu bez analogicznego sprawdzenia osoby.

Dowod:

- create: `JobProfileService.cs:36-52`, linia 46,
- update: `JobProfileService.cs:57-74`, linia 69.

To jest szczegolnie niebezpieczne jako wzorzec: nazwa `ValidateReferencesAsync` daje reviewerowi poczucie, ze wszystkie referencje sa sprawdzone, podczas gdy jedna z istotnych nie jest.

---

# 9. P0-TENANT-005 - Offboarding.ProcessOwnerId bez tenant validation

**Severity:** HIGH / release blocker  
**Status:** OTWARTE

W tworzeniu offboardingu osoba odchodzaca jest sprawdzana w kontekscie organizacji, ale `ProcessOwnerId` jest przekazywany dalej bez analogicznej weryfikacji.

Dowod:

- `OffboardingService.cs` okolice linii 193-210,
- przekazanie `request.ProcessOwnerId` przy linii 208,
- update przy linii 236.

### Naprawa

`ProcessOwnerId` musi zostac zweryfikowany jako aktywna osoba/uzytkownik nalezacy do tej samej organizacji, zalezne od tego co domenowo oznacza owner procesu.

---

# 10. P0-TENANT-006 - korekta ownera assetu w audycie przyjmuje obcy PersonId

**Severity:** HIGH / release blocker  
**Status:** OTWARTE

`AssetAuditCampaignService.ResolveItemAsync` przy `OwnershipCorrected` sprawdza glownie, czy `NewOwnerPersonId` istnieje jako wartosc, a nastepnie wykonuje:

`asset.CorrectOwner(request.NewOwnerPersonId.Value)`

bez lookupu osoby po aktualnym `OrganizationId`.

Dowod:

- `AssetAuditCampaignService.cs:515-539`.

To jest bardzo istotne, poniewaz ten use-case wprost modyfikuje relacje wlascicielstwa aktywa.

---

# 11. P0-TENANT-007 - ServiceTicket.AssetInspectionId bez tenant validation

**Severity:** HIGH / release blocker  
**Status:** OTWARTE

`ServiceTicketService.OpenAsync` waliduje asset po tenant ID, ale `request.AssetInspectionId` jest przekazywany bez podobnego sprawdzenia.

Dowod:

- `ServiceTicketService.cs:56-75`, konstrukcja ticketu przy linii 67.

Baza rowniez nie daje tu kompletnego tenant-aware defense-in-depth.

---

# 12. Problem systemowy: niekompletne tenant-aware FK

Composite migration jest dobrym krokiem, ale obejmuje tylko wycinek grafu danych.

Relacje wymagajace ponownego przegladu i w wielu przypadkach composite FK to m.in.:

- Asset -> Team,
- Person -> Team,
- Person -> Manager,
- Team -> Manager,
- JobProfile -> DefaultManager,
- Assignment -> Person,
- ProcedureAcceptance -> Procedure/Person,
- ProcedureDocument -> Procedure,
- AssetInspection -> Asset/Assignment/OffboardingItem,
- AuditItem -> Asset/ExpectedPerson,
- AuditParticipant -> Person,
- OffboardingCase -> Person/ProcessOwner,
- OffboardingItem -> Asset/Assignment/License,
- Reservation -> requester/assignment,
- ReservationItem -> category/kit/asset/original asset,
- ServiceTicket -> Asset/Inspection.

Nie kazda relacja musi miec identyczna semantyke kasowania, ale **kazda tenant-owned relacja powinna miec wymuszenie zgodnosci OrganizationId albo bardzo mocno udokumentowany powod, dlaczego nie**.

---

# 13. Zalecany model obrony multi-tenant

Dla tego systemu nie polegam na jednej warstwie. Rekomenduje 3 warstwy:

## Warstwa 1 - Application

Kazdy request z foreign GUID:

- lookup przez `(currentOrganizationId, foreignId)`,
- reject 400/404 jezeli obiekt nie nalezy do organizacji,
- zadnego repository `GetById(id)` dla tenant-owned danych.

## Warstwa 2 - EF Core

Rozwazyc scoped `ITenantContext` i globalne `HasQueryFilter` dla encji tenant-owned.

Wyjatki dla background jobs, public capability links i administracji musza byc jawne i testowane. `IgnoreQueryFilters()` powinno byc traktowane jak operacja uprzywilejowana.

## Warstwa 3 - PostgreSQL

Minimum:

- alternate/unique `(OrganizationId, Id)`,
- composite FK `(OrganizationId, ForeignId)` dla wszystkich tenant-owned references.

Dla jeszcze mocniejszej obrony: PostgreSQL RLS. Przy RLS trzeba poprawnie obslugiwac connection pooling i `SET LOCAL` w transakcji oraz oddzielic role migracyjne/backgroundowe.

---

# 14. P1-SEC-001 - uploady nadal sa buforowane przed limitem

**Severity:** HIGH dla availability  
**Status:** OTWARTE

W wielu endpointach wystepuje wzorzec:

1. `ReadFormAsync`,
2. kopiowanie `IFormFile` do `MemoryStream`,
3. `ToArray()`,
4. dopiero pozniej service-level validation limitu.

Przyklady w `TenebitEndpoints.cs`:

- onboarding multipart okolice linii 398,
- asset evidence okolice 554,
- procedure docs okolice 937,
- public offboarding upload okolice 1351,
- public audit upload okolice 1401,
- wspolny helper `ReadFileAsync` okolice 1501-1506.

### Dlaczego to problem

Limit np. 5 MB sprawdzony po odczytaniu calego body nie chroni procesu przed duzym body. Anonimowy endpoint moze zwiekszyc zuzycie RAM zanim logika biznesowa odrzuci plik.

Dodatkowo obrazy sa dekodowane przez ImageSharp. Sam limit rozmiaru skompresowanego pliku nie gwarantuje bezpiecznej liczby pikseli po dekompresji.

### Wymagane

- request body limits na serwerze/endpointach,
- `FormOptions.MultipartBodyLengthLimit`,
- weryfikacja `IFormFile.Length` przed kopiowaniem,
- streaming do ograniczonego bufora/storage,
- limit sumaryczny requestu i liczby plikow,
- limit szerokosc x wysokosc / max pixels przed pelnym decode,
- timeouty i rate limiting per actor/IP dla public uploads.

---

# 15. P1-SEC-002 - reset hasla nie uniewaznia istniejacych sesji

**Severity:** HIGH  
**Status:** OTWARTE

`AuthService.ResetPasswordAsync`:

- waliduje token,
- ustawia nowy password hash,
- oznacza reset token jako used,
- zapisuje zmiany.

Nie widze w tej sciezce:

- revoke wszystkich refresh tokenow uzytkownika,
- revoke trusted devices,
- security/session version bump,
- natychmiastowego odciecia istniejacych access tokenow.

Dowod: `AuthService.cs:420-443`.

### Scenariusz

Jezeli reset hasla nastapil dlatego, ze konto moglo zostac przejete, atakujacy posiadajacy stary refresh token moze nadal utrzymac sesje po zmianie hasla.

### Wymagane

Przy reset password:

1. revoke wszystkich refresh sessions,
2. revoke trusted device tokens,
3. podnies security/session version,
4. opcjonalnie krotszy access-token TTL i sprawdzanie session version przy requestach wysokiego ryzyka.

---

# 16. P1-SEC-003 - Stripe webhook: replay, idempotency i kolejnosc eventow

**Severity:** HIGH  
**Status:** OTWARTE

Weryfikacja HMAC jest obecna, co jest plusem. Problemem jest brak kompletnej ochrony lifecycle webhooka.

W aktualnej implementacji nie widze:

- wymuszenia tolerancji timestampu podpisu,
- trwalego zapisu Stripe Event ID,
- ochrony przed ponownym przetworzeniem tego samego eventu,
- ochrony przed starszym eventem nadpisujacym nowszy stan subskrypcji.

`SubscriptionService.HandleWebhookAsync` przetwarza event bez widocznej warstwy deduplikacji event ID.

Dodatkowo mapowanie nieznanego statusu Stripe do `Active` jest podejsciem fail-open. Nieznany status powinien byc traktowany konserwatywnie, logowany i nie powinien automatycznie dawac praw platnego planu.

### Wymagane

- timestamp tolerance zgodna z modelem Stripe,
- tabela processed webhook events z unique `EventId`,
- idempotent handler,
- ochrona przed out-of-order update,
- unknown status -> stan bezpieczny / manual review, a nie Active,
- test replay tego samego payloadu 2x,
- test starszy event po nowszym evencie.

---

# 17. P1-SEC-004 - rate limiter jest globalny dla calej aplikacji

**Severity:** HIGH/MEDIUM availability  
**Status:** OTWARTE

`Program.cs:63-77` definiuje fixed-window limiter:

- `auth`: 10/min,
- `public`: 60/min.

Nie jest to partitioned limiter per IP, tenant, user czy endpoint.

### Konsekwencja

Jeden klient lub bot moze zuzyc wspolny limit i chwilowo zablokowac logowanie lub publiczne akcje wszystkim pozostalym firmom.

Przy wielu replikach sytuacja odwrotna tez jest problemem: limit jest per proces, wiec efektywny limit zmienia sie wraz z liczba instancji.

### Naprawa

- `PartitionedRateLimiter`,
- auth: trusted client IP + endpoint + opcjonalnie email bucket,
- authenticated API: tenant/user,
- public capability endpoint: token/IP/endpoint,
- public report: asset/IP/cooldown,
- przy HA rozwazyc limiter na ingress/API gateway lub shared store.

---

# 18. P1-SEC-005 - X-Forwarded-For jest traktowany jako zaufany input

**Severity:** HIGH dla integralnosci audytu / MEDIUM security  
**Status:** OTWARTE

`CurrentUser.IpAddress` najpierw czyta bezposrednio `X-Forwarded-For`:

- `CurrentUser.cs:32-43`.

Nie znalazlem konfiguracji `UseForwardedHeaders` z zaufanymi proxy/networkami.

### Konsekwencja

Klient moze sam wyslac naglowek i ustawic dowolny adres IP, ktory potem moze trafic do tamper-evident acceptance/confirmation records.

Jezeli IP ma wartosc dowodowa/audytowa, obecny zapis jest latwy do sfałszowania.

### Naprawa

- skonfigurowac `ForwardedHeadersOptions`,
- jawnie okreslic KnownProxies/KnownNetworks,
- uruchomic middleware przed odczytem IP,
- korzystac z `RemoteIpAddress` po normalizacji,
- nie traktowac IP jako samodzielnego dowodu tozsamosci.

---

# 19. P1-HA-001 - OAuth state i 2FA challenge sa w IMemoryCache

**Severity:** HIGH dla HA / MEDIUM security  
**Status:** OTWARTE

`OAuthStateStore` i `TwoFactorChallengeStore` sa singletonami opartymi o in-memory state.

Przy jednej instancji jest to funkcjonalne. Przy kilku replikach:

1. start OAuth/2FA trafia do replica A,
2. callback/challenge trafia do replica B,
3. replica B nie ma stanu,
4. poprawny flow zostaje odrzucony.

Sticky sessions tylko maskuja problem.

### Naprawa

Redis/distributed cache albo kryptograficznie chroniony self-contained state z replay protection.

---

# 20. P1-HA-002 - background jobs uruchamiaja sie na kazdej replice

**Severity:** HIGH dla skalowania horyzontalnego  
**Status:** OTWARTE

`Infrastructure/DependencyInjection.cs:67-70` rejestruje kilka `AddHostedService`:

- AlertBackgroundService,
- DashboardSnapshotBackgroundService,
- OffboardingBackgroundService,
- EvidenceRetentionBackgroundService.

Nie znalazlem rozproszonego locka/leader election gwarantujacego single execution.

### Ryzyko

Przy 3 replikach ten sam job moze zostac wykonany 3 razy.

Skutki moga obejmowac:

- zduplikowane maile,
- zduplikowane akcje offboardingu,
- wyscigi przy cleanup,
- dodatkowe obciazenie DB,
- niespojne logi.

### Naprawa

- distributed lock w DB/Redis,
- job framework z persistent store,
- albo dedykowany worker single-active,
- kazdy job dodatkowo idempotentny.

---

# 21. P1-PRIV-001 - runtime logs sa nadal dostarczane razem z kodem

**Severity:** HIGH operational/privacy  
**Status:** OTWARTE

W przekazanej paczce znajduje sie ok. **7 MB runtime logow backendu**.

Podczas audytu potwierdzilem, ze pliki logow zawieraja m.in.:

- adresy e-mail,
- GUID-y obiektow/uzytkownikow,
- stack trace,
- sciezki i szczegoly runtime.

Nie reprodukuje konkretnych wartosci z logow w tym raporcie.

### Dlaczego to wazne

Paczka source code powinna byc bezpieczna do przekazania developerowi, CI lub audytorowi bez przypadkowego przekazywania danych runtime klientow.

Brakuje tez widocznego `.gitignore`/`.dockerignore` w dostarczonym materiale, ktory wykluczalby logs/node_modules/dist.

### Wymagane

Wykluczyc:

- `logs/`,
- `*.log`,
- `node_modules/`,
- `dist/`,
- lokalne sekrety/env,
- dumpy DB,
- pliki uploadow.

Logi produkcyjne musza miec retention, access control i polityke PII redaction.

---

# 22. P1-FUNC-001 - frontend nadal kopiuje bledny publiczny link offboardingu

**Severity:** HIGH funkcjonalny / MEDIUM security design  
**Status:** OTWARTE

`OffboardingPage.tsx` nadal buduje link w rodzaju:

`/exit/{caseItem.id}`

podczas gdy backendowy model publiczny opiera sie na capability tokenie.

To jest pozostaloscia po starym modelu UUID-as-link.

### Skutek

UI moze kopiowac niedzialajacy albo konceptualnie niepoprawny link.

### Naprawa

Frontend nie powinien sam konstruowac publicznego URL z encji ID. Backend powinien zwracac gotowy public URL/token po regenerate/create i tylko ten URL ma byc kopiowany.

---

# 23. P1-AUTH-001 - access JWT nie waliduje issuer/audience

**Severity:** MEDIUM/HIGH hardening  
**Status:** OTWARTE

`Program.cs:85-93`:

- `ValidateIssuer = false`,
- `ValidateAudience = false`.

Przy jednym backendzie i poprawnie chronionym signing key nie daje to samo w sobie prostego tenant takeover, ale oslabia granice zaufania tokenu.

### Rekomendacja

TokenIssuer powinien ustawic kontrolowane `iss` i `aud`, a API powinno je wymagac.

---

# 24. P1-AUTH-002 - role/deactivation moga pozostac wazne do wygasniecia access JWT

**Severity:** MEDIUM/HIGH  
**Status:** OTWARTE

Access token ma ok. 30 minut waznosci. Role sa zapisane w JWT.

Po:

- degradacji roli,
- odebraniu dostepu,
- deaktywacji usera,

stary access token moze zachowac stare claims do expiry, o ile endpoint nie wykonuje dodatkowego live checku.

### Rekomendacja

Opcje:

1. krotszy access TTL, np. 5-10 min,
2. `SecurityVersion` / `SessionVersion` w userze i claimie,
3. cache'owany server-side check dla operacji wysokiego ryzyka,
4. natychmiastowy revoke refresh sessions przy deaktywacji/role downgrade.

---

# 25. P1-DATA-001 - TOTP secret w plaintext

**Severity:** HIGH po kompromitacji DB/backupu  
**Status:** OTWARTE

Sekret TOTP jest danymi uwierzytelniajacymi i nie powinien byc traktowany jak zwykle pole profilu.

Hash nie wystarczy, bo backend potrzebuje sekretu do generowania/weryfikowania kodow. Potrzebne jest szyfrowanie at rest z kluczem poza baza.

### Rekomendacja

- envelope encryption / KMS / Data Protection z trwalym key ringiem,
- rotacja kluczy,
- brak sekretu w logach,
- scisly access path.

---

# 26. P1-DATA-002 - sensitive custom fields i license keys sa plaintext w DB

**Severity:** HIGH zalezne od danych klienta  
**Status:** OTWARTE

UI masking jest wartosciowe, ale nie chroni przed:

- wyciekiem backupu,
- SQL read access,
- kompromitacja DB account,
- snapshotem infrastruktury.

Jesli custom field jest oznaczony jako sensitive, oczekiwalbym rzeczywistej ochrony danych at rest, a nie tylko ukrycia w UI.

### Rekomendacja

- jawna klasyfikacja danych,
- szyfrowanie per-field dla sekretow,
- osobne uprawnienia do reveal,
- audit reveal,
- nie zwracac sekretu w zwyklym list endpointzie.

---

# 27. P1-ABUSE-001 - public QR report moze generowac spam e-mail

**Severity:** MEDIUM/HIGH availability/abuse  
**Status:** OTWARTE

Publiczne zgloszenie issue dla assetu moze wysylac wiadomosc do owner/admin organizacji.

Przy globalnym limiterze publicznym nie ma mocnej ochrony per asset/per source.

### Atak

Bot moze wielokrotnie zglaszac ten sam asset i generowac spam do administratorow firmy.

### Rekomendacja

- per-IP + per-asset cooldown,
- deduplikacja identycznych zgloszen,
- kolejka mailowa,
- abuse telemetry,
- opcjonalny CAPTCHA/challenge po przekroczeniu progu.

---

# 28. P1-DOMAIN-001 - globalna unikalnosc e-mail moze blokowac multi-company usera

**Severity:** MEDIUM produktowo / HIGH jesli wymagany cross-company account  
**Status:** OTWARTE / wymaga decyzji produktowej

Model posiada globalny unique index e-mail oraz dodatkowo org+email.

Dla SaaS obslugujacego 100 firm jest realny przypadek:

- konsultant,
- outsourced IT,
- biuro rachunkowe,
- MSP,
- wlasciciel kilku spolek,

ktory powinien miec dostep do wiecej niz jednej organizacji tym samym adresem.

Obecny model moze to blokowac.

### Docelowo

Lepszy model:

- Identity/User globalny,
- OrganizationMembership osobno,
- role i status per membership.

Jesli produkt swiadomie zakazuje userowi wielu organizacji, trzeba to zapisac jako invariant i przetestowac.

---

# 29. P1-RACE-001 - limit planu assetow ma race condition

**Severity:** MEDIUM  
**Status:** OTWARTE

`AssetService.CreateAsync`:

1. laduje liste aktywow,
2. liczy `currentAssets.Count`,
3. sprawdza limit,
4. pozniej dodaje asset.

Dwa rownolegle requesty moga oba zobaczyc np. 99/100 i oba utworzyc asset, dajac 101/100.

Dodatkowo ladowanie calej listy tylko do policzenia jest mniej wydajne niz `COUNT(*)`.

### Naprawa

- atomowy counter/invariant,
- transakcja i odpowiednia izolacja/lock,
- lub licznik usage aktualizowany atomowo,
- przynajmniej repository `CountAsync`, nie materializacja wszystkich assetow.

---

# 30. P1-RACE-002 - check-then-insert uniqueness nadal moze skonczyc sie 500

**Severity:** MEDIUM  
**Status:** OTWARTE

W wielu serwisach wystepuje:

1. `ExistsAsync`,
2. jezeli false -> insert,
3. DB ma unique index.

Dwa rownolegle requesty moga przejsc check i jeden z nich dostanie provider `DbUpdateException` na save.

Obecny centralny mapping dotyczy przede wszystkim `DbUpdateConcurrencyException`, nie wszystkich unique violations.

### Naprawa

- traktowac constraint w DB jako source of truth,
- mapowac konkretne PostgreSQL unique violation do 409,
- zachowac pre-check tylko dla lepszego UX.

---

# 31. P1-SCALE-001 - public token lookup jest O(N) po wszystkich tenantach

**Severity:** MEDIUM teraz, HIGH przy wzroscie  
**Status:** OTWARTE

Dotyczy assignmentow, a podobny wzorzec wystepuje w offboardingu.

Nie powinno sie pobierac wszystkich aktywnych capability records z calej bazy do procesu tylko po to, aby porownac jeden token.

### Wymagane

- deterministic hash tokenu,
- exact DB lookup po hash,
- indexed column,
- tylko potrzebne Include,
- expiry/revocation w WHERE.

---

# 32. P1-SCALE-002 - joby skanuja organizacje globalnie

**Severity:** MEDIUM  
**Status:** OTWARTE

Dla 100 firm moze to jeszcze dzialac, ale koszt jobow rosnacy liniowo po wszystkich tenantach oraz uruchamianie ich na kazdej replice jest zlym fundamentem.

### Rekomendacja

- query tylko rekordow wymagajacych dzialania,
- indeksy po status/due date/OrganizationId,
- batching,
- checkpoint,
- idempotency,
- distributed scheduling.

---

# 33. P2-PERF-001 - location endpoints nadal laduja zbyt duzo danych

**Severity:** MEDIUM  
**Status:** CZESCIOWO OTWARTE

Czesc delete/count zostala poprawiona na DB-side count, co jest plusem.

Natomiast list/inventory nadal moze ladowac szeroki zestaw assets i people organizacji, zamiast wykonywac precyzyjne agregacje/projekcje po stronie DB.

Dla kilku tysiecy rekordow per firma zacznie to generowac niepotrzebne allocations i transfer z DB.

---

# 34. P2-DOMAIN-001 - location hierarchy ma magiczny guard 20

**Severity:** MEDIUM  
**Status:** OTWARTE

Ograniczenie petli `guard < 20` zapobiega nieskonczonej petli, ale nie jest pelnym invariantem domenowym.

Problemy:

- glebokosc >20 moze zostac cicho ucieta,
- nie ma czytelnego bledu biznesowego,
- cycle detection powinien uzywac `visited` IDs, a nie tylko licznika.

### Rekomendacja

Jawnie ustalic max depth i odrzucac niepoprawne drzewo przy write, a przy read uzywac cycle detection.

---

# 35. P2-API-001 - OpenAPI JSON jest mapowane bez warunku Production

**Severity:** LOW/MEDIUM  
**Status:** OTWARTE

`app.MapOpenApi()` jest poza `IsDevelopment()`.

Scalar UI jest tylko development, ale schema API pozostaje publicznie mapowana.

Nie jest to samo w sobie powazna luka, ale w zamknietym SaaS ograniczylbym lub uwierzytelnil schema endpoint na produkcji, chyba ze publiczne OpenAPI jest swiadoma cecha produktu.

---

# 36. P2-DEPLOY-001 - TLS/HSTS/proxy trust nie sa wymuszone w aplikacji

**Severity:** MEDIUM zalezne od infrastruktury  
**Status:** OTWARTE / moze byc realizowane na ingress

Nie znalazlem w aplikacji kompletnego:

- `UseHttpsRedirection`,
- HSTS,
- trusted ForwardedHeaders,
- restrykcyjnego AllowedHosts.

Jezeli reverse proxy/ingress gwarantuje TLS, HSTS i host validation, aplikacja nie musi duplikowac wszystkiego. Ale taka zaleznosc powinna byc jawna, testowana i udokumentowana.

`AllowedHosts=*` plus zaufanie do surowego X-Forwarded-For jest obecnie za luzne.

---

# 37. P2-AUTH-001 - OAuth callback nadal przenosi access JWT w URL fragment

**Severity:** MEDIUM  
**Status:** OTWARTE

Fragment `#token=...` nie jest wysylany w HTTP Referer tak jak query string, co ogranicza czesc ryzyka. Nadal token trafia do browser URL state i jest dostepny dla JavaScriptu/extensions/devtools.

Lepszy model:

- callback ustawia secure HttpOnly refresh/session cookie,
- frontend dostaje jednorazowy krotki authorization code,
- access token pobierany jest przez kontrolowany endpoint i trzymany w pamieci.

---

# 38. P2-VALID-001 - centralna walidacja requestow jest niekompletna

**Severity:** MEDIUM  
**Status:** CZESCIOWO NAPRAWIONE

Dodano `ValidationEndpointFilter`, co jest dobrym ruchem po poprzednim przypadku `login` -> 500 dla niepoprawnego inputu.

Problem: filtr DataAnnotations moze walidowac tylko te request DTO, ktore faktycznie maja atrybuty.

W heurystycznym przegladzie positional `*Request` records znalazlem ok. 63 request models, z czego tylko ok. 11 mialo widoczne DataAnnotations. To nie jest formalny parser Roslyn, ale pokazuje skale nierownomiernego pokrycia.

Przyklady obszarow wymagajacych kompletnej walidacji:

- assets create/update/report,
- service tickets,
- procedures,
- people/team,
- offboarding,
- licenses,
- organization update,
- job profiles,
- assignments,
- onboarding,
- audits.

### Rekomendacja

Wybrac jeden spójny standard:

- DataAnnotations na wszystkich request DTO, albo
- FluentValidation/analogiczny validator per request,
- automatyczne HTTP tests: null/empty/too long/invalid enum/invalid GUID relation.

---

# 39. Security pozytywy, ktorych nie nalezy ignorowac

Raport jest krytyczny, ale trzeba uczciwie wskazac co jest dobrze:

- repository reads w przejrzanych glownych sciezkach konsekwentnie przyjmuja `OrganizationId`,
- raw SQL w LocationEndpoints jest parametryzowany; nie znalazlem tam prostego SQL injection,
- CORS ma jawne originy zamiast `AllowAnyOrigin` z credentials,
- refresh token jest rotowany przy refresh,
- forgot password nie ujawnia wprost czy konto istnieje,
- OAuth state jest jednorazowy i PKCE S256 jest uzywane,
- OIDC token validation jest teraz kryptograficzna,
- global exception handler nie zwraca stack trace do klienta,
- health check nie zwraca raw DB exception,
- CSV export ma ochrone przed formula injection,
- QR label text jest HTML-encoded,
- ImageSanitizer usuwa metadata EXIF/ICC/IPTC/XMP i re-encoduje obraz,
- produkcyjny JWT/DB default fail-closed jest duzym krokiem naprzod,
- assignment capability token jest losowy i hashowany,
- dodano realne integration tests przez WebApplicationFactory.

---

# 40. Clean Architecture - **65/100**

## Co jest dobre

Kierunek zaleznosci projektow jest zasadniczo poprawny:

- Domain nie zalezy od infrastruktury,
- Application zalezy od Domain,
- Infrastructure implementuje Application abstractions,
- API pelni role composition root.

To jest prawdziwy fundament Clean Architecture, a nie tylko nazwy folderow.

## Co obniza wynik

### 40.1. API bezposrednio korzysta z DbContext

`LocationEndpoints` jest najczytelniejszym wyjatkiem. Endpointy zawieraja logike DB i raw SQL zamiast delegowac use-case do Application.

Konsekwencje:

- security rules moga byc zaimplementowane inaczej niz w serwisach,
- trudniejsze testy jednostkowe,
- API zna Infrastructure,
- logika biznesowa rozprasza sie po composition layer.

### 40.2. Giant endpoint file

`TenebitEndpoints.cs` ma ok. **1512 linii**.

Jedna klasa/file mapuje zbyt wiele domen. To pogarsza discoverability, review i ryzyko przypadkowego braku policy/filtera.

### 40.3. Giant services

Przyklady:

- `OffboardingService.cs` ok. 1025 linii,
- `AssetAuditCampaignService.cs` ok. 734,
- `AssignmentService.cs` ok. 675,
- `AlertCheckService.cs` ok. 676,
- `AuthService.cs` ok. 583.

Nie twierdze, ze liczba linii sama w sobie jest bugiem. Tutaj koreluje jednak z liczba odpowiedzialnosci i zaleznosci.

### 40.4. Application tworzy konkretny service przez `new`

`AssignmentService` konstruuje `new AssetReturnDispositionService(...)` zamiast dostac abstrakcje/zaleznosc.

To oslabia DIP i utrudnia testowanie/zmiane implementacji.

---

# 41. SOLID - **49/100**

## S - Single Responsibility: 42/100

Najwiekszy problem.

Duzy OffboardingService, AssignmentService, AuditCampaignService i endpoint file lacza:

- autoryzacje,
- orkiestracje use-case,
- walidacje,
- mapowanie DTO,
- generowanie tokenow/linkow,
- log activity,
- czasem generowanie dokumentow/side effects.

Rekomendacja: dzielic po use-case, nie po technicznej metodzie CRUD.

## O - Open/Closed: 55/100

Abstrakcje repozytoriow i gatewayow pomagaja, ale duze switche i centralne serwisy oznaczaja, ze dodanie nowego wariantu czesto wymaga modyfikacji wielu miejsc.

## L - Liskov: 70/100

Nie znalazlem wyraznych, systemowych naruszen substytucji. Ten obszar nie jest glownym problemem.

## I - Interface Segregation: 55/100

Repository abstractions sa w wielu miejscach sensowne, ale wraz z rozrostem domeny `IRepositories.cs` i duze zaleznosci serwisow staja sie szerokie.

## D - Dependency Inversion: 45/100

Generalny kierunek App -> abstractions jest dobry, ale:

- LocationEndpoints -> DbContext,
- bezposrednie tworzenie serwisu w AssignmentService,
- czesc runtime state jest przywiazana do IMemoryCache.

---

# 42. DRY - **63/100**

Nie widze katastrofalnej duplikacji, ale sa powtarzajace sie wzorce, ktore warto skonsolidowac.

### Przyklad: CSV escaping

Mechanizm ochrony CSV/formula injection wystepuje w wiecej niz jednym serwisie. Security logic powinna byc jedna, dobrze przetestowana funkcja.

### Przyklad: public token resolution

Assignment i Offboarding maja podobny schemat `ListWithPublicTokenAsync + foreach Verify`. Warto wydzielic repo lookup po hash zamiast duplikowac kosztowny wzorzec.

### Przyklad: role checks

Centralny `AccessPolicy` jest plusem. Trzeba isc dalej i unifikowac role policy w endpoint/use-case tak, aby reviewer nie musial sprawdzac obu warstw recznie.

---

# 43. YAGNI - **69/100**

Projekt nie wyglada na przepelniony abstrakcjami bez celu. Wiele elementow odpowiada realnym wymaganiom SaaS.

Punkty w dol glownie za:

- czesc recznych wrapperow/mapowan, ktore zwiekszaja powierzchnie utrzymania,
- rozrost kilku serwisow zamiast prostszych use-case handlers,
- infrastrukturowe rozwiazania, ktore sa jednoczesnie zbyt proste dla HA, ale wystarczajaco zlozone, by rozpraszac logike.

---

# 44. KISS - **55/100**

Kod czesto jest czytelny lokalnie, ale caly przeplyw use-case wymaga skakania przez:

- endpoint,
- service,
- repo,
- domain,
- mapper,
- activity log,
- czasem helper/gateway.

To jest czesciowo naturalne w Clean Architecture, ale duze serwisy i wyjatki od warstw sprawiaja, ze reguly nie sa latwe do przewidzenia.

Najlepsza poprawa KISS nie polega na usunieciu warstw. Polega na tym, zeby **kazdy use-case mial jedno oczywiste miejsce** i jedna droge autoryzacji/tenant validation.

---

# 45. Clean Code - **55/100**

## Plusy

- nazwy domenowe sa zwykle czytelne,
- duza czesc metod komunikuje intencje,
- error/result model jest lepszy niz przypadkowe exceptiony,
- komentarze przy poprawkach security sa sensowne,
- centralny correlation ID i generyczny 500 pomagaja operacyjnie.

## Minusy

### 45.1. Za duze pliki

Najwieksze pliki aplikacyjne utrudniaja review security. Przy systemie multi-tenant to ma znaczenie praktyczne: im wiecej use-case'ow w jednym serwisie, tym latwiej przeoczyc jeden brak `OrganizationId`.

### 45.2. Nierowna walidacja

Czesc DTO jest dobrze walidowana, czesc polega na DomainException, czesc na bazie, a czesc nie ma pelnego guardowania foreign IDs.

### 45.3. Magic strings / tlumaczenie error messages

`ErrorMessageTranslator` opiera sie na dokladnych stringach. Zmiana tekstu backendowego moze popsuc mapping frontendu.

Lepsze sa stale error codes i osobne localized message resources.

### 45.4. Empty catches po stronie frontendu

Miejsca takie jak download protocol w Offboarding UI powinny przynajmniej dawac feedback, telemetry lub controlled error state.

---

# 46. Frontend - **62/100**

## Co sprawdzilem

Uruchomilem TypeScript compiler bez Vite bundlera:

`tsc -b`

Wynik: **PASS / exit code 0**.

To oznacza, ze aktualny frontend przechodzi typecheck w dostarczonym stanie zaleznosci.

## Czego nie udalo sie wiarygodnie uruchomic

Pelny `vite build` i Vitest nie startuja z dostarczonego `node_modules`, poniewaz paczka nie zawiera wymaganej binarnej zaleznosci Rollupa dla Linux (`@rollup/rollup-linux-x64-gnu`).

Nie traktuje tego jako dowodu, ze source frontend jest zepsuty. Traktuje to jako dowod, ze **dostarczanie node_modules w paczce jest nieprzenosne i nie powinno byc elementem procesu build/deploy**.

CI powinno wykonywac clean install z lockfile na docelowym runnerze.

## Plusy

- TypeScript typecheck przechodzi,
- access token jest zasadniczo trzymany w pamieci, a nie w stalej localStorage,
- refresh flow opiera sie o cookie,
- React domyslnie escapuje zwykle renderowane wartosci,
- QR SVG pochodzi z kontrolowanego backend generatora i label jest kodowany.

## Minusy

- Offboarding public link jest nadal zbudowany wedlug starego modelu,
- tylko kilka plikow testowych frontendu,
- brak widocznego E2E critical path suite,
- bardzo duze strony/komponenty: `AssetsPage`, `PeoplePage`, `SettingsPage`, `OffboardingPage`,
- bardzo duzy `domain.ts`,
- brak pelnego lint gate w scripts/config przekazanej paczki,
- `node_modules` i `dist` sa w source package.

---

# 47. Testy / QA - **59/100**

To jest jeden z obszarow z najwiekszym postepem.

## Duzy plus: integration tests z prawdziwym hostem API

Dodano `WebApplicationFactory<Program>` i `TenantIsolationTests`.

W aktualnym pliku widze 9 testow obejmujacych m.in.:

- tenant B nie moze odczytac asset tenant A,
- tenant B nie moze update asset tenant A,
- tenant B nie moze delete asset tenant A,
- listy sa izolowane,
- employee nie moze tworzyc location,
- owner moze,
- employee nie moze listowac organization users,
- unauthenticated request jest odrzucany,
- nieznany public assignment token jest odrzucany.

To jest **realna poprawa** w stosunku do samych unit testow serwisow.

## Dlaczego nadal tylko 59/100

Obecne testy koncentruja sie glownie na cross-tenant READ/CRUD konkretnego assetu. Nie testuja najwazniejszej klasy bledow, ktora pozostala: **foreign-ID injection**.

### Brakujace testy krytyczne

1. tenant A tworzy Asset z TeamId tenant B -> reject,
2. tenant A update Asset z TeamId tenant B -> reject,
3. tenant A tworzy Person z TeamId tenant B -> reject,
4. tenant A tworzy Person z ManagerId tenant B -> reject,
5. tenant A update Person z B refs -> reject,
6. tenant A tworzy Team z ManagerId B -> reject,
7. JobProfile A z DefaultManagerId B -> reject,
8. Offboarding A z ProcessOwnerId B -> reject,
9. Audit ownership correction A z PersonId B -> reject,
10. ServiceTicket A z AssetInspectionId B -> reject,
11. bezposrednia proba DB insert z cross-org composite relation -> constraint violation,
12. public token expired -> 404/generic reject,
13. public token revoked -> reject,
14. regeneracja tokenu uniewaznia stary token,
15. password reset uniewaznia wszystkie refresh sessions,
16. role downgrade/deactivation zachowanie aktywnego JWT,
17. Stripe webhook replay x2 -> stan zmienia sie raz,
18. out-of-order Stripe webhook -> starszy event nie cofa stanu,
19. upload > limit jest odrzucony przed alokacja calego body,
20. rate limiter nie pozwala jednemu IP zablokowac wszystkich firm.

## RequestValidationTests

Dodano 6 testow walidacji. Sa przydatne, ale w duzej mierze testuja `Validator.TryValidateObject` bez pelnego realnego HTTP pipeline.

Dla krytycznych requestow potrzebne sa testy `POST` do prawdziwego hosta.

## Ograniczenie audytu backend tests

W srodowisku audytu nie ma SDK `dotnet`, dlatego **nie moglem wykonac `dotnet build` ani `dotnet test`**. Nie wpisuje im PASS bez uruchomienia.

Ocena 59 uwzglednia jakosc i zakres kodu testow, ale nie udaje wyniku wykonania, ktorego nie bylo.

---

# 48. Backend reliability - **59/100**

## Poprawione

- startup czeka na inicjalizacje DB,
- production bad-secret/default-db config zatrzymuje start,
- concurrency exception jest mapowane na 409,
- generic 500 nie wycieka exception message,
- dodano wiecej validation guardow.

## Nadal problematyczne

- check-then-insert races,
- plan limit race,
- public token O(N) lookup,
- reset session lifecycle,
- webhook replay/order,
- background job duplication,
- upload memory pressure,
- niekompletna walidacja requestow,
- niekompletne tenant foreign reference checks.

---

# 49. Scalability / HA - **39/100**

100 firm samo w sobie nie jest duza liczba dla jednego sensownie zaprojektowanego PostgreSQL i kilku instancji API. Problemem nie jest liczba tenantow, tylko kilka wzorcow, ktore utrudniaja bezpieczne skalowanie:

- state OAuth/2FA w pamieci jednej instancji,
- hosted jobs na kazdej replice,
- rate limiter per-process/global bucket,
- public token lookup po wszystkich rekordach,
- ladowanie kolekcji tylko do policzenia,
- location inventory broad loads,
- brak jawnego distributed coordination.

Przed scale-out trzeba te elementy uporzadkowac. Inaczej dodanie drugiej repliki moze zmniejszyc przewidywalnosc systemu zamiast ja zwiekszyc.

---

# 50. Deployment / configuration - **48/100**

## Znaczna poprawa

- production JWT secret fail-closed,
- production default Postgres password fail-closed,
- AutoCreate/Seed w bazowym appsettings sa bezpieczniejsze,
- migracja przed serving traffic.

## Nadal do poprawy

- fail-closed tylko dla `Production`, nie dla kazdego non-dev environment,
- `node_modules`/`dist`/runtime logs w paczce,
- brak widocznych ignore files,
- brak pelnego reproducible build z czystego lockfile w audytowanej paczce,
- OpenAPI schema mapowane globalnie,
- TLS/proxy assumptions nie sa jawne w aplikacji,
- brak backend SDK w moim srodowisku uniemozliwil weryfikacje builda.

---

# 51. Observability - **47/100**

## Plusy

- correlation ID,
- Serilog request logging,
- centralny exception handler,
- szczegoly bledow po stronie serwera zamiast klienta.

## Minusy

- runtime logs w source package,
- potwierdzone adresy e-mail/GUID-y w logach,
- potencjalny raw provider response przy Stripe error,
- brak dowodu na scentralizowana redakcje PII,
- brak w raporcie/paczce dowodu na alerting dla cross-tenant anomaly, auth abuse, job duplicate czy webhook replay.

### Wymagane security telemetry

Warto miec alerty dla:

- powtarzajacych sie 403/404 z obcymi GUID,
- wielu invalid public tokens z jednego IP,
- masowych reset/login failures,
- powtarzanych Stripe event IDs,
- background job overlap,
- storage/upload reject rate,
- nietypowej liczby public reports na jeden asset.

---

# 52. Status wszystkich glownych problemow z poprzedniego audytu

Legenda:

- **FIXED** - rdzen problemu zamkniety,
- **PARTIAL** - poprawiono wazna czesc, ale problem nie jest kompletnie zamkniety,
- **OPEN** - zasadniczo nadal wystepuje,
- **DECISION** - wymaga swiadomej decyzji produktowej/infrastrukturalnej.

| # | Poprzedni problem | Status teraz | Komentarz |
|---:|---|---|---|
| 1 | Public assignment UUID jako credential | **FIXED** | losowy hashowany token + expiry/revoke; lookup O(N) pozostaje |
| 2 | Domyslny JWT + production fail-open | **FIXED/PARTIAL** | Production zatrzymuje start; non-prod public env hardening pozostaje |
| 3 | Brak centralnego tenant enforcement | **PARTIAL / BLOCKER** | dodano czesc composite FK, nadal brak kompletnego wymuszenia i sa cross-tenant writes |
| 4 | Location mutation role + API -> DbContext | **PARTIAL** | authz poprawione, naruszenie warstw nadal istnieje |
| 5 | Assignment protocol bez role gate | **FIXED** | role gate jest |
| 6 | OIDC id_token tylko dekodowany | **FIXED CORE** | podpis/iss/aud/lifetime walidowane; nonce/provider hardening pozostaje |
| 7 | Brak central request validation / login 500 | **PARTIAL** | dodano filter i atrybuty dla czesci DTO, pokrycie niepelne |
| 8 | Upload buforowany przed limitem | **OPEN** | nadal kilka sciezek MemoryStream/ToArray po ReadFormAsync |
| 9 | Password reset nie revoke sessions | **OPEN** | brak revoke refresh/trusted/session version |
| 10 | Stripe replay/idempotency/order | **OPEN** | podpis jest, lifecycle eventu nadal niepelny |
| 11 | Global auth/public rate limiter | **OPEN** | shared bucket, nie partitioned |
| 12 | Raw X-Forwarded-For trust | **OPEN** | nadal bez trusted proxy middleware |
| 13 | Health leaks ex.Message | **FIXED** | generyczna odpowiedz |
| 14 | App start przed migracja | **PARTIAL/FIXED CORE** | init przed Run; deployment migration policy nadal wymaga dopiecia |
| 15 | Default PostgreSQL password fail-open | **FIXED Production** | app nie startuje w Production |
| 16 | OAuth/2FA state IMemoryCache | **OPEN** | problem przy multi-replica |
| 17 | Jobs na kazdej replice | **OPEN** | brak distributed leader/lock |
| 18 | Evidence moze wskazac obcy assignment | **FIXED** | walidacja + tenant composite FK |
| 19 | Organization users list bez role gate | **FIXED** | owner/admin gate + test |
| 20 | Offboarding UI kopiuje stary public link | **OPEN** | nadal buduje URL z case ID |
| 21 | Custom fields concurrency update | **FIXED CORE** | poprawiony model aktualizacji |
| 22 | Runtime logs w paczce | **OPEN** | potwierdzone PII/GUID w logach |
| 23 | OpenAPI schema na produkcji | **OPEN** | `MapOpenApi()` globalne |
| 24 | TLS/HSTS/ForwardedHeaders/AllowedHosts | **OPEN/DECISION** | moze byc ingress, ale brak jawnego enforcementu |
| 25 | OAuth access token w URL fragment | **OPEN** | nadal obecny wzorzec |
| 26 | TOTP secret plaintext | **OPEN** | wymaga szyfrowania at rest |
| 27 | Sensitive fields/license keys plaintext | **OPEN** | UI masking nie chroni DB/backupu |
| 28 | Concurrency -> generic 500 | **FIXED** | mapowanie na 409 |
| 29 | Manual Stripe client/resilience | **OPEN** | nadal brak pelnego resilient gateway pattern |
| 30 | Public QR report e-mail spam | **OPEN** | global limiter jest niewystarczajacy |
| 31 | Global email uniqueness | **OPEN/DECISION** | blokuje multi-org identity model |
| 32 | Asset plan limit race | **OPEN** | count/check/insert nieatomowe |
| 33 | Jobs scan all organizations | **OPEN** | skala liniowo |
| 34 | Location loads broad tenant data | **PARTIAL** | czesc count poprawiona, inventory/list nadal szerokie |
| 35 | Location max depth magic 20 | **OPEN** | guard zamiast jawnego invariant/cycle detection |
| 36 | App JWT issuer/audience disabled | **OPEN** | oba false |
| 37 | Stale role/deactivation JWT 30 min | **OPEN** | brak session/security version |
| 38 | Check-then-insert unique race | **OPEN** | brak globalnego mapowania unique violation |
| 39 | AutoCreate/Seed unsafe defaults | **FIXED BASE/PARTIAL** | appsettings bezpieczniejsze; code fallbacks warto tez ustawic fail-safe |
| 40 | Brak real HTTP tenant tests | **PARTIAL - DUZA POPRAWA** | WebApplicationFactory + 9 tests; foreign-ID matrix nadal brak |
| 41 | Giant classes / SRP | **OPEN** | duze services/endpoints pozostaja |
| 42 | `new AssetReturnDispositionService` | **OPEN** | DIP/testability |
| 43 | CSV duplication | **OPEN low** | security escaping poprawione, duplikacja zostala |
| 44 | ErrorMessageTranslator po stringach | **OPEN** | lepsze stabilne error codes |
| 45 | node_modules/dist w paczce | **OPEN** | obecnie dodatkowo utrudnia reproducible Linux build |

---

# 53. Nowe problemy wykryte w drugiej wersji / doprecyzowane po poprawkach

Najwazniejsze nowe ustalenie to nie nowa funkcja, lecz **dokladniejsze udowodnienie niekompletnej izolacji**.

Poprzednio ocena multi-tenancy opierala sie w duzej mierze na braku centralnego defense-in-depth. Teraz po dodaniu czesci composite FK przesledzilem referencje foreign IDs i znalazlem konkretne use-case'y, ktore nadal przepuszczaja obcy GUID.

Nowe/poglebione findings:

1. Asset.TeamId cross-tenant injection,
2. Person.TeamId cross-tenant injection,
3. Person.ManagerId cross-tenant injection,
4. Team.ManagerId cross-tenant injection,
5. JobProfile.DefaultManagerId cross-tenant injection,
6. Offboarding.ProcessOwnerId cross-tenant injection,
7. Audit OwnershipCorrected -> NewOwnerPersonId cross-tenant injection,
8. ServiceTicket.AssetInspectionId cross-tenant injection,
9. public token lookup materializuje rekordy wszystkich tenantow,
10. public image upload ma nie tylko byte-size issue, ale tez potencjalny decompression/pixel bomb problem.

To sa rzeczy, ktore nalezy naprawic przed uznaniem multi-tenant boundary za bezpieczna.

---

# 54. Priorytet napraw - P0 przed realnymi klientami

## P0.1 - zamknac wszystkie foreign-ID tenant boundaries

Nie naprawiac tylko siedmiu wymienionych recznie. Zrobic systematyczny audit kazdego pola `*Id` w tenant-owned entity.

Dla kazdego pola odpowiedziec:

1. czy referenced entity jest tenant-owned?
2. czy Application sprawdza `(OrganizationId, Id)`?
3. czy DB ma composite FK?
4. czy test integracyjny probuje uzyc ID z tenant B?

Dopiero kompletna tabela relacji daje pewnosc.

## P0.2 - test matrix tenant A vs B

Dodac testy foreign-ID injection wymienione w sekcji testow.

Wazne: test ma sprawdzac nie tylko status HTTP, ale rowniez DB state po requestcie.

## P0.3 - DB defense-in-depth

Rozszerzyc `(OrganizationId, Id)` alternate keys i composite FKs na wszystkie tenant-owned relacje.

## P0.4 - body/upload limits przed alokacja

Publiczne uploady musza miec twardy limit na warstwie HTTP i dekodera obrazu.

## P0.5 - reset password = revoke sessions

Password reset nie jest zakonczony security-wise, dopoki stare refresh sessions pozostaja wazne.

## P0.6 - Stripe webhook idempotency/replay/order

Nie wdrazac billing automation bez persistent event dedupe i timestamp/order protection.

## P0.7 - multi-replica safety przed scale-out

Jesli produkcja ma miec >1 API instance:

- distributed OAuth/2FA state,
- distributed/single-active jobs,
- partitioned/distributed rate limiting.

Jesli pierwszy release ma miec 1 instance, zapisac to jako jawne ograniczenie operacyjne, nie udawac HA.

---

# 55. Priorytet P1 - przed stabilnym production rollout

1. JWT issuer/audience.
2. Krotszy access token lub security version.
3. trusted ForwardedHeaders.
4. TOTP encryption.
5. sensitive custom fields/license key encryption.
6. naprawa Offboarding public link.
7. exact indexed public token lookup.
8. per-actor public/auth rate limit.
9. QR report anti-spam.
10. unique DB violation -> 409.
11. atomowy plan asset limit.
12. OpenAPI production policy.
13. clean source/package hygiene.
14. CI build from lockfile, bez node_modules w ZIP.
15. frontend E2E dla auth, assignment, offboarding, tenant permissions.

---

# 56. Priorytet P2 - jakosc i utrzymanie

1. rozbic `TenebitEndpoints.cs` na domenowe endpoint modules,
2. przeniesc Location logic do Application,
3. rozbic OffboardingService na use-case handlers,
4. rozbic AssignmentService,
5. wyeliminowac `new AssetReturnDispositionService` z Application service,
6. wspolny CSV writer/security helper,
7. stabilne error codes zamiast tlumaczenia po stringach,
8. jawny location hierarchy invariant,
9. projekcje/COUNT w DB zamiast ladowania list,
10. analyzers i warnings-as-errors dla krytycznych projektow,
11. ESLint + frontend quality gate,
12. arch tests pilnujace kierunku zaleznosci.

---

# 57. Minimalny security test suite przed GO

## Tenant isolation

- A cannot GET B entity for kazdy glowny aggregate,
- A cannot PUT/PATCH/DELETE B entity,
- A cannot create A entity referencing B foreign ID,
- A cannot update A entity to reference B foreign ID,
- direct DB cross-org FK insert fails,
- list/search/export never returns B records,
- background jobs retain organization boundary,
- public capability token resolves exactly one object and no org enumeration.

## Auth

- bad/default secret -> process does not start in all non-dev public envs,
- expired JWT rejected,
- wrong issuer rejected,
- wrong audience rejected,
- disabled user behavior tested,
- role downgrade tested,
- refresh rotation replay rejected,
- reset password revokes refresh sessions,
- TOTP trusted device revoke tested.

## Public links

- random invalid token -> generic not found,
- expired -> reject,
- revoked -> reject,
- regenerate -> old token reject,
- rate limit per actor,
- no token in logs,
- token lookup uses index/exact hash.

## Uploads

- > limit rejected before full buffering,
- too many files rejected,
- huge image dimensions rejected,
- malformed image returns controlled 400,
- public upload concurrency stress,
- metadata stripped,
- content-type not trusted blindly.

## Stripe

- invalid signature rejected,
- valid but old timestamp rejected,
- duplicate EventId processed once,
- older event after newer does not revert state,
- unknown status does not grant paid access.

---

# 58. Clean Architecture target po poprawkach

Nie rekomenduje rewrite projektu. Fundament jest wystarczajaco dobry, aby go uporzadkowac iteracyjnie.

Docelowy przeplyw powinien byc:

`HTTP endpoint -> request validator -> application use case -> tenant-aware repositories -> domain -> unit of work`

A nie:

`HTTP endpoint -> DbContext/raw SQL/business decisions`

Dla kazdego use-case wymagajacego foreign ID warto miec wspolny wzorzec:

- CurrentTenant pobierany raz,
- referenced entities resolved przez tenant-scoped repo,
- domain otrzymuje juz zweryfikowane ID/entity,
- DB composite FK jest ostatnia linia obrony.

---

# 59. Czy potrzebny jest PostgreSQL RLS?

Nie jest absolutnie wymagany, aby zbudowac bezpieczny SaaS, ale przy wymaganiu "100 firm i zero wycieku miedzy nimi" daje wartosciowy defense-in-depth.

Jesli RLS nie zostanie wdrozone, wtedy wymagania wobec pozostalych warstw sa wyzsze:

- 100% tenant-aware repositories,
- global query filters lub rownowazny centralny mechanism,
- 100% composite FK,
- bardzo szeroka tenant integration suite,
- arch test blokujacy bezposredni DbContext poza Infrastructure/wyjatkami.

Moja preferencja dla tego projektu:

1. najpierw naprawic wszystkie cross-tenant reference holes,
2. dodac komplet composite FK,
3. dodac global tenant query filter/context,
4. potem rozwazyc RLS jako dodatkowa warstwe dla najbardziej wrazliwych tabel.

---

# 60. Czy 100 firm jest technicznie realne?

**Tak, liczba 100 tenantow sama w sobie nie jest problemem.**

Nie widze powodu do osobnych baz tylko dlatego, ze jest 100 firm. Shared PostgreSQL z `OrganizationId` moze spokojnie obslugiwac taki model.

Warunek jest jeden: tenant boundary musi byc wymuszona systemowo, a nie zalezec od pamieci developera w kazdej nowej metodzie.

Aktualny kod nadal w kilku miejscach zalezy wlasnie od tej pamieci.

---

# 61. Co oznacza obecne 56/100

To nie jest ocena "kod jest slaby" w ogolnym sensie.

Projekt ma:

- sensowny model warstw,
- sporo domeny,
- sporo testow jednostkowych,
- coraz lepsze testy integracyjne,
- poprawiona kryptografie/auth,
- sensowne repository scoping w wielu odczytach,
- coraz lepszy error handling.

Ale security SaaS jest asymetryczne. Dziesiec dobrych query nie kompensuje jednego query/write bez tenant constraint.

Dlatego:

- **quality/codebase:** okolice 60+/100,
- **authz po poprawkach:** ponad 70/100,
- **production tenant safety:** nadal ok. 41/100,
- **overall risk-weighted:** 56/100.

---

# 62. Co musi sie wydarzyc, zebym podniosl projekt do 70+

Minimum:

1. wszystkie wskazane cross-tenant foreign references zamkniete,
2. tenant FK matrix kompletna,
3. testy A/B dla foreign-ID injection zielone,
4. upload limits przed bufferingiem,
5. password reset revoke sessions,
6. Stripe event idempotency/replay protection,
7. XFF trust poprawiony,
8. public token lookup indexed/exact,
9. Offboarding link poprawiony,
10. clean build/test w CI z czystego checkoutu.

To moze realnie podniesc projekt w okolice **70-78/100** bez rewrite.

---

# 63. Co musi sie wydarzyc, zebym dal GO dla 100 firm

Moje minimalne kryteria:

- **0 otwartych P0 tenant boundary findings**,
- brak mozliwosci zapisania foreign ID z innej organizacji,
- kompletne DB defense-in-depth albo udokumentowane i przetestowane wyjatki,
- integration test suite tenant A/B dla read/write/reference/export,
- production startup fail-closed,
- auth/session lifecycle po reset/deactivation zweryfikowany,
- upload DoS controls,
- Stripe webhook replay/idempotency,
- multi-replica strategy albo jawny single-instance constraint,
- czysty backend build + wszystkie testy green,
- czysty frontend install/build/test z lockfile,
- brak runtime logs/secrets/customer data w artefaktach source/deploy.

Dopoki te warunki nie sa spelnione, moj status pozostaje **NO-GO**.

---

# 64. Ograniczenia audytu

Ten raport jest statycznym/strukturalnym audytem dostarczonej paczki plus proba uruchomienia dostepnych elementow.

## Udalo sie

- rozpakowac i przejrzec aktualna paczke,
- porownac ja z poprzednia wersja,
- przejrzec backend i frontend,
- przesledzic poprawki poprzednich P0/HIGH,
- sprawdzic repository/service tenant patterns,
- przejrzec migracje composite FK,
- przejrzec integration tests,
- uruchomic TypeScript typecheck - PASS,
- sprawdzic probe Vite/Vitest,
- przejrzec dostarczone logi pod katem rodzaju danych.

## Nie udalo sie wykonac

- `dotnet build`,
- `dotnet test`,

poniewaz w srodowisku audytu nie ma SDK .NET.

Nie bede udawal, ze backend test suite jest zielony bez uruchomienia.

Pelny Vite/Vitest nie wystartowal z powodu niekompletnego/platform-specific spakowanego `node_modules`, mimo ze TypeScript compiler przeszedl poprawnie.

Nie wykonywalem dynamicznego penetration testu przeciwko uruchomionej instancji z realnym PostgreSQL i reverse proxy. Dlatego findingi security sa oparte na kodzie i modelu danych, a nie na deklaracji, ze przeprowadzilem pelny pentest sieciowy.

---

# 65. Finalna tabela ocen

| Kategoria | Wynik / 100 | Werdykt |
|---|---:|---|
| Security | **58** | duzy postep, nadal kilka HIGH |
| Multi-tenant isolation | **39** | **release blocker** |
| Authentication / session | **66** | OIDC/JWT lepiej, lifecycle sesji do poprawy |
| Authorization | **71** | znacznie lepiej |
| Data protection | **44** | at-rest secrets nadal slabe |
| Clean Architecture | **65** | dobry fundament, kilka wyraznych wyjatkow |
| SOLID | **49** | SRP/DIP glownie obnizaja |
| DRY | **63** | akceptowalne, ale security helpers do konsolidacji |
| YAGNI | **69** | sensownie |
| KISS | **55** | za duze use-case services |
| Clean Code | **55** | czytelne lokalnie, slaba modularnosc duzych plikow |
| Backend reliability | **59** | poprawa, pozostaja race/HA issues |
| Frontend | **62** | typecheck PASS, malo testow, link bug |
| Tests / QA | **59** | duzy postep przez integration tests |
| Scalability / HA | **39** | single-instance assumptions nadal mocne |
| Deployment / config | **48** | fail-closed lepiej, packaging slaby |
| Observability | **47** | dobre podstawy, PII/log hygiene problem |

## **WYNIK OGOLNY: 56/100**

## **GOTOWOSC PRODUKCYJNA DLA 100 FIRM: 41/100**

# **FINALNY WERDYKT: NO-GO**

---

# 66. Najkrotsze podsumowanie dla decyzji biznesowej

Poprzednia wersja miala kilka problemow, przez ktore nie ufalbym nawet samemu mechanizmowi logowania/public access. Te problemy zostaly w duzej mierze naprawione.

Obecna wersja ma juz znacznie bardziej wiarygodny fundament security, ale nadal nie dopuscilbym jej do danych 100 niezaleznych klientow, poniewaz **tenant isolation jest niekompletne na poziomie relacji danych**.

Najwazniejsza rzecz do zrobienia teraz nie brzmi "dodaj jeszcze kilka ifow". Trzeba systemowo zamknac model:

**kazdy foreign ID tenant-owned -> Application validation po OrganizationId + composite FK w DB + test tenant A vs tenant B.**

Po tym dopiero nalezy zamknac upload DoS, session revoke, Stripe idempotency i HA. Wtedy ten projekt moze wejsc na poziom, przy ktorym ponowny audyt ma realna szanse zakonczyc sie `GO` bez przepisywania calego systemu.
