# Plan landing page Tenebit

## Cel

Landing ma w pierwszej kolejności pokazać działający produkt, następnie pomóc odbiorcy
rozpoznać własny problem i dopiero później opisać funkcje. Trzy nowe elementy to:

1. interaktywny scenariusz end-to-end oparty na obecnym oknie demo,
2. sekcja rezultatów dla konkretnych ról,
3. sekcja bezpieczeństwa i zaufania oparta wyłącznie na funkcjach dostępnych w produkcie.

## Kolejność strony

1. Nawigacja z odnośnikami do demo, ról, funkcji, bezpieczeństwa i cennika.
2. Hero z główną obietnicą oraz rejestracją.
3. Prowadzone demo jako największy i najważniejszy element strony.
4. Rezultaty dla IT, HR, Office/Operations oraz zarządu.
5. Obecna siatka funkcji jako techniczne rozwinięcie rezultatów.
6. Bezpieczeństwo i zaufanie.
7. Cennik i końcowe CTA.
8. Stopka prawna.

## 1. Prowadzone demo

### Układ desktopowy

- Nad obecną makietą aplikacji znajduje się nagłówek wyjaśniający, że można przejść cały proces.
- Cztery przyciski tworzą linię scenariusza: pracownik, sprzęt, procedura, potwierdzenie.
- Kliknięcie kroku przełącza istniejącą makietę na odpowiedni moduł i wyróżnia właściwy rekord.
- Ostatni krok pokazuje zapisany dowód: osobę, sprzęt, procedurę oraz datę i godzinę.
- Obecne zakładki modułów pozostają dostępne, aby po scenariuszu swobodnie poznawać produkt.
- Makieta wykorzystuje pełną szerokość sekcji; nie dodajemy panelu bocznego, który ją ścieśni.

### Przebieg

1. Dodaj pracownika: widok osób wyróżnia pracownika IT.
2. Przypisz sprzęt: widok aktywów wyróżnia przypisany laptop.
3. Dołącz procedurę: widok procedur wyróżnia dokument onboardingowy.
4. Zachowaj dowód: podsumowanie pokazuje, kto, co i kiedy potwierdził.

### Telefon pionowy

- Kroki układają się pionowo i mają co najmniej 44 px wysokości dotykowej.
- Zakładki modułów przewijają się wyłącznie wewnątrz własnego poziomego paska.
- Tabele zmieniają się w karty z podpisanymi polami, bez poziomego przewijania strony.
- Demo pokazuje maksymalnie trzy przykładowe rekordy danego widoku, aby nie tworzyć bardzo
  wysokiego bloku; wyróżniany rekord zawsze mieści się w tej grupie.
- Ramka, cień i zawartość mieszczą się przy szerokości 320 px.

## 2. Sekcje dla ról

Sekcja ma odpowiadać na pytanie "co zyskuję w swojej pracy", a nie powtarzać nazwy modułów.
Każda karta zawiera nazwę roli, rezultat jako nagłówek, krótki scenariusz oraz wyróżniony efekt.

- IT / administrator: wiadomo, gdzie jest sprzęt, kto go ma i co wymaga działania.
- HR / People Ops: onboarding i offboarding kończą się kompletnym potwierdzeniem.
- Office / Operations: lokalizacje, wyposażenie, QR i przeglądy są w jednym obiegu.
- Zarząd / finanse: widoczna jest wartość floty, ryzyko i niewykorzystane zasoby.

Na desktopie karty tworzą układ 2 x 2, na tablecie dwie kolumny, a na telefonie jedną kolumnę.

## 3. Bezpieczeństwo i zaufanie

Komunikaty nie mogą deklarować certyfikatów ani standardów, których produkt nie posiada.
Sekcja pokazuje sprawdzalne mechanizmy:

- dostęp według ról i uprawnień,
- uwierzytelnianie dwuskładnikowe,
- historię operacji z osobą i czasem zdarzenia,
- kontrolowane linki do potwierdzeń oraz wersjonowane procedury i protokoły PDF.

Sekcję zamyka odnośnik do polityki prywatności. Na desktopie mechanizmy tworzą cztery kolumny,
na tablecie dwie, a na telefonie jedną.

## Dostępność

- Kroki scenariusza tworzą opisaną grupę przycisków z `aria-pressed`, a moduły używają
  semantyki zakładek z `aria-selected` i obsługą strzałek, Home oraz End.
- Aktywny panel jest opisany i aktualizowany przez `aria-live="polite"`.
- Każdy przycisk ma widoczny stan fokusu i działa z klawiatury.
- Animacje są wyłączane przez `prefers-reduced-motion`.
- Kontrast i kolory korzystają z istniejących tokenów interfejsu.

## Pliki

- `Tenebit.Frontend/src/pages/LandingPage.tsx`: struktura, scenariusz i zawartość sekcji.
- `Tenebit.Frontend/src/styles/index.css`: układ desktopowy i mobilny.
- `Tenebit.Frontend/src/i18n/translations.ts`: teksty PL, EN, ES, DE, IT i FR.

## Weryfikacja

1. `npm test`
2. `npm run lint`
3. `npm run build`
4. Kontrola szerokości 320, 375, 768 i 1180 px bez poziomego przewijania strony.
5. Przejście czterech kroków myszą i klawiaturą oraz ręczne użycie zakładek demo.
