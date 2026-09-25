import type { PickerOption } from '../components/OptionPicker';

// Listy krajów i walut budujemy z Intl zamiast trzymać ręcznie przepisane słowniki: nazwy od razu są w
// języku interfejsu, a przeglądarka zna aktualne kody ISO.

const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

// Kody, które Intl.DisplayNames zna, ale które nie są krajami (regiony zbiorcze, kody zastrzeżone).
const NON_COUNTRY_REGIONS = new Set(['AC', 'CP', 'CQ', 'DG', 'EA', 'EU', 'EZ', 'IC', 'QO', 'TA', 'UN', 'XA', 'XB', 'ZZ']);

const FALLBACK_CURRENCIES = ['PLN', 'EUR', 'USD', 'GBP', 'CHF', 'CZK', 'SEK', 'NOK', 'DKK', 'HUF', 'RON', 'UAH', 'CAD', 'AUD', 'JPY'];

function displayNames(language: string, type: 'region' | 'currency'): Intl.DisplayNames | null {
  try {
    return new Intl.DisplayNames([language, 'en'], { type, fallback: 'none' });
  } catch {
    return null;
  }
}

function sortByLabel(options: PickerOption[], language: string): PickerOption[] {
  return options.sort((a, b) => a.label.localeCompare(b.label, language));
}

/// Kraje ISO 3166-1 alfa-2 z nazwą w danym języku. `keep` dokłada wartość spoza listy (np. stary wpis
/// z czasów wolnego pola tekstowego), żeby edycja nie gubiła zapisanych danych.
export function countryOptions(language: string, keep?: string | null): PickerOption[] {
  const names = displayNames(language, 'region');
  const options: PickerOption[] = [];
  for (const first of LETTERS) {
    for (const second of LETTERS) {
      const code = first + second;
      if (NON_COUNTRY_REGIONS.has(code)) continue;
      const name = names?.of(code);
      if (name && name !== code) options.push({ value: code, label: `${name} (${code})` });
    }
  }
  if (!options.length) options.push({ value: 'PL', label: 'PL' });
  const kept = keep?.trim().toUpperCase();
  if (kept && !options.some(option => option.value === kept)) options.push({ value: kept, label: kept });
  return sortByLabel(options, language);
}

function currencyCodes(): string[] {
  const intl = Intl as unknown as { supportedValuesOf?: (key: string) => string[] };
  try {
    const codes = intl.supportedValuesOf?.('currency');
    if (codes?.length) return codes;
  } catch {
    // starsze przeglądarki - zostaje lista zapasowa
  }
  return FALLBACK_CURRENCIES;
}

/// Waluty ISO 4217. Waluty `pinned` (np. waluta organizacji) idą na górę listy - to po nie sięga się
/// najczęściej, a pełna lista jest i tak przeszukiwalna.
export function currencyOptions(language: string, pinned: Array<string | null | undefined> = []): PickerOption[] {
  const names = displayNames(language, 'currency');
  const codes = new Set(currencyCodes().map(code => code.toUpperCase()));
  const top = [...new Set(pinned.filter((code): code is string => !!code?.trim()).map(code => code.trim().toUpperCase()))];
  for (const code of top) codes.add(code);
  const label = (code: string) => {
    const name = names?.of(code);
    return name && name !== code ? `${code} · ${name}` : code;
  };
  const rest = sortByLabel([...codes].filter(code => !top.includes(code)).map(code => ({ value: code, label: label(code) })), language);
  return [...top.map(code => ({ value: code, label: label(code) })), ...rest];
}
