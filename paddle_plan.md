# Plan migracji Stripe → Paddle

Status: **plan gotowy, cały kod przepisany na Paddle, backend buduje się i przechodzi 780/780 testów
(751 jednostkowych + 29 integracyjnych - te ostatnie odpalone naprawdę, na prawdziwym Postgresie w
kontenerze Docker, nie pomijane). Migracja bazy nie tylko napisana, ale i faktycznie wykonana w obie
strony (up→down→up) na przywróconej kopii Twojej prawdziwej bazy `tenebit-db` - patrz §5a, bo tam
znalazłem coś, co zmieniło treść migracji. Nieprzetestowane na żywym koncie Paddle (brak kluczy API) -
do uruchomienia potrzebne są tylko dane z sekcji "Czego potrzebuję rano" na końcu tego pliku.**

## 0. Dlaczego to robimy

Stripe w obecnej integracji jest **wyłącznie pośrednikiem płatności (PSP)** — pobiera pieniądze z
karty, ale to Tenebit (jako sprzedawca) jest prawnym wystawcą faktury i to Tenebit odpowiada za
naliczenie i rozliczenie VAT w każdym kraju klienta. Stripe **nie wystawia faktury we własnym
imieniu** — `billing_address_collection`/`tax_id_collection` w `StripePaymentGateway` tylko zbierają
dane, które i tak trzeba by wystawić samodzielnie (Stripe Invoicing to osobny, nieużywany tu
produkt).

Paddle Billing działa inaczej: Paddle jest **Merchant of Record (MoR)** — to Paddle jest sprzedawcą
wobec klienta końcowego, Paddle nalicza i odprowadza VAT/sales tax we wszystkich jurysdykcjach,
Paddle wystawia i przechowuje prawnie ważną fakturę, Paddle bierze na siebie ryzyko chargebacków i
zgodności podatkowej. Tenebit dostaje przelew net (już po potrąceniu prowizji Paddle) i już nie musi
być stroną podatkową transakcji.

To jest architektonicznie inny model niż Stripe, nie tylko inny dostawca — stąd zakres zmian poniżej.

## 1. Koszty — co do grosza

**Ceny planów** (`SubscriptionPlan.cs`, ustalone 2026-09-06): Starter 11,95 €, Growth 28,95 €,
Business 58,95 €, Enterprise 98,95 € / miesiąc. Free = 0 €, nigdy nie przechodzi przez bramkę płatności.

### 1.1 Prowizja Paddle

Standardowa stawka Paddle (plan "Essentials", brak umowy enterprise): **5% + 0,50 $ za transakcję**,
pobierane automatycznie przed wypłatą — Tenebit nigdy nie widzi kwoty brutto na koncie. W tej opłacie
mieści się: przetwarzanie płatności, cały VAT/sales tax (naliczenie, pobranie, odprowadzenie),
wystawianie faktur, obsługa fraudu i chargebacków, obsługa PayPal/kart/portfeli lokalnych.

Poniżej wyliczenie przy założeniu 5% + ok. 0,46 € stałej opłaty (przelicznik z 0,50 USD; **do
potwierdzenia w Paddle Dashboard → Pricing dla konta rozliczanego w EUR w dniu podłączenia klucza —
Paddle może pokazać stawkę zaokrągloną w EUR, nie przeliczaną z USD na bieżąco**):

| Plan | Cena brutto | Prowizja Paddle (5% + 0,46 €) | Wpływ netto | Efektywna stawka |
|---|---|---|---|---|
| Starter | 12,00 € | 0,60 + 0,46 = 1,06 € | **10,94 €** | 8,83% |
| Growth | 29,00 € | 1,45 + 0,46 = 1,91 € | **27,09 €** | 6,59% |
| Business | 59,00 € | 2,95 + 0,46 = 3,41 € | **55,59 €** | 5,78% |
| Enterprise | 99,00 € | 4,95 + 0,46 = 5,41 € | **93,59 €** | 5,46% |

### 1.2 Dla porównania — obecny koszt Stripe (sam processing, bez VAT/faktur)

Stripe EU (karta europejska): ok. 1,5% + 0,25 € za transakcję. To **nie obejmuje** VAT ani faktury —
ten koszt trzeba by doliczyć osobno (biuro rachunkowe / narzędzie do fakturowania / ryzyko błędnego
naliczenia VAT w innym kraju UE, gdzie progi OSS wymagają rejestracji).

| Plan | Cena brutto | Prowizja Stripe (1,5% + 0,25 €) | Wpływ netto (processing) | Uwaga |
|---|---|---|---|---|
| Starter | 12,00 € | 0,43 € | 11,57 € | + koszt VAT/fakturowania nieujęty |
| Growth | 29,00 € | 0,69 € | 28,32 € | + koszt VAT/fakturowania nieujęty |
| Business | 59,00 € | 1,14 € | 57,87 € | + koszt VAT/fakturowania nieujęty |
| Enterprise | 99,00 € | 1,74 € | 97,27 € | + koszt VAT/fakturowania nieujęty |

**Wniosek do grosza:** Paddle kosztuje nominalnie więcej na transakcję (różnica 1,3–4,5 p.p.), ale w
tej różnicy mieści się cała obsługa VAT/faktur/compliance, którą dziś i tak trzeba by płacić komuś
innemu (księgowość, Stripe Tax jako osobny płatny dodatek, albo ryzyko prawne przy braku rejestracji
VAT w innych krajach UE). Realny koszt "all-in" prawdopodobnie wychodzi podobny lub niższy z Paddle.

### 1.3 Wypłaty (cashflow)

- Stripe: standardowo rolling payout co 2 dni robocze (Europa).
- Paddle: **wypłaty miesięczne** (do ok. 30-45 dni po zakończeniu miesiąca rozliczeniowego), Paddle
  trzyma rezerwę na chargebacki. To pogorszenie cashflow, które trzeba świadomie zaakceptować —
  jednorazowy efekt przy przejściu (pierwszy miesiąc bez wpływów ze sprzedaży, potem stabilizuje się).

### 1.4 Waluta

Ceny planów zostają w EUR (`SubscriptionPlan.Currency = "EUR"`) — Paddle **automatycznie** pokazuje
klientowi checkout w jego lokalnej walucie (adaptive pricing) bez potrzeby zakładania osobnego Price
per waluta, więc funkcjonalność z commita "Default billing currency to EUR..." (wybór waluty
raportowania organizacji) **nie jest powiązana** z walutą płatności i nie wymaga zmian.

## 2. Mapowanie pojęć Stripe → Paddle

| Stripe | Paddle Billing | Uwagi |
|---|---|---|
| `Customer` | `Customer` | 1:1, `POST /customers` |
| `Price` (per plan) | `Price` (per plan) | 1:1, jeden Price ID per płatny plan, konfigurowany ręcznie w Paddle Dashboard tak jak dziś w Stripe Dashboard |
| Checkout Session (hosted redirect URL) | **brak odpowiednika** — Paddle.js overlay/inline checkout | **Zmiana architektoniczna**: nie ma serwerowego URL-a do przekierowania, front musi załadować Paddle.js i otworzyć checkout po stronie klienta |
| Billing Portal Session | Customer Portal Session (`POST /customers/{id}/portal-sessions`) | Bliski odpowiednik, ale węższy zakres (patrz 3.4) |
| "Confirm subscription update" portal (nasz świeży branch audytowy) | **brak odpowiednika** | Zastępujemy własnym dialogiem z podglądem kwoty (mamy go już dla downgrade'u) — patrz 3.3 |
| `subscription_schedules` (osobny obiekt do zaplanowania downgrade'u) | **niepotrzebne** — `PATCH /subscriptions/{id}` z `proration_billing_mode: full_next_billing_period` zwraca `scheduled_change.effective_at` na samej subskrypcji | Duże uproszczenie — całe `StripeScheduleId`, `ScheduleDowngradeAsync`, `ReleaseScheduleAsync` znikają |
| Coupon (`POST /coupons`) | Discount (`POST /discounts`, `restrict_to`) | 1:1 |
| Webhook `Stripe-Signature: t=...,v1=...` | Webhook `Paddle-Signature: ts=...;h1=...` | Ten sam schemat HMAC-SHA256(sekret, `"{ts}.{body}"}"`), inny nagłówek/separator |
| `invoice.id` / `hosted_invoice_url` / `invoice_pdf` | `Transaction` + `GET /transactions/{id}/invoice` (link do PDF, wygasa po 1h) | Lista płatności w adminie: `GET /transactions?customer_id=...` |
| Kwoty w centach (int) | Kwoty jako **string** reprezentujący najmniejszą jednostkę (np. `"1200"` = 12,00 €) | Trzeba parsować jako `long`, nie `int`, i konwertować string→decimal |
| API base URL: zawsze `api.stripe.com` | `api.paddle.com` (produkcja) / `sandbox-api.paddle.com` (sandbox) — **różne klucze API dla każdego środowiska** | `Paddle:Environment` w configu przełącza bazowy URL |

## 3. Zmiany funkcjonalne — jak to ma działać

### 3.1 Pierwsze wykupienie planu (dziś: Stripe Checkout redirect)

**Stripe (stare):** frontend woła `POST /api/subscription/checkout` → backend tworzy Stripe Checkout
Session → zwraca `url` → `window.location.assign(url)` na stronę Stripe.

**Paddle (nowe):** nie ma serwerowego URL-a. Nowy przepływ:
1. Frontend woła `POST /api/subscription/checkout-params` z `{planKey, promoCode}`.
2. Backend: upewnia się, że organizacja ma `PaddleCustomerId` (tworzy przez `POST /customers` jeśli
   brak), sprawdza limity/rolę/promo-kod tak jak dziś, zwraca `{ priceId, customerId, discountId? }`
   (żadnych sekretów — to bezpieczne dane do przekazania do Paddle.js, analogicznie do dzisiejszego
   `client_reference_id`).
3. Frontend (mając już załadowany Paddle.js z publicznym `clientToken`) woła:
   ```js
   Paddle.Checkout.open({
     items: [{ priceId, quantity: 1 }],
     customer: { id: customerId },
     discountId,
     settings: { successUrl: absolutnyUrlDoAplikacji }
   });
   ```
4. Paddle pokazuje nakładkę (overlay) z formularzem płatności, sam obsługuje VAT/fakturę.
5. Po sukcesie: webhook `subscription.created` (patrz 3.5) synchronizuje nasz rekord — dokładnie tak
   jak dziś robi to `customer.subscription.created` ze Stripe.
6. Paddle.js callback `eventCallback` z `checkout.completed` dodatkowo przekierowuje frontend na
   `successUrl` od razu (nie trzeba czekać na webhook, żeby użytkownik zobaczył potwierdzenie —
   identyczna zasada "reload może chwilę pokazywać stary plan", którą już mamy w `PricingPage.tsx`).

### 3.2 Upgrade istniejącej płatnej subskrypcji (dziś: proration + Stripe hosted "confirm" portal)

Świeży branch audytowy (`eadb4b3`, niescommitowane zmiany na wierzchu) świadomie przeniósł potwierdzenie
naliczenia na stronę hostowaną przez Stripe, bo inaczej silent charge w tle wyglądał jak "nic się nie
stało". **Paddle nie ma takiej hostowanej strony potwierdzenia** dla update'u istniejącej subskrypcji
przez API — ale mamy już dokładnie ten sam mechanizm zaimplementowany dla downgrade'u: **własny dialog
z podglądem kwoty przed kliknięciem "Zmień"**. Ujednolicamy: i upgrade, i downgrade idą teraz przez
`POST /subscription/change-plan/preview` (pokazuje dokładną kwotę z Paddle `PATCH
/subscriptions/{id}/preview`) → klient widzi kwotę w naszym własnym dialogu → potwierdza → `POST
/subscription/change-plan` faktycznie nalicza przez `PATCH /subscriptions/{id}` z
`proration_billing_mode: prorated_immediately`. To **usuwa całą koncepcję
`CreatePlanChangePortalSessionAsync`** (i niescommitowaną zmianę w `PricingPage.tsx`, która do niej
prowadziła) na rzecz jednego spójnego dialogu z realną kwotą — moim zdaniem lepsze UX niż
przekierowanie na obcą domenę, i jedyna opcja jaką Paddle w ogóle daje.

### 3.3 Downgrade (dziś: Stripe Subscription Schedule)

`PATCH /subscriptions/{id}` z pełną listą `items` (nowy, tańszy Price) i
`proration_billing_mode: full_next_billing_period` — Paddle **sam** trzyma subskrypcję na starym
planie do końca opłaconego okresu i przełącza automatycznie, zwracając `scheduled_change.effective_at`
w odpowiedzi. Zero dodatkowych obiektów, zero `ScheduleId` do pamiętania. Anulowanie zaplanowanej
zmiany: `PATCH /subscriptions/{id}` z `scheduled_change: null`.

### 3.4 Zarządzanie płatnością / rachunki (dziś: Stripe Billing Portal)

`POST /customers/{customerId}/portal-sessions` (opcjonalnie z `subscription_ids: [...]`) zwraca URL
portalu klienta Paddle — logowanie metody płatności, historia faktur, anulowanie. Sesja jest
jednorazowa (nie cache'ować), więc backend generuje ją na żądanie tak jak dziś
`CreateBillingPortalSessionAsync`.

**Ograniczenie do zaakceptowania:** portal klienta Paddle nie ma dokładnie tych samych dwóch trybów co
Stripe (ogólny portal vs. deep-link "potwierdź tę konkretną zmianę") — nie jest to problem, bo 3.2
i tak przenosi potwierdzanie zmian planu do naszego własnego dialogu.

### 3.5 Webhooks

Nagłówek `Paddle-Signature: ts=<unix>;h1=<hex hmac>` (zamiast `Stripe-Signature: t=...,v1=...`).
Podpis: `HMAC-SHA256(webhookSecret, "{ts}:{raw_body}")` — **uwaga, separator to `:` w Paddle, `.` w
Stripe** (do zweryfikowania co do znaku przy pierwszym prawdziwym webhooku w sandboxie — jeśli
weryfikacja się nie zgadza, to pierwsza rzecz do sprawdzenia). Tolerancja czasu: 5 minut, tak jak dziś.

Nasłuchiwane zdarzenia (odpowiednik dzisiejszych trzech `customer.subscription.*`):
- `subscription.created`
- `subscription.updated` (catch-all: renewals, upgrade, downgrade, resume)
- `subscription.canceled`

`EventId` do idempotencji = `notification_id` z payloadu (`ntf_...`) — dokładny odpowiednik
dzisiejszego `ProcessedStripeEvent.EventId`, tylko nazwa tabeli/klasy zmienia się na
`ProcessedPaddleEvent` / `processed_paddle_events`.

Status subskrypcji Paddle → `SubscriptionStatus`: `active`/`trialing` → `Active`, `past_due` →
`PastDue`, `canceled`/`paused` → `Cancelled`, cokolwiek innego → `Unknown` (te same zasady
bezpieczeństwa co dziś — nieznany status nigdy nie odblokowuje płatnego planu, audyt AUD3-010 zostaje
w mocy).

### 3.6 Kody promocyjne

`PromoCode` (nasza domenowa encja, licznik użyć, wygasanie) **zostaje bez zmian** — to własna logika
marketingowa Tenebit, niezależna od dostawcy. Zmienia się tylko to, co dzieje się "pod spodem" przy
realizacji: zamiast tworzyć Stripe `Coupon`, `PaddlePaymentGateway` tworzy/reużywa Paddle `Discount`
(`POST /discounts`, `type: percentage|flat`, `restrict_to: [priceId]`, `usage_limit` wg potrzeby) i
przekazuje `discount_id` do checkoutu / do `PATCH /subscriptions/{id}`.

## 4. Zmiany w kodzie — plik po pliku

### Backend

| Plik | Zmiana |
|---|---|
| `Tenebit.Application/Abstractions/IPaymentGateway.cs` | Nowy kontrakt: `CreateCustomerAsync` (bez zmian sygnatury), `GetCheckoutParamsAsync` (zastępuje `CreateCheckoutSessionAsync` — zwraca dane dla Paddle.js zamiast URL), `CreateCustomerPortalSessionAsync` (zastępuje `CreateBillingPortalSessionAsync`), **usunięte**: `CreatePlanChangePortalSessionAsync`, `ScheduleDowngradeAsync`+`ReleaseScheduleAsync` (zastąpione parametrem `prorationMode` w `ChangeSubscriptionPlanAsync`/`CancelScheduledPlanChangeAsync` na subskrypcji), `ParseWebhookEvent`/`GetSubscriptionAsync`/`FindSubscriptionByCustomerAsync`/`PreviewPlanChangeAsync`/`ListInvoicesAsync` zostają z tą samą sygnaturą (zmienia się tylko implementacja) |
| `Tenebit.Infrastructure/Services/StripePaymentGateway.cs` | **Usunięty**, zastąpiony przez `PaddlePaymentGateway.cs` (nowy plik) |
| `Tenebit.Infrastructure/Services/PaddlePaymentGateway.cs` | **Nowy.** Implementuje `IPaymentGateway` przez REST `api.paddle.com`/`sandbox-api.paddle.com`, `Authorization: Bearer {ApiKey}`, `Paddle-Signature` weryfikacja |
| `Tenebit.Domain/Subscriptions/OrganizationSubscription.cs` | `StripeCustomerId`→`PaddleCustomerId`, `StripeSubscriptionId`→`PaddleSubscriptionId`, `HasLiveStripeSubscription`→`HasLivePaddleSubscription`, usunięte `StripeScheduleId` (Paddle nie potrzebuje osobnego ID harmonogramu), `ScheduleDowngrade`/`SyncFromStripe`/`ReconcileFromStripe` przemianowane na `...FromPaddle`, logika bez zmian |
| `Tenebit.Domain/Subscriptions/ProcessedStripeEvent.cs` | Przemianowany na `ProcessedPaddleEvent.cs` |
| `Tenebit.Domain/Subscriptions/SubscriptionPlan.cs` | Bez zmian strukturalnych, tylko komentarz o `Paddle:Prices:<planKey>` zamiast `Stripe:Prices:<planKey>` |
| `Tenebit.Application/Abstractions/Repositories/IProcessedStripeEventRepository.cs` | Przemianowany na `IProcessedPaddleEventRepository.cs` |
| `Tenebit.Infrastructure/Repositories/ProcessedStripeEventRepository.cs` | Przemianowany na `ProcessedPaddleEventRepository.cs`, tabela `processed_paddle_events` |
| `Tenebit.Application/Abstractions/Repositories/ISubscriptionRepository.cs` | `GetByStripeCustomerAsync`→`GetByPaddleCustomerAsync`, `ListWithStripeSubscriptionAsync`→`ListWithPaddleSubscriptionAsync`, `ListPendingStripeLinkAsync`→`ListPendingPaddleLinkAsync` |
| `Tenebit.Infrastructure/Repositories/SubscriptionRepository.cs` | Odzwierciedla zmiany nazw kolumn/metod powyżej |
| `Tenebit.Application/Subscriptions/SubscriptionService.cs` | `CreateCheckoutSessionAsync`→`GetCheckoutParamsAsync` (zwraca DTO zamiast URL), `CreatePlanChangePortalSessionAsync` **usunięty**, `ChangePlanAsync` woła teraz jeden gateway-owy `ChangeSubscriptionPlanAsync` z odpowiednim `prorationMode` zamiast osobnych `ScheduleDowngradeAsync`/portal-owych ścieżek, `CreateBillingPortalSessionAsync`→`CreateCustomerPortalSessionAsync`, wiadomości błędów po polsku zamieniają "Stripe" na "Paddle" |
| `Tenebit.Application/Subscriptions/SubscriptionReconciliationService.cs` | Woła nowe nazwy metod repo/gateway, komentarze "Stripe"→"Paddle" |
| `Tenebit.Infrastructure/Services/SubscriptionReconciliationBackgroundService.cs` | Nazwa joba w `PostgresJobLock`: `stripe-subscription-reconciliation`→`paddle-subscription-reconciliation` (nowy klucz = uruchomi się od razu po wdrożeniu, nie czeka na 6h z poprzedniego klucza) |
| `Tenebit.Api/Endpoints/SubscriptionEndpoints.cs` | `/subscription/checkout`→`/subscription/checkout-params` (zwraca JSON, nie string URL), `/subscription/change-plan/portal` **usunięty**, `/subscription/billing-portal` woła nową metodę, webhook endpoint czyta nagłówek `Paddle-Signature` zamiast `Stripe-Signature` |
| `Tenebit.Infrastructure/DependencyInjection.cs` | `AddHttpClient<IPaymentGateway, StripePaymentGateway>`→`AddHttpClient<IPaymentGateway, PaddlePaymentGateway>`, bez sztywnego `BaseAddress` (ustawiany w konstruktorze wg `Paddle:Environment`) |
| `Tenebit.Api/appsettings.json` | Sekcja `Stripe` **usunięta**, nowa sekcja `Paddle` (patrz §6) |
| `Tenebit.Api/ProductionSecurityConfiguration.cs` | Walidacja startowa: `Paddle` musi być albo w pełni wyłączony, albo mieć `ApiKey`+`ClientSideToken`+`WebhookSecret`+co najmniej jeden `Paddle:Prices:<plan>` |
| `Tenebit.Application/Common/ErrorCodeResolver.cs` / `ErrorMessageTranslator.cs` | Wszystkie klucze błędów ze słowem "Stripe" w treści PL → treść z "Paddle", kody `SUBSCRIPTION_*_STRIPE_*` → `SUBSCRIPTION_*_PADDLE_*`, tłumaczenia EN/ES/DE/IT/FR zaktualizowane |
| `Tenebit.Tests/StripePaymentGatewayTests.cs` | Przemianowany na `PaddlePaymentGatewayTests.cs`, przepisany pod nowe request/response Paddle (proration_billing_mode, scheduled_change, Paddle-Signature) |
| `Tenebit.Tests/SubscriptionServiceTests.cs`, `SubscriptionReconciliationServiceTests.cs` | Zaktualizowane pod nowe nazwy metod/pól |
| `Tenebit.Tests/Fakes/InMemoryBusinessRepositories.cs` | `InMemoryProcessedStripeEventRepository`→`InMemoryProcessedPaddleEventRepository`, fake gateway odzwierciedla nowy `IPaymentGateway` |
| `Tenebit.Tests/ArchitectureTests.cs` | Nazwy w listach dozwolonych/oczekiwanych metod repo zaktualizowane |

### Baza danych — migracje EF Core

Patrz §5 poniżej — dwie migracje, pisane ręcznie jako SQL (zgodnie z konwencją już stosowaną w
`PromoCodes.cs` / `ScheduledPlanDowngrade.cs`).

### Frontend

| Plik | Zmiana |
|---|---|
| `index.html` (lub punkt wejścia) | `<script src="https://cdn.paddle.com/paddle/v2/paddle.js">` + inicjalizacja `Paddle.Initialize({ token: <publiczny clientToken>, environment: 'sandbox'\|'production' })` |
| `src/api/endpoints.ts` | `createCheckoutSession`→`getCheckoutParams` (zwraca `{priceId, customerId, discountId}`), `createPlanChangePortalSession` **usunięty**, `createBillingPortalSession` bez zmian nazwy (endpoint URL ten sam) |
| `src/pages/PricingPage.tsx` | Nowy hook `usePaddle()` ładujący/inicjalizujący Paddle.js raz; `confirmUpgrade` dla nowego klienta woła `Paddle.Checkout.open(...)` zamiast `window.location.assign(checkoutUrl)`; upgrade istniejącej subskrypcji **nie** przekierowuje już nigdzie — używa tego samego dialogu z podglądem co downgrade (patrz 3.2), więc kod z niescommitowanego brancha audytowego (`redirectToStripeNotice`, `planChangeConfirmedOnStripe`) wraca do wcześniejszego kształtu (pokazanie `dueNow` z realnego preview) |
| `src/i18n/translations.ts` | Wszystkie klucze `pricing.*` ze słowem "Stripe" (`confirmChangePlanDetail`, `planChangeConfirmedOnStripe`→usunięty, `redirectToStripeNotice`→usunięty) zaktualizowane w 6 językach (pl, en, es, de, it, fr) |

## 5. Migracje bazy danych

Jedna migracja w górę, jedna w dół — bez utraty danych (rename, nie drop+create), zgodnie z tym, że
**nie ma jeszcze żadnych prawdziwych płatności produkcyjnych** (`Stripe:SecretKey` w
`appsettings.json` jest puste — integracja nigdy nie poszła na produkcję z prawdziwymi kluczami, więc
bezpiecznie zakładamy zero rekordów z niepustym `StripeCustomerId` na produkcji; jeśli to założenie
jest błędne, patrz uwaga na końcu tej sekcji).

**Plik:** `Tenebit.Backend/Tenebit.Infrastructure/Data/Migrations/{timestamp}_MigrateToPaddleBilling.cs`

```csharp
[DbContext(typeof(TenebitDbContext))]
[Migration("{timestamp}_MigrateToPaddleBilling")]
public partial class MigrateToPaddleBilling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.subscriptions RENAME COLUMN "StripeCustomerId" TO "PaddleCustomerId";
            ALTER TABLE tenebit.subscriptions RENAME COLUMN "StripeSubscriptionId" TO "PaddleSubscriptionId";
            ALTER INDEX tenebit."IX_subscriptions_StripeCustomerId" RENAME TO "IX_subscriptions_PaddleCustomerId";
            ALTER TABLE tenebit.subscriptions DROP COLUMN IF EXISTS "StripeScheduleId";

            ALTER TABLE tenebit.processed_stripe_events RENAME TO processed_paddle_events;
            ALTER INDEX tenebit."IX_processed_stripe_events_EventId" RENAME TO "IX_processed_paddle_events_EventId";
            ALTER TABLE tenebit.processed_paddle_events RENAME CONSTRAINT "PK_processed_stripe_events" TO "PK_processed_paddle_events";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.subscriptions RENAME COLUMN "PaddleCustomerId" TO "StripeCustomerId";
            ALTER TABLE tenebit.subscriptions RENAME COLUMN "PaddleSubscriptionId" TO "StripeSubscriptionId";
            ALTER INDEX tenebit."IX_subscriptions_PaddleCustomerId" RENAME TO "IX_subscriptions_StripeCustomerId";
            ALTER TABLE tenebit.subscriptions ADD COLUMN "StripeScheduleId" character varying(80);

            ALTER TABLE tenebit.processed_paddle_events RENAME TO processed_stripe_events;
            ALTER INDEX tenebit."IX_processed_paddle_events_EventId" RENAME TO "IX_processed_stripe_events_EventId";
            ALTER TABLE tenebit.processed_stripe_events RENAME CONSTRAINT "PK_processed_paddle_events" TO "PK_processed_stripe_events";
            """);
    }
}
```

`PendingPlanKey` / `PendingPlanEffectiveAt` (z `ScheduledPlanDowngrade`) zostają **bez zmian** — są już
provider-agnostyczne.

## 5a. Zweryfikowane na Twojej prawdziwej bazie (nie założone — sprawdzone)

Ten plan pierwotnie *zakładał* zero rekordów z prawdziwym Stripe ID, bo `appsettings.json` w repo ma
puste klucze. **To założenie było błędne.** Zrobiłem `pg_dump` żywej `tenebit-db` (odczyt, bez zmian),
odtworzyłem kopię w izolowanym kontenerze i sprawdziłem:

- Kontener `tenebit-backend` ma klucze Stripe wstrzyknięte przez zmienne środowiskowe (nie z
  `appsettings.json`) — i jest to **`sk_test_...`, tryb testowy Stripe**, nie produkcyjny. Zero realnych
  pieniędzy/klientów w grze.
- W tabeli `subscriptions` są **3 rekordy**, wszystkie z niepustym `StripeCustomerId`:
  - `Tenebit Demo` — plan `free`, ma tylko `StripeCustomerId` (nieudokończony checkout), brak subskrypcji.
  - `test2` — plan `enterprise`, żywa (testowa) subskrypcja Stripe + zaplanowany downgrade do `business`.
  - `Mienio` — **Twoje własne konto** — plan `enterprise`, żywa (testowa) subskrypcja Stripe + zaplanowany
    downgrade do `starter`.
- Tabela `processed_stripe_events` ma 25 przetworzonych webhooków.

Sam rename (jak w pierwszej wersji tej migracji) zostawiłby te 3 rekordy z Stripe-owym ID pod nazwą
kolumny "Paddle*" — bezużytecznym dla Paddle API (404 przy każdej próbie: portal, reconciliation, anulowanie).
**Naprawiłem migrację** (`Up()` w §5 wyżej) tak, żeby najpierw zerowała każdy rekord z niepustym Stripe ID
z powrotem do czystego planu Free (żadnego prawdziwego dostawcy pod spodem = żadnych uprawnień płatnego
planu), *zanim* wykona rename. Obejmuje to konto `Mienio` i `test2` — po migracji oba wrócą na Free i
trzeba je będzie ręcznie znowu wykupić przez nowy checkout Paddle (zwykła operacja w aplikacji, nie coś
do naprawiania w bazie).

**To już przetestowałem naprawdę**, nie tylko w teorii:
1. `pg_dump` żywej bazy → odtworzenie w izolowanym, jednorazowym kontenerze Postgres.
2. `dotnet ef database update` (Up) na tej kopii → wszystkie 3 rekordy poprawnie wyzerowane do `free`,
   tabela `processed_paddle_events` istnieje, indeksy/klucze przemianowane.
3. `dotnet ef database update` w dół do `ScheduledPlanDowngrade` (Down) → schemat wraca 1:1 do stanu
   sprzed (kolumny `StripeCustomerId`/`StripeSubscriptionId`/`StripeScheduleId` z powrotem; dane w
   `StripeCustomerId`/`StripeSubscriptionId` **nie wracają** — zostały już świadomie wyzerowane w Up(),
   Down() cofa tylko kształt schematu, nie cofa decyzji biznesowej, która już zaszła; jest to
   udokumentowane w komentarzu w kodzie migracji).
4. Migracja w górę ponownie (Up) → działa bez błędów (potwierdzona powtarzalność up→down→up).
5. Pełny zestaw testów (**780/780**, w tym 29 testów integracyjnych na prawdziwym, świeżym Postgresie z
   migracjami odpalonymi od zera przez `Database__AutoCreate`) — zielony.
6. Izolowane kontenery testowe i zrzut bazy usunięte po weryfikacji; **żywa `tenebit-db` nigdy nie została
   zmodyfikowana** (tylko odczyt/`pg_dump`) — liczby `StripeCustomerId`/`processed_stripe_events` przed i
   po są identyczne (3 i 25).

## 6. Konfiguracja

`appsettings.json` — sekcja `Stripe` zastąpiona przez:

```json
"Paddle": {
  "Environment": "sandbox",
  "ApiKey": "",
  "ClientSideToken": "",
  "WebhookSecret": "",
  "Prices": {
    "starter": "",
    "growth": "",
    "business": "",
    "enterprise": ""
  }
}
```

- `Environment`: `"sandbox"` → baza `https://sandbox-api.paddle.com`, `"production"` →
  `https://api.paddle.com`.
- `ApiKey`: serwerowy klucz (Paddle Dashboard → Developer tools → Authentication) — **sekret**, nigdy
  do frontendu.
- `ClientSideToken`: publiczny token do Paddle.js — bezpieczny do ekspozycji w przeglądarce (jak
  Stripe's `pk_...`), wystawiany przez `GET /api/subscription/paddle-config` (nowy, publiczny,
  bez auth, zwraca `{clientToken, environment}` do zainicjowania Paddle.js).
- `WebhookSecret`: sekret notification destination (Paddle Dashboard → Developer tools → Notifications
  → wybrana destynacja) — **jeden na środowisko** (sandbox i produkcja mają osobne).
- `Prices:<planKey>`: Paddle Price ID (`pri_...`) per płatny plan — zakładane ręcznie w Paddle
  Dashboard, analogicznie do dzisiejszych Stripe Price ID.

CSP (`Tenebit.Frontend/security-headers.conf`) — dodane domeny (do zweryfikowania w konsoli
przeglądarki przy pierwszym teście sandboxowym, Paddle nie publikuje zamkniętej listy):
- `script-src`: `https://cdn.paddle.com`
- `connect-src`: `https://cdn.paddle.com https://checkout-service.paddle.com
  https://sandbox-checkout-service.paddle.com`
- `frame-src` (nowa dyrektywa, dziś brak → domyślnie `default-src 'self'`): `'self'
  https://buy.paddle.com https://sandbox-buy.paddle.com https://checkout.paddle.com`

## 7. Kolejność wdrożenia (gdy dostanę klucze)

1. Uzupełnić `Paddle:ApiKey`, `Paddle:ClientSideToken`, `Paddle:WebhookSecret` w konfiguracji sandboxa
   (User Secrets lokalnie / zmienne środowiskowe na serwerze — **nigdy** do `appsettings.json` w
   repo).
2. Założyć w Paddle Sandbox Dashboard 4 produkty/ceny (Starter/Growth/Business/Enterprise) z cenami
   12/29/59/99 EUR miesięcznie, wkleić `pri_...` do `Paddle:Prices:*`.
3. Skonfigurować Notification destination w Paddle Dashboard → URL:
   `https://<domena>/api/subscription/webhook`, zdarzenia: `subscription.created`,
   `subscription.updated`, `subscription.canceled` → skopiować webhook secret.
4. `dotnet ef database update` (uruchamia `MigrateToPaddleBilling`).
5. Zbudować i wdrożyć backend + frontend.
6. Przetestować w sandboxie: nowe wykupienie planu (karta testowa Paddle), upgrade z realnym podglądem
   kwoty, downgrade z odroczonym efektem, anulowanie zaplanowanej zmiany, portal klienta, webhook
   (Paddle Dashboard ma "Simulate" do wysłania testowego zdarzenia bez prawdziwej płatności).
7. Sprawdzić w devtoolsach, czy CSP z §6 nie blokuje niczego (Paddle.js loguje błędy CSP wyraźnie w
   konsoli) — dociągnąć domeny, jeśli trzeba.
8. Dopiero po zielonym przebiegu w sandboxie: powtórzyć 1–3 dla środowiska produkcyjnego Paddle
   (osobne klucze, osobny webhook secret), zmienić `Paddle:Environment` na `production`.

## 8. Rollback

Ponieważ integracja Stripe nigdy nie była użyta produkcyjnie z prawdziwymi kluczami (puste
`appsettings.json`), rollback to zwykłe `git revert` commita(-ów) migracji + `dotnet ef database
update` do poprzedniej migracji (`Down` odwraca rename 1:1, patrz §5). Nie ma równoległego okresu
przejściowego do zaplanowania — to twardy cutover, zgodnie z Twoim "wywracamy do góry nogami".

## 9. Co jest już zrobione vs. co czeka na Ciebie

**Zrobione w tej sesji (kod wdrożony, kompiluje się, testy zaktualizowane i faktycznie odpalone):**
- Wszystkie zmiany z §4 (backend + frontend + testy).
- Migracja bazy danych z §5, **naprawiona po weryfikacji na kopii Twojej prawdziwej bazy** (§5a) i
  przetestowana w obie strony (up→down→up) na tej kopii, nie tylko przeczytana wzrokiem.
- Szkielet `PaddlePaymentGateway` ze wszystkimi metodami z nowego `IPaymentGateway`, gotowy do
  uderzenia w prawdziwe (sandboxowe) API Paddle.
- Pełny zestaw testów backendu: **780/780 zielonych** (751 jednostkowych + 29 integracyjnych, te drugie
  odpalone naprawdę na świeżym Postgresie w kontenerze Docker z migracjami wykonanymi od zera).

**Ograniczenie tej sesji, o którym powinieneś wiedzieć:** to środowisko nie ma zainstalowanego
Node.js/npm, więc frontend (`PricingPage.tsx`, `paddleClient.ts`, `endpoints.ts`, `translations.ts`)
został tylko przejrzany ręcznie pod kątem typów — **nie przeszedł przez `tsc`/`vite build`**. Zanim
uznasz frontend za gotowy, uruchom `npm run build` w `Tenebit.Frontend` i napraw, co wypluje (najbardziej
podejrzane miejsce: `src/api/paddleClient.ts`, nowy plik z augmentacją `Window.Paddle`). Backend
natomiast przeszedł pełną weryfikację opisaną wyżej.

**Czeka na Ciebie rano — bez tego nic nie połączy się z prawdziwym Paddle:**
- Konto Paddle (sandbox wystarczy na start) + `ApiKey` + `ClientSideToken` + `WebhookSecret`.
- 4 Price ID (`pri_...`) dla Starter/Growth/Business/Enterprise.
- Potwierdzenie w Paddle Dashboard dokładnej stawki prowizji dla Twojego konta (§1.1 to szacunek
  ze standardowego cennika — realna stawka bywa negocjowana indywidualnie od pewnego wolumenu).
- Decyzja: sandbox najpierw, czy od razu produkcja (rekomendacja: sandbox, patrz §7).
- Po wdrożeniu: konta `Mienio` i `test2` wrócą na Free (§5a) — jeśli chcesz je z powrotem na płatnym
  planie do dalszych testów, to zwykłe ponowne wykupienie planu w aplikacji przez nowy checkout Paddle.
