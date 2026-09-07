# Plan: Program afiliacyjny TENEB.IT PARTNERS

Scope: affiliate-program
Depth: tree 2
Mode: orchestrated-planned, solo-executed (jedna, ciągła sesja implementacyjna zamiast wielu
równoległych subagentów — kod backend/frontend musi trzymać się jednego, spójnego zestawu
konwencji z resztą repo; PLAN.md pełni tu rolę wymaganego podziału na zadania, `gates/*` są
prowadzone i weryfikowane sekwencyjnie przez tego samego agenta, budując realny, testowany kod
krok po kroku zamiast planować wszystko na raz).

Dokument źródłowy: `spec/AFFILIATE_PROGRAM_PLAN.md` (pełna architektura). Ten plik dzieli go na
zadania i dopisuje decyzje właściciela produktu przekazane wprost w briefie z 2026-09-06.

## Kontrakt

- **Interfejsy**: `token_scope=affiliate` (JWT), prefiks `/api/partner/*` dla afilianta,
  `/api/admin/affiliates/*` + `/api/admin/affiliate-settings` dla admina, `GET /r/{code}` publiczny
  redirect. DTO `/api/partner/conversions` fizycznie bez pól PII klienta.
- **Własność ścieżek** (bez nakładania się leafów):
  - 1.1: `Tenebit.Backend/Tenebit.Domain/Affiliates/**`
  - 1.2: `Tenebit.Backend/Tenebit.Infrastructure/Data/**` (DbContext + migracja + repozytoria),
    `Tenebit.Backend/Tenebit.Application/Abstractions/*Affiliate*Repository*.cs`
  - 1.3: `Tenebit.Backend/Tenebit.Api/Auth/Affiliate*.cs`, `Program.cs` (rate limiter + DI + deny-list),
    `Tenebit.Backend/Tenebit.Application/Affiliates/AffiliateAuthService.cs`,
    `Tenebit.Backend/Tenebit.Api/Endpoints/PartnerEndpoints.cs` (sekcja auth)
  - 1.4: `AffiliateCodeService.cs`, `PartnerEndpoints.cs` (sekcja codes)
  - 1.5: `RedirectEndpoints.cs`, `AffiliateTrackingService.cs`
  - 1.6: pole `RevolutTag` w `Affiliate` (część 1.1) + endpoint profilu w `PartnerEndpoints.cs`
  - 1.7: `Tenebit.Frontend/src/partner/affiliateTermsContent.ts` + wpięcie w rejestrację
  - 1.8: `AffiliateMessageService.cs`, sekcja messages w `PartnerEndpoints.cs`/`AdminEndpoints.cs`
  - 1.9: `AffiliateAdminService.cs`, sekcja affiliates w `AdminEndpoints.cs`,
    `Tenebit.Frontend/src/admin/AdminAffiliates*.tsx`
  - 1.10: `AffiliateSettingsAdminService.cs` + `AdminAffiliateSettingsPage.tsx`
  - 1.11: `Tenebit.Frontend/src/partner/**` (cały panel afilianta)
  - 1.12: `Tenebit.Backend/Tenebit.Tests/Affiliate*.cs` + `Tenebit.Frontend` odpowiedniki (Vitest)
  - 2.1: `AffiliateConversion`/`AffiliatePayoutPeriod`/`AffiliatePayout` — model + serwis zamykania
    okresu + "oznacz jako zapłacone" (bez realnego zasilenia z Paddle, patrz C13)
  - 2.2: rozszerzenie `IPaymentGateway`/`PaddlePaymentGateway`/webhook o `transaction.completed`
    i realne kwoty — **odłożone**, patrz "Decyzje" niżej
- **Toolchain**: `dotnet build Tenebit.sln`, `dotnet test Tenebit.Backend/Tenebit.Tests`,
  `dotnet ef migrations add <Name> -p Tenebit.Infrastructure -s Tenebit.Api` (z katalogu
  `Tenebit.Backend`, PATH musi zawierać `~/.dotnet/tools`), frontend: `npm run build`/`npm test`
  w `Tenebit.Frontend`.
- **Konwencje**: mirror 1:1 istniejących wzorców — `PromoCode`/`ProcessedPaddleEvent` (domena),
  `AdminEndpoints`/`AuthEndpoints` (API), `PromoCodeAdminService` (serwis admina), `adminApi.ts`/
  `AuthProvider.tsx` (frontend). Panel admina i panel partnera są **wyłącznie polskojęzyczne** (bez
  i18n) — tak jak dzisiejszy `src/admin/**`.
- **Manual review**: właściciel produktu recenzuje treść regulaminu (leaf 1.7) i domyślne stawki w
  `AffiliateProgramSettings` (leaf 1.10) przed pierwszym realnym zaproszeniem partnera.

## Decyzje właściciela produktu (z briefu 2026-09-06)

Rozstrzygnięte wprost przez właściciela — nie są już otwartymi pytaniami z §16 planu źródłowego:

1. Wypłaty wyłącznie na Revolut — pole `RevolutTag` (format `@nazwa`), opcjonalne przy rejestracji,
   uzupełnialne później w profilu partnera.
2. Dzień rozliczeniowy: 20. każdego miesiąca, ale okres rozliczeniowy = pełny miesiąc kalendarzowy
   zamykany 1. dnia kolejnego miesiąca — wpłata z 10 września trafia do okresu "wrzesień", płatnego
   do 20 **października**, nigdy do 20 września tego samego miesiąca (już tak zaprojektowane w
   `spec/AFFILIATE_PROGRAM_PLAN.md` §7.1 — brief tylko to potwierdza, żadna zmiana modelu).
3. Maks. 5 dni poślizgu po dniu rozliczeniowym (do 25. dnia miesiąca) — dodane jako pole
   `AffiliateProgramSettings.PayoutGraceDays = 5` i widoczne w regulaminie.
4. Regulamin programu partnerskiego wymagany do zaakceptowania: krótki, "wygląda profesjonalnie",
   inspirowany strukturą Amazon Associates (jasne sekcje: jak działa program, zasady prowizji,
   harmonogram wypłat, wymóg konta Revolut, wypowiedzenie umowy przez każdą ze stron, prawo Tenebit
   do odmowy/zablokowania konta za naruszenia, kanał zgłaszania zastrzeżeń).
5. Przycisk "Zgłoś zastrzeżenie" w panelu partnera (osobny od zwykłych wiadomości, wpięty w ten sam
   model wątków — patrz 1.8).
6. Admin musi mieć drill-down: klik na afilianta → historia kto/kiedy/ile wpłacił (konwersje) i
   statystyki — już w §9.2 planu źródłowego, brief podkreśla priorytet ("na pewno na adminie będzie
   dużo działo").
7. Testy: najpierw jednostkowe (ten plan), "inne" (integracyjne/E2E) odłożone na kolejną sesję.

## Decyzje odłożone (wymagają jawnej zgody właściciela przed wdrożeniem)

- **C13 / leaf 2.2 — realne naliczanie prowizji z Paddle.** Dzisiejszy `PaddlePaymentGateway`
  paruje tylko `subscription.created/updated/canceled` i nigdzie nie przechowuje kwoty
  transakcji/opłaty Paddle (`PaymentWebhookEvent` nie ma pól kwotowych; `ListInvoicesAsync` pobiera
  kwoty na żądanie, nie z webhooka). Podłączenie `AffiliateConversion` do realnych pieniędzy
  wymaga: (a) rozszerzenia `IPaymentGateway`/webhook o `transaction.completed` z realnym payloadem
  Paddle, (b) weryfikacji na koncie testowym Paddle jak dokładnie wygląda `data.custom_data` i
  rozbicie kwoty netto/brutto — dokładnie to, przed czym ostrzega już §17 planu źródłowego. Model
  danych (`AffiliateConversion` itd.) i UI po stronie admina/partnera budujemy teraz (leaf 2.1), ale
  **bez** tego podłączenia — dopóki nie zostanie potwierdzone na koncie Paddle, ręczne dopisywanie
  konwersji przez admina jest wyłączone (tylko webhook może je tworzyć, zgodnie z regułą domenową).
- Stawka prowizji/okno prowizyjne/próg minimalny wypłaty/status prawny (B2B vs nagroda)/ranking
  publiczny — zostają **ustawieniami w adminie** (`AffiliateProgramSettings`) z sensownymi
  wartościami domyślnymi (prowizja 20%, podstawa netto, okno dożywotnie, brak progu minimalnego,
  ranking prywatny, kod nie daje rabatu domyślnie) — nie blokują wdrożenia, bo są zmienialne bez
  wdrożenia kodu.

## Drzewo

- 1 Program afiliacyjny — Faza 1 (MVP + priorytety brief) ...... GATES.md
  - 1.1 Domena i dane .......................... gates/node-1.1.md
    - 1.1.1 Model domenowy (encje, reguły) ..... gates/leaf-1.1.1.md
    - 1.1.2 EF Core: migracja + repozytoria .... gates/leaf-1.1.2.md
  - 1.2 Backend — tożsamość i tracking ......... gates/node-1.2.md
    - 1.2.1 Auth afilianta + izolacja scope .... gates/leaf-1.2.1.md
    - 1.2.2 Kody afiliacyjne ................... gates/leaf-1.2.2.md
    - 1.2.3 Redirect + tracking kliknięć ....... gates/leaf-1.2.3.md
  - 1.3 Backend — zaufanie i komunikacja ....... gates/node-1.3.md
    - 1.3.1 Regulamin + akceptacja + revtag .... gates/leaf-1.3.1.md
    - 1.3.2 Wiadomości + "Zgłoś zastrzeżenie" .. gates/leaf-1.3.2.md
  - 1.4 Backend — panel admina ................. gates/node-1.4.md
    - 1.4.1 Lista/szczegóły/moderacja afiliantów gates/leaf-1.4.1.md
    - 1.4.2 Ustawienia globalne programu ....... gates/leaf-1.4.2.md
  - 1.5 Backend — rozliczenia (model, bez Paddle) gates/node-1.5.md
    - 1.5.1 Konwersje/okresy/wypłaty + audyt ... gates/leaf-1.5.1.md
  - 1.6 Frontend ............................... gates/node-1.6.md
    - 1.6.1 Panel partnera (`/partner/*`) ...... gates/leaf-1.6.1.md
    - 1.6.2 Panel admina (afilianci + ustawienia) gates/leaf-1.6.2.md
  - 1.7 Testy jednostkowe (przekrojowe) ........ gates/leaf-1.7.md
- 2 Faza 2+ (kolejna sesja) ................... (poza zakresem teraz)
  - 2.1 Podłączenie realnych kwot z Paddle (transaction.completed) — WAITING, wymaga decyzji
  - 2.2 Kraje/rabaty per-kraj, ranking, heurystyki antyfraudowe — WAITING (Faza 4 planu źródłowego)

## Tabela dyspozycji leafów

| Leaf | Owns | Needs | Tier | Fala | Stan |
|---|---|---|---|---|---|
| 1.1.1 | Tenebit.Backend/Tenebit.Domain/Affiliates/** | - | judgment | 1 | VERIFIED |
| 1.1.2 | Tenebit.Backend/Tenebit.Infrastructure/Data/** (część), Tenebit.Application/Abstractions/*Affiliate*.cs | 1.1.1 | mechanical | 2 | VERIFIED |
| 1.2.1 | Tenebit.Backend/Tenebit.Api/Auth/Affiliate*.cs, Program.cs, Tenebit.Application/Affiliates/AffiliateAuthService.cs | 1.1.2 | judgment | 3 | VERIFIED |
| 1.2.2 | Tenebit.Backend/Tenebit.Application/Affiliates/AffiliateCodeService.cs | 1.1.2 | judgment | 3 | VERIFIED |
| 1.2.3 | Tenebit.Backend/Tenebit.Api/Endpoints/RedirectEndpoints.cs, AffiliateTrackingService.cs | 1.1.2 | judgment | 3 | VERIFIED |
| 1.3.1 | Affiliate.RevolutTag (1.1.1), Tenebit.Frontend/src/partner/affiliateTermsContent.ts | 1.2.1 | judgment | 4 | VERIFIED |
| 1.3.2 | AffiliateMessageService.cs | 1.2.1 | judgment | 4 | VERIFIED |
| 1.4.1 | AffiliateAdminService.cs, AdminEndpoints.cs (sekcja affiliates) | 1.2.1,1.2.2,1.2.3 | judgment | 4 | VERIFIED |
| 1.4.2 | AffiliateSettingsAdminService.cs | 1.1.2 | mechanical | 4 | VERIFIED |
| 1.5.1 | AffiliateConversion/PayoutPeriod/Payout (model+serwis) | 1.1.2 | judgment | 4 | VERIFIED |
| 1.6.1 | Tenebit.Frontend/src/partner/** | 1.2.1,1.2.2,1.2.3,1.3.1,1.3.2 | judgment | 5 | VERIFIED |
| 1.6.2 | Tenebit.Frontend/src/admin/AdminAffiliate*.tsx | 1.4.1,1.4.2,1.5.1 | judgment | 5 | VERIFIED |
| 1.7 | Tenebit.Backend/Tenebit.Tests/Affiliate*.cs | wszystkie powyższe | judgment | 6 | VERIFIED |

## Log statusu

Zapisywany w `.unlazy/affiliate-program/status.log` w miarę postępu (bez native dispatch — jedna
sesja, zapisy tylko informacyjne).
