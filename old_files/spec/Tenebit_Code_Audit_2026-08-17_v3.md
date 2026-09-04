# Tenebit: pełny audyt techniczny i bezpieczeństwa, wersja 3

**Data audytu:** 2026-08-17

**Audytowany artefakt:** `Tenebit audit3.zip`

**Zakres:** backend, frontend, model domenowy, EF Core/PostgreSQL, migracje, autoryzacja, uwierzytelnianie, OAuth/OIDC, 2FA, sesje, multi-tenancy, tokeny publiczne, Stripe, uploady, walidacja, testy, Clean Architecture, SOLID, DRY, YAGNI, KISS, Clean Code, wydajność, skalowanie, deployment i observability.

**Założenie ryzyka:** system ma obsługiwać około 100 niezależnych firm. Najwyższy priorytet ma brak możliwości odczytu, modyfikacji lub powiązania danych jednej firmy z danymi innej firmy oraz poprawne ograniczanie dostępu wewnątrz jednej firmy.

# 1. Werdykt

# **WYNIK OGÓLNY: 58/100**

# **JAKOŚĆ INŻYNIERSKA KODU: 68/100**

# **GOTOWOŚĆ PRODUKCYJNA DLA 100 FIRM: 34/100**

# **FINALNY WERDYKT: NO-GO**

Nie dopuściłbym tej wersji do obsługi realnych danych 100 firm. Kod jest wyraźnie dojrzalszy od pierwszej i drugiej paczki, ale nadal zawiera potwierdzone ścieżki eskalacji uprawnień, obejścia dezaktywacji i 2FA, naruszenia row-level authorization oraz błędy cyklu życia linków publicznych. To nie są kwestie kosmetyczne ani wyłącznie Clean Code. Są to błędy, które mogą zmienić uprawnienia, sfałszować akceptację wydania albo ujawnić dane pracowników i majątku osobom, które według deklarowanego modelu ról nie powinny ich widzieć.

Najważniejsze rozróżnienie:

- **Nie potwierdziłem obecnie prostej ścieżki typu firma A odczytuje rekord firmy B przez standardowy endpoint.** Organizacyjne filtrowanie repozytoriów i część composite foreign keys są dużo lepsze.
- **Potwierdziłem jednak poważne naruszenia uprawnień wewnątrz organizacji.** Pracownik może zaakceptować cudze wydanie, manager widzi dane całej organizacji zamiast własnego zespołu, a każdy zalogowany użytkownik może otrzymać dashboard i inwentarz lokalizacji.
- **Baza nie jest jeszcze tenant-safe by construction.** Wiele relacji tenant-owned nadal zależy od ręcznej walidacji w serwisie. Jeden przyszły import, job lub endpoint bez tej walidacji może stworzyć cross-tenant reference.
- **Poprawki krytyczne z poprzednich wersji są realne.** Nie przenoszę starych zarzutów automatycznie. Przykładowo limit aktywów jest teraz egzekwowany pod blokadą organizacyjną, publiczny token assignment ma hash i indeks, a OIDC sprawdza podpis/JWKS.

## 1.1. Dlaczego gotowość produkcyjna jest niższa niż sugerowała poprzednia ocena

Poprzedni audyt przyznał autoryzacji 71/100. Po przejściu przez wszystkie alternatywne ścieżki i zestawieniu ich z opisem ról okazało się, że ta ocena była zbyt optymistyczna. Wersja trzecia poprawia kod, ale głębszy audyt ujawnia problemy, które muszą mieć większą wagę niż liczba naprawionych drobnych punktów. Ten raport zastępuje poprzednią ocenę.

## 1.2. Najważniejsze blokery produkcyjne

- **Admin może nadać sobie rolę Owner i może zdegradować lub dezaktywować właściciela.**
- **Dezaktywowany użytkownik może zalogować się przez wcześniej połączony OAuth.**
- **OAuth wydaje pełną sesję bez przejścia przez aplikacyjne TOTP/2FA.**
- **Pracownik może zaakceptować assignment innej osoby, jeżeli zna jego GUID.**
- **OAuth state nie jest związany z przeglądarką, co umożliwia login-CSRF/session swapping.**
- **Opis ról „tylko swoje” i „tylko własny zespół” nie jest egzekwowany na poziomie zasobu.**
- **Linki publiczne offboardingu i audytu mogą pozostać aktywne po anulowaniu lub zakończeniu procesu.**
- **Stripe nadal ma logikę entitlement fail-open dla nieznanego statusu oraz ufa metadanym organizacji bez pełnego skojarzenia z customer/subscription.**
- **Klucz szyfrowania pól może odziedziczyć klucz JWT. Rotacja JWT może wtedy uniemożliwić odszyfrowanie danych.**

# 2. Metodyka, zakres i ograniczenia

## 2.1. Co zostało wykonane

- Pełny przegląd źródeł backendu i frontendu, a nie tylko diff względem poprzedniej paczki.
- Przegląd granic warstw, zależności i największych klas.
- Przegląd wszystkich głównych ścieżek authn/authz, OAuth/OIDC, 2FA, refresh tokenów i publicznych tokenów.
- Przegląd modelu EF Core, composite keys, foreign keys i relacji tenant-owned.
- Przegląd uploadów, limitów, sanityzacji obrazów, przechowywania plików i współbieżności.
- Przegląd Stripe webhooków, idempotencji, kolejności zdarzeń, mapowania statusów i redirect URL.
- Przegląd konfiguracji produkcyjnej, forwarded headers, Dockerfile, nginx, logowania i artefaktu ZIP.
- Uruchomienie frontendowego TypeScript typecheck oraz ESLint.
- Inwentaryzacja testów i wyszukanie brakujących testów regresyjnych dla odkrytych scenariuszy.

## 2.2. Co udało się uruchomić

| Kontrola | Wynik |
| --- | --- |
| TypeScript `tsc -b` | **PASS**, kod wyjścia 0 |
| ESLint | **0 błędów, 19 ostrzeżeń** |
| Frontend production build | **NIEURUCHOMIONY POPRAWNIE** z powodu dołączonego Windowsowego `node_modules` i braku `@rollup/rollup-linux-x64-gnu` |
| Backend build/test | **NIEURUCHOMIONY**, środowisko audytu nie ma SDK .NET |
| PostgreSQL integration tests | **NIEURUCHOMIONE**, brak lokalnego PostgreSQL/Dockera/Podmana |

## 2.3. Ograniczenia audytu

- Nie twierdzę, że backend się kompiluje ani że 423 testy xUnit są zielone. SDK .NET nie było dostępne.
- Nie wykonano dynamicznego pentestu działającej instancji ani ataku na realny PostgreSQL.
- Nie wykonano pełnego dependency/CVE audit z czystym pobraniem zależności. Dołączony `node_modules` nie jest reprodukowalnym dowodem poprawnego builda.
- Nie oceniono zewnętrznej konfiguracji sieci, WAF, KMS, backupów i reverse proxy poza plikami znajdującymi się w paczce.
- Wszystkie stwierdzenia oznaczone jako „potwierdzone” wynikają bezpośrednio z przepływu kodu. Ryzyka deployment-dependent są wyraźnie oznaczone warunkowo.

# 3. Metryki projektu i artefaktu

| Metryka | Wartość |
| --- | --- |
| Pliki C# bez migration designerów | 345 |
| Linie C# | 36 266 |
| Pliki TS/TSX w `src` | 88 |
| Linie TS/TSX | 19 126 |
| Testy xUnit `[Fact]` | 405 |
| Testy xUnit `[Theory]` | 18 |
| Pliki frontend test/spec | 8 |
| Pliki w ZIP | 10 621 |
| Rozmiar po rozpakowaniu | 170 257 051 bajtów |
| `node_modules` w ZIP | 10 043 pliki, 155 188 944 bajty |
| Logi w ZIP | 9 plików aplikacyjnych, 9 142 671 bajtów |
| Wystąpienia adresów e-mail w logach | 126 w logach aplikacyjnych; 128 razem z logiem zależności |

## 3.1. Największe klasy backendu

| Plik | Linie | Ocena |
| --- | --- | --- |
| `OffboardingService.cs` | 851 | Za duża odpowiedzialność i zbyt wiele przejść stanu w jednym serwisie |
| `TenebitDbContext.cs` | 808 | Bardzo duża konfiguracja modelu, trudna do audytu i utrzymania |
| `AssetAuditCampaignService.cs` | 725 | Workflow, tokeny, raporty i publiczne operacje w jednej klasie |
| `AlertCheckService.cs` | 676 | Złożone reguły i wysyłka powiadomień w jednym miejscu |
| `AuthService.cs` | 589 | Hasła, OAuth, 2FA, reset, refresh i linkowanie kont |
| `AssignmentService.cs` | 567 | Tworzenie, akceptacja, zwroty, dowody i tokeny |
| `AssetService.cs` | 462 | Duży, ale część odpowiedzialności została już wydzielona |

## 3.2. Największe pliki frontendu

| Plik | Linie | Ocena |
| --- | --- | --- |
| `translations.ts` | 6097 | Centralny plik tłumaczeń jest trudny w przeglądzie i mergeowaniu |
| `AssetsPage.tsx` | 1309 | Zbyt wiele modalów, formularzy, filtrów i operacji w jednej stronie |
| `types/domain.ts` | 905 | Monolityczny kontrakt domenowy frontendu |
| `PeoplePage.tsx` | 755 | Za duży komponent strony |
| `SettingsPage.tsx` | 740 | Wiele niezależnych paneli i odpowiedzialności |
| `OffboardingPage.tsx` | 698 | Złożony workflow w jednym komponencie |

# 4. Co zostało realnie poprawione względem wcześniejszych wersji

| Obszar | Status V3 | Komentarz |
| --- | --- | --- |
| Domyślny klucz JWT i startup | **NAPRAWIONE** | Produkcja blokuje znany/za krótki sekret zamiast tylko logować |
| Walidacja podpisu OIDC | **NAPRAWIONE CZĘŚCIOWO** | JWKS, issuer, audience i lifetime są sprawdzane; otaczający flow nadal ma luki |
| Publiczny token assignment | **NAPRAWIONE** | Losowy token, hash, expiry, revoke i indeks zamiast samego UUID |
| Location write authorization | **NAPRAWIONE** | Mutacje są w Application Layer i mają role |
| Pobieranie protokołu assignment | **NAPRAWIONE** | Kontrola roli wróciła do właściwej ścieżki |
| Composite FK dla części relacji tenantowych | **DUŻA POPRAWA** | Team, Manager, ProcessOwner, Inspection i część workflow mają pary `(OrganizationId, Id)` |
| Wyścig limitu aktywów | **NAPRAWIONE** | Create działa pod advisory lock per organizacja |
| Stripe EventId | **POPRAWIONE CZĘŚCIOWO** | Jest idempotency store i ochrona przed starszym zdarzeniem; pozostały problemy samej sekundy i asocjacji |
| Szyfrowanie pól | **DODANE, ALE NIEGOTOWE OPERACYJNIE** | AES-GCM jest poprawnym prymitywem; brakuje bezpiecznego lifecycle kluczy |
| CSV formula injection | **NAPRAWIONE** | Eksport neutralizuje prefiksy formuł |
| Upload obrazów | **DUŻA POPRAWA** | Kontrola sygnatury, limit pikseli, re-encode i usuwanie metadanych |
| Concurrency/unique errors | **POPRAWIONE** | Więcej błędów jest mapowanych na 409 zamiast 500 |
| Jawna polityka single-instance | **ŚWIADOME OGRANICZENIE** | Eliminuje cichy split pamięci i duplikację jobów, ale blokuje HA |
| Frontend typecheck | **PASS** | TypeScript kompiluje się statycznie |

# 5. Skala ważności

- **CRITICAL / P0:** bezpośrednia eskalacja uprawnień, obejście zabezpieczenia konta, naruszenie integralności dowodu lub kontrola, której brak samodzielnie blokuje produkcję.
- **HIGH / P0-P1:** poważny błąd bezpieczeństwa, prywatności, multi-tenancy lub niezawodności. Powinien zostać zamknięty przed realnymi klientami.
- **MEDIUM / P1-P2:** ryzyko operacyjne, wydajnościowe lub jakościowe, które zwiększa prawdopodobieństwo incydentu.
- **LOW / P2-P3:** hardening, ergonomia, spójność i dług techniczny.

# 6. Blokery i najpoważniejsze ustalenia

## AUD3-001 | Administrator może nadać sobie rolę Owner i przejąć uprawnienia właścicielskie

| Pole | Ocena |
| --- | --- |
| Severity | CRITICAL |
| Priorytet | P0 przed produkcją |
| Pewność | Wysoka, przepływ potwierdzony statycznie |
| Status | OTWARTY |

### Dowód w kodzie

- `Tenebit.Application/Identity/UserAccessService.cs:58-100`: zarówno `CreateAsync`, jak i `UpdateAsync` dopuszczają `Owner` lub `Admin`.
- `UserAccessService.cs:124-129`: `ValidateRoles` sprawdza wyłącznie, czy rola istnieje w `TenebitRoles.All`. Nie sprawdza hierarchii.
- `Tenebit.Application/Common/TenebitRoles.cs:18-23`: Owner ma pełne płatności i zarządzanie, Admin ma mieć konfigurację bez płatności właściciela.
- Brak reguły: tylko Owner może nadać/usunąć Owner. Brak ochrony ostatniego aktywnego właściciela. Brak ochrony przed samodezaktywacją i samodemocją.

### Scenariusz błędu lub nadużycia

- Użytkownik z rolą `admin` wysyła `PUT /api/users/{własny-id}` z `roles: ["owner"]`.
- Ten sam administrator może utworzyć nowe konto z rolą Owner, a następnie użyć go do płatności lub zmian właścicielskich.
- Administrator może zmienić role prawdziwego właściciela albo ustawić `IsActive=false`, jeżeli zna jego ID z listy użytkowników.

### Wpływ biznesowy i techniczny

- Pełna tenant-local privilege escalation.
- Możliwość przejęcia płatności, zarządzania subskrypcją i użytkownikami.
- Możliwość pozostawienia firmy bez aktywnego Ownera.
- Audyt trail pokaże zmianę, ale nie zapobiegnie incydentowi.

### Dlaczego obecna implementacja nie wystarcza

- Role są traktowane jako płaska lista stringów, mimo że model biznesowy deklaruje hierarchię.
- Samo sprawdzenie `TenebitRoles.All` chroni przed literówką, nie przed eskalacją.
- Frontendowe ukrycie przycisku nie ma znaczenia dla API.

### Minimalna poprawka blokująca incydent

- W `UserAccessService` rozdziel akcje właścicielskie i administracyjne.
- Jeżeli actor nie ma Owner, odrzuć każdy request dodający lub usuwający Owner.
- Zablokuj dezaktywację/demotowanie ostatniego aktywnego Ownera.
- Zablokuj samododanie Owner przez Admin niezależnie od bieżącej roli docelowego użytkownika.
- Przy zmianie ról lub `IsActive` unieważnij wszystkie refresh/device-trust tokeny użytkownika.

### Docelowe rozwiązanie

- Wprowadź osobny `RoleAdministrationPolicy` z metodą `CanAssign(actorRoles, currentTargetRoles, requestedRoles, isSelf)`.
- Zapisuj w audycie stare i nowe role, actor ID, powód oraz correlation ID.
- Rozważ krok ponownego uwierzytelnienia dla operacji Owner, billing i transfer ownership.
- Rozważ dedykowany workflow transferu własności zamiast zwykłego PUT użytkownika.

### Wymagane testy regresyjne

- `Admin_CannotGrantOwnerToSelf`.
- `Admin_CannotCreateOwner`.
- `Admin_CannotDemoteOwner`.
- `Owner_CanCreateSecondOwner` zgodnie z przyjętą polityką.
- `CannotDeactivateLastActiveOwner`.
- `RoleChange_RevokesAllSessionsAndTrustedDevices`.

### Kryterium zamknięcia

- Żaden Admin nie może nadać, odebrać ani pośrednio uzyskać Owner.
- W bazie zawsze pozostaje co najmniej jeden aktywny Owner.
- Testy HTTP i serwisowe są zielone.

---

## AUD3-002 | Dezaktywowany użytkownik może wrócić przez wcześniej połączony OAuth

| Pole | Ocena |
| --- | --- |
| Severity | CRITICAL |
| Priorytet | P0 przed produkcją |
| Pewność | Wysoka, potwierdzone dwa branch’e bez `IsActive` |
| Status | OTWARTY |

### Dowód w kodzie

- `AuthService.ExternalLoginAsync:311-323`: dla linked user kod zwraca użytkownika bez sprawdzenia `linkedUser.IsActive`.
- `AuthService.ExternalLoginAsync:330-352`: dla istniejącego e-maila również nie ma kontroli `existingUser.IsActive`.
- `AuthService.LoginAsync:127-133`: logowanie hasłem poprawnie sprawdza `!user.IsActive`, co pokazuje niespójność ścieżek.
- `ExternalAuthEndpoints.cs:102-112`: po sukcesie ExternalLogin od razu powstaje refresh cookie i JWT.

### Scenariusz błędu lub nadużycia

- Owner lub Admin dezaktywuje konto pracownika albo byłego administratora.
- Konto miało wcześniej połączony Google/Microsoft/Apple/Facebook.
- Użytkownik wybiera social login i otrzymuje nową pełną sesję mimo `IsActive=false`.

### Wpływ biznesowy i techniczny

- Dezaktywacja konta nie jest skuteczną kontrolą bezpieczeństwa.
- Były pracownik lub odebrany administrator może odzyskać dostęp.
- Może dojść do dalszych zmian danych zanim incydent zostanie zauważony.

### Dlaczego obecna implementacja nie wystarcza

- Ścieżka OAuth nie używa wspólnego gate’u konta aktywnego.
- Refresh sprawdza aktywność, ale nowy OAuth login wydaje nowy refresh token, więc nie pomaga.

### Minimalna poprawka blokująca incydent

- Po znalezieniu linked/existing user natychmiast sprawdź `IsActive` przed linkowaniem, aktualizacją i wydaniem sesji.
- Zwracaj generyczne `oauth_rejected`, bez ujawniania statusu konta.
- Przy dezaktywacji unieważniaj refresh tokeny i trusted-device tokeny.

### Docelowe rozwiązanie

- Wydziel wspólne `AccountSignInPolicy.EnsureCanSignIn(user)` używane przez password, refresh, OAuth i przyszłe SSO.
- Dodaj `SessionVersion`/security stamp do JWT i weryfikuj go dla operacji wrażliwych albo stosuj krótkie access tokeny z centralnym revoke.

### Wymagane testy regresyjne

- `ExternalLogin_LinkedInactiveUser_IsRejected`.
- `ExternalLogin_ExistingInactiveUser_IsRejected`.
- `Deactivation_RevokesRefreshAndDeviceTrust`.
- Test HTTP callback, który nie ustawia żadnego cookie po odrzuceniu.

### Kryterium zamknięcia

- Każda ścieżka logowania stosuje identyczną kontrolę aktywności.
- Dezaktywowany użytkownik nie może uzyskać nowego access ani refresh tokenu.

---

## AUD3-003 | OAuth omija aplikacyjne TOTP/2FA

| Pole | Ocena |
| --- | --- |
| Severity | CRITICAL |
| Priorytet | P0 przed produkcją |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `AuthService.LoginAsync:135-142`: password login wymaga challenge, gdy `IsTwoFactorEnabled` i urządzenie nie jest zaufane.
- `ExternalAuthEndpoints.cs:102-112`: callback OAuth po ExternalLogin od razu wydaje refresh cookie i JWT.
- Brak sprawdzenia `IsTwoFactorEnabled`, brak challenge, brak `amr`/`acr`, brak decyzji czy MFA providera spełnia politykę Tenebit.

### Scenariusz błędu lub nadużycia

- Użytkownik włącza 2FA w Tenebit, oczekując ochrony konta.
- Atakujący przejmuje konto social provider albo korzysta z sesji providera bez lokalnego TOTP.
- Social callback tworzy pełną sesję Tenebit bez kodu TOTP.

### Wpływ biznesowy i techniczny

- Deklarowane 2FA nie chroni wszystkich metod logowania.
- Właściciel może uważać konto za zabezpieczone, podczas gdy alternatywny kanał omija kontrolę.

### Dlaczego obecna implementacja nie wystarcza

- 2FA jest zaimplementowane wyłącznie w endpointach logowania hasłem.
- Token JWT zawiera informację, że 2FA jest włączone, ale nie że zostało wykonane dla tej sesji.

### Minimalna poprawka blokująca incydent

- Po ExternalLogin, jeżeli użytkownik ma TOTP, utwórz ten sam challenge co dla hasła i nie wydawaj refresh/JWT przed jego ukończeniem.
- Nie uznawaj automatycznie MFA providera bez zweryfikowanego `amr`/`acr` i jawnej polityki.

### Docelowe rozwiązanie

- Wprowadź wspólny `SignInFlow` zwracający `RequiresSecondFactor`, niezależnie od pierwszego czynnika.
- Dodaj claim `amr` do sesji i polityki step-up auth dla owner/billing/role changes.
- Rejestruj metodę logowania i wykonany poziom uwierzytelnienia.

### Wymagane testy regresyjne

- `OAuthLogin_WhenTotpEnabled_RequiresSecondFactor`.
- `OAuthLogin_DoesNotIssueRefreshBeforeTotp`.
- `OAuthLogin_InvalidTotp_DoesNotCreateSession`.
- `StepUpRequired_ForOwnerRoleChangeAndBilling`.

### Kryterium zamknięcia

- Nie istnieje ścieżka wydania pełnej sesji dla użytkownika z TOTP bez zweryfikowania drugiego czynnika lub jawnie zaakceptowanego równoważnego MFA.

---

## AUD3-004 | Pracownik może zaakceptować wydanie sprzętu innej osoby

| Pole | Ocena |
| --- | --- |
| Severity | CRITICAL/HIGH |
| Priorytet | P0 przed produkcją |
| Pewność | Wysoka, bezpośredni brak ownership check |
| Status | OTWARTY |

### Dowód w kodzie

- `AssignmentService.AcceptAsync:274-289`: dopuszcza rolę `Employee`.
- Assignment jest pobierany wyłącznie po `OrganizationId` i przekazanym `id`.
- Brak powiązania `_currentUser.Email/Subject` z `Person` oraz brak warunku `assignment.PersonId == currentPerson.Id`.
- `DashboardService:91-93` zwraca `EntityId` w recent activity wszystkim uwierzytelnionym rolom, co może ułatwiać poznanie ID.

### Scenariusz błędu lub nadużycia

- Pracownik A zdobywa GUID assignmentu pracownika B z aktywności, logu UI, linku lub innego źródła.
- A wysyła `POST /api/assignments/{id}/accept`.
- System zapisuje akceptację, IP i actor subject A dla assignmentu B.

### Wpływ biznesowy i techniczny

- Fałszywy dowód akceptacji sprzętu lub procedur.
- Spór z klientem/pracownikiem, błędny protokół i naruszenie integralności procesu.
- Możliwy problem prawny i dowodowy, ponieważ system ma generować tamper-evident artefakty.

### Dlaczego obecna implementacja nie wystarcza

- Rola Employee jest potraktowana jako wystarczająca bez autoryzacji zasobowej.
- Organization scope nie oznacza ownership scope.

### Minimalna poprawka blokująca incydent

- Najprościej usuń `Employee` z wewnętrznego `AcceptAsync` i kieruj pracownika wyłącznie przez bezpieczny capability link.
- Alternatywnie powiąż OrganizationUser z Person i wymagaj zgodności `assignment.PersonId` z bieżącą osobą.
- Oddziel administracyjne `AcceptOnBehalf` z osobnym policy, powodem i silnym audytem.

### Docelowe rozwiązanie

- Wprowadź centralny resource authorization handler dla `AssignmentAction.Accept`.
- W odpowiedzi API nie ujawniaj assignment ID rolom, które nie mogą go użyć.
- Rozważ podpisanie protokołu kontekstem użytkownika i metodą akceptacji.

### Wymagane testy regresyjne

- `EmployeeA_CannotAccept_AssignmentOfEmployeeB`.
- `Employee_CanAccept_OnlyOwnAssignment` albo `Employee_CannotUseInternalAcceptEndpoint`.
- `AdminAcceptOnBehalf_RequiresReason_AndAudit`.
- Test integralności protokołu po odrzuconej próbie.

### Kryterium zamknięcia

- Każda akceptacja jest związana z właściwą osobą lub jawnym administracyjnym override.
- Nie da się zaakceptować cudzego assignmentu samym GUID.

---

## AUD3-005 | OAuth state nie jest związany z przeglądarką: login-CSRF i session swapping

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0 |
| Pewność | Średnio-wysoka, klasyczny przepływ możliwy statycznie |
| Status | OTWARTY |

### Dowód w kodzie

- `OAuthStateStore.cs:14-30`: state, verifier i return path są zapisane wyłącznie w globalnym `IMemoryCache`.
- `ExternalAuthEndpoints.cs:66-78`: start tworzy state, ale nie ustawia correlation cookie.
- `ExternalAuthEndpoints.cs:81-112`: callback akceptuje state z cache i nie sprawdza, czy callback wrócił do tej samej przeglądarki.
- PKCE chroni kod przed podmianą w kanale, ale w tej implementacji verifier jest serwerowy i nie wiąże transakcji z browserem ofiary.

### Scenariusz błędu lub nadużycia

- Atakujący inicjuje OAuth w Tenebit i loguje się swoim kontem provider.
- Przekazuje ofierze gotowy callback URL lub powoduje jego otwarcie.
- Serwer konsumuje ważny state i ustawia ofierze refresh cookie dla konta atakującego.
- Ofiara może nieświadomie wprowadzać dane do organizacji atakującego.

### Wpływ biznesowy i techniczny

- Session swapping i możliwość wyłudzenia danych w kontekście konta atakującego.
- Bardzo trudny do zauważenia incydent, bo logowanie technicznie wygląda poprawnie.

### Dlaczego obecna implementacja nie wystarcza

- State zabezpiecza tylko nieprzewidywalność i jednorazowość. Nie zapewnia same-browser correlation.
- Brak OIDC nonce dodatkowo utrudnia jednoznaczne związanie odpowiedzi z transakcją.

### Minimalna poprawka blokująca incydent

- Ustaw losowy, HttpOnly, Secure, SameSite=Lax correlation cookie i przechowuj jego hash w transakcji OAuth.
- W callback wymagaj zgodności state, providera, correlation cookie i nonce.
- Dodaj rate limiting na start/callback.

### Docelowe rozwiązanie

- Preferuj standardowy middleware ASP.NET Core OpenID Connect/OAuth zamiast własnego flow.
- Przenieś transakcje do distributed cache, jeżeli ma istnieć więcej niż jedna instancja.
- Nie przekazuj access JWT w URL fragment. Zakończ callback krótkim jednorazowym code exchange.

### Wymagane testy regresyjne

- `CallbackWithoutCorrelationCookie_IsRejected`.
- `CallbackWithCookieFromDifferentBrowser_IsRejected`.
- `StateIsSingleUse`.
- `NonceMismatch_IsRejected`.
- `OAuthStartAndCallback_AreRateLimited`.

### Kryterium zamknięcia

- Każdy callback jest kryptograficznie i stanowo związany z transakcją tej samej przeglądarki.

---

## AUD3-006 | Deklarowany model ról nie ma centralnej autoryzacji zasobowej

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0 |
| Pewność | Wysoka |
| Status | OTWARTY, problem systemowy |

### Dowód w kodzie

- `TenebitRoles.cs:22`: Manager ma widok zespołu.
- `TenebitRoles.cs:23`: Employee ma widok tylko swoich danych.
- `PeopleService.cs:41-59`: Manager z `PeopleViewers` otrzymuje listę i dowolną osobę z całej organizacji.
- `AssetService.cs:53-85`: Manager z `AssetViewers` otrzymuje wszystkie aktywa organizacji.
- `AssignmentService.cs:80-98`: Manager z `AssignmentViewers` otrzymuje wszystkie assignmenty organizacji.
- `MyWorkspaceService.cs:39-49`: Manager może podać dowolny personId w organizacji, bez sprawdzenia zarządzanego zespołu.
- `OnboardingService.cs:277-318`: Employee i Manager mogą podać dowolny personId w tej samej organizacji.
- `ProcedureService.cs:140-145`: Employee może pobrać dowolny dokument procedury w organizacji, bez sprawdzenia przypisania.

### Scenariusz błędu lub nadużycia

- Manager zespołu A odpytuje listy ludzi, aktywów i assignmentów zespołu B.
- Employee zmienia `personId` w URL checklisty i odczytuje onboarding innej osoby.
- Employee pobiera plik procedury, która nie została mu przypisana.

### Wpływ biznesowy i techniczny

- Naruszenie zasady least privilege i prywatności pracowników.
- Różnica między opisem ról a rzeczywistą ochroną API.
- Ryzyko ujawnienia nazw, e-maili, stanowisk, sprzętu, kosztów i dokumentów wewnętrznych.

### Dlaczego obecna implementacja nie wystarcza

- Obecny `AccessPolicy.EnsureAnyRole` jest wyłącznie module-level gate.
- Brakuje actor-to-resource relation: own person, managed team, assigned procedure, owned assignment.
- Filtrowanie po OrganizationId chroni tenant, ale nie role wewnątrz tenant.

### Minimalna poprawka blokująca incydent

- Do czasu wdrożenia row-level policies usuń Manager/Employee z endpointów, których nie da się bezpiecznie ograniczyć.
- Powiąż OrganizationUser z Person w sposób jednoznaczny i nie opieraj ownership wyłącznie na e-mailu.
- Dodaj filtry team/person w repozytoriach lub query specifications, nie po pobraniu całej organizacji.

### Docelowe rozwiązanie

- Wprowadź `ResourceAuthorizationService` z wymaganiami `OwnResource`, `ManagedTeam`, `TenantWideModule`, `OwnerOnly`.
- ICurrentUser powinien dostarczać OrganizationUserId, PersonId i skuteczny zakres zespołów.
- Każdy use case powinien deklarować akcję i zasób, a nie tylko tablicę ról.
- Zbuduj formalną macierz RBAC/ABAC i testuj ją automatycznie.

### Wymagane testy regresyjne

- `Manager_CannotListPeopleOutsideManagedTeams`.
- `Manager_CannotReadAssetOutsideManagedTeams`.
- `Manager_CannotReadWorkspaceOutsideManagedTeams`.
- `Employee_CannotReadOtherPersonChecklist`.
- `Employee_CannotDownloadUnassignedProcedureDocument`.
- `Employee_CanReadOnlyOwnWorkspace`.

### Kryterium zamknięcia

- Opis każdej roli odpowiada rzeczywistym query i command policies.
- Test macierzy autoryzacji obejmuje każdy endpoint i rolę.

---

## AUD3-007 | Dashboard i inwentarz lokalizacji ujawniają dane każdemu zalogowanemu użytkownikowi

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0/P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `TenebitEndpoints.cs:12-13`: grupa `/api` wymaga tylko uwierzytelnienia.
- `DashboardEndpoints.cs:44-58`: brak dodatkowej polityki roli.
- `DashboardService.cs:65-134`: ładuje wszystkie aktywa, osoby, assignmenty, licencje i aktywność organizacji.
- `DashboardService.cs:91-93`: zwraca action, entity type, entity ID, details, actor subject i timestamp.
- `LocationEndpoints.cs:10-28`: list i inventory nie mają policy.
- `LocationService.cs`: inventory zwraca dokładne asset IDs, nazwy, tagi, statusy oraz person name/email/job title/manager/location.

### Scenariusz błędu lub nadużycia

- Użytkownik tylko z rolą Employee loguje się i wywołuje `/api/dashboard`.
- Ten sam użytkownik odpytuje `/api/locations/{id}/inventory` dla kolejnych lokalizacji.
- Otrzymuje dane całej firmy, mimo że rola ma widzieć wyłącznie własne zasoby.

### Wpływ biznesowy i techniczny

- Ujawnienie wartości majątku, liczby licencji, lokalizacji sprzętu i danych osobowych pracowników.
- Entity IDs z activity mogą ułatwiać wykorzystanie innych błędów IDOR.

### Dlaczego obecna implementacja nie wystarcza

- Frontend ma dashboard dostępny dla wszystkich, ale bezpieczeństwo musi wynikać z backendu.
- Brak role gate i brak wariantu dashboardu ograniczonego do własnych danych.

### Minimalna poprawka blokująca incydent

- Ogranicz pełny dashboard do jawnych ról, np. Owner/Admin/AssetOperator/Finance/Auditor według pola.
- Dla Employee zbuduj osobny endpoint `my-dashboard` bez danych innych osób i bez activity IDs.
- Location inventory ogranicz do ról inwentaryzacyjnych. Manager filtruj po zarządzanych zespołach/lokalizacjach.

### Docelowe rozwiązanie

- Rozbij response na widżety z osobnymi policies. Nie zwracaj jednego nadzbioru i nie licz na ukrycie UI.
- Zastosuj field-level authorization dla kosztów, activity details i danych osobowych.

### Wymagane testy regresyjne

- `Employee_CannotAccessTenantDashboardSummary` albo otrzymuje wyłącznie własny wariant.
- `Employee_CannotAccessLocationInventory`.
- `Finance_CanSeeCostsButNotUnneededPeopleDetails`.
- `RecentActivity_DoesNotExposeUnauthorizedEntityIds`.

### Kryterium zamknięcia

- Dla każdej roli istnieje udokumentowany i przetestowany zestaw pól dashboardu/inwentarza.

---

## AUD3-008 | Link publiczny offboardingu pozostaje użyteczny po anulowaniu lub przywróceniu zatrudnienia

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0/P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `OffboardingService.CancelAsync:636-674`: zmienia stan, ale nie unieważnia `PublicToken`.
- `OffboardingService.RestoreEmploymentAsync:676+`: analogicznie nie widać wymuszonego revoke w przejściu.
- `OffboardingService.ResolveByTokenAsync:782-791`: weryfikuje hash, expiry i revokedAt, ale nie status sprawy.
- `RecordEmployeeResponsesAsync:802-831`: brak warunku, że sprawa nadal jest aktywna.
- `UploadPublicEvidenceAsync:834-849`: brak warunku statusu sprawy.

### Scenariusz błędu lub nadużycia

- HR uruchamia offboarding i wysyła link.
- Proces zostaje anulowany albo zatrudnienie przywrócone.
- Stary link nadal pozwala odczytać dane, zapisać odpowiedzi lub dodać dowody do anulowanej sprawy.

### Wpływ biznesowy i techniczny

- Niespójność workflow i możliwość modyfikacji procesu po decyzji administracyjnej.
- Ujawnienie danych z anulowanej sprawy.
- Trudność w obronie integralności protokołu i audytu.

### Dlaczego obecna implementacja nie wystarcza

- Token validity nie jest połączone z parent state.
- Unieważnianie przy `Complete` nie wystarcza, bo istnieją inne terminalne przejścia.

### Minimalna poprawka blokująca incydent

- W jednej transakcji z Cancel/Restore ustaw `PublicTokenRevokedAt`.
- Każda publiczna komenda musi sprawdzić dozwolony stan parent, nawet gdy token jest ważny kryptograficznie.
- Dla anulowanej sprawy zwracaj generyczny 404/410 zgodnie z polityką.

### Docelowe rozwiązanie

- Modeluj capability jako aktywne tylko dla jawnego zestawu stanów.
- Zastosuj wspólny `PublicCapabilityPolicy` i centralne revoke hooks na przejściach domenowych.

### Wymagane testy regresyjne

- `Cancel_RevokesPublicToken`.
- `RestoreEmployment_RevokesPublicToken`.
- `CancelledCase_PublicReadWriteUpload_AreRejected`.
- `Complete_RevokesToken_Atomically`.

### Kryterium zamknięcia

- Żaden terminalny lub anulowany workflow nie przyjmuje starego capability tokenu.

---

## AUD3-009 | Token publicznego audytu aktywów ma koszt O(N) i przeżywa zakończenie kampanii

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0/P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `AssetAuditCampaignService.ResolveByTokenAsync:307-319`: pobiera wszystkich uczestników z aktywnym tokenem i kolejno wykonuje verify.
- `AssetAuditParticipantRepository.cs:26-29`: `ListWithActiveTokenAsync` nie wyszukuje po hash.
- `CompleteAsync:566-595` i `CancelAsync:597-619`: nie unieważniają tokenów uczestników.
- `RecordItemResponseAsync:356-375` i `UploadPublicEvidenceAsync:408-420`: nie sprawdzają statusu kampanii.
- `BuildPublicResponseAsync:330-347`: per item wykonuje osobne pobranie assetu i dowodów, tworząc N+1.

### Scenariusz błędu lub nadużycia

- Atakujący wysyła wiele losowych tokenów do publicznego endpointu.
- Każda próba ładuje wszystkie aktywne tokeny uczestników i wykonuje koszt kryptograficzny dla każdego.
- Po anulowaniu lub zakończeniu kampanii stary link nadal może odczytywać, zmieniać odpowiedzi albo uploadować, zależnie od statusu uczestnika.

### Wpływ biznesowy i techniczny

- Publiczny DoS rosnący wraz z liczbą uczestników audytów.
- Modyfikacja danych po zamknięciu procesu.
- Wysokie obciążenie DB przez N+1 i listę wszystkich tokenów.

### Dlaczego obecna implementacja nie wystarcza

- Assignment i offboarding mają już deterministyczny hash lookup, ale audit participants nie.
- Participant status nie zastępuje campaign status.

### Minimalna poprawka blokująca incydent

- Dodaj `TokenHash` jako wyszukiwalny, unikalny częściowy indeks i repo `FindByTokenHashAsync`.
- Przy Complete/Cancel zbiorczo revoke wszystkie tokeny w tej samej transakcji.
- Publiczne read/write/upload wymagają campaign status Active oraz właściwego participant status.
- Zbatchuj assety i evidence w BuildPublicResponse.

### Docelowe rozwiązanie

- Ujednolić mechanizm capability tokenów dla assignment, offboarding i audits.
- Dodać metryki invalid-token rate oraz limit per IP i per token hash prefix.

### Wymagane testy regresyjne

- `InvalidToken_PerformsSingleIndexedLookup`.
- `CompleteCampaign_RevokesAllParticipantTokens`.
- `CancelCampaign_RevokesAllParticipantTokens`.
- `TerminalCampaign_PublicReadWriteUpload_AreRejected`.
- Test wydajności dla 10 000 uczestników.

### Kryterium zamknięcia

- Rozwiązanie tokenu jest O(1) po indeksie, a stan parent zawsze blokuje operacje po terminalnym przejściu.

---

## AUD3-010 | Stripe może utrzymać lub nadać płatne entitlement dla nieznanego statusu i niepełnego skojarzenia

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0/P1 |
| Pewność | Wysoka dla logiki; wystąpienie nieznanego statusu zależy od danych Stripe |
| Status | OTWARTY |

### Dowód w kodzie

- `StripePaymentGateway.MapStatus:135-155`: status nieznany mapuje się na `PastDue`.
- `OrganizationSubscription.HasActiveStripeSubscription:38-40`: `PastDue` jest traktowany jako aktywna subskrypcja.
- `StripePaymentGateway.ParseWebhookEvent:130-132`: każdy obsłużony obiekt subskrypcji ma plan Pro.
- `SubscriptionService.HandleWebhookAsync:221-223`: jeżeli metadata zawiera OrganizationId, lookup preferuje organizację zamiast customer ID.
- `SubscriptionService:233`: zdarzenie z timestampem równym ostatniemu jest ignorowane. Stripe `created` ma rozdzielczość sekundową.

### Scenariusz błędu lub nadużycia

- Nowy albo malformed status trafia do fallback `PastDue`, a rekord zachowuje/otrzymuje Pro.
- Webhook z metadanym OrganizationId wskazuje inną organizację niż powiązany customer/subscription. Kod nie sprawdza pełnej zgodności.
- Dwa różne zdarzenia powstają w tej samej sekundzie. Drugie może zostać pominięte przez `<=`.

### Wpływ biznesowy i techniczny

- Nieprawidłowe entitlement i limit aktywów.
- Błędne przypisanie subskrypcji do organizacji.
- Niespójność billing state trudna do odtworzenia po czasie.

### Dlaczego obecna implementacja nie wystarcza

- „PastDue jako konserwatywny fallback” nie jest fail-closed, jeżeli PastDue daje paid access.
- Metadata jest pomocniczym routingiem, nie dowodem własności customer/subscription.
- Timestamp sekundowy nie jest wystarczającym total ordering.

### Minimalna poprawka blokująca incydent

- Nieznany status mapuj na `Unknown/Quarantined` bez nowego entitlement.
- Przed aktualizacją wymagaj zgodności OrganizationId, StripeCustomerId i StripeSubscriptionId albo wykonaj canonical fetch z Stripe.
- Nie ignoruj różnych event IDs tylko dlatego, że mają tę samą sekundę.
- Wymagaj `WebhookSecret` w `IsConfigured` dla ścieżki webhookowej.

### Docelowe rozwiązanie

- Użyj oficjalnego Stripe SDK i po webhooku pobieraj canonical subscription state.
- Oddziel billing state od entitlement state. Entitlement ma jawny allowlist, np. Active/Trialing, a PastDue ma okres grace określony biznesowo.
- Zapisuj event ID, type, created, customer, subscription, applied decision i reconciliation status.

### Wymagane testy regresyjne

- `UnknownStripeStatus_DoesNotGrantPro`.
- `MetadataOrganizationMismatch_IsRejectedAndAlerted`.
- `TwoEventsSameSecond_AreBothHandledOrReconciled`.
- `OutOfOrderEvents_ResultMatchesCanonicalStripeState`.
- `MissingWebhookSecret_FailsStartupOrWebhookReadiness`.

### Kryterium zamknięcia

- Entitlement wynika wyłącznie z jawnie dozwolonego, zweryfikowanego stanu powiązanego customer/subscription.

---

## AUD3-011 | Klucz szyfrowania pól jest sprzężony z JWT i nie ma rotacji

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0/P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `FieldEncryptor.cs:19-24`: `Auth:FieldEncryptionKey ?? Auth:SigningKey ?? development fallback`.
- `Program.cs` waliduje produkcyjny SigningKey, ale nie wymusza odrębnego FieldEncryptionKey.
- `FieldEncryptor.cs:47-66`: format ma tylko prefiks `v1:`, bez key ID.
- Wartości bez prefiksu są zwracane plaintext, więc migracja istniejących danych nie jest wymuszona.
- Malformed lub uszkodzony `v1:` może rzucić Base64/AES wyjątek i zakończyć request 500.

### Scenariusz błędu lub nadużycia

- Produkcja nie ustawia dedykowanego FieldEncryptionKey, więc używa JWT SigningKey.
- Operator rotuje JWT secret po incydencie lub zgodnie z polityką.
- Zaszyfrowane TOTP/license/custom fields stają się nieczytelne, ponieważ stary klucz nie jest dostępny.

### Wpływ biznesowy i techniczny

- Ryzyko utraty dostępności danych i kont z 2FA.
- Rotacja kluczy bezpieczeństwa staje się operacyjnie niebezpieczna.
- Brak możliwości bezpiecznej re-encryption i rozpoznania użytego klucza.

### Dlaczego obecna implementacja nie wystarcza

- Poprawny prymityw AES-GCM nie rozwiązuje lifecycle klucza.
- Fallback do JWT tworzy ukrytą zależność między dwoma niezależnymi sekretami.

### Minimalna poprawka blokująca incydent

- W Production wymagaj osobnego, silnego FieldEncryptionKey i fail startup przy braku.
- Usuń fallback do SigningKey i development secret poza Development.
- Dodaj kontrolowany błąd i alert dla uszkodzonego ciphertext zamiast nieobsłużonego 500.
- Sprawdź maksymalny plaintext względem kolumny `Value` po narzucie nonce/tag/Base64.

### Docelowe rozwiązanie

- Format `v2:{keyId}:{payload}` i key ring z aktywnym kluczem write oraz historycznymi read keys.
- KMS/Key Vault, audyt dostępu, plan rotacji i background re-encryption.
- Migracja/backfill istniejącego plaintext oraz raport rekordów legacy.

### Wymagane testy regresyjne

- `ProductionWithoutDedicatedFieldKey_FailsStartup`.
- `OldCiphertext_DecryptsAfterKeyRotation`.
- `NewWritesUseNewKeyId`.
- `CorruptedCiphertext_ReturnsControlledErrorAndAlert`.
- `MaxAllowedPlaintext_FitsDatabaseColumn`.

### Kryterium zamknięcia

- Rotacja JWT nie wpływa na dane zaszyfrowane.
- Istnieje przetestowana procedura rotacji i rollbacku kluczy danych.

---

## AUD3-012 | Zmiany hasła, ról, aktywności i 2FA nie tworzą spójnego lifecycle sesji

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P0/P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `AuthService.ResetPasswordAsync:440-447`: hasło/token są zapisywane, a revokacja refresh/device tokens wykonywana potem osobnymi operacjami.
- `UserAccessService.UpdateAsync:84-99`: zmienia role i aktywność bez revokacji sesji.
- `AuthService.DisableTwoFactorAsync:221-238`: usuwa recovery codes, ale nie unieważnia device trust tokens.
- `DeviceTrustCookie.cs`: istnieje Append, brak centralnego Delete używanego przy zmianie 2FA.
- `AuthEndpoints.cs:78-87`: challenge jest konsumowany przed walidacją kodu, więc jedna literówka wymusza nowy login.
- `AuthService.RefreshAsync:549-575`: revoke starego i dodanie nowego refresh tokenu nie ma jawnej atomowej claim/update ani token family/reuse detection.

### Scenariusz błędu lub nadużycia

- Reset hasła zapisuje nowe hasło, ale revokacja sesji kończy się błędem. Stare sesje pozostają aktywne.
- Admin odbiera rolę lub dezaktywuje konto, ale istniejący JWT działa do expiry, a OAuth ma osobną lukę.
- Użytkownik wyłącza i ponownie włącza 2FA. Stary 30-dniowy device trust może ominąć nowe TOTP.
- Dwa równoległe refresh requesty mogą oba zobaczyć token jako ważny i utworzyć dwa następcze tokeny.

### Wpływ biznesowy i techniczny

- Niepełne odcięcie przejętego konta.
- Niespójne zachowanie po administracyjnej zmianie ról.
- Możliwy refresh replay i rozgałęzienie sesji.

### Dlaczego obecna implementacja nie wystarcza

- Zabezpieczenia są rozproszone po serwisie i repozytoriach, bez jednej transakcji bezpieczeństwa.
- JWT nie ma wersji sesji/security stamp weryfikowanej po zmianach bezpieczeństwa.

### Minimalna poprawka blokująca incydent

- Zmień reset hasła i revokacje w jedną transakcję DB.
- Przy zmianie role/active/2FA/password unieważnij refresh, device trust i podnieś `SecurityStamp`.
- Dodaj `DeviceTrustCookie.Delete` i użyj go przy disable/re-enable/setup.
- Refresh token rotuj atomowym UPDATE z warunkiem `RevokedAt IS NULL`.

### Docelowe rozwiązanie

- Token families z parent/replacedBy, reuse detection i family revoke.
- Session management UI z listą urządzeń i opcją revoke all.
- Krótki access token oraz policy wymagająca aktualnego security stamp dla wrażliwych operacji.

### Wymagane testy regresyjne

- `PasswordReset_IsAtomicWithSessionRevocation`.
- `RoleChange_RevokesAllSessions`.
- `Disable2FA_RevokesTrustedDevicesAndDeletesCookie`.
- `ConcurrentRefresh_OnlyOneSucceeds`.
- `ReusedRefresh_RevokesFamily`.

### Kryterium zamknięcia

- Każda zmiana stanu bezpieczeństwa natychmiast i atomowo unieważnia właściwe sesje.

---

## AUD3-013 | Baza nadal nie wymusza wszystkich relacji tenant-owned

| Pole | Ocena |
| --- | --- |
| Severity | HIGH |
| Priorytet | P1 przed produkcją |
| Pewność | Wysoka dla modelu; brak potwierdzonego bezpośredniego A→B read |
| Status | OTWARTY CZĘŚCIOWO |

### Dowód w kodzie

- W `TenebitDbContext.cs` dodano wiele dobrych composite FK, m.in. Team, AssignedPerson, Manager, ProcessOwner, OffboardingCase i Inspection.
- Nadal istnieją relacje oparte na samym ID albo bez FK do targetu tenant-owned, m.in. Asset.CategoryId, AssetInspection.AssetId/AssignmentId/OffboardingItemId, ServiceTicket.AssetId, ProcedureDocument.ProcedureId, LicenseSeat.PersonId, Assignment.PersonId, AssignmentAsset.AssetId, ProcedureAcceptance Person/Procedure, część audit/reservation/offboarding/evidence.
- Brak globalnych query filters dla tenant entities.
- Brak PostgreSQL row-level security.

### Scenariusz błędu lub nadużycia

- Obecny serwis poprawnie sprawdza foreign ID, ale przyszły import/job/endpoint pomija jedną walidację.
- Baza akceptuje powiązanie rekordu firmy A do ID rekordu firmy B, jeżeli relacja nie zawiera OrganizationId.
- Późniejsze include/query/report może ujawnić albo uszkodzić dane w nieoczekiwanym kontekście.

### Wpływ biznesowy i techniczny

- Ryzyko przyszłego cross-tenant association i trudne do wykrycia zanieczyszczenie danych.
- Brak defense-in-depth dla najważniejszej granicy systemu.
- Ręczna walidacja w każdym serwisie nie skaluje się bezbłędnie przy rozwoju produktu.

### Dlaczego obecna implementacja nie wystarcza

- Application validation jest potrzebna, ale baza powinna odrzucić naruszenie nawet przy błędzie aplikacji.
- Sam `OrganizationId` na tabeli nie gwarantuje zgodności organizacji obu końców relacji.

### Minimalna poprawka blokująca incydent

- Dodaj alternate unique key `(OrganizationId, Id)` na każdej tenant entity.
- Każda FK do tenant entity powinna zawierać `(OrganizationId, TargetId)`.
- Dodaj brakujące FK i migracje z preflight skanu istniejących danych.
- Dodaj testy A/B dla każdego foreign ID przy create i update.

### Docelowe rozwiązanie

- Utwórz automatyczną konwencję/model test, który wykrywa tenant FK bez OrganizationId.
- Rozważ PostgreSQL RLS jako dodatkową granicę, szczególnie dla raportów i jobów.
- Wprowadź `TenantId` jako obowiązkową część repository specification, bez metod typu global Get(id) poza jawnie publicznymi tokenami.

### Wymagane testy regresyjne

- Model test enumerujący wszystkie FK tenant-owned.
- `OrgA_CannotReference_OrgB_<Entity>` dla każdej relacji.
- Test migracji na danych z celowo zanieczyszczoną relacją.
- Test job/import path, nie tylko endpointów HTTP.

### Kryterium zamknięcia

- Baza fizycznie odrzuca każdą relację między różnymi OrganizationId.
- Tenant isolation suite działa na realnym PostgreSQL w CI.

### Uwagi

- Nie twierdzę, że obecna wersja ma potwierdzony standardowy endpoint odczytu A→B. Problem dotyczy braku pełnego wymuszenia granicy i ryzyka regresji.

---

# 7. Poważne problemy P1/P2

## AUD3-014 | Forwarded headers ufają każdemu bezpośredniemu peerowi

| Pole | Ocena |
| --- | --- |
| Severity | HIGH/MEDIUM, deployment-dependent |
| Priorytet | P1 |
| Pewność | Wysoka dla konfiguracji; exploit zależy od ekspozycji portu backendu |
| Status | OTWARTY WARUNKOWO |

### Dowód w kodzie

- `Program.cs:76-82`: `KnownIPNetworks.Clear()` i `KnownProxies.Clear()`, ForwardLimit=1.
- Rate limiter i audit IP używają `RemoteIpAddress` po forwarded headers.
- Backend w kontenerze nasłuchuje na interfejsach, a bezpieczeństwo opiera się na założeniu, że tylko nginx może się połączyć.

### Scenariusz błędu lub nadużycia

- Jeżeli port Kestrel jest osiągalny z sieci klienta lub z innego niezaufanego workloadu, atakujący ustawia `X-Forwarded-For`.
- Może obchodzić limiter per IP lub wpisać fałszywy IP do śladu akceptacji/audytu.

### Wpływ biznesowy i techniczny

- Osłabienie rate limiting i wiarygodności IP w protokołach.

### Dlaczego obecna implementacja nie wystarcza

- Komentarz o prywatnym backendzie nie jest enforcement w kodzie.

### Minimalna poprawka blokująca incydent

- Skonfiguruj dokładne IP/sieć reverse proxy.
- Zablokuj port backendu network policy/firewallem i nie publikuj go na host.
- Dodaj startup self-check lub dokumentowany deployment invariant.

### Docelowe rozwiązanie

- Użyj platformowej integracji forwarded headers z listą trusted proxies.
- Dla dowodów prawnych nie traktuj samego IP jako tożsamości podpisu.

### Wymagane testy regresyjne

- Bezpośrednie połączenie z Kestrel z XFF nie zmienia klienta.
- Połączenie przez zaufany proxy poprawnie ustawia IP.

### Kryterium zamknięcia

- Tylko znany reverse proxy może wpływać na forwarded headers.

---

## AUD3-015 | Limit pięciu zdjęć jest podatny na race condition; upload jest wielokrotnie buforowany

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM/HIGH |
| Priorytet | P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `AssetEvidenceService.cs:95, 128, 161`: osobne `CountAsync`, potem Add/Save bez blokady i bez constraint.
- `MaxPerAssetAndPhase = 5`, ale DB nie wymusza ordinal/slot/limit.
- Endpointy wykonują `ReadFormAsync`, kopiują do MemoryStream i używają `ToArray`, więc request może być buforowany wielokrotnie.
- `FormOptions.MultipartBodyLengthLimit = 30 MB`, a `MaxEvidenceBundleUploadBytes = 40 MB`, więc deklarowany limit endpointu jest niespójny z globalnym.

### Scenariusz błędu lub nadużycia

- Dwa lub więcej równoległych uploadów widzi count=4 i każdy zapisuje kolejny rekord.
- Duży multipart jest buforowany przez framework i ponownie kopiowany do byte[].

### Wpływ biznesowy i techniczny

- Przekroczenie limitu biznesowego.
- Skoki pamięci, GC pressure i możliwość memory DoS.
- Nieprzewidywalny błąd dla payload 30-40 MB.

### Dlaczego obecna implementacja nie wystarcza

- Count-then-insert nie jest atomowe.
- Per-endpoint limit ustawiany po wejściu w handler nie zawsze zastępuje wcześniejsze etapy buforowania.

### Minimalna poprawka blokująca incydent

- Wykonuj count+insert pod lockiem per `(organization, asset, phase)` albo modeluj pięć slotów/unikalny ordinal.
- Ujednolić limit nginx, Kestrel, FormOptions i endpoint.
- Streamuj do object storage/tymczasowego pliku z limitem, zamiast pełnego MemoryStream+ToArray.

### Docelowe rozwiązanie

- Przenieś binaria poza główną bazę PostgreSQL do storage z signed URL, checksum, AV scan i retention.
- Dodaj backpressure i limity per organization/user.

### Wymagane testy regresyjne

- `SixConcurrentUploads_OnlyFivePersist`.
- `PayloadAboveConfiguredLimit_IsRejectedBeforeBuffering`.
- Test pamięci dla maksymalnego bundla.

### Kryterium zamknięcia

- Limit jest wymuszony atomowo, a największy request ma przewidywalny budżet pamięci.

---

## AUD3-016 | Globalna walidacja requestów obejmuje tylko 11 z 65 DTO

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM/HIGH |
| Priorytet | P1 |
| Pewność | Wysoka |
| Status | OTWARTY CZĘŚCIOWO |

### Dowód w kodzie

- `ValidationEndpointFilter.cs:7-12` jawnie opisuje przyrostowe pokrycie.
- `ValidationEndpointFilter.cs:47-50`: DTO bez ValidationAttribute jest pomijane.
- Inwentaryzacja: 65 pozycyjnych rekordów `*Request`, 11 z DataAnnotations, 54 bez centralnych reguł.
- Password ma minimum 8 w serwisie, ale brak spójnego maksymalnego rozmiaru na DTO.
- Filtr rekurencyjnie przechodzi kolekcje, ale nie wszystkie zagnieżdżone obiekty jako graph.

### Scenariusz błędu lub nadużycia

- Klient wysyła null, bardzo długi string, ogromną kolekcję lub nieprawidłową kombinację pól do DTO bez adnotacji.
- Błąd ujawnia się dopiero w domenie/repozytorium, czasem jako 500 albo kosztowna operacja.

### Wpływ biznesowy i techniczny

- Niespójne 400/409/500.
- Ryzyko nadmiernego zużycia CPU/pamięci i błędów danych.
- Walidacja jest trudna do audytu, bo część jest w atrybutach, część w serwisach, część w domenie.

### Dlaczego obecna implementacja nie wystarcza

- Sama obecność globalnego filtra daje fałszywe poczucie pełnego coverage.

### Minimalna poprawka blokująca incydent

- Dodaj max length/count/range do wszystkich request DTO.
- Wymuś maksymalną długość hasła, tokenów, komentarzy, nazw, URL i list ID.
- Dodaj recursive validation dla nested DTO lub użyj FluentValidation.

### Docelowe rozwiązanie

- Jedna biblioteka walidacji, jeden pipeline, test refleksyjny wykrywający request bez validatora.
- Oddziel syntactic validation od business validation, ale obie muszą być obowiązkowe.

### Wymagane testy regresyjne

- `EveryRequestDto_HasValidator`.
- Property-based tests dla null/empty/oversized collections.
- Fuzz test wybranych endpointów.

### Kryterium zamknięcia

- Każdy request ma jawny validator i przewidywalny maksymalny rozmiar.

---

## AUD3-017 | Redirect URL w Stripe pochodzi od klienta; webhook czyta całe body i maskuje błędy

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM/HIGH |
| Priorytet | P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `SubscriptionEndpoints.cs:52-57, 74-76`: SuccessUrl, CancelUrl i ReturnUrl przychodzą z requestu.
- `SubscriptionService.cs:141-194`: URL jest przekazywany do gateway bez allowlisty origin.
- `SubscriptionEndpoints.cs:60-65`: publiczny webhook czyta całe body do stringa.
- `SubscriptionService.cs:200-208`: każdy exception jest mapowany na „invalid signature”, także błąd parsera lub wewnętrzny.
- `StripePaymentGateway.cs:221-224`: pełne body odpowiedzi Stripe jest logowane przy błędzie.

### Scenariusz błędu lub nadużycia

- Owner podaje domenę phishingową jako success/return URL, a Stripe redirectuje tam po płatności lub portalu.
- Duży publiczny body powoduje alokację stringa przed walidacją podpisu.
- Wewnętrzny błąd jest zwracany jako 4xx, przez co Stripe może nie retry’ować zdarzenia wymagającego ponowienia.

### Wpływ biznesowy i techniczny

- Open redirect/phishing przez zaufaną ścieżkę płatności.
- Memory DoS.
- Utrata webhook event i rozjazd billing state.
- Logowanie potencjalnie wrażliwej odpowiedzi dostawcy.

### Dlaczego obecna implementacja nie wystarcza

- URL pochodzący od uwierzytelnionego Ownera nadal nie powinien sterować dowolnym redirectem z domeny Stripe.
- Catch-all miesza błąd bezpieczeństwa z błędem operacyjnym.

### Minimalna poprawka blokująca incydent

- Buduj URL po stronie serwera z `App:PublicUrl` i ogranicz return path do relative path.
- Ustaw mały request body limit na webhook przed odczytem.
- Rozróżnij invalid signature 400 od transient internal error 500.
- Redaguj body odpowiedzi Stripe w logach.

### Docelowe rozwiązanie

- Wprowadź durable inbox/outbox: szybko weryfikuj, zapisuj event, odpowiadaj 2xx, przetwarzaj idempotentnie workerem.

### Wymagane testy regresyjne

- `ExternalCheckoutRedirect_IsRejected`.
- `OversizedWebhook_IsRejectedBeforeReadToEnd`.
- `TransientProcessingError_ReturnsRetryableStatus`.
- `StripeErrorLog_DoesNotContainSensitiveBody`.

### Kryterium zamknięcia

- Wszystkie redirect URL są serwerowo allowlisted, a webhook ma poprawną semantykę retry.

---

## AUD3-018 | Single-instance guard usuwa cichy split-brain, ale tworzy SPOF i blokuje rolling deployment

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM/HIGH |
| Priorytet | P1/P2 |
| Pewność | Wysoka |
| Status | OTWARTY JAKO OGRANICZENIE ARCHITEKTONICZNE |

### Dowód w kodzie

- `SingleInstanceGuardService.cs:9-15`: aplikacja jest jawnie single-instance przez globalny PostgreSQL advisory lock.
- `SingleInstanceGuardService.cs:30-45`: druga instancja kończy start wyjątkiem.
- OAuth state i 2FA challenge są w IMemoryCache.
- Background jobs działają w procesie API bez per-job distributed lock.

### Scenariusz błędu lub nadużycia

- Podczas rolling update nowa instancja nie startuje, dopóki stara nie odda locka.
- Awaria jedynej instancji zatrzymuje API i wszystkie joby.
- Nie można skalować poziomo przy wzroście ruchu.

### Wpływ biznesowy i techniczny

- Brak HA i trudniejszy deployment bez przestoju.
- Wszystkie procesy biznesowe zależą od jednego procesu.

### Dlaczego obecna implementacja nie wystarcza

- Guard jest uczciwszy niż ciche uruchomienie dwóch niespójnych instancji, ale nie rozwiązuje przyczyny.

### Minimalna poprawka blokująca incydent

- Udokumentuj single-instance jako twarde ograniczenie i zapewnij szybki restart/health monitoring.
- Nie próbuj uruchamiać dwóch replik dopóki state/jobs nie zostaną rozproszone.

### Docelowe rozwiązanie

- OAuth/2FA state do Redis lub zaszyfrowanego cookie.
- Joby do osobnego workera z per-job leader election/advisory locks.
- Usunąć globalny lock i dopuścić N stateless API replicas.

### Wymagane testy regresyjne

- Dwie repliki obsługują ten sam OAuth/2FA flow.
- Job wykonuje się dokładnie raz logicznie przy wielu workerach.
- Rolling deployment bez utraty requestów.

### Kryterium zamknięcia

- Co najmniej dwie repliki API mogą działać bez split state i duplikacji jobów.

---

## AUD3-019 | Lokalizacja jest przechowywana jako tekstowa ścieżka w Asset/Person

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM |
| Priorytet | P2 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- Asset i Person używają string `Location`, a moduł Location ma osobne encje z ParentId i FullPath.
- Rename/reparent lokalizacji nie aktualizuje automatycznie historycznych stringów na aktywach/osobach.
- Brak DB FK dla ParentId i brak unikalności nazwy w obrębie rodzica.
- List/inventory liczy dane w pamięci i wielokrotnie porównuje ścieżki.

### Scenariusz błędu lub nadużycia

- Administrator zmienia nazwę lub rodzica lokalizacji.
- Aktywa nadal mają starą ścieżkę, więc znikają z nowego inventory albo tworzą „sieroty”.
- Dwie lokalizacje o tej samej nazwie/ścieżce stają się niejednoznaczne.

### Wpływ biznesowy i techniczny

- Błędne raporty inwentaryzacyjne i niespójna filtracja.
- Rosnący koszt O(L*(A+P)) przy większej liczbie lokalizacji.

### Dlaczego obecna implementacja nie wystarcza

- Tekstowa denormalizacja bez kontrolowanego cascade jest krucha.

### Minimalna poprawka blokująca incydent

- Dodaj `LocationId` do Asset i Person z composite tenant FK.
- FullPath traktuj jako wartość wyliczaną/cache, nie tożsamość relacji.
- Dodaj unique sibling constraint `(OrganizationId, ParentId, NormalizedName)`.

### Docelowe rozwiązanie

- Materialized path/ltree albo closure table, zależnie od potrzeb descendant queries.
- Raporty licz w SQL z indeksami, nie przez pełne listy w pamięci.

### Wymagane testy regresyjne

- `RenameLocation_DoesNotOrphanAssets`.
- `ReparentLocation_PreservesInventory`.
- `DuplicateSiblingName_IsRejected`.

### Kryterium zamknięcia

- Relacja lokalizacji opiera się na stabilnym ID i ma DB integrity.

---

## AUD3-020 | Paczka wydaniowa zawiera node_modules, dist, logi i wyniki testów

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM/HIGH |
| Priorytet | P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- ZIP ma 10 621 plików i 170 MB po rozpakowaniu.
- `node_modules`: 155 MB i ponad 10 tysięcy plików.
- Logi aplikacyjne: ponad 9 MB.
- W logach znaleziono co najmniej 126 wystąpień adresów e-mail.
- Dołączone są `dist`, `test-results` i `tsbuildinfo`.
- Dołączony `node_modules` jest platformowo niezgodny z audytem Linux i blokuje Vite/Rollup build.

### Scenariusz błędu lub nadużycia

- Archiwum jest wysyłane klientowi, wykonawcy lub na serwer wraz z logami i PII.
- Dołączenie node_modules utrudnia wykrycie, które zależności są faktycznie reprodukowalne z lockfile.
- Przypadkowy sekret, token albo payload diagnostyczny może zostać dołączony w kolejnej paczce.

### Wpływ biznesowy i techniczny

- Ryzyko prywatności i niekontrolowanego transferu danych diagnostycznych.
- Niepowtarzalny build i większa powierzchnia supply-chain.
- Wolniejsze skanowanie, deployment i backup.

### Dlaczego obecna implementacja nie wystarcza

- Ręczne ZIPowanie katalogu projektu nie jest pipeline’em wydaniowym.

### Minimalna poprawka blokująca incydent

- Dodaj `.gitignore`, `.dockerignore` i skrypt `pack-source` z allowlistą.
- Nigdy nie pakuj logów, node_modules, dist, test results, sekretów i lokalnych configów.
- Uruchamiaj secret/PII scan przed publikacją artefaktu.

### Docelowe rozwiązanie

- CI buduje czysty source artifact oraz osobny immutable runtime image z lockfile.
- SBOM i podpis obrazu/artefaktu.

### Wymagane testy regresyjne

- `ReleaseArtifact_DoesNotContainForbiddenPaths`.
- Secret scan i PII pattern scan w CI.
- Clean clone build na Linux.

### Kryterium zamknięcia

- Artefakt wydaniowy jest minimalny, reprodukowalny i nie zawiera danych runtime.

---

## AUD3-021 | Frontend ma ostrzeżenia hooków, niepewny logout i surowe SVG w DOM

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM |
| Priorytet | P1/P2 |
| Pewność | Wysoka dla kodu; XSS przez SVG niepotwierdzone |
| Status | OTWARTY CZĘŚCIOWO |

### Dowód w kodzie

- TypeScript typecheck przechodzi.
- ESLint: 0 errors, 19 warnings.
- `useAsyncData.ts`: brakujące zależności `loader` i `t`, dynamic dependency list.
- `AssetsPage.tsx`, `LicensesPage.tsx`, `PeoplePage.tsx`, `ProceduresPage.tsx`: brakujące dependencies mogą powodować stale closure.
- `AuthProvider.tsx:127-130`: logout ignoruje błąd revoke i natychmiast czyści tylko lokalną sesję.
- `AssetsPage.tsx` i `TwoFactorCard.tsx`: `dangerouslySetInnerHTML` dla SVG z backendu.
- `SocialCallbackPage.tsx`: access JWT jest pobierany z URL fragment.

### Scenariusz błędu lub nadużycia

- Stale closure wykonuje operację na nieaktualnie zaznaczonych rekordach.
- Logout request nie dociera do backendu, ale użytkownik uważa sesję za unieważnioną. Skradziony refresh nadal działa.
- Jeżeli generator SVG lub dane wejściowe kiedyś staną się kontrolowane, surowy SVG sink zwiększa powierzchnię XSS.

### Wpływ biznesowy i techniczny

- Błędy UI trudne do reprodukcji, możliwe operacje na złym zestawie rekordów.
- Nieskuteczny revoke przy problemie sieciowym.
- Defense-in-depth XSS jest słabsze niż potrzebne.

### Dlaczego obecna implementacja nie wystarcza

- Warnings hooków dotyczą semantyki, nie tylko stylu.
- Logout powinien informować o stanie revoke, a nie połykać każdy błąd.

### Minimalna poprawka blokująca incydent

- Napraw wszystkie exhaustive-deps warnings albo świadomie wydziel stabilne callbacks.
- Przy logout usuń lokalny token, ale pokaż stan „server revoke failed” i zaoferuj revoke-all po ponownym połączeniu.
- Renderuj SVG jako blob/object/img po sanityzacji albo generuj PNG.
- Zamień JWT-in-fragment na jednorazowy code exchange.

### Docelowe rozwiązanie

- Rozbij wielkie strony na feature components/hooks z testami.
- Dodaj CSP ograniczające skrypty i obiekty.

### Wymagane testy regresyjne

- ESLint bez warnings z kategorii hooks.
- Test logout przy 500/offline.
- Test, że callback URL nie zawiera długowiecznego access tokenu.

### Kryterium zamknięcia

- Frontend build, lint, unit i E2E są zielone z czystego install.

---

## AUD3-022 | Brak pełnego hardeningu kontenerów i nginx

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM |
| Priorytet | P1/P2 |
| Pewność | Wysoka dla plików w paczce |
| Status | OTWARTY |

### Dowód w kodzie

- Backend Dockerfile nie przełącza procesu na nie-root user.
- Frontend nginx również używa domyślnego modelu użytkownika obrazu.
- nginx config nie ustawia kompletnego CSP, HSTS, X-Content-Type-Options, Referrer-Policy i Permissions-Policy.
- AllowedHosts pozostaje szerokie, a część bezpieczeństwa zależy od zewnętrznego proxy.
- API wykonuje migracje przy starcie, co łączy rollout aplikacji ze zmianą schematu.

### Scenariusz błędu lub nadużycia

- Kompromitacja procesu daje większe uprawnienia w kontenerze niż konieczne.
- Brak security headers zwiększa wpływ przyszłego XSS/clickjacking/MIME sniffing.
- Długa migracja lub konflikt blokuje startup podczas deployu.

### Wpływ biznesowy i techniczny

- Słabsza izolacja runtime i trudniejszy bezpieczny rollout.

### Dlaczego obecna implementacja nie wystarcza

- Bezpieczeństwo hosta nie zastępuje least privilege w obrazie.

### Minimalna poprawka blokująca incydent

- Dodaj nieuprzywilejowanego USER, read-only filesystem, tmpfs i drop capabilities.
- Dodaj security headers w nginx/reverse proxy.
- Ogranicz AllowedHosts i CORS do produkcyjnych originów.
- Rozdziel migrator job od uruchomienia aplikacji.

### Docelowe rozwiązanie

- Podpisane minimalne obrazy, SBOM, vulnerability scanning i policy admission.
- Blue/green lub rolling-safe expand/migrate/contract.

### Wymagane testy regresyjne

- Kontener startuje jako non-root i działa na read-only FS.
- Automatyczny test security headers.
- Migracje są kompatybilne z co najmniej dwiema wersjami aplikacji podczas rollout.

### Kryterium zamknięcia

- Runtime spełnia least privilege i bezpieczny deployment bez manualnych wyjątków.

---

## AUD3-023 | Testów jest dużo, ale brakuje testów dla najgroźniejszych ścieżek

| Pole | Ocena |
| --- | --- |
| Severity | HIGH jako luka procesu |
| Priorytet | P0/P1 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- Repo zawiera 405 Fact i 18 Theory, co jest mocnym plusem.
- Tenant integration suite ma tylko kilka podstawowych przypadków A/B i wymaga lokalnego PostgreSQL.
- Brak znalezionych testów dla inactive OAuth, OAuth+2FA, login-CSRF, concurrent refresh, Stripe unknown status, dashboard Employee, checklist Employee i role hierarchy Admin→Owner.
- Assignment tests sprawdzają normalne Accept, ale nie `Employee A cannot accept Employee B`.
- Public token tests sprawdzają expired/revoked/unknown, ale nie wszystkie terminalne przejścia parent workflow.

### Scenariusz błędu lub nadużycia

- Refactor naprawia jeden endpoint, ale alternatywna ścieżka nadal omija policy.
- Testy in-memory przechodzą, a realny PostgreSQL constraint lub transaction zachowuje się inaczej.
- Kolejna zmiana przywraca cross-tenant relation bez alarmu.

### Wpływ biznesowy i techniczny

- Duża liczba testów nie chroni najważniejszych invariantów.
- Brak obiektywnej bramki do wydania dla 100 tenantów.

### Dlaczego obecna implementacja nie wystarcza

- Coverage ilościowe nie zastępuje risk-based test design.

### Minimalna poprawka blokująca incydent

- Dodaj testy z listy P0 w tym raporcie zanim poprawki zostaną uznane za zamknięte.
- Uruchamiaj integration suite na ephemeral PostgreSQL w CI.
- Dodaj test refleksyjny endpoint-role matrix i tenant FK model test.

### Docelowe rozwiązanie

- Testcontainers lub dedykowany CI service PostgreSQL.
- Security regression suite jako osobny wymagany job.
- Mutation testing dla kluczowych policy, żeby sprawdzić, czy test wykryje usunięcie guardu.

### Wymagane testy regresyjne

- Każdy AUD3-P0 ma co najmniej jeden negatywny test HTTP i jeden test Application/DB.
- CI nie pozwala merge/release przy ich awarii.

### Kryterium zamknięcia

- Wyniki backend build, unit, integration, migrations i E2E są powtarzalne z czystego checkoutu.

---

## AUD3-024 | Duże serwisy i strony naruszają SRP, utrudniając audyt bezpieczeństwa

| Pole | Ocena |
| --- | --- |
| Severity | MEDIUM |
| Priorytet | P2 |
| Pewność | Wysoka |
| Status | OTWARTY |

### Dowód w kodzie

- `OffboardingService` 851 linii, `AssetAuditCampaignService` 725, `AuthService` 589, `AssignmentService` 567.
- `AssetsPage.tsx` 1309 linii, `PeoplePage.tsx` 755, `SettingsPage.tsx` 740.
- AuthService łączy password login, OAuth linking, 2FA, reset, verification i refresh lifecycle.
- API endpoint files mają wiele mechanicznych i nieużywanych usingów, w tym Infrastructure, co zaciera boundary.
- Architecture tests sprawdzają głównie reference graph, nie wszystkie konwencje domenowe i forbidden dependencies.

### Scenariusz błędu lub nadużycia

- Programista zmienia jeden workflow i nie zauważa alternatywnego path w tej samej klasie.
- Policy jest stosowana w jednym use case, ale pominięta w drugim.
- Review diffu staje się zbyt duży, aby wykrywać regresje.

### Wpływ biznesowy i techniczny

- Wyższy koszt zmian, więcej regresji i trudniejsza odpowiedzialność zespołu.
- Błędy bezpieczeństwa w tym audycie są częściowo skutkiem rozproszenia policy po dużych klasach.

### Dlaczego obecna implementacja nie wystarcza

- Sama warstwowa struktura projektu nie gwarantuje Clean Architecture na poziomie use case.

### Minimalna poprawka blokująca incydent

- Rozbij Auth na SignIn, ExternalIdentity, TwoFactor, PasswordRecovery i Session services/use cases.
- Rozbij Offboarding/Audit na commands, query builders, public capability handlers i transition services.
- Frontend rozbij na feature hooks i mniejsze komponenty.

### Docelowe rozwiązanie

- Command/query handlers lub cienkie use-case services bez tworzenia nadmiernego frameworku.
- Centralne policies wstrzykiwane do use cases.
- Architecture tests dla namespace/dependency/convention i zakaz bezpośredniego DbContext w endpointach.

### Wymagane testy regresyjne

- Test architektury: Application nie referuje API/Infrastructure.
- Test: endpoint nie importuje DbContext/repository concrete.
- Test: każdy mutating use case ma authz policy i audit event.

### Kryterium zamknięcia

- Najbardziej wrażliwe use cases są małe, mają jedną odpowiedzialność i oddzielne testy.

---

# 8. Authentication i zarządzanie tożsamością: ocena 35/100

## 8.1. Co jest dobre

- Hasła są hashowane PBKDF2-SHA256, z losową solą i fixed-time compare.
- JWT ma issuer, audience, podpis i krótki czas życia konfigurowany produkcyjnie.
- OIDC id_token jest kryptograficznie walidowany przez metadata/JWKS.
- Refresh tokeny, reset tokens i publiczne tokeny są przechowywane jako hash.
- Cookie refresh i device trust są HttpOnly, Secure poza Development i SameSite=Lax.
- Login i refresh mają rate limiting.

## 8.2. Co nadal blokuje

- Inactive OAuth bypass.
- OAuth omija TOTP.
- Brak browser correlation i nonce w OAuth.
- Microsoft issuer validator akceptuje dowolny tenant GUID nawet przy specyficznym TenantId. Konfiguracja single-tenant nie jest egzekwowana przez validator.
- Facebook access token jest przekazywany w query string do Graph API, a każdy zwrócony e-mail jest traktowany jako verified.
- Nowe konto social jest oznaczane email verified, także gdy provider nie dostarczył wystarczającego sygnału w każdym flow.
- Access JWT w URL fragment zwiększa ryzyko przechwycenia przez rozszerzenia, historię diagnostyczną i kod strony.
- 2FA setup nie wymaga recent authentication.
- Device trust lifecycle nie jest związany z resetem/re-enable 2FA w jednym spójnym mechanizmie.
- Brak refresh family/reuse detection.

# 9. Authorization i prywatność wewnątrz firmy: ocena 22/100

To jest najsłabszy obszar wersji trzeciej. Role istnieją i są stosowane w wielu miejscach, ale większość kontroli ma charakter modułowy. Brakuje relacji actor-resource. Dla systemu zawierającego dane pracowników i sprzętu to za mało.

## 9.1. Minimalna docelowa macierz zakresów

| Rola | Zakres docelowy | Zakazane bez jawnego override |
| --- | --- | --- |
| Owner | Cały tenant, billing, role Owner | Brak poza operacjami platformowymi |
| Admin | Cały tenant operacyjnie | Nadawanie Owner, billing owner-only, transfer ownership |
| Manager | Osoby i zasoby zarządzanych zespołów | Inne zespoły, globalne koszty, role |
| Employee | Własna Person, własne assignmenty, przypisane procedury | Dowolne personId/assignmentId/procedureId |
| HR | People/onboarding/offboarding według polityki | Billing i techniczne sekrety |
| AssetOperator | Aktywa i wydania | Role użytkowników, billing |
| Finance | Koszty i subskrypcja | Niepotrzebne dane osobowe i operacyjne |
| Auditor | Tylko odczyt jawnie audytowalnych danych | Mutacje i sekrety |

## 9.2. Zasada implementacyjna

- Każdy endpoint musi przejść trzy pytania: czy actor jest w tenant, czy ma akcję, czy ma relację do konkretnego zasobu.
- Nie pobieraj całej organizacji, aby później filtrować w pamięci. Scope ma być częścią query.
- Deny by default. Brak policy dla nowego endpointu powinien powodować test/build failure.
- Frontend navigation jest UX, nie security control.

# 10. Multi-tenancy: ocena 68/100

## 10.1. Mocne strony

- Większość repozytoriów przyjmuje OrganizationId i filtruje query.
- Wersja trzecia dodała wiele composite FK `(OrganizationId, Id)` dla najbardziej oczywistych relacji.
- Serwisy walidują wiele foreign IDs po organizacji.
- Istnieją integration tests dla części podstawowych scenariuszy tenant A/B.
- Publiczne tokeny assignment/offboarding wyszukują hash bez przyjmowania OrganizationId od klienta.

## 10.2. Dlaczego to nadal nie jest 90+/100

- Nie każda relacja tenant-owned ma composite FK.
- Brak centralnego query filter lub RLS oznacza, że każdy nowy repository/query musi pamiętać o tenant scope.
- Integration suite nie obejmuje wszystkich create/update foreign IDs.
- Joby, raporty i importy mają ten sam poziom ryzyka co endpointy, ale są słabiej testowane A/B.
- OrganizationId w JWT jest wystarczający tylko wtedy, gdy wszystkie powiązania zasobów są również poprawne.

## 10.3. Relacje wymagające dalszego utwardzenia

- `Asset.CategoryId` do `AssetCategory`.
- `AssetInspection.AssetId`, `AssignmentId`, `OffboardingItemId`.
- `ServiceTicket.AssetId`.
- `Location.ParentId` jako tenant-safe self FK.
- `ProcedureDocument.ProcedureId` z OrganizationId.
- Owned joins dla JobProfile category/procedure.
- `LicenseSeat.PersonId`.
- `Assignment.PersonId` i `AssignmentAsset.AssetId`.
- `ProcedureAcceptance.PersonId/ProcedureId`.
- `AssetAuditParticipant.PersonId`, audit item Asset/ExpectedPerson.
- `EquipmentReservation.RequesterPersonId/AssignmentId` i ReservationItem targets.
- `OffboardingCase.PersonId`, OffboardingItem Asset/Assignment/License.
- `AssetEvidence` parent IDs.
- `DashboardLayout.OrganizationUserId` powiązane z tym samym OrganizationId.

# 11. Ochrona danych i kryptografia: ocena 50/100

- AES-GCM i HKDF per purpose są dobrym wyborem prymitywów.
- Hashowanie capability tokenów jest poprawne.
- Największy problem to key management, rotation, legacy plaintext i operacyjny recovery.
- Szyfrowanie aplikacyjne nie zastępuje szyfrowania dysku, backupów, TLS i ograniczeń dostępu do bazy.
- TOTP secret, license keys i custom sensitive fields wymagają klasyfikacji, retention i audit access.
- Nie przechowuj danych wrażliwych w logach lub activity details.

# 12. Stripe i billing: ocena 38/100

- Podpis webhooka jest weryfikowany HMAC z fixed-time compare i pięciominutową tolerancją.
- EventId idempotency to ważna poprawa.
- Największe problemy to fail-open entitlement, niepełne powiązanie organization/customer/subscription, same-second ordering, dowolne redirect URL i catch-all błędów.
- Ręczna implementacja klienta i parsera Stripe zwiększa koszt utrzymania i ryzyko różnic względem API.
- Billing powinien mieć reconciliation job porównujący lokalny stan z canonical Stripe state.

# 13. Publiczne linki: ocena 37/100

| Flow | Hash lookup | Expiry/Revoke | Parent-state guard | Ocena |
| --- | --- | --- | --- | --- |
| Assignment | Tak, indeksowany | Tak | Akceptacja blokowana stanem assignment; read receipt pozostaje do TTL | Najlepszy z trzech, wymaga decyzji policy o read-after-accept |
| Offboarding | Tak | Tak | Brak pełnego guard/revoke dla Cancel/Restore | NO-GO |
| Asset audit | Nie, scan wszystkich tokenów | Tak per participant | Brak guard/revoke dla Complete/Cancel | NO-GO i ryzyko DoS |

# 14. Backend: ocena 70/100 jakości, 56/100 reliability

## 14.1. Plusy

- Dobra ogólna separacja Domain/Application/Infrastructure/API.
- Repozytoria i use-case services dają sensowny szkielet Clean Architecture.
- Result/Error model ogranicza wyjątki jako normalny flow.
- Wiele domenowych invariantów i stanów workflow jest obecnych.
- Jest sporo testów jednostkowych.
- Poprawiono concurrency mapping i plan limit lock.

## 14.2. Minusy

- Duże serwisy mieszają authz, orchestration, tokeny, e-mail, raporty i persistence.
- Brak wspólnego resource authorization powoduje luki między use case’ami.
- Manualne listowanie całych organizacji w wielu serwisach szkodzi wydajności.
- DB bytea dla dużych dokumentów/zdjęć zwiększa rozmiar bazy, backupów i I/O.
- Brak atomowości w kilku procesach bezpieczeństwa i limitach.

# 15. Frontend: ocena 68/100

## 15.1. Wyniki narzędzi

- `tsc -b`: PASS.
- ESLint: 0 errors, 19 warnings.
- Vite build: nie został potwierdzony z dostarczonej paczki, ponieważ `node_modules` zawierało zależności z innej platformy.

## 15.2. Najważniejsze ostrzeżenia ESLint

- `useAsyncData.ts`: dynamic dependency list, missing `loader` i `t`.
- `Layout.tsx`: cleanup używa ref, którego wartość może się zmienić.
- `AssetsPage.tsx`: `statuses` destabilizuje deps i brakuje `selected` w trzech hookach.
- `LicensesPage.tsx`, `PeoplePage.tsx`, `ProceduresPage.tsx`: brakujące zależności hooków.
- Pozostałe warnings Fast Refresh są mniej istotne produkcyjnie, ale wskazują mieszanie helperów i komponentów.

# 16. Clean Architecture: 69/100

- Warstwy są czytelne i projekt nie jest monolitem typu „wszystko w controllerach”.
- Domain nie zależy od Infrastructure, co jest poprawne.
- Application używa abstrakcji repozytoriów i usług.
- API nadal ma mechaniczne importy Infrastructure, a część composition/health bezpośrednio zna DbContext.
- Największy problem nie leży w nazwach folderów, tylko w braku centralnej policy layer dla zasobów i w zbyt dużych use-case services.
- Clean Architecture powinna chronić invarianty biznesowe. Jeżeli Employee może zaakceptować cudzy assignment, struktura folderów nie spełnia celu architektury.

# 17. SOLID: 54/100

## 17.1. Single Responsibility: 42/100

- AuthService, OffboardingService, AssetAuditCampaignService i AssignmentService mają zbyt wiele odpowiedzialności.
- Frontend pages łączą fetch, formularze, modale, selekcję, eksport i workflow.

## 17.2. Open/Closed: 58/100

- Role są stringami i tablicami w wielu miejscach. Dodanie nowej roli wymaga ręcznej aktualizacji wielu use case’ów.
- Manual Stripe parser wymaga zmian przy nowych statusach/eventach.

## 17.3. Liskov: 72/100

- Nie wykryto oczywistych naruszeń dziedziczenia. Projekt używa niewiele hierarchii implementacyjnych.

## 17.4. Interface Segregation: 60/100

- Repozytoria są relatywnie tematyczne, ale część serwisów zależy od wielu abstrakcji.

## 17.5. Dependency Inversion: 48/100

- Application generalnie zależy od abstrakcji.
- Są miejsca z zależnością na konkretne serwisy i API/Infrastructure boundary jest miejscami rozmyte.

# 18. DRY: 64/100

- Wspólne helpery tokenów, CSV, image sanitizer i response builders są plusem.
- Kontrole roli i scope są powtarzane ręcznie, co doprowadziło do niespójności.
- Public capability flows mają trzy różne warianty wyszukiwania i state guard.
- Endpoint files mają powielone usingi i mechaniczny boilerplate.
- Docelowo należy scentralizować invarianty, a nie tylko helpery składniowe.

# 19. YAGNI: 70/100

- Większość funkcji odpowiada realnemu produktowi.
- Nie ma masowej abstrakcji bez użycia ani licznych TODO/NotImplemented.
- Equipment reservation i rozbudowane moduły warto wdrażać dopiero po domknięciu bezpieczeństwa podstawowych flow.
- Nie należy teraz przepisywać całego systemu ani wprowadzać ciężkiego frameworku CQRS tylko dla nazwy. Potrzebne są konkretne policies i małe use cases.

# 20. KISS: 58/100

- Podstawowy podział warstw jest zrozumiały.
- Manualny OAuth i manualny Stripe są bardziej złożone i ryzykowne niż standardowe middleware/SDK.
- Location jako jednocześnie encja drzewa i tekstowa ścieżka na zasobie jest zbyt skomplikowane semantycznie.
- Single-instance global lock upraszcza consistency, ale komplikuje deployment i HA.
- Najprostsze bezpieczne rozwiązanie to centralne policy i jawne scope query, nie kolejne lokalne ify.

# 21. Clean Code: 62/100

- Nazewnictwo jest przeważnie czytelne, a komentarze często opisują przyczynę poprawki.
- Komentarze `AUD-*` są pomocne tymczasowo, ale część z nich deklaruje bezpieczeństwo, którego kod jeszcze nie domyka.
- Duże klasy i metody utrudniają review.
- Brak TODO/FIXME nie oznacza braku długu. Część długu jest w strukturze i niespójnych policy.
- Warto usuwać mechaniczne usingi i utrzymywać warning-free build.

# 22. Testy i QA: 56/100

- Liczba testów jednostkowych jest dobra.
- Są testy integracyjne tenant isolation, ale ich zakres jest zbyt wąski dla liczby relacji i endpointów.
- Brak .NET SDK w środowisku oznacza, że nie potwierdzono aktualnego builda ani wyniku testów.
- Frontend ma mało test files względem 19 tysięcy linii i złożonych workflow.
- Brakuje security test matrix i testów współbieżności dla refresh/upload/webhook.

# 23. Scalability i HA: 34/100

- Single-instance jest obecnie wymagane przez IMemoryCache i joby.
- Duże dashboard/location/workspace query pobierają całe organizacje do pamięci.
- Audit public token invalid lookup rośnie O(N).
- Pliki w PostgreSQL zwiększają obciążenie bazy i backupów.
- Brak kolejki/outbox dla e-maili, webhooków i ciężkich zadań.
- Dla 100 firm liczba rekordów może nadal być umiarkowana, ale problemem jest burst i jedna instancja, nie tylko średni wolumen.

# 24. Deployment i konfiguracja: 44/100

- Produkcja lepiej waliduje JWT i domyślne DB credentials.
- Brakuje fail-closed dla dedicated encryption key, produkcyjnych URL/originów i pełnej konfiguracji Stripe/OAuth/SMTP.
- Single-instance utrudnia rolling deploy.
- Dołączone logi/node_modules pokazują brak czystego release pipeline.
- Kontenery nie są jawnie non-root/read-only.
- Security headers są niepełne.

# 25. Observability: 45/100

- Serilog i activity log zapewniają podstawowy ślad.
- Logi są lokalnymi plikami i znalazły się w paczce źródłowej.
- Pełne body błędu Stripe może trafić do logu.
- Brakuje jawnych metryk: auth failures by path, OAuth state failures, token invalid rate, cross-tenant guard failures, webhook reconciliation, job lag.
- Brakuje korelacji request/event/workflow w wielu logach biznesowych.
- PII redaction i retention powinny być formalną polityką, nie przypadkiem.

# 26. Wydajność: 51/100

- Paged repositories istnieją dla części głównych list.
- Dashboard ładuje pełne listy assets/people/assignments/categories/teams/licenses.
- Location liczy agregaty w pamięci.
- MyWorkspace pobiera wszystkie assets/procedures/assignments organizacji, potem filtruje.
- Asset audit public response ma N+1.
- Public audit invalid token ma O(N).
- Szyfrowanie i sanityzacja obrazów są kosztowne CPU, a brak kolejki może blokować request thread.

# 27. Mniejsze i dodatkowe problemy

- Subscription details są dostępne wszystkim zalogowanym rolom. Powinny być Owner/Finance/Admin zgodnie z polityką.
- Rejestracja od razu wydaje pełną sesję, a email verification nie ogranicza funkcji. Weryfikacja ma więc głównie charakter kosmetyczny.
- Globalny unique e-mail uniemożliwia tej samej osobie konto w wielu firmach. To ograniczenie produktu i modelu identity.
- Invitation token jest zapisany przed wysyłką e-maila. Przy błędzie wysyłki pozostaje ważny token w bazie, choć raw token nie jest ujawniony.
- Microsoft issuer validator powinien respektować skonfigurowany TenantId, jeżeli produkt ma być single-tenant Entra.
- OAuth start/callback powinny mieć rate limiting i limit rozmiaru formularza.
- IMemoryCache nie ma jawnego SizeLimit dla OAuth/2FA entries.
- 2FA challenge jest single-attempt. To nie jest luka, ale pogarsza UX i może zwiększać login traffic.
- Password max length powinien być ograniczony, aby uniknąć niekontrolowanego kosztu hash/input.
- Malformed encrypted field powinien generować kontrolowany security event, nie zwykły 500.
- Asset encrypted value może przekroczyć limit kolumny po Base64 overhead.
- Assignment public token po akceptacji pozostaje read-capability do expiry. Należy jawnie zdecydować, czy to zamierzony receipt link, czy token powinien zostać rozdzielony.
- Evidence count race dotyczy także publicznych uploadów audit/offboarding.
- Procedure documents do 25 MB nie mają widocznego skanowania antymalware.
- Nginx 50 MB i backend/FormOptions 30 MB są niespójne.
- Catch-all DomainException bywa mapowany poprawnie, ale nie wszystkie exception paths mają stabilny error code.
- Manualne `HttpClient` w Stripe powinno pochodzić z IHttpClientFactory, mieć timeout, retry policy tylko dla bezpiecznych operacji i telemetry.
- Rate limiter jest per instancja. Po przejściu na HA musi być globalny dla krytycznych operacji lub wsparty WAF.
- Activity details mogą zawierać ex.Message lub dane biznesowe. Należy sklasyfikować i redagować.
- Dashboard layout FK powinien wymuszać, że user i layout mają ten sam OrganizationId.
- Brakujące FK mogą również powodować orphan records, nie tylko tenant risk.
- Brak testów migracji z poprzedniej wersji produkcyjnego schematu.
- API migration-on-start wymaga bardzo ostrożnej kolejności startupu jobów i ruchu.
- AllowedHosts `*` należy ograniczyć w produkcji.
- CORS powinien fail startup przy pustej/zbyt szerokiej produkcyjnej konfiguracji.
- SMTP/OAuth brak konfiguracji powinien mieć readiness status, nie tylko późny błąd przy użyciu.
- Brak jawnego backup restore drill dla zaszyfrowanych danych i key ring.
- Brak privacy retention dla logów i evidence poza częścią ustawień aplikacyjnych.
- Brak formalnego threat modelu dla public capability links.
- QR public report endpoint powinien mieć dodatkowe abuse controls poza samym IP, szczególnie przy rozproszonym ataku.

# 28. Punktacja 0-100

| Obszar | V2 | V3 | Komentarz |
| --- | --- | --- | --- |
| Wynik ogólny risk-weighted | 56 | **58** | Kod poprawiony, ale głębszy audyt odkrył blokery authz |
| Gotowość produkcyjna 100 firm | 41 | **34** | Ocena skorygowana w dół przez potwierdzone privilege/row-level bugs |
| Jakość inżynierska kodu | około 60+ | **68** | Lepsze FK, tokeny, concurrency i struktura |
| Security overall | 58 | **38** | OIDC lepsze, ale OAuth/2FA/session/RBAC mają blokery |
| Authentication/session | 66 | **35** | Inactive OAuth, 2FA bypass, login-CSRF, session lifecycle |
| Authorization | 71 | **22** | Poprzednia ocena była zbyt optymistyczna; brak resource scope |
| Multi-tenancy | 39 | **68** | Duża poprawa w org scoping i composite FK, nadal niepełna DB defense |
| Ochrona danych | 44 | **50** | AES-GCM dodane, ale lifecycle kluczy blokuje wyższą ocenę |
| Clean Architecture | 65 | **69** | Dobry szkielet, słaba centralizacja policy |
| SOLID | 49 | **54** | Część odpowiedzialności wydzielona, duże serwisy pozostają |
| DRY | 63 | **64** | Helpery lepsze, authz nadal powtarzane ręcznie |
| YAGNI | 69 | **70** | Ogólnie rozsądny zakres |
| KISS | 55 | **58** | Poprawa, ale manual OAuth/Stripe i dual location model |
| Clean Code | 55 | **62** | Czytelność lepsza, lecz monolity klas/stron |
| Backend reliability | 59 | **56** | Asset limit naprawiony, sesje/upload/webhook race pozostają |
| Frontend | 62 | **68** | Typecheck pass, lecz hook warnings i brak potwierdzonego clean build |
| Testy/QA | 59 | **56** | Dużo testów, ale brak security regression i brak uruchomienia backendu |
| Scalability/HA | 39 | **34** | Jawny single-instance blokuje HA |
| Deployment/config | 48 | **44** | Lepszy fail-closed JWT, ale artefakt i runtime hardening słabe |
| Observability | 47 | **45** | Logi istnieją, lecz PII i brak security metrics |
| Performance | niepunktowane | **51** | Full-list queries, N+1 i O(N) token lookup |
| Validation/API | niepunktowane | **43** | Tylko 11/65 request DTO objętych centralnym validation |
| Stripe/billing | niepunktowane | **38** | Idempotency lepsze, entitlement/asocjacja nadal ryzykowne |
| Public links | niepunktowane | **37** | Assignment dobry, offboarding/audit terminal state błędny |

**Ważne:** 58/100 nie jest średnią arytmetyczną tabeli. W systemie multi-tenant pojedyncza potwierdzona eskalacja lub fałszywa akceptacja ma większą wagę niż wiele dobrze napisanych klas.

# 29. Priorytet napraw P0

1. **Zamknąć hierarchię ról: Admin nie może nadać/odebrać Owner, ochrona ostatniego Ownera, revoke sesji po role/active.**
2. **Naprawić ExternalLogin: IsActive, wspólny SignInPolicy i brak sesji przed 2FA.**
3. **Zastąpić własny OAuth flow standardowym middleware albo dodać correlation cookie, nonce, rate limit i one-time code exchange.**
4. **Naprawić Assignment Accept ownership. Employee nie może zaakceptować cudzego assignmentu.**
5. **Wdrożyć centralne resource authorization dla Employee/Manager i zamknąć dashboard/location/checklist/procedure leaks.**
6. **Unieważniać public tokeny i sprawdzać parent state dla Cancel/Complete/Restore w offboardingu i auditach.**
7. **Naprawić Stripe entitlement fail-closed, association validation, event reconciliation i redirect allowlist.**
8. **Wymusić dedykowany field encryption key, key IDs i rotację.**
9. **Dodać atomowy session lifecycle: security stamp, refresh family, concurrent refresh protection, device trust revoke.**
10. **Dodać security regression suite na realnym PostgreSQL i zablokować release do czasu zielonego wyniku.**

# 30. Priorytet napraw P1

1. Dokończyć composite tenant FK dla wszystkich relacji i dodać model test.
2. Ograniczyć trusted proxies i zabezpieczyć port backendu.
3. Naprawić atomiczny limit evidence oraz streaming/object storage.
4. Dodać validator do każdego request DTO i limity długości/kolekcji.
5. Przenieść OAuth/2FA state do shared store, a joby do worker/leader locks.
6. Znormalizować LocationId na Asset/Person.
7. Oczyścić release artifact i wdrożyć clean CI build/SBOM/secret scan.
8. Naprawić frontend hook warnings i potwierdzić build/unit/E2E z czystego install.
9. Utwardzić kontenery, nginx, CORS, AllowedHosts i security headers.
10. Wdrożyć log redaction, centralny log store, metryki security i alerting.

# 31. Wymagana macierz testów przed GO

## 31.1. Role i użytkownicy

- [ ] Admin nie może nadać Owner sobie ani innemu użytkownikowi.
- [ ] Admin nie może odebrać Owner ani dezaktywować ostatniego Ownera.
- [ ] Zmiana ról/aktywności natychmiast unieważnia refresh i trusted devices.
- [ ] Stary JWT nie przechodzi operacji wymagającej aktualnego security stamp.

## 31.2. OAuth i 2FA

- [ ] Inactive linked user i inactive email-matched user są odrzucani.
- [ ] OAuth user z TOTP otrzymuje challenge, nie token.
- [ ] Callback bez correlation cookie/nonce jest odrzucany.
- [ ] State jest one-time i związany z browserem/providerem.
- [ ] Specific Microsoft TenantId odrzuca issuer innego tenant.

## 31.3. Assignment i row-level authorization

- [ ] Employee A nie może zaakceptować assignmentu Employee B.
- [ ] Manager A nie widzi people/assets/assignments Team B.
- [ ] Employee nie widzi dashboardu tenant-wide ani location inventory.
- [ ] Employee nie pobiera checklisty innej osoby.
- [ ] Employee nie pobiera nieprzypisanej procedury.

## 31.4. Multi-tenancy A/B

- [ ] Każde foreign ID z create/update jest testowane z targetem organizacji B.
- [ ] DB constraint odrzuca cross-tenant insert wykonany bezpośrednio w DbContext/SQL.
- [ ] Raporty, exporty, joby i public tokeny nie przeciekają A/B.
- [ ] Model test wykrywa FK tenant entity bez OrganizationId.

## 31.5. Public tokens

- [ ] Cancel/Complete/Restore revoke token w tej samej transakcji.
- [ ] Po terminalnym stanie read/write/upload są odrzucone.
- [ ] Invalid audit token robi jeden indexed lookup.
- [ ] Regenerate link unieważnia poprzedni token.

## 31.6. Sesje

- [ ] Dwa równoległe refresh requesty: dokładnie jeden sukces.
- [ ] Reuse starego refresh revokuje rodzinę.
- [ ] Reset password jest atomowy z revokacją.
- [ ] Disable/re-enable 2FA unieważnia wszystkie trusted devices.

## 31.7. Stripe

- [ ] Unknown status nie daje Pro.
- [ ] Metadata/customer/subscription mismatch jest odrzucony i alertowany.
- [ ] Dwa eventy w tej samej sekundzie są prawidłowo reconciled.
- [ ] Out-of-order delivery kończy się canonical state.
- [ ] External redirect URL jest odrzucony.

## 31.8. Uploady

- [ ] Równoległe uploady nie przekraczają limitu 5.
- [ ] Oversized body jest odrzucane przed pełnym buforowaniem.
- [ ] Nieprawidłowy magic number, polyglot i decompression bomb są odrzucane.
- [ ] Metadata EXIF/GPS nie pozostaje w zapisanym obrazie.

## 31.9. Deployment

- [ ] Clean Linux build bez dołączonego node_modules.
- [ ] Backend build i migrations na czystym checkout.
- [ ] Non-root container i read-only FS.
- [ ] Security headers i trusted proxy tests.

# 32. Proponowany docelowy model techniczny

## 32.1. Identity i sesje

- Jedna ścieżka `SignInOrchestrator` dla password i external provider.
- Wspólne gates: active, organization active, second factor, security stamp, account lockout.
- Refresh token family z atomową rotacją i reuse detection.
- Step-up authentication dla Owner, billing, role changes i key reveal.
- OAuth przez standardowe middleware, correlation cookie, nonce i distributed state.

## 32.2. Autoryzacja

- Policy = action + resource + scope, nie tylko role string.
- ICurrentActor: OrganizationUserId, OrganizationId, PersonId, roles, managed team IDs.
- Repository queries przyjmują scope specification i filtrują w SQL.
- Deny-by-default endpoint convention oraz automatyczny policy coverage test.

## 32.3. Multi-tenancy

- Każda tenant table ma OrganizationId i alternate key `(OrganizationId, Id)`.
- Każda relacja do tenant table ma composite FK.
- Opcjonalnie RLS z connection/session tenant context jako dodatkowa bariera.
- Background jobs i importy używają tych samych tenant-safe abstractions.

## 32.4. Public capabilities

- Wspólny capability token service: random token, deterministic hash, unique index, expiry, revoke, purpose i parent state.
- Write token jednorazowy lub ograniczony stanem. Read-only receipt może być osobnym tokenem.
- Brak globalnego skanowania hashy.

## 32.5. Billing

- Oficjalny SDK, durable webhook inbox, idempotent worker i reconciliation.
- Entitlement engine z allowlist i grace period.
- Server-generated redirect URL.

## 32.6. Pliki

- Object storage, streaming, checksum, content scan, quarantine, signed download URL.
- Baza przechowuje metadata i pointer, nie duże bytea.
- Atomiczny quota/limit per tenant/resource.

# 33. Kryteria GO dla 100 firm

- [ ] Zero otwartych CRITICAL/P0 z tego raportu.
- [ ] Admin nie może uzyskać Owner, a last-owner invariant jest wymuszony.
- [ ] Wszystkie metody logowania respektują active i 2FA.
- [ ] Row-level RBAC dla Employee/Manager jest wdrożone i testowane.
- [ ] Tenant DB model odrzuca cross-tenant foreign references.
- [ ] Public tokeny są automatycznie revoke na każdym terminalnym przejściu.
- [ ] Stripe entitlement jest fail-closed i reconciled.
- [ ] Dedykowany encryption key oraz rotation test są gotowe.
- [ ] Backend build, unit, integration PostgreSQL, migrations, frontend build/test i E2E są zielone z czystego checkoutu.
- [ ] Release artifact nie zawiera logów, node_modules, danych runtime ani sekretów.
- [ ] Istnieje backup restore drill wraz z key ring.
- [ ] Monitoring/alerting obejmuje auth, tenant violations, public token abuse, webhook failures i job lag.

# 34. Ostateczna decyzja

# **NO-GO**

Wersja trzecia jest lepszym fundamentem niż poprzednie. Najważniejsze poprawki infrastrukturalne nie są pozorne: JWT jest fail-closed, OIDC weryfikuje podpis, część tenant FK jest poprawna, asset limit jest atomowy, publiczny assignment token jest bezpieczniejszy, a uploady obrazów są lepiej sanityzowane.

To jednak nie wystarcza do bezpiecznej produkcji. W tej chwili system ma jednocześnie:

- potwierdzoną eskalację Admin → Owner,
- potwierdzone obejście dezaktywacji przez OAuth,
- potwierdzone obejście TOTP przez OAuth,
- potwierdzoną możliwość akceptacji cudzego assignmentu przez Employee,
- systemowy brak team/own resource authorization,
- aktywne capability links po terminalnych zmianach części workflow,
- niegotowy lifecycle klucza szyfrowania i sesji,
- billing logic, która w części stanów nie jest fail-closed.

Nie rekomenduję wdrożenia realnych klientów, nawet pilotażowego, dopóki P0 nie zostanie zamknięte i pokryte testami regresyjnymi. Pilotaż z danymi syntetycznymi i jedną kontrolowaną organizacją może służyć wyłącznie do testów funkcjonalnych, nie jako dowód bezpieczeństwa multi-tenant.

Po zamknięciu P0, dokończeniu tenant FK i uruchomieniu pełnej suite na PostgreSQL projekt ma realną szansę wejść w zakres 75-82/100 bez przepisywania całości. Największy zwrot da teraz nie kolejny moduł, lecz centralna autoryzacja zasobowa, spójny sign-in/session lifecycle i automatyczne testy granic.

