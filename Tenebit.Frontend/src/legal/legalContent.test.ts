import { describe, expect, it } from 'vitest';
import { languages } from '../i18n/translations';
import { legalContentFor, type LegalDocumentKind } from './legalContent';

const CODES = languages.map(item => item.value);
const KINDS: LegalDocumentKind[] = ['privacy', 'terms', 'cookies'];

describe('treści prawne', () => {
  it.each(CODES)('%s ma własne dokumenty, nie angielski zamiennik', (language) => {
    expect(legalContentFor(language).documentsLanguage).toBe(language);
  });

  it.each(CODES)('%s ma komplet dokumentów z sekcjami i treścią', (language) => {
    const { documents } = legalContentFor(language);
    for (const kind of KINDS) {
      const document = documents[kind];
      expect(document.title.trim(), `${language}/${kind}/title`).toBeTruthy();
      expect(document.description.trim(), `${language}/${kind}/description`).toBeTruthy();
      expect(document.sections.length, `${language}/${kind}/sections`).toBeGreaterThan(0);
      for (const section of document.sections) {
        expect(section.title.trim(), `${language}/${kind}/section`).toBeTruthy();
        const hasBody = (section.paragraphs?.length ?? 0) + (section.bullets?.length ?? 0) > 0;
        expect(hasBody, `${language}/${kind}/${section.title} jest pusta`).toBe(true);
      }
    }
  });

  // Sekcje są numerowane i cytowane ("zgodnie z pkt 4 regulaminu"), więc każda wersja językowa musi
  // mieć tę samą liczbę sekcji w tej samej kolejności - inaczej numeracja rozjeżdża się między językami.
  it.each(KINDS)('%s ma tę samą strukturę sekcji we wszystkich językach', (kind) => {
    const reference = legalContentFor('en').documents[kind].sections.length;
    for (const language of CODES) {
      expect(legalContentFor(language).documents[kind].sections.length, `${language}/${kind}`).toBe(reference);
    }
  });
});
