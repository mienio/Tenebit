import { describe, expect, it } from 'vitest';
import { paddleLocaleFor, readTransactionId, stripTransactionId } from './paddleClient';
import { languages } from '../i18n/translations';

describe('paddleClient', () => {
  // Paddle dokleja `_ptxn` do Default payment link - bez tego parametru /checkout nie ma czego otworzyc.
  it('czyta id transakcji z query stringa', () => {
    expect(readTransactionId('?_ptxn=txn_01h8')).toBe('txn_01h8');
    expect(readTransactionId('?foo=1&_ptxn=txn_01h8&bar=2')).toBe('txn_01h8');
  });

  it('traktuje brak i pusty parametr jak brak transakcji', () => {
    expect(readTransactionId('')).toBeNull();
    expect(readTransactionId('?foo=1')).toBeNull();
    expect(readTransactionId('?_ptxn=')).toBeNull();
    expect(readTransactionId('?_ptxn=%20')).toBeNull();
  });

  // Paddle.js sam wypatruje `_ptxn` przy Initialize. /checkout otwiera checkout jawnie (zeby podac
  // jezyk i wlasne handlery), wiec parametr musi zniknac z adresu, zanim overlay otworzy sie dwa razy.
  it('usuwa _ptxn z adresu, zostawiajac reszte nietknieta', () => {
    expect(stripTransactionId('https://teneb.it/checkout?_ptxn=txn_01h8')).toBe('/checkout');
    expect(stripTransactionId('https://teneb.it/checkout?utm=mail&_ptxn=txn_01h8')).toBe('/checkout?utm=mail');
    expect(stripTransactionId('https://teneb.it/checkout?_ptxn=txn_01h8#top')).toBe('/checkout#top');
  });

  // Overlay to ostatni ekran przed zaplata - ma mowic tym samym jezykiem co reszta aplikacji.
  it('kazdy jezyk aplikacji ma odpowiednik w locale Paddle', () => {
    const bezLocale = languages.map(item => item.value).filter(code => !paddleLocaleFor(code));
    expect(bezLocale).toEqual([]);
  });

  it('nieobslugiwany jezyk zostawia wybor locale Paddle', () => {
    expect(paddleLocaleFor('cs')).toBeUndefined();
  });
});
