import { describe, expect, it } from 'vitest';
import { estimatePasswordStrength } from './passwordStrength';

describe('estimatePasswordStrength', () => {
  it('treats anything under 8 characters as weak regardless of complexity', () => {
    expect(estimatePasswordStrength('aB3!')).toBe('weak');
  });

  it('treats a long lowercase-only password as weak or fair, never strong without variety', () => {
    expect(estimatePasswordStrength('aaaaaaaaaaaaaaaa')).not.toBe('strong');
  });

  it('treats a long password mixing case, digits and symbols as strong', () => {
    expect(estimatePasswordStrength('Correct-Horse-Battery-Staple9!')).toBe('strong');
  });

  it('treats a medium-length password with some variety as fair', () => {
    expect(estimatePasswordStrength('Password1')).toBe('fair');
  });
});
