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
  expect(url.pathname).toMatch(/^\/exit\/.+/);

  await page.goto(url.pathname);
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/return|zwrot/i);
});

test('an unknown exit token shows an error instead of a broken page', async ({ page }) => {
  await page.goto('/exit/this-token-does-not-exist');
  await expect(page.getByText(/invalid|nieprawidłow|wygas/i)).toBeVisible();
});
