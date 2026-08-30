import { test, expect } from '@playwright/test';
import { registerOrg, createPerson, authHeaders } from './helpers';

test('offboarding public exit link resolves to the real token, not the case id', async ({ page, request }) => {
  const org = await registerOrg(request, 'offb');
  const person = await createPerson(request, org.token, 'offb');

  const createResponse = await request.post('/api/offboarding', {
    headers: authHeaders(org.token),
    data: {
      personId: person.id,
      employmentEndsAt: '2026-09-01T00:00:00Z',
      returnDueDate: '2026-09-05T00:00:00Z',
      blockNewReservations: true,
      cancelFutureReservations: true,
      autoReleaseLicenses: true,
    },
  });
  expect(createResponse.ok(), await createResponse.text()).toBeTruthy();
  const { case: offboardingCase } = await createResponse.json();

  const startResponse = await request.post(`/api/offboarding/${offboardingCase.id}/start`, {
    headers: authHeaders(org.token),
    data: {},
  });
  expect(startResponse.ok(), await startResponse.text()).toBeTruthy();

  const linkResponse = await request.post(`/api/offboarding/${offboardingCase.id}/regenerate-link`, {
    headers: authHeaders(org.token),
  });
  expect(linkResponse.ok(), await linkResponse.text()).toBeTruthy();
  const link = (await linkResponse.json()) as string;

  // Regression check for audit P1-FUNC-001: the link must carry the actual public token,
  // never the case's internal database id.
  expect(link).not.toContain(offboardingCase.id);
  const url = new URL(link);

  // Token siedzi we fragmencie, nie w ścieżce (AppLinkBuilder: `/exit#<token>`). Fragment nigdy nie
  // trafia do serwera, więc sekret nie ląduje w logach proxy ani w RequestPath - stąd ta zmiana
  // formatu względem starego `/exit/<token>`.
  expect(url.pathname).toBe('/exit');
  expect(url.hash.replace(/^#/, '').length).toBeGreaterThan(0);
  expect(url.hash).not.toContain(offboardingCase.id);

  await page.goto(`${url.pathname}${url.hash}`);
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/return|zwrot/i);
});

test('an unknown exit token shows an error instead of a broken page', async ({ page }) => {
  await page.goto('/exit/this-token-does-not-exist');
  await expect(page.getByText(/invalid|nieprawidłow|wygas/i)).toBeVisible();
});
