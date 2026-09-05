import type { Language } from '../i18n/translations';

export type LegalDocumentKind = 'privacy' | 'terms' | 'cookies';

type LegalSection = {
  title: string;
  paragraphs?: string[];
  bullets?: string[];
};

type LegalDocument = {
  title: string;
  description: string;
  sections: LegalSection[];
};

type LegalUi = {
  home: string;
  privacy: string;
  terms: string;
  cookies: string;
  contact: string;
  contactPrompt: string;
  operator: string;
  address: string;
  registration: string;
  taxId: string;
  effectiveDate: string;
  version: string;
  missingOperator: string;
  storageNotice: string;
  storageNoticeDetails: string;
  consentAccept: string;
  consentReject: string;
  manageConsent: string;
  footerRights: string;
};

type LegalLanguageContent = {
  ui: LegalUi;
  documents: Record<LegalDocumentKind, LegalDocument>;
};

// Etykiety interfejsu tłumaczymy od razu, ale same dokumenty tylko wtedy, gdy istnieje wersja
// przejrzana pod kątem prawnym. Język bez własnych dokumentów dostaje angielskie - świadomie, bo
// regulamin i polityka prywatności są wiążące i maszynowy przekład byłby tu gorszy niż obcy język.
type LegalLanguageEntry = {
  ui: LegalUi;
  documents?: Record<LegalDocumentKind, LegalDocument>;
};

const legalEntries: Record<Language, LegalLanguageEntry> = {
  pl: {
    ui: {
      home: 'Strona główna',
      privacy: 'Polityka prywatności',
      terms: 'Regulamin',
      cookies: 'Cookies i pamięć urządzenia',
      contact: 'Kontakt',
      contactPrompt: 'Pytania biznesowe albo problem, z którym możemy pomóc?',
      operator: 'Operator usługi',
      address: 'Adres',
      registration: 'Dane rejestrowe',
      taxId: 'NIP / VAT',
      effectiveDate: 'Obowiązuje od',
      version: 'Wersja',
      missingOperator: 'Przed publikacją uzupełnij dane operatora w zmiennych VITE_LEGAL_OPERATOR_*.',
      storageNotice: 'Tenebit używa technicznej pamięci potrzebnej do logowania i bezpieczeństwa oraz, za Twoją zgodą, Google Analytics do statystyk odwiedzin.',
      storageNoticeDetails: 'Dane techniczne działają zawsze. Google Analytics uruchomimy dopiero po Twojej zgodzie i możesz ją cofnąć w każdej chwili. Szczegóły znajdziesz w informacji o cookies.',
      consentAccept: 'Akceptuję',
      consentReject: 'Odrzucam',
      manageConsent: 'Zarządzaj zgodą na cookies',
      footerRights: 'Wszelkie prawa zastrzeżone.'
    },
    documents: {
      privacy: {
        title: 'Polityka prywatności',
        description: 'Informacja o przetwarzaniu danych osobowych w serwisie Tenebit.',
        sections: [
          {
            title: '1. Role w przetwarzaniu danych',
            paragraphs: [
              'Operator Tenebit jest administratorem danych związanych z kontem, rozliczeniami, bezpieczeństwem usługi, kontaktem i korzystaniem z publicznej strony.',
              'W odniesieniu do danych pracowników, współpracowników, sprzętu, procedur i innych informacji wprowadzonych przez organizację, administratorem pozostaje ta organizacja. Tenebit działa wtedy jako podmiot przetwarzający na jej udokumentowane polecenie.'
            ]
          },
          {
            title: '2. Jakie dane przetwarzamy',
            bullets: [
              'dane konta, takie jak imię, nazwisko, nazwa wyświetlana, adres e-mail, organizacja, role i ustawienia językowe',
              'dane organizacji, subskrypcji, płatności i rozliczeń',
              'dane techniczne i bezpieczeństwa, w tym identyfikatory sesji, zdarzenia logowania, adres IP w zakresie wymaganym do ochrony usługi i dzienniki audytowe',
              'treści wprowadzone przez klientów, w tym dane osób, aktywów, wydań, zwrotów, procedur, załączników i potwierdzeń',
              'treść korespondencji oraz zgłoszeń do pomocy technicznej'
            ]
          },
          {
            title: '3. Cele i podstawy prawne',
            bullets: [
              'zawarcie i wykonanie umowy oraz prowadzenie konta i organizacji',
              'zapewnienie bezpieczeństwa, zapobieganie nadużyciom, dochodzenie i obrona roszczeń na podstawie prawnie uzasadnionego interesu',
              'realizacja obowiązków podatkowych, rachunkowych i innych obowiązków prawnych',
              'obsługa korespondencji i zgłoszeń',
              'wysyłanie komunikacji marketingowej wyłącznie wtedy, gdy istnieje właściwa podstawa prawna i możliwość łatwej rezygnacji'
            ]
          },
          {
            title: '4. Odbiorcy danych',
            paragraphs: [
              'Dane mogą być powierzane dostawcom hostingu, poczty elektronicznej, monitoringu bezpieczeństwa, płatności, wsparcia technicznego i usług prawnych lub księgowych. Otrzymują oni tylko dane potrzebne do wykonania swoich zadań i są związani odpowiednimi obowiązkami poufności oraz ochrony danych.',
              'Dane mogą zostać ujawnione organom publicznym, gdy wynika to z przepisów lub prawnie wiążącego żądania.'
            ]
          },
          {
            title: '5. Przekazywanie poza EOG',
            paragraphs: [
              'Jeżeli dostawca przetwarza dane poza Europejskim Obszarem Gospodarczym, stosujemy mechanizm dozwolony przez RODO, taki jak decyzja stwierdzająca odpowiedni stopień ochrony albo standardowe klauzule umowne wraz z dodatkowymi zabezpieczeniami, gdy są potrzebne.'
            ]
          },
          {
            title: '6. Okres przechowywania',
            paragraphs: [
              'Dane konta i organizacji przechowujemy przez czas świadczenia usługi, a później przez okres wymagany przepisami lub potrzebny do rozliczeń i ochrony roszczeń. Dane przestrzeni roboczej są przechowywane zgodnie z umową, ustawieniami retencji i poleceniami organizacji. Tokeny bezpieczeństwa są usuwane albo tracą ważność po wykorzystaniu lub upływie terminu. Kopie zapasowe są nadpisywane w zwykłym cyklu technicznym.'
            ]
          },
          {
            title: '7. Prawa osób',
            bullets: [
              'dostęp do danych i otrzymanie ich kopii',
              'sprostowanie, usunięcie lub ograniczenie przetwarzania',
              'sprzeciw wobec przetwarzania opartego na prawnie uzasadnionym interesie',
              'przenoszenie danych, gdy ma zastosowanie',
              'wycofanie zgody bez wpływu na zgodność wcześniejszego przetwarzania',
              'skarga do właściwego organu nadzorczego, w Polsce do Prezesa Urzędu Ochrony Danych Osobowych'
            ],
            paragraphs: [
              'W sprawach danych wprowadzonych przez pracodawcę lub inną organizację należy w pierwszej kolejności skontaktować się z tą organizacją. Tenebit wspiera ją w realizacji praw osób.'
            ]
          },
          {
            title: '8. Zautomatyzowane decyzje i dzieci',
            paragraphs: [
              'Tenebit nie podejmuje wobec użytkowników decyzji wywołujących skutki prawne wyłącznie w sposób zautomatyzowany. Usługa jest przeznaczona dla organizacji i osób upoważnionych do działania w ich imieniu, a nie dla dzieci.'
            ]
          },
          {
            title: '9. Bezpieczeństwo i kontakt',
            paragraphs: [
              'Stosujemy środki techniczne i organizacyjne odpowiednie do ryzyka, w tym kontrolę dostępu, rozdzielenie organizacji, rejestrowanie zdarzeń, szyfrowanie transmisji i mechanizmy unieważniania sesji. Żaden system nie daje jednak absolutnej gwarancji bezpieczeństwa.',
              'W sprawach prywatności skontaktuj się z adresem wskazanym w sekcji danych operatora.'
            ]
          }
        ]
      },
      terms: {
        title: 'Regulamin świadczenia usługi Tenebit',
        description: 'Zasady korzystania z aplikacji do zarządzania aktywami, osobami, wydaniami i procedurami.',
        sections: [
          {
            title: '1. Zakres usługi',
            paragraphs: [
              'Tenebit jest usługą SaaS przeznaczoną przede wszystkim dla przedsiębiorców i innych organizacji. Umożliwia prowadzenie rejestru aktywów, osób, lokalizacji, wydań, zwrotów, procedur, audytów oraz historii czynności.',
              'Regulamin jest udostępniany bezpłatnie przed zawarciem umowy i może zostać zapisany lub wydrukowany.'
            ]
          },
          {
            title: '2. Wymagania techniczne',
            bullets: [
              'aktualna przeglądarka internetowa z włączonym JavaScriptem',
              'połączenie z internetem i możliwość korzystania z technicznej pamięci przeglądarki',
              'aktywny adres e-mail do logowania, odzyskiwania dostępu i komunikacji operacyjnej',
              'po stronie organizacji odpowiednie urządzenie i konfiguracja sieciowa'
            ]
          },
          {
            title: '3. Konto i bezpieczeństwo',
            paragraphs: [
              'Użytkownik podaje prawdziwe dane, chroni dane logowania, nie udostępnia konta osobom nieuprawnionym i niezwłocznie zgłasza podejrzenie przejęcia konta. Organizacja odpowiada za nadawanie ról, uprawnień i usuwanie dostępu osobom, które nie powinny już korzystać z usługi.',
              'Operator może czasowo zablokować konto lub sesję, gdy jest to konieczne dla bezpieczeństwa, ochrony innych klientów lub wykonania obowiązku prawnego.'
            ]
          },
          {
            title: '4. Zawarcie i rozwiązanie umowy',
            paragraphs: [
              'Umowa zostaje zawarta po utworzeniu organizacji i zaakceptowaniu regulaminu albo zgodnie z odrębnym zamówieniem. Plan płatny obowiązuje przez wybrany okres rozliczeniowy. Zasady ceny, podatków, limitów i odnowienia są prezentowane przed zakupem.',
              'Limit liczby aktywów określony dla danego planu obowiązuje w tej samej wysokości dla liczby pracowników, lokalizacji, procedur, licencji, zespołów, zestawów stanowiskowych oraz kategorii utworzonych przez organizację. Przekroczenie limitu wymaga przejścia na wyższy plan.',
              'Niezależnie od planu obowiązują techniczne pułapy uczciwego korzystania, chroniące stabilność usługi. Kategoria aktywów może mieć maksymalnie 200 zdefiniowanych pól własnych. Operator może zmienić te pułapy, informując o tym w regulaminie.',
              'Organizacja może zakończyć korzystanie z usługi zgodnie z ustawieniami konta lub umową. Operator może wypowiedzieć umowę z ważnych powodów, w szczególności przy istotnym naruszeniu regulaminu, braku płatności, nadużyciu lub zagrożeniu bezpieczeństwa, z zachowaniem wymaganego prawem lub umową terminu.'
            ]
          },
          {
            title: '5. Dozwolone korzystanie',
            bullets: [
              'nie wolno wprowadzać treści bezprawnych ani naruszających prawa innych osób',
              'nie wolno obchodzić zabezpieczeń, testować podatności bez pisemnej zgody, zakłócać działania usługi ani używać jej do nadużyć',
              'organizacja musi posiadać podstawę prawną do przetwarzania danych, które wprowadza, oraz przekazać osobom wymagane informacje',
              'użytkownik nie może podejmować prób uzyskania dostępu do danych innej organizacji'
            ]
          },
          {
            title: '6. Treści i dane klienta',
            paragraphs: [
              'Organizacja zachowuje prawa do swoich danych i udziela operatorowi upoważnienia niezbędnego do hostowania, zabezpieczania, tworzenia kopii zapasowych i technicznego przetwarzania tych danych w celu świadczenia usługi.',
              'Tenebit nie zastępuje profesjonalnej porady prawnej, kadrowej, podatkowej ani BHP. Organizacja odpowiada za treść regulaminów, procedur, terminów i decyzji podejmowanych na podstawie danych w systemie.'
            ]
          },
          {
            title: '7. Dostępność, zmiany i odpowiedzialność',
            paragraphs: [
              'Operator rozwija usługę i może zmieniać funkcje, jeżeli nie pozbawia to klienta głównego celu umowy. Przerwy mogą wystąpić z powodu konserwacji, awarii, działań dostawców lub siły wyższej. O planowanych istotnych pracach operator informuje w rozsądnym zakresie.',
              'W granicach dopuszczonych prawem odpowiedzialność operatora nie obejmuje szkód wynikających z nieprawidłowych danych klienta, braku wymaganych uprawnień, działania usług zewnętrznych lub korzystania niezgodnego z dokumentacją. Bezwzględnie obowiązujące przepisy mają pierwszeństwo.'
            ]
          },
          {
            title: '8. Reklamacje',
            paragraphs: [
              'Reklamację można wysłać na adres pomocy wskazany w danych operatora. Należy podać organizację, opis problemu, datę wystąpienia i oczekiwany sposób rozwiązania. Operator odpowiada bez zbędnej zwłoki, co do zasady w ciągu 14 dni, chyba że szczególny przepis lub umowa przewiduje inny termin.'
            ]
          },
          {
            title: '9. Zmiany regulaminu i prawo właściwe',
            paragraphs: [
              'O istotnej zmianie regulaminu operator poinformuje z wyprzedzeniem umożliwiającym zapoznanie się ze zmianą i, gdy jest to wymagane, zakończenie umowy. Prawo właściwe i sąd określa umowa oraz bezwzględnie obowiązujące przepisy. Wobec konsumenta nie ogranicza to ochrony przysługującej mu z mocy prawa.'
            ]
          }
        ]
      },
      cookies: {
        title: 'Cookies i pamięć urządzenia',
        description: 'Wyjaśnienie, z jakich mechanizmów pamięci korzysta Tenebit i kiedy potrzebna jest zgoda.',
        sections: [
          {
            title: '1. Aktualny zakres',
            paragraphs: [
              'Tenebit używa technicznych cookies oraz pamięci localStorage i sessionStorage potrzebnych do logowania, zabezpieczania sesji, realizowania wybranej funkcji i zapamiętania ustawień użytkownika - zawsze, bez pytania o zgodę. Za zgodą użytkownika usługa uruchamia dodatkowo Google Analytics do statystyk odwiedzin publicznej strony.'
            ]
          },
          {
            title: '2. Mechanizmy niezbędne',
            bullets: [
              'cookie odświeżania sesji z flagami HttpOnly i Secure w środowisku produkcyjnym',
              'cookie zaufanego urządzenia, gdy użytkownik świadomie wybierze zapamiętanie urządzenia dla 2FA',
              'krótkotrwałe cookies korelacyjne używane podczas logowania zewnętrznego',
              'krótkotrwała sesja publicznego linku do potwierdzenia wydania, zwrotu lub audytu',
              'pamięć języka, widoku listy, zamkniętych komunikatów i roboczych ustawień interfejsu'
            ]
          },
          {
            title: '3. Mechanizmy analityczne (za zgodą)',
            paragraphs: [
              'Google Analytics (Google LLC) ustawia własne cookies (m.in. _ga, _ga_*) i przesyła do Google zanonimizowane w miarę możliwości dane o odwiedzinach publicznej strony, takie jak odwiedzane podstrony, przybliżona lokalizacja i typ urządzenia. Dane mogą być przetwarzane przez Google poza EOG na podstawie mechanizmów dopuszczonych przez RODO.',
              'Google Analytics uruchamia się wyłącznie po kliknięciu „Akceptuję” w komunikacie o cookies i nigdy wcześniej.'
            ]
          },
          {
            title: '4. Zgoda',
            paragraphs: [
              'Dla mechanizmów ściśle potrzebnych do transmisji komunikatu lub dostarczenia funkcji wyraźnie żądanej przez użytkownika uprzednia zgoda nie jest wymagana - działają zawsze.',
              'Google Analytics jest mechanizmem nieniezbędnym, więc komunikat Tenebit prezentuje realny wybór „Akceptuję” / „Odrzucam”. Brak decyzji lub kliknięcie „Odrzucam” oznacza, że Google Analytics się nie uruchamia. Zgodę można cofnąć w każdej chwili w ustawieniach cookies opisanych w sekcji 5 - równie łatwo, jak została udzielona.'
            ]
          },
          {
            title: '5. Zarządzanie pamięcią i zgodą',
            paragraphs: [
              'Cookies można usunąć lub zablokować w ustawieniach przeglądarki. Usunięcie technicznych danych może wylogować użytkownika, usunąć zapamiętany język lub widok oraz uniemożliwić działanie niektórych funkcji. Dane z usług płatniczych lub logowania społecznościowego podlegają również informacjom ich dostawców, gdy użytkownik uruchomi daną integrację.',
              'Aby zmienić wcześniejszą decyzję o Google Analytics, użyj przycisku „Zarządzaj zgodą na cookies” na tej stronie - komunikat o zgodzie pojawi się ponownie.'
            ]
          }
        ]
      }
    }
  },
  en: {
    ui: {
      home: 'Home', privacy: 'Privacy policy', terms: 'Terms of service', cookies: 'Cookies and device storage', contact: 'Contact', contactPrompt: 'Business inquiries or something we can help fix?', operator: 'Service operator', address: 'Address', registration: 'Registration details', taxId: 'Tax / VAT ID', effectiveDate: 'Effective from', version: 'Version', missingOperator: 'Complete the operator details in VITE_LEGAL_OPERATOR_* before production release.', storageNotice: 'Tenebit uses technical storage needed for sign-in and security, and, with your consent, Google Analytics for visit statistics.', storageNoticeDetails: 'Technical storage always applies. Google Analytics only runs after you consent, and you can withdraw at any time. See the cookies notice for details.', consentAccept: 'Accept', consentReject: 'Reject', manageConsent: 'Manage cookie consent', footerRights: 'All rights reserved.'
    },
    documents: {
      privacy: {
        title: 'Privacy policy', description: 'Information about personal data processing in Tenebit.', sections: [
          { title: '1. Processing roles', paragraphs: ['The Tenebit operator is the controller for account, billing, service security, contact and public website data.', 'For employee, contractor, equipment, procedure and other workspace data entered by an organization, that organization remains the controller and Tenebit acts as its processor under documented instructions.'] },
          { title: '2. Data we process', bullets: ['account data such as name, display name, email, organization, roles and language preferences', 'organization, subscription, payment and billing data', 'technical and security data, including session identifiers, sign-in events, IP address where needed for service protection and audit logs', 'customer workspace content, including people, assets, handovers, returns, procedures, attachments and confirmations', 'support messages and correspondence'] },
          { title: '3. Purposes and legal bases', bullets: ['entering into and performing the contract and operating the account', 'security, abuse prevention and legal claims based on legitimate interests', 'tax, accounting and other legal obligations', 'handling support and correspondence', 'marketing only where a valid legal basis exists and an easy opt-out is provided'] },
          { title: '4. Recipients', paragraphs: ['Data may be entrusted to hosting, email, security monitoring, payment, technical support, legal and accounting providers. They receive only the data necessary for their tasks and are bound by appropriate confidentiality and data protection duties.', 'Data may be disclosed to public authorities where required by law or a binding request.'] },
          { title: '5. Transfers outside the EEA', paragraphs: ['Where a provider processes data outside the European Economic Area, we use a GDPR-permitted mechanism such as an adequacy decision or standard contractual clauses with supplementary safeguards where necessary.'] },
          { title: '6. Retention', paragraphs: ['Account and organization data is kept while the service is provided and later for periods required by law, settlement and claims. Workspace data follows the contract, retention settings and the organization instructions. Security tokens expire or are removed after use. Backups are overwritten in the normal technical cycle.'] },
          { title: '7. Your rights', bullets: ['access and a copy of data', 'rectification, erasure or restriction', 'objection to legitimate-interest processing', 'data portability where applicable', 'withdrawal of consent without affecting earlier lawful processing', 'a complaint to the competent supervisory authority'], paragraphs: ['For data entered by an employer or another organization, contact that organization first. Tenebit assists it in responding to data subject requests.'] },
          { title: '8. Automated decisions and children', paragraphs: ['Tenebit does not make decisions producing legal effects solely by automated means. The service is intended for organizations and authorized professional users, not children.'] },
          { title: '9. Security and contact', paragraphs: ['We apply technical and organizational measures proportionate to risk, including access controls, tenant separation, event logging, transport encryption and session revocation. No system can guarantee absolute security.', 'Use the privacy address shown in the operator details for privacy requests.'] }
        ]
      },
      terms: {
        title: 'Tenebit terms of service', description: 'Rules for using the asset, people, handover and procedure management service.', sections: [
          { title: '1. Service scope', paragraphs: ['Tenebit is a SaaS service primarily intended for businesses and other organizations. It supports asset, people, location, handover, return, procedure, audit and activity records.', 'These terms are available free of charge before the contract and may be saved or printed.'] },
          { title: '2. Technical requirements', bullets: ['an up-to-date browser with JavaScript enabled', 'internet access and permission to use technical browser storage', 'an active email address for access recovery and operational messages', 'a suitable device and network configuration maintained by the organization'] },
          { title: '3. Account security', paragraphs: ['Users must provide accurate data, protect credentials, avoid sharing accounts and report suspected compromise. The organization manages roles and promptly removes access that is no longer needed.', 'The operator may temporarily block an account or session where necessary for security, protection of other customers or compliance with law.'] },
          { title: '4. Contract and termination', paragraphs: ['The contract is concluded when an organization is created and these terms are accepted, or under a separate order. Paid plan prices, taxes, limits and renewal rules are shown before purchase.', 'The asset limit shown for a plan applies equally to the number of people, locations, procedures, licenses, teams, job profiles and organization-created categories stored in the system for that organization. Exceeding the limit requires upgrading to a higher plan.', 'Regardless of plan, technical fair-use ceilings apply to protect service stability. An asset category may define at most 200 custom fields. The operator may change these ceilings by updating these terms.', 'The organization may terminate according to account settings or the contract. The operator may terminate for material breach, non-payment, abuse or security risk, subject to the notice required by law or contract.'] },
          { title: '5. Acceptable use', bullets: ['do not submit unlawful content or infringe third-party rights', 'do not bypass safeguards, test vulnerabilities without written permission, disrupt the service or use it for abuse', 'the organization must have a lawful basis for workspace data and provide required notices', 'do not attempt to access another organization data'] },
          { title: '6. Customer content', paragraphs: ['The organization retains its rights to customer data and authorizes the operator to host, secure, back up and technically process it to provide the service.', 'Tenebit is not legal, HR, tax or occupational safety advice. The organization is responsible for its policies, deadlines and decisions.'] },
          { title: '7. Availability, changes and liability', paragraphs: ['The operator may develop and modify features without removing the core purpose of the contract. Interruptions may result from maintenance, failures, suppliers or force majeure. Material planned work is communicated where reasonably possible.', 'To the extent permitted by law, the operator is not liable for incorrect customer data, missing permissions, third-party services or use contrary to documentation. Mandatory law prevails.'] },
          { title: '8. Complaints', paragraphs: ['Send complaints to the support address in the operator details, with the organization, issue, date and requested resolution. The operator responds without undue delay, generally within 14 days unless a specific law or contract provides otherwise.'] },
          { title: '9. Changes and governing law', paragraphs: ['Material changes are announced in advance so customers can review them and, where required, terminate. Governing law and jurisdiction follow the contract and mandatory rules. Consumer protections are not limited where they apply.'] }
        ]
      },
      cookies: {
        title: 'Cookies and device storage', description: 'How Tenebit uses browser storage and when consent is needed.', sections: [
          { title: '1. Current scope', paragraphs: ['Tenebit uses technical cookies, localStorage and sessionStorage for sign-in, session security, requested features and interface preferences - always, without asking for consent. With your consent, the service additionally runs Google Analytics for visit statistics on the public website.'] },
          { title: '2. Necessary mechanisms', bullets: ['an HttpOnly refresh-session cookie, with Secure enabled in production', 'a trusted-device cookie when the user explicitly remembers a device for 2FA', 'short-lived correlation cookies for external sign-in', 'a short-lived public-link session for handover, return or audit confirmation', 'language, list-view, dismissed-message and temporary interface preferences'] },
          { title: '3. Analytics mechanisms (with consent)', paragraphs: ['Google Analytics (Google LLC) sets its own cookies (including _ga, _ga_*) and sends Google data about visits to the public website, such as pages viewed, approximate location and device type. Data may be processed by Google outside the EEA under a GDPR-permitted mechanism.', 'Google Analytics only runs after you click "Accept" in the cookie notice, never before.'] },
          { title: '4. Consent', paragraphs: ['Prior consent is not required for storage strictly necessary to transmit communications or provide a feature explicitly requested by the user - it always applies.', 'Google Analytics is a non-essential mechanism, so the Tenebit notice presents a real "Accept" / "Reject" choice. No decision, or clicking "Reject", means Google Analytics does not run. Consent can be withdrawn at any time through the cookie settings described in section 5, as easily as it was given.'] },
          { title: '5. Managing storage and consent', paragraphs: ['You may remove or block cookies in browser settings. Removing technical data may sign you out, clear language or view preferences and stop some features. Payment and social sign-in providers may also use storage under their own notices when you activate those integrations.', 'To change an earlier Google Analytics decision, use the "Manage cookie consent" button on this page - the consent notice will reappear.'] }
        ]
      }
    }
  },
  es: {
    ui: {
      home: 'Inicio', privacy: 'Política de privacidad', terms: 'Términos del servicio', cookies: 'Cookies y almacenamiento del dispositivo', contact: 'Contacto', contactPrompt: '¿Consultas comerciales o algo que podamos ayudarte a resolver?', operator: 'Operador del servicio', address: 'Dirección', registration: 'Datos registrales', taxId: 'NIF / IVA', effectiveDate: 'Vigente desde', version: 'Versión', missingOperator: 'Completa los datos del operador en VITE_LEGAL_OPERATOR_* antes de publicar.', storageNotice: 'Tenebit utiliza almacenamiento técnico necesario para iniciar sesión y proteger la cuenta y, con tu consentimiento, Google Analytics para estadísticas de visitas.', storageNoticeDetails: 'El almacenamiento técnico siempre está activo. Google Analytics solo se activa con tu consentimiento y puedes retirarlo en cualquier momento. Consulta el aviso de cookies para más detalles.', consentAccept: 'Aceptar', consentReject: 'Rechazar', manageConsent: 'Gestionar el consentimiento de cookies', footerRights: 'Todos los derechos reservados.'
    },
    documents: {
      privacy: {
        title: 'Política de privacidad', description: 'Información sobre el tratamiento de datos personales en Tenebit.', sections: [
          { title: '1. Roles de tratamiento', paragraphs: ['El operador de Tenebit es responsable de los datos de cuenta, facturación, seguridad, contacto y sitio público.', 'Para los datos de empleados, colaboradores, equipos, procedimientos y demás contenido introducido por una organización, dicha organización sigue siendo responsable y Tenebit actúa como encargado según sus instrucciones documentadas.'] },
          { title: '2. Datos tratados', bullets: ['datos de cuenta, nombre, correo, organización, roles e idioma', 'datos de suscripción, pago y facturación', 'datos técnicos y de seguridad, incluidos identificadores de sesión, eventos de acceso, IP cuando sea necesaria y registros de auditoría', 'contenido del espacio de trabajo, incluidas personas, activos, entregas, devoluciones, procedimientos, adjuntos y confirmaciones', 'mensajes de soporte y correspondencia'] },
          { title: '3. Finalidades y bases jurídicas', bullets: ['celebrar y ejecutar el contrato y gestionar la cuenta', 'seguridad, prevención de abusos y reclamaciones sobre la base del interés legítimo', 'obligaciones fiscales, contables y legales', 'soporte y comunicaciones', 'marketing solo con una base válida y una baja sencilla'] },
          { title: '4. Destinatarios', paragraphs: ['Los datos pueden facilitarse a proveedores de alojamiento, correo, seguridad, pagos, soporte, servicios jurídicos y contables, únicamente en la medida necesaria y con obligaciones adecuadas de protección.', 'También podrán comunicarse a autoridades cuando lo exija la ley o una solicitud vinculante.'] },
          { title: '5. Transferencias internacionales', paragraphs: ['Cuando un proveedor trate datos fuera del EEE, se utiliza un mecanismo permitido por el RGPD, como una decisión de adecuación o cláusulas contractuales tipo con garantías adicionales cuando proceda.'] },
          { title: '6. Conservación', paragraphs: ['Los datos de cuenta y organización se conservan durante la prestación del servicio y después durante los plazos legales, de liquidación y reclamación. Los datos del espacio de trabajo siguen el contrato, la retención configurada y las instrucciones de la organización. Los tokens caducan o se eliminan tras su uso.'] },
          { title: '7. Derechos', bullets: ['acceso y copia', 'rectificación, supresión o limitación', 'oposición al tratamiento basado en interés legítimo', 'portabilidad cuando corresponda', 'retirada del consentimiento', 'reclamación ante la autoridad de control competente'], paragraphs: ['Para datos introducidos por un empleador u otra organización, contacta primero con esa organización. Tenebit le presta asistencia.'] },
          { title: '8. Decisiones automatizadas y menores', paragraphs: ['Tenebit no adopta decisiones con efectos jurídicos basadas únicamente en procesos automatizados. El servicio está destinado a organizaciones y usuarios profesionales autorizados, no a menores.'] },
          { title: '9. Seguridad y contacto', paragraphs: ['Aplicamos medidas proporcionadas al riesgo, como control de acceso, separación entre organizaciones, registros, cifrado de transporte y revocación de sesiones. Ningún sistema ofrece seguridad absoluta.', 'Utiliza el correo de privacidad indicado en los datos del operador.'] }
        ]
      },
      terms: {
        title: 'Términos del servicio Tenebit', description: 'Reglas de uso del servicio de gestión de activos, personas, entregas y procedimientos.', sections: [
          { title: '1. Alcance', paragraphs: ['Tenebit es un servicio SaaS dirigido principalmente a empresas y otras organizaciones. Permite gestionar activos, personas, ubicaciones, entregas, devoluciones, procedimientos, auditorías e historial de actividad.', 'Los términos están disponibles gratuitamente antes del contrato y pueden guardarse o imprimirse.'] },
          { title: '2. Requisitos técnicos', bullets: ['navegador actualizado con JavaScript', 'internet y almacenamiento técnico del navegador', 'correo activo para recuperación y mensajes operativos', 'dispositivo y red adecuados gestionados por la organización'] },
          { title: '3. Cuenta y seguridad', paragraphs: ['El usuario proporciona datos correctos, protege sus credenciales, no comparte la cuenta y comunica cualquier sospecha de compromiso. La organización administra roles y elimina accesos innecesarios.', 'El operador puede bloquear temporalmente una cuenta o sesión por seguridad, protección de clientes o cumplimiento legal.'] },
          { title: '4. Contrato y terminación', paragraphs: ['El contrato se celebra al crear la organización y aceptar los términos o mediante un pedido separado. Precio, impuestos, límites y renovación se muestran antes de comprar.', 'El límite de activos indicado para un plan se aplica igualmente al número de personas, ubicaciones, procedimientos, licencias, equipos, perfiles de puesto y categorías creadas por la organización. Superar el límite requiere pasar a un plan superior.', 'Con independencia del plan, se aplican topes técnicos de uso razonable para proteger la estabilidad del servicio. Una categoría de activos puede definir como máximo 200 campos personalizados. El operador puede modificar estos topes actualizando estos términos.', 'La organización puede terminar según la cuenta o el contrato. El operador puede resolver por incumplimiento grave, impago, abuso o riesgo de seguridad respetando el preaviso aplicable.'] },
          { title: '5. Uso permitido', bullets: ['no introducir contenido ilícito ni vulnerar derechos ajenos', 'no eludir controles, probar vulnerabilidades sin permiso, interrumpir el servicio ni abusar de él', 'la organización debe tener base jurídica para los datos y facilitar los avisos necesarios', 'no intentar acceder a datos de otra organización'] },
          { title: '6. Contenido del cliente', paragraphs: ['La organización conserva sus derechos y autoriza el alojamiento, protección, copia y tratamiento técnico necesarios para prestar el servicio.', 'Tenebit no sustituye asesoramiento jurídico, laboral, fiscal ni de prevención. La organización responde de sus políticas, plazos y decisiones.'] },
          { title: '7. Disponibilidad, cambios y responsabilidad', paragraphs: ['El operador puede desarrollar funciones sin eliminar la finalidad principal del contrato. Puede haber interrupciones por mantenimiento, fallos, proveedores o fuerza mayor.', 'En la medida permitida, no responde por datos incorrectos, falta de permisos, servicios externos o uso contrario a la documentación. Prevalece la ley imperativa.'] },
          { title: '8. Reclamaciones', paragraphs: ['Envía la reclamación al soporte indicado, con organización, problema, fecha y solución esperada. Se responde sin demora indebida, normalmente en 14 días salvo norma o contrato distinto.'] },
          { title: '9. Cambios y ley aplicable', paragraphs: ['Los cambios importantes se anuncian con antelación. La ley y jurisdicción siguen el contrato y las normas imperativas, sin limitar derechos de consumidores cuando correspondan.'] }
        ]
      },
      cookies: {
        title: 'Cookies y almacenamiento del dispositivo', description: 'Cómo usa Tenebit el almacenamiento del navegador y cuándo se requiere consentimiento.', sections: [
          { title: '1. Alcance actual', paragraphs: ['Tenebit usa cookies técnicas, localStorage y sessionStorage para acceso, seguridad, funciones solicitadas y preferencias - siempre, sin pedir consentimiento. Con tu consentimiento, el servicio activa además Google Analytics para estadísticas de visitas del sitio público.'] },
          { title: '2. Mecanismos necesarios', bullets: ['cookie HttpOnly de renovación de sesión, Secure en producción', 'cookie de dispositivo de confianza cuando el usuario lo solicita para 2FA', 'cookies breves de correlación para acceso externo', 'sesión breve de enlaces públicos para entrega, devolución o auditoría', 'preferencias de idioma, vista, mensajes cerrados e interfaz temporal'] },
          { title: '3. Mecanismos analíticos (con consentimiento)', paragraphs: ['Google Analytics (Google LLC) establece sus propias cookies (entre otras, _ga, _ga_*) y envía a Google datos sobre las visitas al sitio público, como páginas vistas, ubicación aproximada y tipo de dispositivo. Google puede tratar los datos fuera del EEE conforme a un mecanismo permitido por el RGPD.', 'Google Analytics solo se activa después de pulsar "Aceptar" en el aviso de cookies, nunca antes.'] },
          { title: '4. Consentimiento', paragraphs: ['No se requiere consentimiento previo para mecanismos estrictamente necesarios para transmitir comunicaciones o prestar una función solicitada; estos siempre están activos.', 'Google Analytics es un mecanismo no esencial, por lo que el aviso de Tenebit muestra una elección real "Aceptar" / "Rechazar". Sin decisión, o al pulsar "Rechazar", Google Analytics no se activa. El consentimiento puede retirarse en cualquier momento desde la gestión de cookies descrita en la sección 5, con la misma facilidad con la que se otorgó.'] },
          { title: '5. Gestión del almacenamiento y del consentimiento', paragraphs: ['Puedes eliminar o bloquear cookies en el navegador. Esto puede cerrar la sesión, borrar preferencias o impedir funciones. Los proveedores de pagos o acceso social pueden usar su propio almacenamiento cuando actives esas integraciones.', 'Para cambiar una decisión anterior sobre Google Analytics, usa el botón "Gestionar el consentimiento de cookies" de esta página: el aviso de consentimiento volverá a aparecer.'] }
        ]
      }
    }
  },
  de: {
    ui: {
      home: 'Startseite', privacy: 'Datenschutzerklärung', terms: 'Nutzungsbedingungen', cookies: 'Cookies und Gerätespeicher', contact: 'Kontakt', contactPrompt: 'Geschäftliche Anfragen oder ein Problem, bei dem wir helfen können?', operator: 'Diensteanbieter', address: 'Anschrift', registration: 'Registerangaben', taxId: 'Steuer / USt-ID', effectiveDate: 'Gültig ab', version: 'Version', missingOperator: 'Ergänze vor der Veröffentlichung die Betreiberangaben in VITE_LEGAL_OPERATOR_*.', storageNotice: 'Tenebit verwendet technischen Speicher für Anmeldung und Sicherheit sowie, mit Ihrer Einwilligung, Google Analytics für Besuchsstatistiken.', storageNoticeDetails: 'Technischer Speicher ist immer aktiv. Google Analytics läuft erst nach Ihrer Einwilligung und kann jederzeit widerrufen werden. Details stehen im Cookie-Hinweis.', consentAccept: 'Akzeptieren', consentReject: 'Ablehnen', manageConsent: 'Cookie-Einwilligung verwalten', footerRights: 'Alle Rechte vorbehalten.'
    },
    documents: {
      privacy: {
        title: 'Datenschutzerklärung', description: 'Informationen zur Verarbeitung personenbezogener Daten in Tenebit.', sections: [
          { title: '1. Rollen', paragraphs: ['Der Tenebit-Betreiber ist Verantwortlicher für Konto-, Abrechnungs-, Sicherheits-, Kontakt- und öffentliche Webseitendaten.', 'Für Mitarbeiter-, Auftragnehmer-, Geräte-, Verfahrens- und sonstige von einer Organisation eingegebene Daten bleibt diese Organisation Verantwortlicher. Tenebit verarbeitet diese Daten in ihrem Auftrag.'] },
          { title: '2. Verarbeitete Daten', bullets: ['Kontodaten wie Name, E-Mail, Organisation, Rollen und Sprache', 'Organisations-, Abonnement-, Zahlungs- und Abrechnungsdaten', 'technische und Sicherheitsdaten einschließlich Sitzungskennungen, Anmeldeereignissen, erforderlicher IP-Adresse und Audit-Logs', 'Arbeitsbereichsinhalte einschließlich Personen, Assets, Übergaben, Rückgaben, Verfahren, Anhängen und Bestätigungen', 'Supportnachrichten und Korrespondenz'] },
          { title: '3. Zwecke und Rechtsgrundlagen', bullets: ['Vertragsschluss, Vertragserfüllung und Kontobetrieb', 'Sicherheit, Missbrauchsprävention und Rechtsansprüche auf Grundlage berechtigter Interessen', 'steuerliche, buchhalterische und andere gesetzliche Pflichten', 'Support und Kommunikation', 'Marketing nur mit gültiger Rechtsgrundlage und einfacher Abmeldung'] },
          { title: '4. Empfänger', paragraphs: ['Daten können Hosting-, E-Mail-, Sicherheits-, Zahlungs-, Support-, Rechts- und Buchhaltungsdienstleistern im erforderlichen Umfang anvertraut werden. Sie sind zu Vertraulichkeit und Datenschutz verpflichtet.', 'Behörden erhalten Daten, wenn dies gesetzlich oder durch eine bindende Anordnung erforderlich ist.'] },
          { title: '5. Drittlandübermittlungen', paragraphs: ['Bei Verarbeitung außerhalb des EWR verwenden wir einen nach DSGVO zulässigen Mechanismus, etwa einen Angemessenheitsbeschluss oder Standardvertragsklauseln mit zusätzlichen Schutzmaßnahmen.'] },
          { title: '6. Speicherdauer', paragraphs: ['Konto- und Organisationsdaten werden während der Leistungserbringung und anschließend für gesetzliche, abrechnungs- und anspruchsbezogene Fristen gespeichert. Arbeitsbereichsdaten folgen Vertrag, Aufbewahrungseinstellungen und Weisungen der Organisation. Sicherheitstoken verfallen oder werden nach Nutzung entfernt.'] },
          { title: '7. Rechte', bullets: ['Auskunft und Kopie', 'Berichtigung, Löschung oder Einschränkung', 'Widerspruch gegen Verarbeitung aufgrund berechtigter Interessen', 'Datenübertragbarkeit, soweit anwendbar', 'Widerruf einer Einwilligung', 'Beschwerde bei der zuständigen Aufsichtsbehörde'], paragraphs: ['Bei Daten, die ein Arbeitgeber oder eine andere Organisation eingetragen hat, wende dich zuerst an diese Organisation. Tenebit unterstützt sie.'] },
          { title: '8. Automatisierte Entscheidungen und Kinder', paragraphs: ['Tenebit trifft keine ausschließlich automatisierten Entscheidungen mit rechtlicher Wirkung. Der Dienst richtet sich an Organisationen und autorisierte berufliche Nutzer, nicht an Kinder.'] },
          { title: '9. Sicherheit und Kontakt', paragraphs: ['Wir setzen risikogerechte Maßnahmen ein, darunter Zugriffskontrollen, Mandantentrennung, Protokollierung, Transportverschlüsselung und Sitzungswiderruf. Absolute Sicherheit kann kein System garantieren.', 'Datenschutzanfragen sind an die in den Betreiberangaben genannte Adresse zu richten.'] }
        ]
      },
      terms: {
        title: 'Tenebit Nutzungsbedingungen', description: 'Regeln für den Dienst zur Verwaltung von Assets, Personen, Übergaben und Verfahren.', sections: [
          { title: '1. Leistungsumfang', paragraphs: ['Tenebit ist ein SaaS-Dienst vor allem für Unternehmen und andere Organisationen. Er unterstützt Verzeichnisse für Assets, Personen, Standorte, Übergaben, Rückgaben, Verfahren, Audits und Aktivitäten.', 'Die Bedingungen werden vor Vertragsschluss kostenlos bereitgestellt und können gespeichert oder ausgedruckt werden.'] },
          { title: '2. Technische Voraussetzungen', bullets: ['aktueller Browser mit JavaScript', 'Internet und technischer Browserspeicher', 'aktive E-Mail-Adresse für Wiederherstellung und Betriebsnachrichten', 'geeignetes Gerät und Netzwerk der Organisation'] },
          { title: '3. Konto und Sicherheit', paragraphs: ['Nutzer geben richtige Daten an, schützen Zugangsdaten, teilen Konten nicht und melden Verdachtsfälle. Die Organisation verwaltet Rollen und entfernt nicht mehr benötigte Zugriffe.', 'Der Betreiber darf Konten oder Sitzungen aus Sicherheitsgründen, zum Schutz anderer Kunden oder zur Erfüllung gesetzlicher Pflichten vorübergehend sperren.'] },
          { title: '4. Vertrag und Kündigung', paragraphs: ['Der Vertrag kommt mit Erstellung der Organisation und Annahme dieser Bedingungen oder durch eine gesonderte Bestellung zustande. Preise, Steuern, Grenzen und Verlängerung werden vor dem Kauf angezeigt.', 'Die für einen Plan angegebene Asset-Grenze gilt in gleicher Höhe für die Anzahl der Personen, Standorte, Prozeduren, Lizenzen, Teams, Stellenprofile und von der Organisation angelegten Kategorien, die im System gespeichert sind. Eine Überschreitung erfordert ein Upgrade auf einen höheren Plan.', 'Unabhängig vom Plan gelten technische Fair-Use-Obergrenzen zum Schutz der Dienststabilität. Eine Asset-Kategorie kann höchstens 200 benutzerdefinierte Felder definieren. Der Betreiber kann diese Obergrenzen durch Aktualisierung dieser Bedingungen ändern.', 'Die Organisation kann nach Konto oder Vertrag kündigen. Der Betreiber kann bei wesentlichem Verstoß, Zahlungsverzug, Missbrauch oder Sicherheitsrisiko unter Beachtung der anwendbaren Frist kündigen.'] },
          { title: '5. Zulässige Nutzung', bullets: ['keine rechtswidrigen Inhalte oder Verletzung fremder Rechte', 'keine Umgehung von Schutzmaßnahmen, Schwachstellentests ohne Erlaubnis, Störung oder missbräuchliche Nutzung', 'die Organisation benötigt eine Rechtsgrundlage für eingetragene Daten und muss Pflichtinformationen bereitstellen', 'kein Zugriffsversuch auf Daten anderer Organisationen'] },
          { title: '6. Kundendaten', paragraphs: ['Die Organisation behält ihre Rechte und gestattet die für Hosting, Schutz, Sicherung und technische Verarbeitung erforderliche Nutzung.', 'Tenebit ersetzt keine Rechts-, Personal-, Steuer- oder Arbeitsschutzberatung. Die Organisation verantwortet Richtlinien, Fristen und Entscheidungen.'] },
          { title: '7. Verfügbarkeit, Änderungen und Haftung', paragraphs: ['Der Betreiber darf Funktionen weiterentwickeln, ohne den Vertragskern zu beseitigen. Unterbrechungen können durch Wartung, Ausfälle, Anbieter oder höhere Gewalt entstehen.', 'Soweit gesetzlich zulässig, besteht keine Haftung für falsche Kundendaten, fehlende Berechtigungen, externe Dienste oder dokumentationswidrige Nutzung. Zwingendes Recht geht vor.'] },
          { title: '8. Beschwerden', paragraphs: ['Beschwerden sind mit Organisation, Problem, Datum und gewünschter Lösung an den Support zu senden. Die Antwort erfolgt unverzüglich, grundsätzlich binnen 14 Tagen, sofern Gesetz oder Vertrag nichts anderes bestimmen.'] },
          { title: '9. Änderungen und Recht', paragraphs: ['Wesentliche Änderungen werden vorab angekündigt. Recht und Gerichtsstand folgen Vertrag und zwingenden Regeln. Verbraucherrechte werden nicht eingeschränkt, soweit sie anwendbar sind.'] }
        ]
      },
      cookies: {
        title: 'Cookies und Gerätespeicher', description: 'Wie Tenebit Browserspeicher nutzt und wann eine Einwilligung erforderlich ist.', sections: [
          { title: '1. Aktueller Umfang', paragraphs: ['Tenebit nutzt technische Cookies, localStorage und sessionStorage für Anmeldung, Sicherheit, gewünschte Funktionen und Einstellungen - immer, ohne um Einwilligung zu bitten. Mit Ihrer Einwilligung setzt der Dienst zusätzlich Google Analytics für Besuchsstatistiken der öffentlichen Website ein.'] },
          { title: '2. Erforderliche Mechanismen', bullets: ['HttpOnly-Cookie zur Sitzungserneuerung, in Produktion mit Secure', 'Cookie für ein bewusst als vertrauenswürdig gespeichertes 2FA-Gerät', 'kurzlebige Korrelations-Cookies für externe Anmeldung', 'kurzlebige öffentliche Sitzung für Übergabe, Rückgabe oder Audit', 'Sprache, Listenansicht, geschlossene Hinweise und temporäre Oberflächeneinstellungen'] },
          { title: '3. Analysemechanismen (mit Einwilligung)', paragraphs: ['Google Analytics (Google LLC) setzt eigene Cookies (u. a. _ga, _ga_*) und übermittelt Google Daten über Besuche der öffentlichen Website, etwa aufgerufene Seiten, ungefähren Standort und Gerätetyp. Google kann die Daten außerhalb des EWR im Rahmen eines DSGVO-zulässigen Mechanismus verarbeiten.', 'Google Analytics startet erst, nachdem Sie im Cookie-Hinweis auf „Akzeptieren“ geklickt haben - nie vorher.'] },
          { title: '4. Einwilligung', paragraphs: ['Für Speicher, der strikt zur Übertragung oder Bereitstellung einer ausdrücklich gewünschten Funktion erforderlich ist, ist keine vorherige Einwilligung nötig - dieser läuft immer.', 'Google Analytics ist ein nicht notwendiger Mechanismus, daher zeigt der Tenebit-Hinweis eine echte Wahl „Akzeptieren“ / „Ablehnen“. Ohne Entscheidung oder bei Klick auf „Ablehnen“ startet Google Analytics nicht. Die Einwilligung kann jederzeit über die in Abschnitt 5 beschriebene Cookie-Verwaltung widerrufen werden - ebenso einfach, wie sie erteilt wurde.'] },
          { title: '5. Verwaltung von Speicher und Einwilligung', paragraphs: ['Cookies können im Browser gelöscht oder blockiert werden. Dadurch können Anmeldung, Sprache, Ansichten oder Funktionen verloren gehen. Zahlungs- oder Social-Login-Anbieter können bei Aktivierung eigene Speichermechanismen verwenden.', 'Um eine frühere Google-Analytics-Entscheidung zu ändern, nutzen Sie die Schaltfläche „Cookie-Einwilligung verwalten“ auf dieser Seite - der Einwilligungshinweis erscheint erneut.'] }
        ]
      }
    }
  },
  it: {
    ui: {
      home: 'Home', privacy: 'Informativa sulla privacy', terms: 'Termini di servizio', cookies: 'Cookie e archiviazione sul dispositivo', contact: 'Contatti', contactPrompt: 'Richieste commerciali o un problema che possiamo aiutarti a risolvere?', operator: 'Gestore del servizio', address: 'Indirizzo', registration: 'Dati di registrazione', taxId: 'Partita IVA / Codice fiscale', effectiveDate: 'In vigore dal', version: 'Versione', missingOperator: 'Completa i dati del gestore in VITE_LEGAL_OPERATOR_* prima della pubblicazione in produzione.', storageNotice: "Tenebit utilizza l'archiviazione tecnica necessaria per l'accesso e la sicurezza e, con il tuo consenso, Google Analytics per le statistiche di visita.", storageNoticeDetails: "L'archiviazione tecnica è sempre attiva. Google Analytics si attiva solo dopo il tuo consenso e puoi revocarlo in qualsiasi momento. Consulta l'informativa sui cookie per i dettagli.", consentAccept: 'Accetto', consentReject: 'Rifiuto', manageConsent: 'Gestisci il consenso ai cookie', footerRights: 'Tutti i diritti riservati.'
    },
      documents: {
      privacy: {
        title: 'Informativa sulla privacy', description: 'Informazioni sul trattamento dei dati personali in Tenebit.', sections: [
          { title: '1. Ruoli nel trattamento', paragraphs: ['Il gestore di Tenebit è titolare del trattamento per i dati relativi ad account, fatturazione, sicurezza del servizio, contatti e sito pubblico.', "Per i dati di dipendenti, collaboratori, attrezzature, procedure e altri contenuti dello spazio di lavoro inseriti da un'organizzazione, tale organizzazione resta titolare del trattamento e Tenebit agisce come suo responsabile, su istruzioni documentate."] },
          { title: '2. Dati trattati', bullets: ["dati dell'account, quali nome, nome visualizzato, e-mail, organizzazione, ruoli e preferenze di lingua", 'dati di organizzazione, abbonamento, pagamento e fatturazione', "dati tecnici e di sicurezza, inclusi identificativi di sessione, eventi di accesso, indirizzo IP ove necessario per la protezione del servizio e registri di controllo", 'contenuti dello spazio di lavoro del cliente, inclusi persone, asset, consegne, restituzioni, procedure, allegati e conferme', 'messaggi di assistenza e corrispondenza'] },
          { title: '3. Finalità e basi giuridiche', bullets: ["conclusione ed esecuzione del contratto e gestione dell'account", 'sicurezza, prevenzione degli abusi e tutela in giudizio sulla base del legittimo interesse', 'obblighi fiscali, contabili e altri obblighi di legge', "gestione dell'assistenza e della corrispondenza", 'marketing solo in presenza di una valida base giuridica e con facile possibilità di opposizione'] },
          { title: '4. Destinatari', paragraphs: ['I dati possono essere affidati a fornitori di hosting, posta elettronica, monitoraggio della sicurezza, pagamenti, supporto tecnico, servizi legali e contabili. Ricevono soltanto i dati necessari ai loro compiti e sono vincolati da adeguati obblighi di riservatezza e protezione dei dati.', 'I dati possono essere comunicati alle autorità pubbliche quando la legge o una richiesta vincolante lo impongono.'] },
          { title: '5. Trasferimenti fuori dallo SEE', paragraphs: ['Quando un fornitore tratta dati al di fuori dello Spazio economico europeo, utilizziamo un meccanismo ammesso dal GDPR, come una decisione di adeguatezza oppure clausole contrattuali tipo con garanzie supplementari ove necessario.'] },
          { title: '6. Conservazione', paragraphs: ["I dati di account e organizzazione sono conservati per la durata del servizio e successivamente per i periodi richiesti da legge, adempimenti e tutela dei diritti. I contenuti dello spazio di lavoro seguono il contratto, le impostazioni di conservazione e le istruzioni dell'organizzazione. I token di sicurezza scadono o vengono rimossi dopo l'uso. I backup sono sovrascritti nel normale ciclo tecnico."] },
          { title: '7. I tuoi diritti', bullets: ['accesso e copia dei dati', 'rettifica, cancellazione o limitazione', 'opposizione al trattamento basato sul legittimo interesse', 'portabilità dei dati ove applicabile', 'revoca del consenso, senza pregiudicare la liceità del trattamento precedente', "reclamo all'autorità di controllo competente"], paragraphs: ["Per i dati inseriti da un datore di lavoro o da un'altra organizzazione, contatta prima quell'organizzazione. Tenebit la assiste nel rispondere alle richieste degli interessati."] },
          { title: '8. Decisioni automatizzate e minori', paragraphs: ['Tenebit non adotta decisioni che producano effetti giuridici basate unicamente su un trattamento automatizzato. Il servizio è destinato a organizzazioni e utenti professionali autorizzati, non a minori.'] },
          { title: '9. Sicurezza e contatti', paragraphs: ['Applichiamo misure tecniche e organizzative proporzionate al rischio, tra cui controlli degli accessi, separazione dei tenant, registrazione degli eventi, cifratura in transito e revoca delle sessioni. Nessun sistema può garantire una sicurezza assoluta.', 'Per le richieste in materia di privacy utilizza l’indirizzo indicato nei dati del gestore.'] }
        ]
      },
      terms: {
        title: 'Termini di servizio di Tenebit', description: 'Regole di utilizzo del servizio di gestione di asset, persone, consegne e procedure.', sections: [
          { title: '1. Ambito del servizio', paragraphs: ['Tenebit è un servizio SaaS destinato in via principale a imprese e altre organizzazioni. Supporta la registrazione di asset, persone, ubicazioni, consegne, restituzioni, procedure, verifiche e attività.', 'I presenti termini sono disponibili gratuitamente prima della conclusione del contratto e possono essere salvati o stampati.'] },
          { title: '2. Requisiti tecnici', bullets: ['un browser aggiornato con JavaScript abilitato', "accesso a internet e consenso all'uso dell'archiviazione tecnica del browser", "un indirizzo e-mail attivo per il recupero dell'accesso e le comunicazioni operative", "un dispositivo e una configurazione di rete adeguati, mantenuti dall'organizzazione"] },
          { title: '3. Sicurezza dell’account', paragraphs: ["Gli utenti devono fornire dati corretti, proteggere le credenziali, non condividere gli account e segnalare ogni sospetta compromissione. L'organizzazione gestisce i ruoli e revoca tempestivamente gli accessi non più necessari.", "Il gestore può bloccare temporaneamente un account o una sessione ove necessario per la sicurezza, la tutela di altri clienti o l'adempimento di obblighi di legge."] },
          { title: '4. Conclusione e cessazione del contratto', paragraphs: ["Il contratto si conclude con la creazione di un'organizzazione e l'accettazione dei presenti termini, oppure in base a un ordine separato. Prezzi dei piani a pagamento, imposte, limiti e regole di rinnovo sono indicati prima dell'acquisto.", "Il limite di asset indicato per un piano si applica nella stessa misura al numero di persone, ubicazioni, procedure, licenze, team, profili professionali e categorie create dall'organizzazione e conservate nel sistema per tale organizzazione. Il superamento del limite richiede il passaggio a un piano superiore.", 'Indipendentemente dal piano si applicano soglie tecniche di uso corretto, a tutela della stabilità del servizio. Una categoria di asset può definire al massimo 200 campi personalizzati. Il gestore può modificare tali soglie aggiornando i presenti termini.', "L'organizzazione può recedere secondo le impostazioni dell'account o il contratto. Il gestore può risolvere il contratto per inadempimento sostanziale, mancato pagamento, abuso o rischio per la sicurezza, nel rispetto del preavviso previsto dalla legge o dal contratto."] },
          { title: '5. Uso consentito', bullets: ['non è consentito inserire contenuti illeciti né violare diritti di terzi', "non è consentito eludere le misure di sicurezza, testare vulnerabilità senza autorizzazione scritta, interferire con il servizio o utilizzarlo in modo abusivo", "l'organizzazione deve disporre di una base giuridica per i dati dello spazio di lavoro e fornire le informative richieste", "non è consentito tentare di accedere ai dati di un'altra organizzazione"] },
          { title: '6. Contenuti del cliente', paragraphs: ["L'organizzazione conserva i propri diritti sui dati e autorizza il gestore a ospitarli, proteggerli, sottoporli a backup e trattarli tecnicamente per erogare il servizio.", "Tenebit non costituisce consulenza legale, del lavoro, fiscale o in materia di sicurezza sul lavoro. L'organizzazione è responsabile delle proprie politiche, scadenze e decisioni."] },
          { title: '7. Disponibilità, modifiche e responsabilità', paragraphs: ['Il gestore può sviluppare e modificare le funzionalità senza privare il contratto della sua finalità essenziale. Le interruzioni possono derivare da manutenzione, guasti, fornitori o cause di forza maggiore. Gli interventi programmati rilevanti sono comunicati nei limiti del ragionevole.', "Nei limiti consentiti dalla legge, il gestore non risponde di dati inseriti in modo errato dal cliente, autorizzazioni mancanti, servizi di terzi o utilizzi difformi dalla documentazione. Restano ferme le norme imperative."] },
          { title: '8. Reclami', paragraphs: ["Invia i reclami all'indirizzo di assistenza indicato nei dati del gestore, precisando organizzazione, problema, data e soluzione richiesta. Il gestore risponde senza indebito ritardo, di norma entro 14 giorni, salvo diversa previsione di legge o di contratto."] },
          { title: '9. Modifiche e legge applicabile', paragraphs: ['Le modifiche sostanziali sono annunciate in anticipo, affinché i clienti possano prenderne visione e, ove previsto, recedere. Legge applicabile e foro competente seguono il contratto e le norme imperative. Le tutele dei consumatori non sono limitate ove applicabili.'] }
        ]
      },
      cookies: {
        title: 'Cookie e archiviazione sul dispositivo', description: "Come Tenebit utilizza l'archiviazione del browser e quando è necessario il consenso.", sections: [
          { title: '1. Ambito attuale', paragraphs: ["Tenebit utilizza cookie tecnici, localStorage e sessionStorage per l'accesso, la sicurezza della sessione, le funzioni richieste e le preferenze di interfaccia - sempre, senza chiedere il consenso. Con il tuo consenso, il servizio attiva inoltre Google Analytics per le statistiche di visita del sito pubblico."] },
          { title: '2. Meccanismi necessari', bullets: ['un cookie HttpOnly di sessione per il rinnovo, con attributo Secure attivo in produzione', "un cookie di dispositivo attendibile quando l'utente sceglie espressamente di ricordare un dispositivo per la 2FA", 'cookie di correlazione di breve durata per l’accesso tramite provider esterni', 'una sessione di breve durata per i link pubblici di conferma di consegna, restituzione o verifica', 'preferenze di lingua, di visualizzazione degli elenchi, messaggi già chiusi e impostazioni temporanee di interfaccia'] },
          { title: '3. Meccanismi analitici (con consenso)', paragraphs: ['Google Analytics (Google LLC) imposta i propri cookie (tra cui _ga, _ga_*) e invia a Google dati sulle visite al sito pubblico, come le pagine visualizzate, la posizione approssimativa e il tipo di dispositivo. Google può trattare i dati al di fuori dello SEE nell’ambito di un meccanismo consentito dal GDPR.', 'Google Analytics si attiva solo dopo aver cliccato su "Accetto" nell’avviso sui cookie, mai prima.'] },
          { title: '4. Consenso', paragraphs: ["Non è richiesto il consenso preventivo per l'archiviazione strettamente necessaria a trasmettere una comunicazione o a fornire una funzione espressamente richiesta dall'utente: questa è sempre attiva.", 'Google Analytics è un meccanismo non essenziale, perciò l’avviso di Tenebit presenta una scelta reale "Accetto" / "Rifiuto". Senza una decisione, o cliccando su "Rifiuto", Google Analytics non si attiva. Il consenso può essere revocato in qualsiasi momento dalla gestione dei cookie descritta nella sezione 5, con la stessa facilità con cui è stato concesso.'] },
          { title: "5. Gestione dell'archiviazione e del consenso", paragraphs: ["Puoi rimuovere o bloccare i cookie nelle impostazioni del browser. La rimozione dei dati tecnici può disconnetterti, azzerare le preferenze di lingua o di visualizzazione e disattivare alcune funzioni. Anche i fornitori di pagamento e di accesso tramite social possono utilizzare l'archiviazione secondo le proprie informative quando attivi tali integrazioni.", 'Per modificare una decisione precedente su Google Analytics, usa il pulsante "Gestisci il consenso ai cookie" in questa pagina: l’avviso di consenso ricomparirà.'] }
        ]
      }
      }
  },
  fr: {
    ui: {
      home: 'Accueil', privacy: 'Politique de confidentialité', terms: 'Conditions générales', cookies: "Cookies et stockage sur l'appareil", contact: 'Contact', contactPrompt: "Une demande commerciale ou un problème qu'on peut vous aider à résoudre ?", operator: 'Exploitant du service', address: 'Adresse', registration: "Données d'immatriculation", taxId: 'Numéro de TVA / SIRET', effectiveDate: 'En vigueur à partir du', version: 'Version', missingOperator: "Complétez les données de l'exploitant dans VITE_LEGAL_OPERATOR_* avant la mise en production.", storageNotice: "Tenebit utilise le stockage technique nécessaire à la connexion et à la sécurité et, avec votre consentement, Google Analytics pour des statistiques de visite.", storageNoticeDetails: "Le stockage technique est toujours actif. Google Analytics ne démarre qu'après votre consentement, que vous pouvez retirer à tout moment. Consultez la notice sur les cookies pour plus de détails.", consentAccept: 'Accepter', consentReject: 'Refuser', manageConsent: 'Gérer le consentement aux cookies', footerRights: 'Tous droits réservés.'
    },
      documents: {
      privacy: {
        title: 'Politique de confidentialité', description: 'Informations sur le traitement des données personnelles dans Tenebit.', sections: [
          { title: '1. Rôles dans le traitement', paragraphs: ["L'exploitant de Tenebit est responsable du traitement des données relatives au compte, à la facturation, à la sécurité du service, aux contacts et au site public.", "Pour les données de collaborateurs, prestataires, matériels, procédures et autres contenus d'espace de travail saisis par une organisation, cette organisation demeure responsable du traitement et Tenebit agit en qualité de sous-traitant, sur instructions documentées."] },
          { title: '2. Données traitées', bullets: ['données de compte, telles que nom, nom affiché, e-mail, organisation, rôles et préférences linguistiques', "données d'organisation, d'abonnement, de paiement et de facturation", "données techniques et de sécurité, y compris identifiants de session, événements de connexion, adresse IP lorsque nécessaire à la protection du service et journaux d'audit", "contenus de l'espace de travail du client, y compris personnes, actifs, remises, restitutions, procédures, pièces jointes et confirmations", "messages d'assistance et correspondance"] },
          { title: '3. Finalités et bases légales', bullets: ['conclusion et exécution du contrat et gestion du compte', 'sécurité, prévention des abus et défense de droits en justice, sur la base de l’intérêt légitime', 'obligations fiscales, comptables et autres obligations légales', "traitement de l'assistance et de la correspondance", "prospection uniquement en présence d'une base légale valable et avec une possibilité simple de s'y opposer"] },
          { title: '4. Destinataires', paragraphs: ["Les données peuvent être confiées à des prestataires d'hébergement, de messagerie, de supervision de la sécurité, de paiement, d'assistance technique, juridiques et comptables. Ils ne reçoivent que les données nécessaires à leurs missions et sont tenus à des obligations appropriées de confidentialité et de protection des données.", "Les données peuvent être communiquées aux autorités publiques lorsque la loi ou une demande contraignante l'exige."] },
          { title: "5. Transferts hors de l'EEE", paragraphs: ["Lorsqu'un prestataire traite des données en dehors de l'Espace économique européen, nous recourons à un mécanisme admis par le RGPD, tel qu'une décision d'adéquation ou des clauses contractuelles types, assorties de garanties supplémentaires si nécessaire."] },
          { title: '6. Conservation', paragraphs: ["Les données de compte et d'organisation sont conservées pendant la fourniture du service, puis pendant les durées imposées par la loi, les règlements de comptes et la défense de droits. Les contenus de l'espace de travail suivent le contrat, les réglages de conservation et les instructions de l'organisation. Les jetons de sécurité expirent ou sont supprimés après usage. Les sauvegardes sont écrasées selon le cycle technique habituel."] },
          { title: '7. Vos droits', bullets: ['accès et copie des données', 'rectification, effacement ou limitation', "opposition au traitement fondé sur l'intérêt légitime", 'portabilité des données le cas échéant', 'retrait du consentement, sans remettre en cause la licéité du traitement antérieur', "réclamation auprès de l'autorité de contrôle compétente"], paragraphs: ["Pour les données saisies par un employeur ou une autre organisation, contactez d'abord cette organisation. Tenebit l'assiste dans le traitement des demandes des personnes concernées."] },
          { title: '8. Décisions automatisées et mineurs', paragraphs: ["Tenebit ne prend pas de décision produisant des effets juridiques fondée exclusivement sur un traitement automatisé. Le service s'adresse à des organisations et à des utilisateurs professionnels autorisés, non à des mineurs."] },
          { title: '9. Sécurité et contact', paragraphs: ['Nous appliquons des mesures techniques et organisationnelles proportionnées au risque, notamment contrôles d’accès, cloisonnement des locataires, journalisation des événements, chiffrement en transit et révocation des sessions. Aucun système ne peut garantir une sécurité absolue.', "Pour toute demande en matière de confidentialité, utilisez l'adresse indiquée dans les données de l'exploitant."] }
        ]
      },
      terms: {
        title: 'Conditions générales de Tenebit', description: 'Règles d’utilisation du service de gestion des actifs, des personnes, des remises et des procédures.', sections: [
          { title: '1. Objet du service', paragraphs: ['Tenebit est un service SaaS destiné principalement aux entreprises et autres organisations. Il permet le suivi des actifs, des personnes, des emplacements, des remises, des restitutions, des procédures, des audits et des activités.', "Les présentes conditions sont disponibles gratuitement avant la conclusion du contrat et peuvent être enregistrées ou imprimées."] },
          { title: '2. Prérequis techniques', bullets: ['un navigateur à jour avec JavaScript activé', "un accès à internet et l'autorisation d'utiliser le stockage technique du navigateur", "une adresse e-mail active pour la récupération de l'accès et les messages opérationnels", "un appareil et une configuration réseau adaptés, maintenus par l'organisation"] },
          { title: '3. Sécurité du compte', paragraphs: ["Les utilisateurs doivent fournir des données exactes, protéger leurs identifiants, ne pas partager de compte et signaler toute compromission suspectée. L'organisation gère les rôles et retire sans délai les accès devenus inutiles.", "L'exploitant peut bloquer temporairement un compte ou une session lorsque cela est nécessaire à la sécurité, à la protection d'autres clients ou au respect de la loi."] },
          { title: '4. Conclusion et fin du contrat', paragraphs: ["Le contrat est conclu lors de la création d'une organisation et de l'acceptation des présentes conditions, ou en vertu d'une commande distincte. Les prix des forfaits payants, les taxes, les limites et les règles de renouvellement sont indiqués avant l'achat.", "La limite d'actifs affichée pour un forfait s'applique dans la même mesure au nombre de personnes, d'emplacements, de procédures, de licences, d'équipes, de profils de poste et de catégories créées par l'organisation et enregistrés dans le système pour celle-ci. Le dépassement de la limite impose le passage à un forfait supérieur.", "Quel que soit le forfait, des plafonds techniques d'usage raisonnable s'appliquent afin de préserver la stabilité du service. Une catégorie d'actifs peut définir au maximum 200 champs personnalisés. L'exploitant peut modifier ces plafonds en mettant à jour les présentes conditions.", "L'organisation peut résilier selon les réglages du compte ou le contrat. L'exploitant peut résilier en cas de manquement substantiel, de défaut de paiement, d'abus ou de risque pour la sécurité, dans le respect du préavis prévu par la loi ou le contrat."] },
          { title: '5. Usage autorisé', bullets: ['ne pas publier de contenu illicite ni porter atteinte aux droits de tiers', "ne pas contourner les protections, tester des vulnérabilités sans autorisation écrite, perturber le service ni l'utiliser à des fins abusives", "l'organisation doit disposer d'une base légale pour les données de l'espace de travail et fournir les informations requises", "ne pas tenter d'accéder aux données d'une autre organisation"] },
          { title: '6. Contenus du client', paragraphs: ["L'organisation conserve ses droits sur ses données et autorise l'exploitant à les héberger, les sécuriser, les sauvegarder et les traiter techniquement pour fournir le service.", "Tenebit ne constitue pas un conseil juridique, en ressources humaines, fiscal ou en santé et sécurité au travail. L'organisation est responsable de ses politiques, de ses échéances et de ses décisions."] },
          { title: '7. Disponibilité, évolutions et responsabilité', paragraphs: ["L'exploitant peut faire évoluer et modifier les fonctionnalités sans priver le contrat de son objet essentiel. Des interruptions peuvent résulter de la maintenance, de pannes, de prestataires ou d'un cas de force majeure. Les travaux planifiés importants sont annoncés dans la mesure du raisonnable.", "Dans les limites permises par la loi, l'exploitant n'est pas responsable des données erronées du client, des autorisations manquantes, des services de tiers ni d'une utilisation contraire à la documentation. Les dispositions impératives demeurent applicables."] },
          { title: '8. Réclamations', paragraphs: ["Adressez vos réclamations à l'adresse d'assistance indiquée dans les données de l'exploitant, en précisant l'organisation, le problème, la date et la solution demandée. L'exploitant répond sans retard injustifié, en principe sous 14 jours, sauf disposition légale ou contractuelle contraire."] },
          { title: '9. Modifications et droit applicable', paragraphs: ['Les modifications substantielles sont annoncées à l’avance afin que les clients puissent en prendre connaissance et, le cas échéant, résilier. Le droit applicable et la juridiction compétente suivent le contrat et les règles impératives. Les protections des consommateurs ne sont pas limitées lorsqu’elles s’appliquent.'] }
        ]
      },
      cookies: {
        title: "Cookies et stockage sur l'appareil", description: 'Comment Tenebit utilise le stockage du navigateur et quand le consentement est requis.', sections: [
          { title: '1. Périmètre actuel', paragraphs: ["Tenebit utilise des cookies techniques, localStorage et sessionStorage pour la connexion, la sécurité de la session, les fonctions demandées et les préférences d'interface - toujours, sans demander de consentement. Avec votre consentement, le service active en plus Google Analytics pour des statistiques de visite du site public."] },
          { title: '2. Mécanismes nécessaires', bullets: ["un cookie HttpOnly de session de renouvellement, avec l'attribut Secure activé en production", "un cookie d'appareil de confiance lorsque l'utilisateur choisit expressément de mémoriser un appareil pour la 2FA", 'des cookies de corrélation de courte durée pour la connexion via des fournisseurs externes', "une session de courte durée pour les liens publics de confirmation de remise, de restitution ou d'audit", "préférences de langue, d'affichage des listes, messages déjà fermés et réglages temporaires d'interface"] },
          { title: '3. Mécanismes analytiques (avec consentement)', paragraphs: ["Google Analytics (Google LLC) dépose ses propres cookies (notamment _ga, _ga_*) et transmet à Google des données sur les visites du site public, telles que les pages consultées, la localisation approximative et le type d'appareil. Google peut traiter les données hors de l'EEE selon un mécanisme autorisé par le RGPD.", 'Google Analytics ne démarre qu\'après avoir cliqué sur "Accepter" dans l\'avis relatif aux cookies, jamais avant.'] },
          { title: '4. Consentement', paragraphs: ["Le consentement préalable n'est pas requis pour le stockage strictement nécessaire à la transmission d'une communication ou à la fourniture d'une fonction expressément demandée par l'utilisateur - celui-ci s'applique toujours.", 'Google Analytics est un mécanisme non essentiel : l\'avis de Tenebit présente donc un choix réel "Accepter" / "Refuser". En l\'absence de décision, ou en cliquant sur "Refuser", Google Analytics ne démarre pas. Le consentement peut être retiré à tout moment via la gestion des cookies décrite à la section 5, aussi facilement qu'il a été donné.'] },
          { title: '5. Gestion du stockage et du consentement', paragraphs: ["Vous pouvez supprimer ou bloquer les cookies dans les réglages de votre navigateur. La suppression des données techniques peut vous déconnecter, effacer vos préférences de langue ou d'affichage et désactiver certaines fonctions. Les prestataires de paiement et de connexion via les réseaux sociaux peuvent également utiliser le stockage selon leurs propres informations lorsque vous activez ces intégrations.", 'Pour modifier une décision antérieure sur Google Analytics, utilisez le bouton "Gérer le consentement aux cookies" sur cette page : l\'avis de consentement réapparaîtra.'] }
        ]
      }
      }
  }
};

const fallbackDocuments = legalEntries.en.documents!;

/// Zwraca komplet treści prawnych dla języka: własne etykiety UI i własne dokumenty, a gdy dokumentów
/// jeszcze nie ma - angielskie. `documentsLanguage` mówi, w jakim języku dokumenty faktycznie są.
export function legalContentFor(language: Language): LegalLanguageContent & { documentsLanguage: Language } {
  const entry = legalEntries[language];
  return {
    ui: entry.ui,
    documents: entry.documents ?? fallbackDocuments,
    documentsLanguage: entry.documents ? language : 'en'
  };
}
