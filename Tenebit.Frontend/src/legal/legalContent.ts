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
  operator: string;
  address: string;
  registration: string;
  taxId: string;
  effectiveDate: string;
  version: string;
  missingOperator: string;
  storageNotice: string;
  storageNoticeDetails: string;
  understand: string;
  footerRights: string;
};

type LegalLanguageContent = {
  ui: LegalUi;
  documents: Record<LegalDocumentKind, LegalDocument>;
};

export const legalContent: Record<Language, LegalLanguageContent> = {
  pl: {
    ui: {
      home: 'Strona główna',
      privacy: 'Polityka prywatności',
      terms: 'Regulamin',
      cookies: 'Cookies i pamięć urządzenia',
      contact: 'Kontakt',
      operator: 'Operator usługi',
      address: 'Adres',
      registration: 'Dane rejestrowe',
      taxId: 'NIP / VAT',
      effectiveDate: 'Obowiązuje od',
      version: 'Wersja',
      missingOperator: 'Przed publikacją uzupełnij dane operatora w zmiennych VITE_LEGAL_OPERATOR_*.',
      storageNotice: 'Tenebit używa wyłącznie technicznej pamięci potrzebnej do logowania, bezpieczeństwa i zapamiętania ustawień.',
      storageNoticeDetails: 'Obecna wersja usługi nie korzysta z narzędzi reklamowych ani analitycznych. Szczegóły znajdziesz w informacji o cookies.',
      understand: 'Rozumiem',
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
              'Obecna wersja Tenebit nie korzysta z narzędzi reklamowych ani analitycznych. Usługa używa technicznych cookies oraz pamięci localStorage i sessionStorage potrzebnych do logowania, zabezpieczania sesji, realizowania wybranej funkcji i zapamiętania ustawień użytkownika.'
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
            title: '3. Zgoda',
            paragraphs: [
              'Dla mechanizmów ściśle potrzebnych do transmisji komunikatu lub dostarczenia funkcji wyraźnie żądanej przez użytkownika uprzednia zgoda nie jest wymagana. Dlatego komunikat Tenebit nie udaje banera zgody i nie zawiera przycisku zaakceptowania reklamy lub analityki, których usługa obecnie nie używa.',
              'Jeżeli w przyszłości zostaną dodane analityka, reklama, profilowanie albo inne niekonieczne mechanizmy, nie mogą być uruchamiane przed dobrowolną i granularną zgodą. Wycofanie zgody musi być równie łatwe jak jej udzielenie.'
            ]
          },
          {
            title: '4. Zarządzanie pamięcią',
            paragraphs: [
              'Cookies można usunąć lub zablokować w ustawieniach przeglądarki. Usunięcie technicznych danych może wylogować użytkownika, usunąć zapamiętany język lub widok oraz uniemożliwić działanie niektórych funkcji. Dane z usług płatniczych lub logowania społecznościowego podlegają również informacjom ich dostawców, gdy użytkownik uruchomi daną integrację.'
            ]
          }
        ]
      }
    }
  },
  en: {
    ui: {
      home: 'Home', privacy: 'Privacy policy', terms: 'Terms of service', cookies: 'Cookies and device storage', contact: 'Contact', operator: 'Service operator', address: 'Address', registration: 'Registration details', taxId: 'Tax / VAT ID', effectiveDate: 'Effective from', version: 'Version', missingOperator: 'Complete the operator details in VITE_LEGAL_OPERATOR_* before production release.', storageNotice: 'Tenebit uses only technical storage needed for sign-in, security and saving your settings.', storageNoticeDetails: 'The current version of the service does not use advertising or analytics tools. See the cookies notice for details.', understand: 'Got it', footerRights: 'All rights reserved.'
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
          { title: '4. Contract and termination', paragraphs: ['The contract is concluded when an organization is created and these terms are accepted, or under a separate order. Paid plan prices, taxes, limits and renewal rules are shown before purchase.', 'The organization may terminate according to account settings or the contract. The operator may terminate for material breach, non-payment, abuse or security risk, subject to the notice required by law or contract.'] },
          { title: '5. Acceptable use', bullets: ['do not submit unlawful content or infringe third-party rights', 'do not bypass safeguards, test vulnerabilities without written permission, disrupt the service or use it for abuse', 'the organization must have a lawful basis for workspace data and provide required notices', 'do not attempt to access another organization data'] },
          { title: '6. Customer content', paragraphs: ['The organization retains its rights to customer data and authorizes the operator to host, secure, back up and technically process it to provide the service.', 'Tenebit is not legal, HR, tax or occupational safety advice. The organization is responsible for its policies, deadlines and decisions.'] },
          { title: '7. Availability, changes and liability', paragraphs: ['The operator may develop and modify features without removing the core purpose of the contract. Interruptions may result from maintenance, failures, suppliers or force majeure. Material planned work is communicated where reasonably possible.', 'To the extent permitted by law, the operator is not liable for incorrect customer data, missing permissions, third-party services or use contrary to documentation. Mandatory law prevails.'] },
          { title: '8. Complaints', paragraphs: ['Send complaints to the support address in the operator details, with the organization, issue, date and requested resolution. The operator responds without undue delay, generally within 14 days unless a specific law or contract provides otherwise.'] },
          { title: '9. Changes and governing law', paragraphs: ['Material changes are announced in advance so customers can review them and, where required, terminate. Governing law and jurisdiction follow the contract and mandatory rules. Consumer protections are not limited where they apply.'] }
        ]
      },
      cookies: {
        title: 'Cookies and device storage', description: 'How Tenebit uses browser storage and when consent is needed.', sections: [
          { title: '1. Current scope', paragraphs: ['The current version of Tenebit does not use advertising or analytics tools. The service uses technical cookies, localStorage and sessionStorage for sign-in, session security, requested features and interface preferences.'] },
          { title: '2. Necessary mechanisms', bullets: ['an HttpOnly refresh-session cookie, with Secure enabled in production', 'a trusted-device cookie when the user explicitly remembers a device for 2FA', 'short-lived correlation cookies for external sign-in', 'a short-lived public-link session for handover, return or audit confirmation', 'language, list-view, dismissed-message and temporary interface preferences'] },
          { title: '3. Consent', paragraphs: ['Prior consent is not required for storage strictly necessary to transmit communications or provide a feature explicitly requested by the user. Tenebit therefore does not present a fake analytics or advertising consent choice when those tools are not in use.', 'Future analytics, advertising, profiling or other non-essential storage must remain disabled until freely given, granular consent is recorded, with withdrawal as easy as acceptance.'] },
          { title: '4. Managing storage', paragraphs: ['You may remove or block cookies in browser settings. Removing technical data may sign you out, clear language or view preferences and stop some features. Payment and social sign-in providers may also use storage under their own notices when you activate those integrations.'] }
        ]
      }
    }
  },
  es: {
    ui: {
      home: 'Inicio', privacy: 'Política de privacidad', terms: 'Términos del servicio', cookies: 'Cookies y almacenamiento del dispositivo', contact: 'Contacto', operator: 'Operador del servicio', address: 'Dirección', registration: 'Datos registrales', taxId: 'NIF / IVA', effectiveDate: 'Vigente desde', version: 'Versión', missingOperator: 'Completa los datos del operador en VITE_LEGAL_OPERATOR_* antes de publicar.', storageNotice: 'Tenebit solo utiliza almacenamiento técnico necesario para iniciar sesión, proteger la cuenta y guardar preferencias.', storageNoticeDetails: 'La versión actual del servicio no utiliza herramientas publicitarias ni analíticas. Consulta el aviso de cookies.', understand: 'Entendido', footerRights: 'Todos los derechos reservados.'
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
          { title: '4. Contrato y terminación', paragraphs: ['El contrato se celebra al crear la organización y aceptar los términos o mediante un pedido separado. Precio, impuestos, límites y renovación se muestran antes de comprar.', 'La organización puede terminar según la cuenta o el contrato. El operador puede resolver por incumplimiento grave, impago, abuso o riesgo de seguridad respetando el preaviso aplicable.'] },
          { title: '5. Uso permitido', bullets: ['no introducir contenido ilícito ni vulnerar derechos ajenos', 'no eludir controles, probar vulnerabilidades sin permiso, interrumpir el servicio ni abusar de él', 'la organización debe tener base jurídica para los datos y facilitar los avisos necesarios', 'no intentar acceder a datos de otra organización'] },
          { title: '6. Contenido del cliente', paragraphs: ['La organización conserva sus derechos y autoriza el alojamiento, protección, copia y tratamiento técnico necesarios para prestar el servicio.', 'Tenebit no sustituye asesoramiento jurídico, laboral, fiscal ni de prevención. La organización responde de sus políticas, plazos y decisiones.'] },
          { title: '7. Disponibilidad, cambios y responsabilidad', paragraphs: ['El operador puede desarrollar funciones sin eliminar la finalidad principal del contrato. Puede haber interrupciones por mantenimiento, fallos, proveedores o fuerza mayor.', 'En la medida permitida, no responde por datos incorrectos, falta de permisos, servicios externos o uso contrario a la documentación. Prevalece la ley imperativa.'] },
          { title: '8. Reclamaciones', paragraphs: ['Envía la reclamación al soporte indicado, con organización, problema, fecha y solución esperada. Se responde sin demora indebida, normalmente en 14 días salvo norma o contrato distinto.'] },
          { title: '9. Cambios y ley aplicable', paragraphs: ['Los cambios importantes se anuncian con antelación. La ley y jurisdicción siguen el contrato y las normas imperativas, sin limitar derechos de consumidores cuando correspondan.'] }
        ]
      },
      cookies: {
        title: 'Cookies y almacenamiento del dispositivo', description: 'Cómo usa Tenebit el almacenamiento del navegador y cuándo se requiere consentimiento.', sections: [
          { title: '1. Alcance actual', paragraphs: ['La versión actual de Tenebit no utiliza herramientas publicitarias ni analíticas. El servicio usa cookies técnicas, localStorage y sessionStorage para acceso, seguridad, funciones solicitadas y preferencias.'] },
          { title: '2. Mecanismos necesarios', bullets: ['cookie HttpOnly de renovación de sesión, Secure en producción', 'cookie de dispositivo de confianza cuando el usuario lo solicita para 2FA', 'cookies breves de correlación para acceso externo', 'sesión breve de enlaces públicos para entrega, devolución o auditoría', 'preferencias de idioma, vista, mensajes cerrados e interfaz temporal'] },
          { title: '3. Consentimiento', paragraphs: ['No se requiere consentimiento previo para mecanismos estrictamente necesarios para transmitir comunicaciones o prestar una función solicitada. Por eso Tenebit no muestra una falsa elección sobre analítica o publicidad que no utiliza.', 'Cualquier futura analítica, publicidad, perfilado o almacenamiento no esencial deberá permanecer desactivado hasta obtener un consentimiento libre y granular, que pueda retirarse con igual facilidad.'] },
          { title: '4. Gestión', paragraphs: ['Puedes eliminar o bloquear cookies en el navegador. Esto puede cerrar la sesión, borrar preferencias o impedir funciones. Los proveedores de pagos o acceso social pueden usar su propio almacenamiento cuando actives esas integraciones.'] }
        ]
      }
    }
  },
  de: {
    ui: {
      home: 'Startseite', privacy: 'Datenschutzerklärung', terms: 'Nutzungsbedingungen', cookies: 'Cookies und Gerätespeicher', contact: 'Kontakt', operator: 'Diensteanbieter', address: 'Anschrift', registration: 'Registerangaben', taxId: 'Steuer / USt-ID', effectiveDate: 'Gültig ab', version: 'Version', missingOperator: 'Ergänze vor der Veröffentlichung die Betreiberangaben in VITE_LEGAL_OPERATOR_*.', storageNotice: 'Tenebit verwendet nur technischen Speicher, der für Anmeldung, Sicherheit und Einstellungen erforderlich ist.', storageNoticeDetails: 'Die aktuelle Version des Dienstes verwendet keine Werbe- oder Analysewerkzeuge. Details stehen im Cookie-Hinweis.', understand: 'Verstanden', footerRights: 'Alle Rechte vorbehalten.'
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
          { title: '4. Vertrag und Kündigung', paragraphs: ['Der Vertrag kommt mit Erstellung der Organisation und Annahme dieser Bedingungen oder durch eine gesonderte Bestellung zustande. Preise, Steuern, Grenzen und Verlängerung werden vor dem Kauf angezeigt.', 'Die Organisation kann nach Konto oder Vertrag kündigen. Der Betreiber kann bei wesentlichem Verstoß, Zahlungsverzug, Missbrauch oder Sicherheitsrisiko unter Beachtung der anwendbaren Frist kündigen.'] },
          { title: '5. Zulässige Nutzung', bullets: ['keine rechtswidrigen Inhalte oder Verletzung fremder Rechte', 'keine Umgehung von Schutzmaßnahmen, Schwachstellentests ohne Erlaubnis, Störung oder missbräuchliche Nutzung', 'die Organisation benötigt eine Rechtsgrundlage für eingetragene Daten und muss Pflichtinformationen bereitstellen', 'kein Zugriffsversuch auf Daten anderer Organisationen'] },
          { title: '6. Kundendaten', paragraphs: ['Die Organisation behält ihre Rechte und gestattet die für Hosting, Schutz, Sicherung und technische Verarbeitung erforderliche Nutzung.', 'Tenebit ersetzt keine Rechts-, Personal-, Steuer- oder Arbeitsschutzberatung. Die Organisation verantwortet Richtlinien, Fristen und Entscheidungen.'] },
          { title: '7. Verfügbarkeit, Änderungen und Haftung', paragraphs: ['Der Betreiber darf Funktionen weiterentwickeln, ohne den Vertragskern zu beseitigen. Unterbrechungen können durch Wartung, Ausfälle, Anbieter oder höhere Gewalt entstehen.', 'Soweit gesetzlich zulässig, besteht keine Haftung für falsche Kundendaten, fehlende Berechtigungen, externe Dienste oder dokumentationswidrige Nutzung. Zwingendes Recht geht vor.'] },
          { title: '8. Beschwerden', paragraphs: ['Beschwerden sind mit Organisation, Problem, Datum und gewünschter Lösung an den Support zu senden. Die Antwort erfolgt unverzüglich, grundsätzlich binnen 14 Tagen, sofern Gesetz oder Vertrag nichts anderes bestimmen.'] },
          { title: '9. Änderungen und Recht', paragraphs: ['Wesentliche Änderungen werden vorab angekündigt. Recht und Gerichtsstand folgen Vertrag und zwingenden Regeln. Verbraucherrechte werden nicht eingeschränkt, soweit sie anwendbar sind.'] }
        ]
      },
      cookies: {
        title: 'Cookies und Gerätespeicher', description: 'Wie Tenebit Browserspeicher nutzt und wann eine Einwilligung erforderlich ist.', sections: [
          { title: '1. Aktueller Umfang', paragraphs: ['Die aktuelle Version von Tenebit verwendet keine Werbe- oder Analysewerkzeuge. Der Dienst nutzt technische Cookies, localStorage und sessionStorage für Anmeldung, Sicherheit, gewünschte Funktionen und Einstellungen.'] },
          { title: '2. Erforderliche Mechanismen', bullets: ['HttpOnly-Cookie zur Sitzungserneuerung, in Produktion mit Secure', 'Cookie für ein bewusst als vertrauenswürdig gespeichertes 2FA-Gerät', 'kurzlebige Korrelations-Cookies für externe Anmeldung', 'kurzlebige öffentliche Sitzung für Übergabe, Rückgabe oder Audit', 'Sprache, Listenansicht, geschlossene Hinweise und temporäre Oberflächeneinstellungen'] },
          { title: '3. Einwilligung', paragraphs: ['Für Speicher, der strikt zur Übertragung oder Bereitstellung einer ausdrücklich gewünschten Funktion erforderlich ist, ist keine vorherige Einwilligung nötig. Tenebit zeigt daher keine fingierte Auswahl für nicht eingesetzte Analyse oder Werbung.', 'Künftige Analyse, Werbung, Profiling oder sonstiger nicht notwendiger Speicher muss bis zu einer freiwilligen, granularen Einwilligung deaktiviert bleiben. Der Widerruf muss ebenso einfach sein.'] },
          { title: '4. Verwaltung', paragraphs: ['Cookies können im Browser gelöscht oder blockiert werden. Dadurch können Anmeldung, Sprache, Ansichten oder Funktionen verloren gehen. Zahlungs- oder Social-Login-Anbieter können bei Aktivierung eigene Speichermechanismen verwenden.'] }
        ]
      }
    }
  }
};
