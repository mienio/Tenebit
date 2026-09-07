# Program afiliacyjny TENEB.IT PARTNERS — plan projektowy

Status: **plan, brak jeszcze kodu.** Dokument opisuje architekturę, model danych, bezpieczeństwo,
API i UI dla ukrytego programu partnerskiego dostępnego pod `teneb.it/partner`. Zaprojektowano go
tak, żeby maksymalnie reużyć wzorce już istniejące w repo (izolacja `token_scope` jak w
`AdminEndpoints`, `PromoCode`/`PromoDurationType` jako wzór na zniżki cykliczne, `ProcessedPaddleEvent`
jako wzór na idempotencję webhooków, `AdminAuditLog` jako wzór na audyt akcji admina) zamiast
wymyślać nowy szkielet.

Powiązane dokumenty w repo: `paddle_plan.md` (Paddle jako Merchant of Record — stąd wynika, że
prowizja afiliacyjna liczona jest od **kwoty netto, którą realnie widzi Tenebit**, nie od kwoty
brutto klienta — patrz §6.1), `SECURITY_HARDENING_MANUAL.md` (konwencje rate-limitingu i WAF, którym
ten plan podlega).

---

## Spis treści

1. [Cel i zasady ogólne](#1-cel-i-zasady-ogólne)
2. [Ukryty routing i dostęp](#2-ukryty-routing-i-dostęp)
3. [Model danych](#3-model-danych)
4. [Uwierzytelnianie afiliantów](#4-uwierzytelnianie-afiliantów)
5. [Kody i linki afiliacyjne](#5-kody-i-linki-afiliacyjne)
6. [Tracking konwersji i naliczanie prowizji](#6-tracking-konwersji-i-naliczanie-prowizji)
7. [Wypłaty (payouty)](#7-wypłaty-payouty)
8. [Komunikacja afiliant ↔ admin](#8-komunikacja-afiliant--admin)
9. [Panel admina](#9-panel-admina)
10. [Panel afilianta (frontend)](#10-panel-afilianta-frontend)
11. [API — pełna specyfikacja endpointów](#11-api--pełna-specyfikacja-endpointów)
12. [Bezpieczeństwo — analiza zagrożeń i mitygacje](#12-bezpieczeństwo--analiza-zagrożeń-i-mitygacje)
13. [Integracja z Paddle](#13-integracja-z-paddle)
14. [Migracje bazy danych](#14-migracje-bazy-danych)
15. [Plan wdrożenia etapami](#15-plan-wdrożenia-etapami)
16. [Decyzje do podjęcia przez właściciela produktu](#16-decyzje-do-podjęcia-przez-właściciela-produktu)
17. [Ryzyka](#17-ryzyka)

---

## 1. Cel i zasady ogólne

Partner afiliacyjny (np. `Damian Kowalski`) zakłada osobne konto niezwiązane z żadną organizacją
klienta, generuje unikalne kody/linki, promuje nimi Tenebit, a gdy ktoś kupi subskrypcję używając
jego kodu — dostaje prowizję. Panel afilianta pokazuje mu **wyłącznie zanonimizowane zdarzenia
sprzedażowe** (data, kwota, kod), nigdy dane osobowe kupującego. Panel admina pokazuje **wszystko**
(kto kupił, ile ma zapłacić afiliantowi, kiedy, czy już zapłacono) oraz umożliwia dwukierunkową
komunikację z afiliantem.

Zasady, które prowadzą cały projekt:

- **Zero PII wycieku do afilianta.** To jest wymóg bezpieczeństwa/RODO, nie tylko UX — afiliant nie
  jest administratorem danych klientów Tenebit i nie ma podstawy prawnej, by je widzieć.
- **Izolacja jak `platform_admin`.** Konto afiliacyjne to trzeci, całkowicie odrębny typ tożsamości
  obok `OrganizationUser` (klient) i platform admina — osobny `token_scope`, osobna tabela, osobny
  JWT, zero nakładania się uprawnień. Patrz wzorzec w `AdminEndpoints.cs` (komentarz: *"Fully isolated
  from /api: separate route group, separate JWT scope... mutually exclusive by construction"*).
- **Unikalność kodu jest twardym constraintem bazy**, nie tylko walidacją aplikacyjną — wyścig
  (race condition) przy równoczesnym tworzeniu tego samego kodu przez dwóch afiliantów musi być
  niemożliwy.
- **Każda wypłata ma dowód i audyt.** Oznaczenie "zapłacono" w adminie to akcja audytowana
  (wzorzec `AdminAuditLog`), z polami: kto oznaczył, kiedy, jaka kwota, jaka metoda/referencja
  przelewu.
- **Idempotencja webhooków.** Konwersja z Paddle musi być zapisana dokładnie raz, nawet przy
  retry'ach webhooka — analogicznie do `ProcessedPaddleEvent`.

---

## 2. Ukryty routing i dostęp

- Frontend: nowa gałąź tras pod `/partner/*` (`/partner/login`, `/partner/register`,
  `/partner/dashboard`, `/partner/codes`, `/partner/conversions`, `/partner/payouts`,
  `/partner/messages`, `/partner/reset-password`), zarejestrowana w `App.tsx` **poza** głównym
  layoutem nawigacyjnym — osobny `<PartnerLayout>` bez linków w publicznym menu, bez wpisu w
  sitemapie, z `<meta name="robots" content="noindex, nofollow">` na każdej stronie `/partner/*`.
- Brak linków z landing page, brak wzmianek w `translations.ts` używanych przez publiczne menu.
  Partnerzy dostają adres bezpośrednio (mail/Slack) od właściciela produktu.
- To **security by obscurity jako dodatek, nie jako jedyna warstwa** — routing jest ukryty, ale
  wszystkie endpointy API pod `/api/partner/*` mają pełną autoryzację niezależnie od tego, czy ktoś
  zgadnie URL. "Ukryty" = nielinkowany, nie "niezabezpieczony".
- Backend: analogicznie osobna grupa endpointów `/api/partner` (nazwa API inna niż frontendowy
  `/partner`, żeby nie kolidować z istniejącym `/api/*` tenant-scoped API), plus publiczny,
  nieautoryzowany redirect `GET /r/{code}` do trackingu kliknięć (opisany w §6).

---

## 3. Model danych

Nowy bounded context `Tenebit.Domain/Affiliates/`, mirroring stylu istniejących agregatów
(prywatne settery, konstruktor walidujący, `DomainException` na złamanie reguł).

### 3.1 `Affiliate` (partner)

| Pole | Typ | Uwagi |
|---|---|---|
| `Id` | `Guid` | PK |
| `Email` | `string` | unikalny (`CI` index), login |
| `PasswordHash` | `string` | ten sam `PasswordHasher` co reszta systemu |
| `FirstName`, `LastName` | `string` | np. Damian, Kowalski |
| `Status` | `AffiliateStatus` | `PendingApproval \| Active \| Blocked` |
| `CountryCode` | `string?` | ISO 3166-1 alpha-2, do kodów per-kraj (§5.3) |
| `CommissionPercent` | `decimal` | domyślnie z globalnego ustawienia, nadpisywalne per-afiliant przez admina |
| `MaxActiveCodes` | `int` | domyślnie 10 (§5.1), nadpisywalne per-afiliant |
| `PayoutDetails` | `string?` (szyfrowane) | IBAN/PayPal — patrz §12.6 o szyfrowaniu w spoczynku |
| `CreatedAt`, `UpdatedAt` | `DateTimeOffset` | |
| `ApprovedAt`, `ApprovedByAdminId` | `DateTimeOffset?`, `Guid?` | audyt akceptacji |
| `BlockedAt`, `BlockedReason` | `DateTimeOffset?`, `string?` | |

Reguły domenowe: `Approve()`, `Block(reason)`, `Reactivate()` — każda zmienia `Status` i sprawdza
dozwolone przejścia (np. nie można `Approve()` z `Blocked` bez jawnego `Reactivate()`).

### 3.2 `AffiliateCode`

| Pole | Typ | Uwagi |
|---|---|---|
| `Id` | `Guid` | PK |
| `AffiliateId` | `Guid` | FK → `Affiliate` |
| `Code` | `string` | **UNIQUE index globalny w całej tabeli**, znormalizowany `ToUpperInvariant()` jak `PromoCode.Code` |
| `CountryCode` | `string?` | opcjonalny — kod tylko dla danego kraju (§5.3); `null` = uniwersalny |
| `IsActive` | `bool` | afiliant/admin może dezaktywować kod bez usuwania historii |
| `CreatedAt` | `DateTimeOffset` | linki **nie wygasają** (wymóg z briefu) — brak `ExpiresAt` |
| `ClickCount` | `int` | licznik zagregowany, do szybkiego dashboardu (denormalizacja, źródło prawdy to `AffiliateClick`) |

Unikalność kodu span'uje **cały system**, nie tylko afilianta — jeden globalny
`CREATE UNIQUE INDEX ux_affiliate_codes_code ON affiliate_codes (code)`. To samo pole musi być też
sprawdzone względem istniejącej tabeli `PromoCode.Code`, żeby afiliacyjny kod nigdy nie kolidował z
kodem promocyjnym sprzedawanym przez marketing (patrz §5.4).

### 3.3 `AffiliateClick`

Surowy log kliknięcia w link afiliacyjny — do trackingu i wykrywania nadużyć (bot traffic, self-referral).

| Pole | Typ | Uwagi |
|---|---|---|
| `Id` | `Guid` | |
| `AffiliateCodeId` | `Guid` | FK |
| `ClickedAt` | `DateTimeOffset` | |
| `IpHash` | `string` | **hash IP, nie IP w plaintext** (HMAC-SHA256 z serwerowym pieprzem) — wystarcza do dedupe/rate-limit bez przechowywania PII |
| `UserAgentHash` | `string` | j.w., do heurystyki bot-detection |
| `AttributionToken` | `Guid` | token wystawiony w podpisanym cookie, patrz §6.2 |

Retencja: `AffiliateClick` starsze niż 13 miesięcy (najdłuższe okno atrybucji + margines) czyszczone
cronem — to log techniczny, nie dokument potrzebny do rozliczeń.

### 3.4 `AffiliateConversion`

Zdarzenie sprzedażowe — serce rozliczeń. Tworzone **wyłącznie** przez handler webhooka Paddle,
nigdy ręcznie.

| Pole | Typ | Uwagi |
|---|---|---|
| `Id` | `Guid` | |
| `AffiliateId` | `Guid` | FK, zdenormalizowane z kodu dla szybkich zapytań |
| `AffiliateCodeId` | `Guid` | FK |
| `OrganizationId` | `Guid` | **widoczne tylko adminowi** — link do prawdziwego klienta |
| `OrganizationSubscriptionId` | `Guid` | FK |
| `PaddleTransactionId` | `string` | **unique index** — idempotencja, jak `ProcessedPaddleEvent.EventId` |
| `EventType` | `AffiliateConversionEventType` | `InitialSale \| Renewal` |
| `OccurredAt` | `DateTimeOffset` | data zdarzenia (z Paddle, nie `UtcNow` lokalnego serwera) |
| `GrossAmount` | `decimal` | kwota zapłacona przez klienta (brutto) |
| `NetAmount` | `decimal` | po odjęciu prowizji Paddle — **baza do liczenia prowizji afiliata**, patrz §6.1 |
| `Currency` | `string` | EUR |
| `CommissionPercent` | `decimal` | **zamrożone w momencie konwersji** (nie referencja live do `Affiliate.CommissionPercent` — zmiana stawki w przyszłości nie może przeliczać historii) |
| `CommissionAmount` | `decimal` | wyliczone, zamrożone |
| `IsWithinCommissionWindow` | `bool` | czy ta odnowa mieści się jeszcze w oknie "X miesięcy prowizji od tego kodu" (§6.1) |

### 3.5 `AffiliatePayoutPeriod` i `AffiliatePayout`

Rozdzielenie na dwa poziomy: **okres rozliczeniowy** (miesiąc, agreguje wszystkie konwersje
afilianta) i **wypłata** (konkretny przelew, może pokrywać jeden lub więcej okresów — np. zaległość
z poprzedniego miesiąca doliczona do kolejnego).

`AffiliatePayoutPeriod`:

| Pole | Typ | Uwagi |
|---|---|---|
| `Id` | `Guid` | |
| `AffiliateId` | `Guid` | |
| `PeriodStart`, `PeriodEnd` | `DateTimeOffset` | miesiąc kalendarzowy |
| `TotalCommission` | `decimal` | suma `AffiliateConversion.CommissionAmount` w oknie |
| `Status` | `PayoutPeriodStatus` | `Open \| AwaitingPayout \| Paid` |

`AffiliatePayout` (sam przelew, potwierdzony przez admina w UI):

| Pole | Typ | Uwagi |
|---|---|---|
| `Id` | `Guid` | |
| `AffiliateId` | `Guid` | |
| `Amount` | `decimal` | |
| `Currency` | `string` | |
| `CoveredPeriodIds` | `Guid[]` | które okresy ta wypłata rozlicza |
| `MarkedPaidAt` | `DateTimeOffset` | |
| `MarkedPaidByAdminId` | `Guid` | **kto** potwierdził — wymóg audytu z briefu ("ja tez chce miec w adminie ze mu zaplacilem") |
| `PaymentReference` | `string?` | nr przelewu / referencja PayPal, opcjonalne pole dowodowe |
| `Note` | `string?` | |

### 3.6 `AffiliateMessageThread` i `AffiliateMessage`

Prosty, wątkowany system wiadomości (patrz §8) — nie pełny helpdesk, jeden wątek "otwarty" na
afilianta wystarcza na start, ale model wspiera wiele wątków (np. "Pytanie o wypłatę" osobno od
"Pytanie o kod").

| `AffiliateMessageThread` | Typ |
|---|---|
| `Id` | `Guid` |
| `AffiliateId` | `Guid` |
| `Subject` | `string` |
| `Status` | `Open \| Closed` |
| `LastMessageAt` | `DateTimeOffset` |
| `UnreadByAdmin` | `bool` | do badge'a w adminie |
| `UnreadByAffiliate` | `bool` | do badge'a w panelu partnera |

| `AffiliateMessage` | Typ |
|---|---|
| `Id` | `Guid` |
| `ThreadId` | `Guid` |
| `SenderType` | `Affiliate \| Admin` |
| `SenderAdminId` | `Guid?` | jeśli od admina |
| `Body` | `string` | max np. 5000 znaków, sanityzowany jako plain text (patrz §12.4 — nie renderować jako HTML) |
| `SentAt` | `DateTimeOffset` |

### 3.7 `AffiliateProgramSettings` (globalne ustawienia — jedna wiersz-config w adminie)

| Pole | Typ | Uwagi |
|---|---|---|
| `DefaultCommissionPercent` | `decimal` | |
| `DefaultCommissionWindowMonths` | `int?` | `null` = dożywotnio (mirror `PromoDurationType.Forever`), liczba = tylko N pierwszych miesięcy subskrypcji klienta liczy się do prowizji |
| `DefaultMaxCodesPerAffiliate` | `int` | domyślnie 10 |
| `PayoutDayOfMonth` | `int` | domyślnie 20 (z briefu: "wyplacic tego 20 dnia miesiaca") |
| `MinimumPayoutAmount` | `decimal?` | próg minimalny do wypłaty (decyzja biznesowa, patrz §16) |

Tabela ustawień per-kraj (`AffiliateCountryDiscountRule` — "x miesiecy to x znizki" z briefu):
osobna tabela `CountryCode → DiscountPercent, DurationMonths`, którą kod przypisany do danego kraju
automatycznie stosuje przy checkout (integruje się z istniejącym mechanizmem `PromoCode`, patrz
§13.2).

---

## 4. Uwierzytelnianie afiliantów

Wzorowane 1:1 na tym, jak `AdminEndpoints` izoluje `platform_admin` — trzeci token scope
`affiliate`, całkowicie rozłączny z `organization_id`-scoped tokenami klientów i z `platform_admin`.

### 4.1 JWT

- Nowy claim `token_scope=affiliate` + `affiliate_id` (analogicznie do `PlatformAdminClaims`).
- Osobna para access/refresh token, **osobny `RefreshToken`-podobny rekord** w nowej tabeli
  `AffiliateRefreshToken` (nie reużywać tabeli `RefreshToken` klientów, żeby jedna literówka w
  autoryzacji nigdy nie mogła pomylić afilianta z użytkownikiem organizacji).
- Middleware/endpoint filter analogiczny do tego w `AdminEndpoints`, wymuszający ten scope na
  **każdej** trasie `/api/partner/*` poza `/login`, `/register`, `/password-reset/*`.
- Krótszy czas życia access tokena niż standard (np. 15 min) + refresh rotation — afiliant nie
  potrzebuje długich sesji jak dashboard operacyjny.

### 4.2 Rejestracja

- `POST /api/partner/register`: email, hasło, imię, nazwisko, (opcjonalnie kraj).
- Konto ląduje w statusie `PendingApproval`. **Zgodnie z briefem** ("w przyszłości można rozważyć
  proces akceptacji") — rekomendacja: **wdrożyć akceptację od razu w MVP**, nie "w przyszłości".
  Uzasadnienie bezpieczeństwa: bez ręcznej akceptacji każdy może zarejestrować się jako afiliant i
  natychmiast generować linki promujące Tenebit pod swoją marką/domeną — to otwiera furtkę na
  spam, phishing-adjacent linki ("promocja Tenebit" prowadząca gdzieś indziej) i fraud (patrz §12.7
  self-referral). Koszt wdrożenia akceptacji jest zerowy (jeden extra status + jeden przycisk w
  adminie), a koszt jej braku jest wysoki — więc to nie jest "nice to have na później".
- Weryfikacja e-mail wymagana przed `Active` (reużyj `EmailVerificationToken` — albo osobna tabela
  `AffiliateEmailVerificationToken` z tego samego wzorca, dla pełnej izolacji).
- Rate limit: nowa polityka `affiliate-register` w `Program.cs`, analogiczna do `auth-register`.

### 4.3 Logowanie i reset hasła

- `POST /api/partner/login` (email + hasło), rate limit `affiliate-login` (agresywniejszy niż
  zwykły `auth-login`, bo to nowa, mniej obserwowana powierzchnia — proponuję okno podobne do
  `admin-login`: 10/min per IP+email).
- `POST /api/partner/password-reset/request` + `/confirm` — reużyj `PasswordResetToken`-owy wzorzec
  (osobna tabela `AffiliatePasswordResetToken`), te same zasady: token jednorazowy, hashowany w
  bazie (`TokenHasher`), krótki TTL (np. 1h), brak potwierdzenia w odpowiedzi czy e-mail istnieje
  (odpowiedź zawsze 200, żeby nie enumerować kont — ten sam wzorzec co pewnie już jest w
  `AuthService` dla klientów).
- 2FA (TOTP): **nie w MVP** dla afiliantów zwykłych (obniżyłoby konwersję rejestracji, a stawka
  ryzyka niższa niż konto admina — afiliant nie ma dostępu do danych klientów). Zostawić `TotpService`
  gotowy do włączenia w Fazie 2, gdy skala prowizji urośnie (patrz §15).

### 4.4 Dane kontaktowe

Imię, nazwisko, e-mail — wymagane przy rejestracji (jak w briefie: "Damian Kowalski"). Numer
telefonu opcjonalny (do kontaktu w razie problemów z wypłatą).

---

## 5. Kody i linki afiliacyjne

### 5.1 Limit kodów

- Twardy limit **10 aktywnych kodów na afilianta** (konfigurowalny globalnie w
  `AffiliateProgramSettings.DefaultMaxCodesPerAffiliate`, nadpisywalny per-afiliant w
  `Affiliate.MaxActiveCodes` dla wyjątków — np. duży partner z wieloma kanałami/krajami).
- Limit liczy **aktywne** kody (`IsActive = true`) — dezaktywowany kod zwalnia slot, ale nie znika
  z historii (konwersje z niego nadal się liczą, `AffiliateCode.IsActive` nie kasuje danych).
- Egzekwowane server-side w handlerze `POST /api/partner/codes` — **nie tylko w UI**, bo to jest
  reguła antyspamowa ("nie chcemy aby zapchał stronę"), więc musi być odporna na ominięcie przez
  bezpośrednie wołanie API.

### 5.2 Generowanie — automatyczne vs własne

- Domyślnie system proponuje kod: `{ZNORMALIZOWANE_NAZWISKO}{losowy_sufiks}` (np. `KOWALSKI-4F2A`),
  z auto-retry przy koincydencji.
- Afiliant może wpisać własny kod (np. `DAMIAN20`) — walidowany:
  - format: `^[A-Z0-9][A-Z0-9-]{2,19}$` (3–20 znaków, wielkie litery/cyfry/myślnik, bez spacji,
    bez znaków specjalnych — zapobiega XSS-w-URL i confusables/homoglyph tricks),
  - blacklist słów zastrzeżonych (`ADMIN`, `TENEBIT`, nazwy planów, wulgaryzmy — prosta lista),
  - sprawdzenie dostępności: `GET /api/partner/codes/availability?code=...` **przed** submitem
    (UX), ale ostateczna walidacja i tak w `POST` (nigdy nie ufaj samemu sprawdzeniu dostępności
    jako gwarancji — między `GET` a `POST` może wejść inny request).
- **Unikalność wymuszona na poziomie bazy** (`UNIQUE INDEX`), nie tylko `SELECT`-then-`INSERT` w
  aplikacji — to jedyny sposób na wykluczenie race condition przy dwóch równoczesnych żądaniach o
  ten sam kod. Insert w transakcji, `catch` na naruszenie unique constraint → zwróć
  `409 Conflict` z komunikatem "kod zajęty", niech UI zaproponuje wariant.

### 5.3 Kody per-kraj

- `AffiliateCode.CountryCode` (nullable) — afiliant może utworzyć kod przypisany do konkretnego
  kraju (np. promuje po niemiecku, kod `DAMIAN-DE`), co automatycznie stosuje regułę zniżki z
  `AffiliateCountryDiscountRule` dla tego kraju przy checkout.
  Kod bez `CountryCode` = uniwersalny, stosuje zniżkę domyślną (jeśli jakaś jest skonfigurowana)
  albo żadną — afiliacyjny kod **nie musi** dawać zniżki klientowi; to jest osobny przełącznik od
  prowizji dla afilianta (można mieć program czysto prowizyjny bez rabatu, albo z rabatem — decyzja
  biznesowa w `AffiliateProgramSettings`, patrz §16).
- Kraj kodu liczy się przy walidacji w checkout: jeśli kod ma `CountryCode = "DE"`, a adres
  rozliczeniowy klienta (z Paddle) to Francja — kod nadal jest ważny (prowizja się liczy), ale
  **rabat per-kraj nie jest stosowany** (bo dotyczy tylko zadeklarowanego kraju promocji). To
  zapobiega arbitrażowi rabatowemu między krajami.

### 5.4 Kolizja z `PromoCode`

Przed zapisem nowego `AffiliateCode` sprawdzić też brak kolizji z tabelą `PromoCode.Code` (i
odwrotnie — `AdminCreatePromoCodeRequest` powinien też sprawdzać `AffiliateCode`). To dwa oddzielne
mechanizmy zniżek (marketingowe promo vs afiliacyjne), ale współdzielą **jedną globalną
przestrzeń nazw kodów** widoczną klientowi w polu "kod promocyjny" przy checkout — więc muszą być
wzajemnie unikalne, inaczej dwa różne rabaty/tory rozliczeniowe mogłyby się nałożyć na ten sam string.

---

## 6. Tracking konwersji i naliczanie prowizji

### 6.1 Podstawa prowizji: `NetAmount`, nie `GrossAmount`

Zgodnie z `paddle_plan.md`, Paddle jest MoR i pobiera 5% + ~0,46 € **zanim** Tenebit zobaczy
pieniądze. Prowizja afiliacyjna powinna być liczona **od kwoty netto, którą Tenebit realnie
otrzymuje** (`NetAmount`), nie od ceny brutto klienta — inaczej przy niskich planach (Starter: 11,95 €
brutto → 10,94 € netto po prowizji Paddle) prowizja afiliacyjna licząca się od brutto mogłaby zjeść
większą część marży Tenebit niż zakładano. To musi być jawnie ustalone w `AffiliateProgramSettings`
(pole `CommissionBase: Gross | Net` — rekomendacja: `Net`, ale to decyzja biznesowa, patrz §16).

### 6.2 Atrybucja kliknięcia → zakupu

1. `GET /r/{code}` (endpoint publiczny, nieautoryzowany, poza `/api` — czysty redirect jak
   krótki link):
   - waliduje istnienie i aktywność kodu,
   - zapisuje `AffiliateClick` (IP/UA zhashowane, patrz §3.3),
   - ustawia **podpisany** cookie (`HMAC`, nie zwykły plaintext) `tnb_aff=<AttributionToken>`,
     `SameSite=Lax`, `Secure`, `HttpOnly`, TTL 30 dni (konfigurowalne — standardowe okno
     atrybucji last-click w branży to 30–90 dni; rekomendacja 30 dni na start, patrz §16),
   - `302` do `https://teneb.it/?ref={code}` (albo bezpośrednio do `/register` / `/pricing` —
     do ustalenia z marketingiem, poza zakresem tego dokumentu technicznego).
2. Przy checkout (Paddle), frontend odczytuje `tnb_aff` cookie i przekazuje kod afiliacyjny jako
   `custom_data.affiliate_code` w żądaniu utworzenia transakcji Paddle (analogicznie do tego, jak
   już zapewne `custom_data` niesie `organization_id` — patrz `PaddlePaymentGateway`).
3. Webhook `transaction.completed` / `subscription.created`: handler czyta
   `custom_data.affiliate_code`, **weryfikuje ponownie po stronie serwera** że kod istnieje i jest
   aktywny (nigdy nie ufać samemu polu z frontend/webhooka bez re-walidacji przeciw bazie), tworzy
   `AffiliateConversion` z `PaddleTransactionId` jako klucz idempotencji (`UNIQUE INDEX`, insert w
   `try/catch` na duplicate — identyczny wzorzec do `ProcessedPaddleEvent`).
4. Odnowienia (`subscription.updated`/kolejne cykle rozliczeniowe): jeśli
   `IsWithinCommissionWindow` (subskrypcja klienta jest wciąż w oknie `DefaultCommissionWindowMonths`
   liczonym od **pierwszego** zakupu tym kodem), tworzony jest kolejny `AffiliateConversion` typu
   `Renewal` za każdy cykl.

### 6.3 Co widzi afiliant (zero PII)

Panel afilianta pokazuje listę: `Data | Kwota prowizji | Kod użyty | Status (naliczono/wypłacono)`.
**Nigdy**: nazwa organizacji, e-mail klienta, adres, NIP. Backend musi to wymusić na poziomie DTO
(osobny `AffiliateConversionSummaryDto` bez `OrganizationId`/danych klienta w ogóle), nie tylko
ukrycia w UI — bo DTO zwrócony przez API jest tym, co faktycznie trzeba chronić (ktoś z devtools nie
może zobaczyć więcej niż UI pokazuje).

### 6.4 Wykrywanie nadużyć

- **Self-referral**: jeśli `OrganizationSubscription` powiązana z kontem, którego adres e-mail
  (lub domena firmowa, jeśli to możliwe do ustalenia) pokrywa się z e-mailem afilianta —
  flagować konwersję jako `RequiresReview` zamiast automatycznie zatwierdzać do wypłaty. Admin
  decyduje ręcznie.
- **Nadmierne kliknięcia z jednego IP** w krótkim czasie → nie liczą się do `ClickCount`
  (rate-limit/dedupe po `IpHash` + `AffiliateCodeId` w oknie np. 1 min), zapobiega sztucznemu
  nabijaniu statystyk (nie ma to wpływu na prowizję — ta zależy od zakupu, nie kliknięcia — ale
  wpływa na wiarygodność rankingu w adminie).

---

## 7. Wypłaty (payouty)

Proces **ręczny z potwierdzeniem w UI**, zgodnie z briefem — nie automatyczny przelew (Tenebit nie
integruje się z bramką wypłat na start, to osobny, znacznie większy projekt regulacyjny — PSD2,
KYC afiliantów itd., poza zakresem MVP, patrz §16).

### 7.1 Cykl

1. Cron/scheduled job (1. dnia miesiąca) zamyka `AffiliatePayoutPeriod` za poprzedni miesiąc dla
   każdego afilianta z niezerową sumą prowizji: sumuje `AffiliateConversion.CommissionAmount` z
   okresu, status → `AwaitingPayout`.
2. Admin widzi w `/admin/affiliates` listę "do wypłaty do dnia 20" z kwotami.
3. Admin robi przelew **poza systemem** (bank/PayPal — ręcznie), wraca do UI i klika "Oznacz jako
   zapłacone" na danym afiliancie/okresie:
   - modal z potwierdzeniem: kwota (pre-wypełniona, edytowalna — na wypadek częściowej wypłaty czy
     zaokrągleń), referencja przelewu (opcjonalna), notatka,
   - **wymaga jawnego potwierdzenia** (checkbox "Potwierdzam, że przelew został wykonany"), nie
     jednego kliknięcia przycisku — to nieodwracalna deklaracja finansowa, UX musi to sygnalizować
     wagą (wzorzec podobny do innych `Admin*ActionDialog` z krokiem potwierdzenia, patrz
     `AdminActionDialog.tsx` już w repo),
   - zapisuje `AffiliatePayout` + wpis w `AdminAuditLog` (kto, kiedy, ile, dla kogo).
4. Afiliant w swoim panelu widzi: `Należne łącznie | Wypłacone łącznie | Do wypłaty | Najbliższa
   data rozliczenia (dzień 20)` + historię wypłat ze statusami.

### 7.2 Zaległości i korekty

Jeśli okres zamknięty, a admin nie zdążył wypłacić do 20 — kwota **nie przepada**, zostaje
`AwaitingPayout` i sumuje się z kolejnym okresem przy najbliższej wypłacie (`AffiliatePayout.
CoveredPeriodIds` może objąć kilka okresów naraz). Zwroty/chargebacki z Paddle
(`subscription.canceled` z refundem, `transaction.refunded`) tworzą **ujemny** `AffiliateConversion`
kompensujący wcześniejszą prowizję — jeśli już wypłacony, saldo przechodzi na "do potrącenia z
następnej wypłaty" (nie ma cofania przelewu bankowego).

---

## 8. Komunikacja afiliant ↔ admin

Prosty, wbudowany system wiadomości (nie e-mail, nie zewnętrzny helpdesk) — z briefu: "zrob jakas
komunikacje ze mną jakis formularz, moze ten formularz przychodzic do mnie na admin... i odwrotnie".

- Afiliant: strona "Wiadomości" w panelu — formularz nowego wątku (temat + treść) + widok
  istniejących wątków z odpowiedziami admina, badge nieprzeczytanych.
- Admin: **widoczne od góry** dashboardu admina (zgodnie z briefem: "bedzie od gory informacje jakie
  przyszly") — komponent podobny do istniejącego `AdminDashboardPage`, sekcja
  "Wiadomości od partnerów" z licznikiem nieprzeczytanych, link do `/admin/affiliate-messages`.
- Powiadomienia e-mail: nowa wiadomość od afilianta → e-mail do admina (adres z configu, ten sam
  mechanizm mailera co reszta systemu); odpowiedź admina → e-mail do afilianta.
- Treść wiadomości renderowana **wyłącznie jako plain text** po obu stronach (żadnego HTML/markdown
  renderowania) — zapobiega storowanemu XSS w panelu admina, który ma wysokie uprawnienia (patrz
  §12.4).

---

## 9. Panel admina

Nowa sekcja w istniejącym `AdminShell.tsx`, obok `AdminPromoCodesPage`, `AdminOrganizationsPage` itd.

### 9.1 `AdminAffiliatesPage` — lista

Tabela: `Nazwisko | E-mail | Status | Aktywne kody | Sprzedaże (szt.) | Aktywni klienci (mies.) |
Należne teraz | Wypłacono łącznie | Najbliższa wypłata (20-go)`. Filtrowanie po statusie
(`PendingApproval` na górze — wymaga akcji), sortowanie po "należne teraz" (priorytet operacyjny).

Akcje z listy: Zatwierdź / Zablokuj (z polem powodu, audytowane — wzorzec `AdminSuspendRequest`),
przejście do szczegółów.

### 9.2 `AdminAffiliateDetailPage`

- Dane kontaktowe, status, edycja `CommissionPercent`/`MaxActiveCodes` per-afiliant (nadpisanie
  globalnych ustawień).
- Lista kodów tego afilianta (z licznikiem kliknięć/konwersji per kod).
- Lista konwersji: `Data | Kwota zakupu | Prowizja | Kod | Status` — **tu** admin widzi pełne dane
  (może kliknąć i przejść do `AdminOrganizationDetailPage` danego klienta, bo admin już ma do tego
  prawo w istniejącym systemie).
- Sekcja wypłat: historia + przycisk "Oznacz jako zapłacone" (§7.1).
- Link do wątku wiadomości z tym afiliantem.

### 9.3 `AdminAffiliateSettingsPage`

Globalne `AffiliateProgramSettings`: domyślna prowizja %, okno prowizji (miesiące / dożywotnio),
domyślny limit kodów (max 10), dzień wypłaty (20), reguły rabatu per-kraj (tabela
kraj→%→miesiące), minimalna kwota do wypłaty.

### 9.4 `AdminAffiliateMessagesPage` + widget na dashboardzie

Inbox wątków (jak opisano w §8), plus kompaktowy widget "najnowsze wiadomości od partnerów" na
`AdminDashboardPage`.

---

## 10. Panel afilianta (frontend)

Nowy katalog `Tenebit.Frontend/src/partner/` (analogicznie do `src/admin/`), własny
`PartnerAuthProvider` (osobny od `AuthProvider` klientów — osobny token w `localStorage`/cookie pod
inną nazwą klucza, żeby dwie sesje — klient i afiliant w tej samej przeglądarce — nigdy się nie
mieszały).

Strony:

- `PartnerLoginPage`, `PartnerRegisterPage`, `PartnerResetPasswordPage`.
- `PartnerDashboardPage`: skrót — aktywne kody, suma prowizji w tym miesiącu, najbliższa data
  wypłaty, ranking (jeśli program ma publiczny/prywatny ranking — decyzja w §16), ostatnie
  konwersje.
- `PartnerCodesPage`: lista kodów + tworzenie nowego (z licznikiem "X/10 wykorzystanych slotów"),
  podgląd linku gotowego do skopiowania (`https://teneb.it/r/{code}`), per-kod statystyki
  kliknięć/konwersji.
- `PartnerConversionsPage`: lista zanonimizowanych zdarzeń sprzedażowych (§6.3).
- `PartnerPayoutsPage`: historia wypłat, status bieżącego okresu, najbliższa data.
- `PartnerMessagesPage`: wątki komunikacji z adminem (§8).

UX: prosty, jasny dashboard w stylu istniejącego `AdminDashboardPage`/`AdminTimeSeriesChart` (reużyć
komponenty wykresów z `components/charts` do wizualizacji sprzedaży w czasie) — "zrób to ui/uxowo
fajnie" z briefu oznacza tu spójność z resto systemu, nie nowy design system.

---

## 11. API — pełna specyfikacja endpointów

Prefiks `/api/partner` dla afilianta, `/api/admin/affiliates` dla admina (rozszerzenie istniejącego
`/api/admin`). Każdy endpoint poniżej wymaga odpowiedniego `token_scope`, poza jawnie oznaczonymi
`[public]`.

### 11.1 Publiczne / auth afilianta

| Metoda | Ścieżka | Opis | Rate limit |
|---|---|---|---|
| `GET` | `/r/{code}` | `[public]` redirect + zapis kliknięcia + cookie atrybucji | `public` |
| `POST` | `/api/partner/register` | rejestracja konta afilianta | `affiliate-register` (nowa polityka) |
| `POST` | `/api/partner/login` | logowanie | `affiliate-login` (nowa, agresywna) |
| `POST` | `/api/partner/refresh` | odświeżenie tokena | `affiliate-refresh` |
| `POST` | `/api/partner/logout` | unieważnienie refresh tokena | — |
| `POST` | `/api/partner/password-reset/request` | wyślij link resetu | `affiliate-recovery` |
| `POST` | `/api/partner/password-reset/confirm` | ustaw nowe hasło | `affiliate-recovery` |
| `POST` | `/api/partner/verify-email` | potwierdzenie e-maila po rejestracji | `affiliate-recovery` |

### 11.2 Panel afilianta (wymaga `token_scope=affiliate`)

| Metoda | Ścieżka | Opis |
|---|---|---|
| `GET` | `/api/partner/me` | profil, status konta |
| `PATCH` | `/api/partner/me` | edycja danych kontaktowych |
| `GET` | `/api/partner/codes` | lista własnych kodów |
| `GET` | `/api/partner/codes/availability?code=` | sprawdzenie dostępności (pre-check, nieostateczny) |
| `POST` | `/api/partner/codes` | utworzenie kodu (egzekwuje limit 10, unikalność) |
| `PATCH` | `/api/partner/codes/{id}` | dezaktywacja/reaktywacja własnego kodu |
| `GET` | `/api/partner/dashboard` | zagregowane statystyki (suma prowizji, klik/konwersja rate) |
| `GET` | `/api/partner/conversions` | lista zanonimizowanych konwersji (paginacja) |
| `GET` | `/api/partner/payouts` | historia wypłat + status bieżącego okresu |
| `GET` | `/api/partner/messages` | lista wątków |
| `POST` | `/api/partner/messages` | nowy wątek |
| `POST` | `/api/partner/messages/{threadId}/reply` | odpowiedź w wątku |

### 11.3 Admin (rozszerzenie `/api/admin`, wymaga `PlatformAdmin` policy — analogicznie do reszty `AdminEndpoints`)

| Metoda | Ścieżka | Opis |
|---|---|---|
| `GET` | `/api/admin/affiliates` | lista + filtrowanie/sortowanie (§9.1) |
| `GET` | `/api/admin/affiliates/{id}` | szczegóły (§9.2) |
| `POST` | `/api/admin/affiliates/{id}/approve` | zatwierdzenie konta (TOTP step-up, jak inne akcje adminowe) |
| `POST` | `/api/admin/affiliates/{id}/block` | zablokowanie + powód (TOTP step-up) |
| `PATCH` | `/api/admin/affiliates/{id}/commission` | nadpisanie % prowizji / limitu kodów dla afilianta |
| `GET` | `/api/admin/affiliates/{id}/conversions` | pełna lista konwersji z danymi klienta |
| `GET` | `/api/admin/affiliates/{id}/payouts` | historia wypłat |
| `POST` | `/api/admin/affiliates/{id}/payouts/mark-paid` | potwierdzenie wypłaty (TOTP step-up, §7.1) |
| `GET`/`PUT` | `/api/admin/affiliate-settings` | globalne ustawienia (§3.7) |
| `GET`/`PUT` | `/api/admin/affiliate-settings/country-rules` | reguły rabatu per-kraj |
| `GET` | `/api/admin/affiliate-messages` | inbox wątków (wszyscy afilianci) |
| `POST` | `/api/admin/affiliate-messages/{threadId}/reply` | odpowiedź admina |
| `GET` | `/api/admin/affiliate-dashboard-summary` | widget na `AdminDashboardPage` (liczba nieprzeczytanych, oczekujące zatwierdzenia, oczekujące wypłaty) |

Wszystkie mutujące endpointy adminowe **wymagają świeżego kodu TOTP** w request body — dokładnie ten
sam wzorzec co `AdminSuspendRequest`/`AdminUserActionRequest` już w repo (`TotpCode` pole,
weryfikowane przy każdym wywołaniu, nie tylko przy logowaniu).

---

## 12. Bezpieczeństwo — analiza zagrożeń i mitygacje

Sekcja centralna zgodnie z priorytetem z briefu ("bezpieczenstwo i jeszcze raz bezpieczenstwo").
Format: zagrożenie → mitygacja → gdzie w projekcie jest wymuszone.

### 12.1 Izolacja tożsamości

**Zagrożenie**: pomylenie/eskalacja uprawnień między kontem afilianta, kontem klienta i platform
adminem (np. token afilianta zaakceptowany na endpointzie tenant-scoped, albo odwrotnie).
**Mitygacja**: trzeci, w pełni rozłączny `token_scope=affiliate`. Każda grupa endpointów (`/api/*`
tenant, `/api/admin/*`, `/api/partner/*`) ma endpoint filter, który **odrzuca** żądanie, jeśli token
niesie inny scope niż wymagany — analogicznie do komentarza w `AdminEndpoints.cs`
("*TenebitEndpoints explicitly rejects this scope on every tenant route*"). Trzeba rozszerzyć
`TenebitEndpoints`/tenant middleware o jawne odrzucenie `token_scope=affiliate` (nie tylko dodanie
nowego scope'u, ale **explicit deny-list** na istniejących trasach — domyślne "milczące ignorowanie
nieznanego scope'u" to błąd projektowy, trzeba fail-closed).
**Osobne tabele refresh tokenów** (`AffiliateRefreshToken` ≠ `RefreshToken`) z tego samego powodu —
zero współdzielonej przestrzeni identyfikatorów sesji.

### 12.2 Wyciek PII klienta do afilianta

**Zagrożenie**: afiliant widzi e-mail/nazwę/dane firmowe kupującego przez API lub UI.
**Mitygacja**: osobny DTO (`AffiliateConversionSummaryDto`) zwracany z `/api/partner/conversions`,
fizycznie nieposiadający pól z danymi klienta (nie "puste pole", tylko pole, którego typ w ogóle nie
istnieje w tym DTO — więc nie da się go przez pomyłkę zserializować). `OrganizationId` **nigdy** nie
trafia do handlera `/api/partner/*`. Code review / test kontraktowy: test integracyjny, który
weryfikuje, że odpowiedź `/api/partner/conversions` nie zawiera żadnego znanego pola z danymi
klienta (regresja na wypadek przyszłej zmiany).

### 12.3 Enumeracja kodów / kont

**Zagrożenie**: atakujący sprawdza masowo `GET /codes/availability?code=X` żeby zmapować istniejące
kody (przydatne do przejęcia cudzej prowizji przez "podszycie się" pod popularny kod w innym
kontekście) albo enumeruje e-maile przez różnicę w odpowiedzi rejestracji/resetu hasła.
**Mitygacja**: `availability` endpoint rate-limitowany per IP+sesja afilianta (nie publiczny —
wymaga zalogowania, więc automatycznie ograniczony do własnego konta, nie całego internetu).
Rejestracja/reset hasła: odpowiedź zawsze identyczna niezależnie od tego, czy e-mail istnieje
(200 OK + komunikat "jeśli konto istnieje, wysłaliśmy e-mail"), zero różnicy w czasie odpowiedzi
możliwej do zmierzenia (constant-time gdzie to praktyczne, albo minimalny sztuczny delay).

### 12.4 Wiadomości jako wektor XSS/injection

**Zagrożenie**: treść wiadomości afilianta (pole wolnotekstowe) wyświetlana w panelu admina z
wysokimi uprawnieniami — klasyczny stored XSS, jeśli renderowana jako HTML.
**Mitygacja**: `Body` renderowany **wyłącznie** jako tekst (React domyślnie escapuje przy zwykłym
`{body}`, nie używać `dangerouslySetInnerHTML` nigdzie w tym module — twarda zasada code review),
limit długości (5000 znaków) i walidacja `[ValidatedRequest]` po stronie backendu, jak reszta API.

### 12.5 Fałszowanie atrybucji (cookie afiliacyjne)

**Zagrożenie**: klient/afiliant modyfikuje cookie `tnb_aff`, żeby przypisać sobie cudzą sprzedaż
(fraudulent commission claiming).
**Mitygacja**: cookie zawiera `AttributionToken` (losowy `Guid`, nie sam kod) **podpisany HMAC**
kluczem serwerowym — modyfikacja tokena unieważnia podpis, backend odrzuca. Nawet gdyby ktoś
podejrzał, jaki `AttributionToken` odpowiada czyjemuś kodowi (są losowe, nieprzewidywalne), backend
przy tworzeniu `AffiliateConversion` **ponownie waliduje** kod z `custom_data` Paddle przeciw bazie
(§6.2 pkt 3), więc nawet nieautoryzowana modyfikacja cookie nie tworzy prowizji dla nieistniejącego
lub cudzego kodu bez realnego zakupu.

### 12.6 Dane wypłatowe (IBAN/PayPal) w spoczynku

**Zagrożenie**: `Affiliate.PayoutDetails` to dane finansowe — wyciek bazy ujawnia numery kont
wszystkich partnerów.
**Mitygacja**: szyfrowanie w spoczynku (application-level encryption, nie tylko disk encryption) —
analogicznie do tego, jak system już musi chronić inne wrażliwe pola (sprawdzić, czy istnieje
już `IDataProtector`/podobny mechanizm w `Infrastructure/Services` do reużycia zamiast wdrażać
nowy). Pole nigdy nie wraca w pełni w żadnym API response poza wywołaniem, gdzie afiliant edytuje
własne dane (i tam może zwrócić zamaskowane `****1234`).

### 12.7 Self-referral i fraud prowizyjny

**Zagrożenie**: afiliant zakłada też konto klienckie, "kupuje sam u siebie" własnym kodem, żeby
wyciągnąć prowizję (jeśli prowizja > koszt subskrypcji przy jakimś rabacie, albo po prostu żeby
sztucznie podbić statystyki przed żądaniem wyższej stawki).
**Mitygacja**: opisana w §6.4 — flagowanie `RequiresReview`, gdy e-mail/domena klienta pokrywa się
z afiliantem. Dodatkowo: alert w adminie, gdy jeden afiliant generuje nietypowo wysoki
click-to-conversion rate w krótkim czasie (heurystyka do dostrojenia w Fazie 2, nie blokująca MVP).

### 12.8 Rate limiting — nowe polityki

Rozszerzenie sekcji `AddRateLimiter` w `Program.cs` (obok istniejących `auth-login`, `admin-login`
itd.), współdzielone przez Postgres między replikami jak reszta (zgodnie z notatką w
`SECURITY_HARDENING_MANUAL.md`: *"App-level rate limiting per policy... shared across replicas via
Postgres"*):

- `affiliate-register`: ~30/godz per IP.
- `affiliate-login`: ~10/min per IP+email (jak `admin-login`).
- `affiliate-recovery`: ~10/godz per IP+email.
- `affiliate-code-check`: ~60/min per zalogowany afiliant (na `availability`).
- `affiliate-redirect` (`/r/{code}`): ~120/min per IP — publiczny endpoint, główny cel botów, ale
  musi zostać użyteczny dla realnego ruchu reklamowego (spike przy viralowym poście).

### 12.9 Audyt

Każda akcja admina na module afiliacyjnym (zatwierdzenie, blokada, zmiana prowizji, oznaczenie
wypłaty, odpowiedź na wiadomość) zapisywana w `AdminAuditLog` z pełnym kontekstem (kto, co, kiedy,
jaki afiliant/kwota) — zero nowych, niewidzianych gdzie indziej mechanizmów audytu, pełny reużyj
istniejącej tabeli i wzorca.

### 12.10 CORS / CSRF

`/r/{code}` i inne publiczne endpointy nie przyjmują ciała żądania podatnego na CSRF (to `GET`).
Mutujące endpointy `/api/partner/*` chronione tym samym mechanizmem co reszta API (JWT w
`Authorization` header, nie w cookie do odczytu przez JS — więc klasyczny CSRF nie aplikuje się do
samych wywołań API; cookie `tnb_aff` jest `HttpOnly` i służy wyłącznie do trackingu atrybucji, nigdy
do autoryzacji).

### 12.11 RODO

Dane osobowe afilianta (imię, nazwisko, e-mail, dane wypłatowe) podlegają tym samym zasadom
retencji/eksportu/usunięcia co dane innych użytkowników systemu — do ustalenia z istniejącą polityką
prywatności Tenebit (jeśli jest formalny proces "eksportuj/usuń moje dane" dla klientów, rozszerzyć
go o afiliantów). `AffiliateClick.IpHash`/`UserAgentHash` **nie są** w plaintext właśnie żeby
zminimalizować zakres danych osobowych przechowywanych bez wyraźnej potrzeby.

---

## 13. Integracja z Paddle

### 13.1 Checkout

Frontend przy inicjacji checkoutu (istniejący flow Paddle, patrz `paddle_plan.md` §"cały kod
przepisany na Paddle") dokłada do `custom_data`:

```json
{
  "organization_id": "...",
  "affiliate_code": "DAMIAN20"
}
```

wyłącznie jeśli cookie `tnb_aff` jest obecne i ważne (rozkodowane server-side przy tworzeniu sesji
checkout, nie ufamy samemu frontendowi co do tego, jaki kod przekazać — backend odczytuje
`AttributionToken` z cookie, sam odnajduje powiązany `AffiliateCode.Code`, i to on wstawia wartość
do `custom_data`, frontend jej nie zna/nie kontroluje).

### 13.2 Rabat per-kraj przy checkout

Jeśli `AffiliateCode.CountryCode` i adres rozliczeniowy się zgadzają, backend **przed** utworzeniem
transakcji Paddle aplikuje odpowiedni `Discount` w Paddle (ten sam mechanizm co
`EnsureDiscountAsync` używany dziś przez `PromoCode` — patrz komentarz w `PromoDurationType`:
*"mirrors Paddle's own Discount recurrence model... see PaddlePaymentGateway.EnsureDiscountAsync"*).
Afiliacyjny rabat per-kraj to więc **technicznie ten sam mechanizm Paddle Discount** co promo-kody,
tylko wyzwalany innym warunkiem wejściowym (kod afiliacyjny + kraj, zamiast ręcznie wpisanego kodu
promocyjnego).

### 13.3 Webhooki

Rozszerzenie istniejącego handlera webhooków Paddle (tam, gdzie dziś przetwarzane jest
`subscription.created`/`transaction.completed` dla `OrganizationSubscription`) o dodatkowy krok:
jeśli `custom_data.affiliate_code` obecne → wywołaj `AffiliateConversionService.RecordConversion(...)`
**w tej samej transakcji bazodanowej** co zapis głównego zdarzenia subskrypcji (atomowość — albo oba
zapisy się powiodą, albo żaden, żeby nigdy nie było subskrypcji bez odpowiadającej jej konwersji przy
kodzie afiliacyjnym). Idempotencja przez `PaddleTransactionId` (§3.4) chroni przed podwójnym
naliczeniem przy retry webhooka Paddle — dokładnie ten sam problem, który `ProcessedPaddleEvent` już
rozwiązuje dla głównego flow subskrypcji.

### 13.4 Odnowienia i anulowania

`subscription.updated` z nowym okresem rozliczeniowym → jeśli oryginalna subskrypcja miała
afiliacyjny kod i mieści się w oknie prowizyjnym → nowy `AffiliateConversion(Renewal)`.
`transaction.refunded` / `subscription.canceled` z refundem → ujemna korekta (§7.2).

---

## 14. Migracje bazy danych

Nowe tabele (EF Core migration, `Tenebit.Infrastructure/Data/Migrations`):

1. `affiliates`
2. `affiliate_refresh_tokens`
3. `affiliate_password_reset_tokens`
4. `affiliate_email_verification_tokens`
5. `affiliate_codes` (unique index na `code`)
6. `affiliate_clicks`
7. `affiliate_conversions` (unique index na `paddle_transaction_id`)
8. `affiliate_payout_periods`
9. `affiliate_payouts`
10. `affiliate_message_threads`
11. `affiliate_messages`
12. `affiliate_program_settings` (single-row config table, jak zapewne istniejący wzorzec dla
    innych globalnych ustawień w `Settings` domain — sprawdzić `Tenebit.Domain/Settings` przed
    implementacją, żeby nie duplikować wzorca)
13. `affiliate_country_discount_rules`

Wszystkie z `created_at`/`updated_at` w `UTC` (`DateTimeOffset`, zgodnie z konwencją widoczną w
istniejących encjach), FK z `ON DELETE RESTRICT` (nigdy kaskadowe usuwanie danych finansowych —
afiliant blokowany, nie usuwany, żeby historia konwersji/wypłat zawsze miała właściciela).

---

## 15. Plan wdrożenia etapami

### Faza 1 — MVP trackingu i tożsamości

- Model danych: `Affiliate`, `AffiliateCode`, `AffiliateClick`.
- Auth afilianta pełny (rejestracja z akceptacją admina, logowanie, reset hasła) — §4.
- Generowanie kodów z limitem 10, sprawdzaniem unikalności — §5.
- `GET /r/{code}` tracking + cookie atrybucji — §6.2.
- Panel afilianta: dashboard podstawowy, lista kodów.
- Panel admina: lista afiliantów, zatwierdzanie/blokowanie.

### Faza 2 — Prowizje i wypłaty

- `AffiliateConversion` + integracja webhooków Paddle — §6, §13.
- `AffiliatePayoutPeriod`/`AffiliatePayout` + UI oznaczania wypłat — §7.
- Panel afilianta: konwersje, wypłaty.
- Panel admina: szczegóły afilianta, potwierdzanie wypłat.

### Faza 3 — Komunikacja i ustawienia zaawansowane

- System wiadomości dwukierunkowych — §8.
- `AffiliateProgramSettings` UI (globalne stawki, dzień wypłaty).
- Nadpisania per-afiliant (indywidualna stawka prowizji).

### Faza 4 — Kraje, ranking, twardnienie

- Kody per-kraj + reguły rabatowe — §5.3, §13.2.
- Ranking/leaderboard (jeśli zdecydowane w §16).
- Heurystyki antyfraudowe (self-referral scoring, anomaly detection na click-to-conversion) — §12.7.
- Rozważenie 2FA dla afiliantów o wysokich obrotach.

Rekomendacja: **nie zaczynać implementacji, dopóki §16 nie ma odpowiedzi** — kilka decyzji zmienia
kształt modelu danych (np. `CommissionBase: Gross | Net` wpływa na `AffiliateConversion` od
pierwszego dnia, kosztowna zmiana post-factum przy istniejących rekordach finansowych).

---

## 16. Decyzje do podjęcia przez właściciela produktu

Rzeczy, których ten dokument **nie może** sam rozstrzygnąć — potrzebna decyzja biznesowa/prawna
przed implementacją Fazy 2 (rozliczeniowej):

1. **Stawka prowizji** — % i czy jednolita, czy tiery (np. wyższa stawka po przekroczeniu progu
   sprzedaży)?
2. **Podstawa prowizji** — od kwoty brutto klienta czy netto po prowizji Paddle (rekomendacja w
   §6.1: netto)?
3. **Okno prowizyjne** — czy prowizja należy się tylko od pierwszej sprzedaży, czy też od
   wszystkich odnowień subskrypcji, i jeśli tak — dożywotnio czy przez ile miesięcy?
4. **Minimalna kwota wypłaty** — czy jest próg, poniżej którego kwota czeka do kolejnego miesiąca?
5. **Status prawny afilianta** — czy to ma być traktowane jako umowa B2B (afiliant wystawia
   fakturę Tenebitowi) czy jako "nagroda"/prowizja bez faktury? To wpływa na to, czy panel musi
   zbierać dane firmowe (NIP) i czy wypłata wymaga dokumentu księgowego po stronie afilianta przed
   przelewem.
6. **Ranking publiczny czy prywatny** — z briefu ("monitorowania... rankingu") — czy afilianci
   widzą tylko swoją pozycję, czy też np. top 10 z pseudonimami? (Widoczność cudzych wyników
   afiliantom to osobna decyzja prywatności — domyślna rekomendacja: tylko własna pozycja liczbowa,
   bez ujawniania danych innych partnerów.)
7. **Czy kod afiliacyjny ma domyślnie dawać rabat klientowi**, czy to opcja włączana per-kraj/kod
   (§5.3)? Wpływa na domyślne ustawienie `AffiliateProgramSettings`.
8. **Umowa/regulamin programu afiliacyjnego** — dokument prawny do zaakceptowania przy rejestracji
   (checkbox "akceptuję regulamin programu partnerskiego") — potrzebna treść od prawnika, poza
   zakresem tego planu technicznego, ale pole `AcceptedTermsAt` warto zarezerwować w modelu
   `Affiliate` od razu.
9. **Waluta wypłaty** — zawsze EUR (spójnie z resztą systemu) czy per-kraj afilianta?

---

## 17. Ryzyka

- **Regulacyjne**: wypłacanie prowizji osobom fizycznym w wielu krajach UE może rodzić obowiązki
  podatkowe/raportowe po stronie Tenebit (np. formularze roczne, w zależności od jurysdykcji i
  statusu prawnego afilianta — patrz §16 pkt 5). Rekomendacja: konsultacja księgowa przed
  publicznym uruchomieniem Fazy 2, nie blokuje jednak Fazy 1 (sama rejestracja/tracking bez
  realnych wypłat).
- **Nadużycia programu**: bez akceptacji ręcznej (którą ten plan rekomenduje wdrożyć od razu, wbrew
  "w przyszłości" z briefu — patrz §4.2) każdy mógłby zarejestrować się i publikować linki
  promujące markę Tenebit bez kontroli treści/kanału promocji.
- **Zależność od Paddle `custom_data`**: cały tracking konwersji opiera się na tym, że
  `custom_data` przetrwa cały cykl życia transakcji Paddle (checkout → webhook). Wymaga
  potwierdzenia w dokumentacji Paddle, że `custom_data` jest zwracane niezmienione we wszystkich
  relevantnych zdarzeniach webhook (`transaction.completed`, `subscription.created`,
  `subscription.updated`) — do zweryfikowania technicznie przed implementacją §13, analogicznie do
  tego, jak `paddle_plan.md` weryfikował inne założenia na prawdziwym koncie Paddle.
- **Skalowanie limitu kodów**: limit 10/afiliant to reguła antyspamowa, ale przy dużym, zaufanym
  partnerze (np. influencer z wieloma kanałami krajowymi) może być za niski — stąd nadpisanie
  per-afiliant w modelu (`Affiliate.MaxActiveCodes`) od samego początku, żeby nie trzeba było
  migracji danych później.
