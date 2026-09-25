import { describe, expect, it } from 'vitest';
import { addDays, addMonths, endOfMonth, todayIsoLocal } from './datePresets';

describe('presety dat', () => {
  it('dzisiaj bierze datę lokalną, nie UTC', () => {
    expect(todayIsoLocal(new Date(2026, 8, 25, 0, 30))).toBe('2026-09-25');
  });

  it('+24 mies. od daty zakupu 25.09.2026 to 25.09.2028', () => {
    expect(addMonths('2026-09-25', 24)).toBe('2028-09-25');
  });

  it('dodawanie miesięcy przycina do końca krótszego miesiąca', () => {
    expect(addMonths('2026-01-31', 1)).toBe('2026-02-28');
    expect(addMonths('2028-01-31', 1)).toBe('2028-02-29');
    expect(addMonths('2026-08-31', 12)).toBe('2027-08-31');
  });

  it('dni przechodzą przez granicę miesiąca i roku', () => {
    expect(addDays('2026-09-25', 7)).toBe('2026-10-02');
    expect(addDays('2026-12-28', 30)).toBe('2027-01-27');
  });

  it('koniec miesiąca', () => {
    expect(endOfMonth('2026-09-25')).toBe('2026-09-30');
    expect(endOfMonth('2028-02-10')).toBe('2028-02-29');
  });

  it('pusta albo zła data daje pusty wynik zamiast NaN', () => {
    expect(addMonths('', 12)).toBe('');
    expect(addDays('abc', 1)).toBe('');
  });
});
