import type { PartnerLocale } from './i18n';

// TENEB.IT PARTNERS affiliate program terms. Version must match the value returned by
// GET /api/admin/affiliate-settings (field termsVersion) - it flows into Affiliate.AcceptedTermsVersion.
// A single version number covers both language editions below - they are translations of the same
// terms, not separately versioned documents.
export const AFFILIATE_TERMS_VERSION = '2026-09-06';

export interface AffiliateTermsSection {
  title: string;
  paragraphs?: string[];
  bullets?: string[];
}

const en: AffiliateTermsSection[] = [
  {
    title: '1. What this program is',
    paragraphs: [
      'The TENEB.IT PARTNERS program lets you promote Tenebit using your own, unique promo codes. When someone pays for a Tenebit subscription using your code, you receive a commission on that payment.',
      'Participation in the program is voluntary and free. Registration requires approval by Tenebit - the account stays in "pending approval" status until it is verified.',
    ],
  },
  {
    title: '2. Your codes',
    paragraphs: [
      'You can create your own code (if available) or use one generated automatically. The limit on active codes is set by Tenebit and shown in the panel.',
      'Codes never expire. Deactivating a code frees up a slot in the limit but does not delete history - commissions already accrued remain accrued.',
    ],
    bullets: [
      'A code must not be misleading, impersonate another brand, or contain offensive content.',
      'Do not promote your code in a way that suggests you are an employee or official representative of Tenebit, unless that is true.',
      'Do not buy ads targeting keywords containing the "Tenebit" brand without prior written consent.',
    ],
  },
  {
    title: '3. Commission accrual',
    paragraphs: [
      'The commission is calculated on the amount actually received by Tenebit (after payment processor fees), at the rate shown in your panel at the time of the sale. A future rate change never recalculates commissions already accrued retroactively.',
      'No commission is due on purchases made by yourself or entities affiliated with you (self-referral). Such transactions are flagged for manual review and may be held pending clarification.',
      'A refund or chargeback to the customer reduces the commission balance due - if the commission has already been paid out, the amount is deducted from the next upcoming payout.',
    ],
  },
  {
    title: '4. Settlement period',
    paragraphs: [
      'The settlement period is a full calendar month. A sale recorded in a given month is included in that month’s settlement - regardless of which day of the month it occurred.',
      'Example: a sale on September 10 goes into the September settlement, payable in October (see point 5) - it is not paid out on September 20.',
    ],
  },
  {
    title: '5. Payouts',
    paragraphs: [
      'Payouts are made exclusively to a PayPal or Revolut account you choose. During registration you can provide your account identifier (a PayPal e-mail, or a Revolut "revtag" in the format @name) - if you don’t provide it, you can add it later in your profile. A payout account is required before any payout can be made.',
      'The target payout day for a closed settlement period is the 20th of the following month. Tenebit commits to making the payout no later than 5 days after that date.',
      'If a payout is not made on time in a given month, the amount due is not forfeited - it is added to the next upcoming payout.',
      'Tenebit may set a minimum payout amount - information about the current threshold (if one applies) is shown in the partner panel.',
    ],
  },
  {
    title: '6. Your responsibilities',
    bullets: [
      'Promote Tenebit in a way that is lawful and follows good business practice.',
      'Do not send unsolicited messages (spam) containing your partner code.',
      'Do not make false promises about the product, pricing, or guarantees.',
      'Report to Tenebit any situation that raises doubts about compliance with these terms.',
    ],
  },
  {
    title: '7. Suspension and blocking of the account',
    paragraphs: [
      'Tenebit reserves the right to refuse to approve a registration, and to suspend or block a partner account at any time, if it has reasonable grounds to suspect a violation of these terms, an attempted fraud, artificially generated traffic (bots), or actions harmful to Tenebit or its customers.',
      'Blocking an account does not strip you of the right to commissions already correctly accrued before the block, unless the block results from fraud relating to those very transactions.',
      'You will be notified of a block along with the reason. You may appeal this decision using the reporting channel described in point 9.',
    ],
  },
  {
    title: '8. Termination',
    paragraphs: [
      'You may withdraw from the program at any time by contacting Tenebit through the partner panel. Tenebit may also terminate your participation in the program with a reasonable notice period, unless the reason is a breach of these terms (in which case termination may be immediate).',
      'Ending your participation in the program does not affect the right to commissions correctly accrued up to the date of termination - they will be paid out according to the standard schedule described in point 5.',
    ],
  },
  {
    title: '9. Reporting concerns',
    paragraphs: [
      'If you disagree with a decision made by Tenebit (e.g. regarding an accrued commission, account status, or a block), you have the right to report a concern directly in the partner panel, in the "Messages" section, using the "Report a concern" option. The report goes directly to the Tenebit team and you will receive a reply in the same place.',
    ],
  },
  {
    title: '10. Personal data',
    paragraphs: [
      'Your data (first and last name, email, contact and payout details) is processed solely for the purpose of administering the partner program and making payouts, in accordance with Tenebit’s general privacy policy.',
      'The affiliate does not have access to the personal data of customers acquired through their code - the partner panel only shows anonymized sales information (date, commission amount, code used).',
    ],
  },
  {
    title: '11. Changes to these terms',
    paragraphs: [
      'Tenebit may change these terms. Partners will be notified of material changes (in particular regarding the commission rate, payout schedule, or settlement rules) with reasonable advance notice. A change to these terms is never retroactive to commissions already accrued.',
    ],
  },
  {
    title: '12. Final provisions',
    paragraphs: [
      'These terms constitute the entire agreement between you and Tenebit regarding participation in the partner program. For matters not covered here, Tenebit’s general terms of use and applicable law apply.',
    ],
  },
];

const pl: AffiliateTermsSection[] = [
  {
    title: '1. Czym jest program',
    paragraphs: [
      'Program partnerski TENEB.IT PARTNERS pozwala promować Tenebit przy użyciu własnych, unikalnych kodów. Gdy ktoś zapłaci za subskrypcję Tenebit, korzystając z Twojego kodu, otrzymujesz prowizję od tej wpłaty.',
      'Uczestnictwo w programie jest dobrowolne i bezpłatne. Rejestracja wymaga zatwierdzenia przez Tenebit - konto pozostaje w stanie "oczekuje na zatwierdzenie" do czasu weryfikacji.',
    ],
  },
  {
    title: '2. Twoje kody',
    paragraphs: [
      'Możesz utworzyć własny kod (jeśli jest dostępny) albo skorzystać z wygenerowanego automatycznie. Limit aktywnych kodów jest ustalany przez Tenebit i widoczny w panelu.',
      'Kody nie wygasają. Dezaktywowanie kodu zwalnia miejsce w limicie, ale nie usuwa historii - naliczone wcześniej prowizje pozostają naliczone.',
    ],
    bullets: [
      'Kod nie może wprowadzać w błąd, podszywać się pod inną markę ani zawierać treści obraźliwych.',
      'Nie promuj kodu w sposób sugerujący, że jesteś pracownikiem lub oficjalnym przedstawicielem Tenebit, chyba że jest to prawdą.',
      'Nie kupuj reklam na frazy zawierające markę "Tenebit" bez wcześniejszej pisemnej zgody.',
    ],
  },
  {
    title: '3. Naliczanie prowizji',
    paragraphs: [
      'Prowizja jest naliczana od kwoty realnie otrzymanej przez Tenebit (po odliczeniu opłat operatora płatności), zgodnie ze stawką widoczną w Twoim panelu w momencie sprzedaży. Zmiana stawki w przyszłości nigdy nie przelicza już naliczonych prowizji wstecz.',
      'Prowizja nie przysługuje od zakupów dokonanych przez Ciebie samego lub podmioty z Tobą powiązane (self-referral). Takie transakcje są oznaczane do ręcznej weryfikacji i mogą zostać wstrzymane do czasu wyjaśnienia.',
      'Zwrot środków klientowi (refund/chargeback) pomniejsza saldo należnej prowizji - jeśli prowizja została już wypłacona, kwota jest potrącana z najbliższej kolejnej wypłaty.',
    ],
  },
  {
    title: '4. Okres rozliczeniowy',
    paragraphs: [
      'Okresem rozliczeniowym jest pełny miesiąc kalendarzowy. Sprzedaż zarejestrowana w danym miesiącu wchodzi w skład rozliczenia za ten miesiąc - niezależnie od tego, którego dnia miesiąca nastąpiła.',
      'Przykład: sprzedaż z 10 września trafia do rozliczenia za wrzesień, płatnego w październiku (patrz punkt 5) - nie jest wypłacana 20 września.',
    ],
  },
  {
    title: '5. Wypłaty',
    paragraphs: [
      'Wypłaty realizowane są wyłącznie na wybrane przez Ciebie konto PayPal lub Revolut. Podczas rejestracji możesz podać identyfikator konta (e-mail PayPal albo revtag Revolut w formacie @nazwa) - jeśli go nie podasz, będziesz mógł/mogła uzupełnić go później w profilu. Konto wypłaty jest wymagane, zanim jakakolwiek wypłata może zostać zrealizowana.',
      'Docelowym dniem wypłaty za zamknięty okres rozliczeniowy jest 20. dzień kolejnego miesiąca. Tenebit zobowiązuje się zrealizować wypłatę najpóźniej w ciągu 5 dni od tej daty.',
      'Jeżeli w danym miesiącu wypłata nie zostanie zrealizowana w terminie, należna kwota nie przepada - zostaje doliczona do najbliższej kolejnej wypłaty.',
      'Tenebit może ustalić minimalną kwotę wypłaty - informacja o aktualnym progu (jeśli obowiązuje) jest widoczna w panelu partnera.',
    ],
  },
  {
    title: '6. Twoje obowiązki',
    bullets: [
      'Promuj Tenebit w sposób zgodny z prawem i dobrymi obyczajami.',
      'Nie wysyłaj niechcianych wiadomości (spam) ze swoim kodem partnerskim.',
      'Nie składaj fałszywych obietnic dotyczących produktu, cen ani gwarancji.',
      'Zgłaszaj Tenebit każdą sytuację, która budzi Twoje wątpliwości co do zgodności z regulaminem.',
    ],
  },
  {
    title: '7. Zawieszenie i zablokowanie konta',
    paragraphs: [
      'Tenebit zastrzega sobie prawo do odmowy zatwierdzenia rejestracji, a także do zawieszenia lub zablokowania konta partnerskiego w każdym momencie, jeśli poweźmie uzasadnione podejrzenie naruszenia niniejszego regulaminu, próby oszustwa, generowania sztucznego ruchu (boty) lub działania na szkodę Tenebit lub jego klientów.',
      'Zablokowanie konta nie pozbawia Cię prawa do prowizji już prawidłowo naliczonych przed zablokowaniem, chyba że blokada wynika z oszustwa dotyczącego tych właśnie transakcji.',
      'O blokadzie zostaniesz poinformowany/a wraz z podaniem powodu. Możesz się od tej decyzji odwołać, korzystając z kanału zgłoszeń opisanego w punkcie 9.',
    ],
  },
  {
    title: '8. Wypowiedzenie',
    paragraphs: [
      'Możesz zrezygnować z udziału w programie w dowolnym momencie, kontaktując się z Tenebit przez panel partnera. Tenebit może również wypowiedzieć Twój udział w programie z zachowaniem rozsądnego okresu wypowiedzenia, chyba że powodem jest naruszenie regulaminu (wtedy wypowiedzenie może być natychmiastowe).',
      'Zakończenie udziału w programie nie wpływa na prawo do prowizji prawidłowo naliczonych do dnia zakończenia - zostaną one wypłacone zgodnie ze standardowym harmonogramem opisanym w punkcie 5.',
    ],
  },
  {
    title: '9. Zgłaszanie zastrzeżeń',
    paragraphs: [
      'Jeśli nie zgadzasz się z decyzją Tenebit (np. w sprawie naliczonej prowizji, statusu konta lub blokady), masz prawo zgłosić zastrzeżenie bezpośrednio w panelu partnera, w sekcji "Wiadomości", przy użyciu opcji "Zgłoś zastrzeżenie". Zgłoszenie trafia bezpośrednio do zespołu Tenebit i otrzymasz odpowiedź w tym samym miejscu.',
    ],
  },
  {
    title: '10. Dane osobowe',
    paragraphs: [
      'Twoje dane (imię, nazwisko, e-mail, dane kontaktowe i wypłatowe) przetwarzane są wyłącznie w celu obsługi programu partnerskiego i realizacji wypłat, zgodnie z ogólną polityką prywatności Tenebit.',
      'Afiliant nie ma dostępu do danych osobowych klientów pozyskanych przez jego kod - w panelu partnera widoczne są wyłącznie zanonimizowane informacje o sprzedaży (data, kwota prowizji, użyty kod).',
    ],
  },
  {
    title: '11. Zmiany regulaminu',
    paragraphs: [
      'Tenebit może zmieniać niniejszy regulamin. O istotnych zmianach (w szczególności dotyczących stawki prowizji, harmonogramu wypłat czy zasad rozliczania) partnerzy zostaną poinformowani z odpowiednim wyprzedzeniem. Zmiana regulaminu nie działa wstecz na już naliczone prowizje.',
    ],
  },
  {
    title: '12. Postanowienia końcowe',
    paragraphs: [
      'Niniejszy regulamin stanowi całość ustaleń między Tobą a Tenebit dotyczących udziału w programie partnerskim. W sprawach nieuregulowanych zastosowanie mają ogólne warunki korzystania z Tenebit oraz obowiązujące przepisy prawa.',
    ],
  },
];

export const affiliateTermsSectionsByLocale: Record<PartnerLocale, AffiliateTermsSection[]> = { en, pl };
