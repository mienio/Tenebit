import { describe, expect, it } from 'vitest';
import { getEmploymentStatusPresentation } from './domain';

describe('getEmploymentStatusPresentation', () => {
  it('maps lifecycle statuses to distinct labels and compatible actions', () => {
    expect(getEmploymentStatusPresentation('Active')).toEqual({ labelKey: 'people.active', badgeClass: 'status--InStock', action: 'deactivate' });
    expect(getEmploymentStatusPresentation('Offboarding')).toEqual({ labelKey: 'people.offboarding', badgeClass: 'status--AwaitingAcceptance', action: 'deactivate' });
    expect(getEmploymentStatusPresentation('Inactive')).toEqual({ labelKey: 'people.inactive', badgeClass: 'status--Draft', action: 'activate' });
  });
});
