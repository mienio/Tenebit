import type { APIRequestContext } from '@playwright/test';

export function uniqueSuffix(): string {
  return `${Date.now()}-${Math.floor(Math.random() * 100000)}`;
}

export interface RegisteredOrg {
  token: string;
  organizationId: string;
  email: string;
  password: string;
}

export async function registerOrg(request: APIRequestContext, namePrefix: string): Promise<RegisteredOrg> {
  const suffix = uniqueSuffix();
  const email = `${namePrefix}-${suffix}@example.test`;
  const password = 'E2ePassword123!';

  const response = await request.post('/api/auth/register', {
    data: {
      organizationName: `${namePrefix} ${suffix}`,
      email,
      password,
      displayName: `${namePrefix} Owner`,
      currency: 'PLN',
      language: 'pl',
    },
  });
  if (!response.ok()) {
    throw new Error(`register failed: ${response.status()} ${await response.text()}`);
  }
  const body = await response.json();
  return { token: body.token as string, organizationId: body.user.organizationId as string, email, password };
}

export function authHeaders(token: string) {
  return { Authorization: `Bearer ${token}` };
}

export async function firstCategoryId(request: APIRequestContext, token: string): Promise<string> {
  const response = await request.get('/api/asset-categories', { headers: authHeaders(token) });
  if (!response.ok()) throw new Error(`asset-categories failed: ${response.status()}`);
  const categories = await response.json();
  if (!categories.length) throw new Error('no starter asset categories found for freshly registered org');
  return categories[0].id as string;
}

export async function createAsset(request: APIRequestContext, token: string, categoryId: string, tag: string) {
  const response = await request.post('/api/assets', {
    headers: authHeaders(token),
    data: { name: `E2E Asset ${tag}`, assetTag: tag, categoryId },
  });
  if (!response.ok()) throw new Error(`create asset failed: ${response.status()} ${await response.text()}`);
  return response.json();
}

export async function createPerson(request: APIRequestContext, token: string, suffix: string) {
  const response = await request.post('/api/people', {
    headers: authHeaders(token),
    data: {
      firstName: 'Jan',
      lastName: `Testowy-${suffix}`,
      email: `jan.testowy-${suffix}@example.test`,
      relationType: 'Pracownik',
    },
  });
  if (!response.ok()) throw new Error(`create person failed: ${response.status()} ${await response.text()}`);
  return response.json();
}
