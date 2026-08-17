import { test, expect } from '@playwright/test';
import { registerOrg, firstCategoryId, createAsset, createPerson, authHeaders } from './helpers';

test('creating an assignment via the API shows up in the Wydania list', async ({ page, request }) => {
  const org = await registerOrg(request, 'assign');
  const categoryId = await firstCategoryId(request, org.token);
  const asset = await createAsset(request, org.token, categoryId, `AS-${Date.now()}`);
  const person = await createPerson(request, org.token, 'assign');

  const createResponse = await request.post('/api/assignments', {
    headers: authHeaders(org.token),
    data: { personId: person.id, assets: [{ assetId: asset.id }], procedureIds: [] },
  });
  expect(createResponse.ok(), await createResponse.text()).toBeTruthy();
  const assignment = await createResponse.json();

  await page.goto('/login');
  await page.getByLabel('E-mail').fill(org.email);
  await page.getByLabel('Hasło').fill(org.password);
  await page.getByRole('button', { name: /zaloguj/i }).click();
  await expect(page).toHaveURL(/\/dashboard$/);

  await page.goto('/assignments');
  const table = page.getByRole('table');
  await expect(table.getByText(assignment.protocolNumber)).toBeVisible();
  await expect(table.getByRole('button', { name: person.fullName })).toBeVisible();
  await expect(table.getByText('Czeka na akceptację')).toBeVisible();
});
