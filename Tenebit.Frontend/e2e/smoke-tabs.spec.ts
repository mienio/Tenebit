import { test, expect } from '@playwright/test';
import { registerOrg, firstCategoryId, createAsset, createPerson, uniqueSuffix } from './helpers';

const AUTH_TABS = [
  '/dashboard', '/my', '/assets', '/people', '/assignments', '/procedures',
  '/onboarding', '/offboarding', '/asset-audits', '/reports', '/licenses',
  '/activity-log', '/settings', '/pricing',
];

const PUBLIC_TABS = ['/login', '/register', '/forgot-password', '/privacy', '/terms', '/cookies'];

interface Problem { tab: string; kind: string; detail: string; }

/**
 * Świeża organizacja nie ma jeszcze migawek dashboardu - zbiera je zadanie w tle przez kolejne dni -
 * więc porównanie okresów zwraca 404 z komunikatem "za mało danych historycznych". Strona /reports
 * renderuje się poprawnie i pokazuje pusty stan, ale przeglądarka i tak loguje nieudany request.
 * To jedyny znany, zamierzony brak - wszystko inne ma być czyste.
 */
const EXPECTED_EMPTY_STATE = /\/api\/dashboard\/comparison/;

function isExpected(problem: Problem): boolean {
  if (problem.tab !== '/reports') return false;
  if (problem.kind === 'http') return EXPECTED_EMPTY_STATE.test(problem.detail);
  return problem.kind === 'console' && problem.detail.includes('404');
}

test.setTimeout(240_000);

test('every tab opens without console errors, failed requests or load-failure banners', async ({ page, request, context }) => {
  const org = await registerOrg(request, 'walk');

  // Seed a little data so list pages render rows, not just empty states.
  const suffix = uniqueSuffix();
  const categoryId = await firstCategoryId(request, org.token);
  await createAsset(request, org.token, categoryId, `W-${suffix}`.slice(0, 12));
  await createPerson(request, org.token, suffix);

  await page.goto('/login');
  await page.getByLabel('E-mail').fill(org.email);
  await page.getByLabel('Hasło', { exact: true }).fill(org.password);
  await page.getByRole('button', { name: /zaloguj/i }).click();
  await expect(page).toHaveURL(/\/dashboard$/);

  const problems: Problem[] = [];
  let current = 'bootstrap';

  page.on('console', msg => {
    if (msg.type() !== 'error') return;
    const text = msg.text();
    if (text.includes('favicon')) return;
    problems.push({ tab: current, kind: 'console', detail: text.slice(0, 300) });
  });
  page.on('pageerror', err => {
    problems.push({ tab: current, kind: 'pageerror', detail: String(err).slice(0, 300) });
  });
  page.on('response', res => {
    if (!res.url().includes('/api/')) return;
    if (res.status() < 400) return;
    problems.push({ tab: current, kind: 'http', detail: `${res.status()} ${res.url()}` });
  });

  for (const tab of [...AUTH_TABS]) {
    current = tab;
    console.log('--> ' + tab);
    await page.goto(tab);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(700);

    const banner = page.getByText(/Nie udało się wczytać|Operacja zakończyła się błędem|Coś poszło nie tak/i);
    if (await banner.count() > 0) {
      problems.push({ tab, kind: 'banner', detail: (await banner.first().innerText()).slice(0, 200) });
    }
    const heading = page.locator('h1, h2').first();
    if (await heading.count() === 0) {
      problems.push({ tab, kind: 'empty', detail: 'no h1/h2 rendered' });
    }
  }

  // Public pages: separate context so the session cookie is not in play.
  const anon = await context.browser()!.newContext({ locale: 'pl-PL' });
  const anonPage = await anon.newPage();
  for (const tab of PUBLIC_TABS) {
    anonPage.on('pageerror', err => problems.push({ tab, kind: 'pageerror(anon)', detail: String(err).slice(0, 300) }));
    await anonPage.goto(new URL(tab, test.info().project.use.baseURL ?? 'http://localhost:5173').toString());
    await anonPage.waitForLoadState('domcontentloaded');
    await anonPage.waitForTimeout(500);
    const banner = anonPage.getByText(/Nie udało się wczytać|Operacja zakończyła się błędem/i);
    if (await banner.count() > 0) {
      problems.push({ tab, kind: 'banner(anon)', detail: (await banner.first().innerText()).slice(0, 200) });
    }
  }
  await anon.close();

  const unexpected = problems.filter(problem => !isExpected(problem));

  console.log('=== TAB WALK RESULT ===');
  console.log(unexpected.length === 0 ? 'ALL CLEAN' : JSON.stringify(unexpected, null, 2));
  expect(unexpected, JSON.stringify(unexpected, null, 2)).toEqual([]);
});
