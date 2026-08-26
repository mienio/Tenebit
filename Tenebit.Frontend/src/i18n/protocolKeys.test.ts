import { describe, expect, it } from 'vitest';
import { translations } from './translations';

// Klucze protokołu przekazania i palety komend. Reszta słownika ma historyczne luki (es/de nie mają
// ~80 kluczy i lecą na angielskim fallbacku), więc test celowo pilnuje tylko tego zakresu - inaczej
// czerwieniłby się od pierwszego uruchomienia i nikt by go nie czytał.
const REQUIRED_KEYS = [
  'signature.title',
  'signature.nameLabel',
  'signature.hint',
  'signature.drawn',
  'signature.clear',
  'signature.ariaLabel',
  'publicAssignment.downloadProtocol',
  'assignments.downloadProtocol',
  'assignments.protocolFailed',
  'offboarding.downloadProtocol',
  'offboarding.protocolFailed',
  'search.title',
  'search.placeholder',
  'search.hintShort',
  'search.empty',
  'search.navigate',
  'search.open',
  'search.command.newAsset',
  'search.command.newAssignment',
  'search.command.newPerson',
];

const LANGUAGES = ['pl', 'en', 'es', 'de'] as const;

describe('protokół i paleta komend - tłumaczenia', () => {
  it.each(LANGUAGES)('%s ma wszystkie klucze', (language) => {
    const dictionary = translations[language];
    const missing = REQUIRED_KEYS.filter((key) => !dictionary[key]);
    expect(missing).toEqual([]);
  });

  it('żaden język nie zostawia pustego napisu', () => {
    for (const language of LANGUAGES) {
      for (const key of REQUIRED_KEYS) {
        expect(translations[language][key]?.trim(), `${language}/${key}`).toBeTruthy();
      }
    }
  });
});
