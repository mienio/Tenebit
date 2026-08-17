import { test, expect, type APIRequestContext } from '@playwright/test';
import { registerOrg, firstCategoryId, createAsset, createPerson, authHeaders, type RegisteredOrg } from './helpers';

// Regression coverage for the audit's P0-TENANT-* findings: every tenant-owned entity must be
// scoped by (OrganizationId, Id), not just Id. These hit the real HTTP API (not just Application
// unit tests with in-memory fakes) so a missing organizationId filter in a repository query would
// actually be caught here. Orgs A/B are shared across the sub-tests below (registered once in
// beforeAll) to keep this file's call count against the per-IP "auth" rate limiter (10/min) low.
test.describe('cross-tenant access is rejected', () => {
  let orgA: RegisteredOrg;
  let orgB: RegisteredOrg;
  let request: APIRequestContext;

  test.beforeAll(async ({ playwright }) => {
    request = await playwright.request.newContext({ baseURL: 'http://localhost:5173' });
    orgA = await registerOrg(request, 'tenant-a');
    orgB = await registerOrg(request, 'tenant-b');
  });

  test.afterAll(async () => {
    await request.dispose();
  });

  test('org B cannot read org A\'s asset by id', async () => {
    const categoryId = await firstCategoryId(request, orgA.token);
    const asset = await createAsset(request, orgA.token, categoryId, `TEN-${Date.now()}`);

    const asOwner = await request.get(`/api/assets/${asset.id}`, { headers: authHeaders(orgA.token) });
    expect(asOwner.ok()).toBeTruthy();

    const asOtherOrg = await request.get(`/api/assets/${asset.id}`, { headers: authHeaders(orgB.token) });
    expect(asOtherOrg.status()).toBe(404);
  });

  test('org B cannot read org A\'s person by id', async () => {
    const person = await createPerson(request, orgA.token, 'tenant');

    const asOtherOrg = await request.get(`/api/people/${person.id}`, { headers: authHeaders(orgB.token) });
    expect(asOtherOrg.status()).toBe(404);
  });

  test('unauthenticated requests are rejected outright', async () => {
    const response = await request.get('/api/people');
    expect(response.status()).toBe(401);
  });
});
