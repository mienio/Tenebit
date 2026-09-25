import { describe, expect, it } from 'vitest';
import { parseProcedureScope, procedureMatchesJobTitle, serializeProcedureScope } from './procedureScope';

describe('zakres procedury jako tagi stanowisk', () => {
  it('rozkłada zapis na tagi, także starszy wolny tekst', () => {
    expect(parseProcedureScope('Handlowiec, Kierowca')).toEqual(['Handlowiec', 'Kierowca']);
    expect(parseProcedureScope('Handlowiec;\nkierowca; handlowiec')).toEqual(['Handlowiec', 'kierowca']);
    expect(parseProcedureScope(null)).toEqual([]);
  });

  it('pusty zakres zapisuje się jako null (wszystkie stanowiska)', () => {
    expect(serializeProcedureScope([])).toBeNull();
    expect(serializeProcedureScope(['Handlowiec', 'Kierowca'])).toBe('Handlowiec, Kierowca');
  });

  it('dopasowuje stanowisko bez względu na wielkość liter i polskie znaki', () => {
    expect(procedureMatchesJobTitle('Specjalista ds. sprzedaży, Kierowca', 'specjalista ds. sprzedazy')).toBe(true);
    expect(procedureMatchesJobTitle('Kierowca', 'Handlowiec')).toBe(false);
    expect(procedureMatchesJobTitle(null, 'Handlowiec')).toBe(false);
    expect(procedureMatchesJobTitle('Kierowca', null)).toBe(false);
  });
});
