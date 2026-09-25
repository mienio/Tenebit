import { describe, expect, it } from 'vitest';
import { filterOptions, pickerKindFor } from './OptionPicker';
import { matchSuggestions, uniqueSuggestions } from './Autocomplete';
import { parseThresholdTag } from './AlertsSettings';

describe('dobór kontrolki do liczby opcji', () => {
  it('2-4 opcje to segmented, 5-8 select, 9+ wyszukiwarka', () => {
    expect(pickerKindFor(3)).toBe('segmented');
    expect(pickerKindFor(4)).toBe('segmented');
    expect(pickerKindFor(5)).toBe('select');
    expect(pickerKindFor(8)).toBe('select');
    expect(pickerKindFor(10)).toBe('search');
    expect(pickerKindFor(52)).toBe('search');
  });

  it('wyszukiwarka filtruje po fragmencie, bez wielkości liter i polskich znaków', () => {
    const options = [{ value: '1', label: 'Monitory' }, { value: '2', label: 'Laptopy' }, { value: '3', label: 'Łódź - magazyn' }];
    expect(filterOptions(options, 'mon').map(option => option.label)).toEqual(['Monitory']);
    expect(filterOptions(options, 'lodz').map(option => option.label)).toEqual(['Łódź - magazyn']);
    expect(filterOptions(options, '')).toHaveLength(3);
  });
});

describe('podpowiedzi z wartości już użytych', () => {
  it('scala warianty pisowni i pomija puste', () => {
    expect(uniqueSuggestions(['Microsoft', ' microsoft', null, '', 'Adobe'])).toEqual(['Adobe', 'Microsoft']);
  });

  it('"Spec" podpowiada "Specjalista ds. sprzedaży"', () => {
    expect(matchSuggestions(['Specjalista ds. sprzedaży', 'Kierowca'], 'Spec')).toEqual(['Specjalista ds. sprzedaży']);
  });

  it('nie podpowiada wartości identycznej z wpisaną', () => {
    expect(matchSuggestions(['Microsoft'], 'microsoft')).toEqual([]);
  });
});

describe('progi alertów jako tagi', () => {
  it('przyjmuje tylko liczby całkowite 0-365', () => {
    expect(parseThresholdTag('30')).toBe('30');
    expect(parseThresholdTag('007')).toBe('7');
    expect(parseThresholdTag('abc')).toBeNull();
    expect(parseThresholdTag('-1')).toBeNull();
    expect(parseThresholdTag('366')).toBeNull();
    expect(parseThresholdTag('7.5')).toBeNull();
  });
});
