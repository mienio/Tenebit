import { normalizeForSearch } from '../components/OptionPicker';

// Zakres procedury ("Zakres / stanowiska") jest listą nazw stanowisk. Backend trzyma go w jednym polu
// tekstowym (AppliesTo, do 240 znaków), więc tagi zapisujemy rozdzielone przecinkiem. Brak zakresu
// (null) znaczy "wszystkie stanowiska". Starsze wpisy z czasów wolnego pola tekstowego też się tu
// rozkładają - średniki i nowe linie traktujemy jak przecinki.

export const PROCEDURE_SCOPE_MAX_LENGTH = 240;

export function parseProcedureScope(appliesTo: string | null | undefined): string[] {
  if (!appliesTo?.trim()) return [];
  const seen = new Set<string>();
  const tags: string[] = [];
  for (const part of appliesTo.split(/[,;\n]/)) {
    const tag = part.trim();
    const key = tag.toLocaleLowerCase();
    if (tag && !seen.has(key)) {
      seen.add(key);
      tags.push(tag);
    }
  }
  return tags;
}

export function serializeProcedureScope(tags: string[]): string | null {
  const clean = tags.map(tag => tag.replace(/[,;\n]/g, ' ').trim()).filter(Boolean);
  return clean.length ? clean.join(', ') : null;
}

/// Czy procedura z danym zakresem pasuje do stanowiska. Pusty zakres ("wszystkie stanowiska") nie liczy
/// się jako dopasowanie - takie procedury nie są podpowiadane, bo dotyczyłyby każdego i zagłuszyłyby
/// właściwe propozycje.
export function procedureMatchesJobTitle(appliesTo: string | null | undefined, jobTitle: string | null | undefined): boolean {
  const title = normalizeForSearch(jobTitle?.trim() ?? '');
  if (!title) return false;
  return parseProcedureScope(appliesTo).some(tag => normalizeForSearch(tag) === title);
}
