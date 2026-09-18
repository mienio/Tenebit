import { describe, expect, it } from 'vitest';
import { validationMessage } from './FormFields';
import { translations } from '../i18n/translations';

const t = (key: string, params?: Record<string, string | number>) => {
  const template = translations.en[key] ?? key;
  return params
    ? Object.entries(params).reduce((text, [name, value]) => text.split(`{${name}}`).join(String(value)), template)
    : template;
};

// Kontrolka formularza udawana na tyle, na ile potrzebuje jej validationMessage: stan walidacji plus
// atrybuty, z których bierzemy liczby do komunikatu.
function control(validity: Partial<ValidityState>, attributes: Record<string, unknown> = {}) {
  return {
    validity: { valueMissing: false, typeMismatch: false, badInput: false, rangeUnderflow: false, rangeOverflow: false, tooShort: false, tooLong: false, stepMismatch: false, patternMismatch: false, customError: false, valid: false, ...validity },
    ...attributes
  } as unknown as HTMLInputElement;
}

describe('komunikaty walidacji pól', () => {
  it('puste pole wymagane dostaje zdanie, nie samą obwódkę', () => {
    expect(validationMessage(control({ valueMissing: true }), t)).toBe('This field is required.');
  });

  it('rozróżnia format e-mail od innych niezgodności typu', () => {
    expect(validationMessage(control({ typeMismatch: true }, { type: 'email' }), t)).toContain('email');
    expect(validationMessage(control({ typeMismatch: true }, { type: 'url' }), t)).toBe('Enter a valid URL.');
  });

  it('podstawia progi liczbowe do komunikatu', () => {
    expect(validationMessage(control({ rangeUnderflow: true }, { min: '3' }), t)).toBe('The value cannot be lower than 3.');
    expect(validationMessage(control({ rangeOverflow: true }, { max: '10' }), t)).toBe('The value cannot be higher than 10.');
    expect(validationMessage(control({ tooShort: true }, { minLength: 8 }), t)).toBe('Enter at least 8 characters.');
  });

  it('nieznany powód nadal daje treść, a nie pusty napis', () => {
    expect(validationMessage(control({}), t)).toBe('The value is invalid.');
  });
});
