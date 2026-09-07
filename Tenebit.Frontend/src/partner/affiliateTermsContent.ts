// Regulamin programu partnerskiego TENEB.IT PARTNERS. Polski jest jedynym językiem tego dokumentu -
// panel partnera jest wewnętrznym, niepublicznym narzędziem (spec §2), inaczej niż główna aplikacja,
// która obsługuje sześć języków. Wersja musi być zgodna z wartością zwracaną przez
// GET /api/admin/affiliate-settings (pole termsVersion) - to ona trafia do Affiliate.AcceptedTermsVersion.
export const AFFILIATE_TERMS_VERSION = '2026-09-06';

export interface AffiliateTermsSection {
  title: string;
  paragraphs?: string[];
  bullets?: string[];
}

export const affiliateTermsSections: AffiliateTermsSection[] = [
  {
    title: '1. Czym jest program',
    paragraphs: [
      'Program partnerski TENEB.IT PARTNERS pozwala promować Tenebit przy użyciu własnych, unikalnych kodów i linków polecających. Gdy ktoś zapłaci za subskrypcję Tenebit, korzystając z Twojego kodu, otrzymujesz prowizję od tej wpłaty.',
      'Uczestnictwo w programie jest dobrowolne i bezpłatne. Rejestracja wymaga zatwierdzenia przez Tenebit - konto pozostaje w stanie "oczekuje na zatwierdzenie" do czasu weryfikacji.',
    ],
  },
  {
    title: '2. Twoje kody i linki',
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
      'Wypłaty realizowane są wyłącznie na konto Revolut. Podczas rejestracji możesz podać swój identyfikator Revolut (tzw. "revtag", w formacie @nazwa) - jeśli go nie podasz, będziesz mógł/mogła uzupełnić go później w profilu. Revtag jest wymagany, zanim jakakolwiek wypłata może zostać zrealizowana.',
      'Docelowym dniem wypłaty za zamknięty okres rozliczeniowy jest 20. dzień kolejnego miesiąca. Tenebit zobowiązuje się zrealizować wypłatę najpóźniej w ciągu 5 dni od tej daty.',
      'Jeżeli w danym miesiącu wypłata nie zostanie zrealizowana w terminie, należna kwota nie przepada - zostaje doliczona do najbliższej kolejnej wypłaty.',
      'Tenebit może ustalić minimalną kwotę wypłaty - informacja o aktualnym progu (jeśli obowiązuje) jest widoczna w panelu partnera.',
    ],
  },
  {
    title: '6. Twoje obowiązki',
    bullets: [
      'Promuj Tenebit w sposób zgodny z prawem i dobrymi obyczajami.',
      'Nie wysyłaj niechcianych wiadomości (spam) z linkiem partnerskim.',
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
