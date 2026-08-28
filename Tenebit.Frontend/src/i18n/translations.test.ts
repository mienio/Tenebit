import { describe, expect, it } from 'vitest';
import { languages, translations } from './translations';

// Angielski jest referencją: t() ma fallback na 'en', więc każdy klucz obecny w 'en' musi istnieć
// wszędzie, inaczej użytkownik dostaje wtręt po angielsku w środku swojego języka. Test pilnuje
// całego słownika, nie wybranego zakresu - luki są dziś zerowe i mają takie zostać.
const REFERENCE = 'en';
const CODES = languages.map(item => item.value);

describe('słownik tłumaczeń', () => {
  it.each(CODES)('%s ma wszystkie klucze z angielskiego', (language) => {
    const missing = Object.keys(translations[REFERENCE]).filter(key => !translations[language][key]);
    expect(missing).toEqual([]);
  });

  it.each(CODES)('%s nie zostawia pustych napisów', (language) => {
    const blank = Object.entries(translations[language])
      .filter(([, value]) => value.trim() === '')
      .map(([key]) => key);
    expect(blank).toEqual([]);
  });

  it('każdy język podstawia te same zmienne co angielski', () => {
    const placeholders = (value: string) => (value.match(/\{[a-zA-Z]+\}/g) ?? []).sort().join(',');
    const mismatched: string[] = [];
    for (const language of CODES) {
      if (language === REFERENCE) continue;
      for (const [key, reference] of Object.entries(translations[REFERENCE])) {
        const translated = translations[language][key];
        if (translated && placeholders(translated) !== placeholders(reference)) {
          mismatched.push(`${language}/${key}`);
        }
      }
    }
    expect(mismatched).toEqual([]);
  });
});
