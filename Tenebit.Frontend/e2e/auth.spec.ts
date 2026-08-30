import { test, expect } from '@playwright/test';
import { uniqueSuffix } from './helpers';

test('register, logout, and log back in', async ({ page }) => {
  const suffix = uniqueSuffix();
  const email = `auth-${suffix}@example.test`;
  const password = 'E2ePassword123!';

  await page.goto('/register');
  await page.getByLabel('Nazwa firmy').fill(`Auth E2E ${suffix}`);
  await page.getByLabel('Twoje imię i nazwisko').fill('Auth Tester');
  await page.getByLabel('E-mail').fill(email);
  // Formularz ma dwa pola hasła, więc dopasowanie musi być dokładne.
  await page.getByLabel('Hasło', { exact: true }).fill(password);
  await page.getByLabel('Powtórz hasło').fill(password);
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'ZAŁÓŻ ORGANIZACJĘ' }).click();

  // Rejestracja nie loguje - wydaje 202 i odsyła na logowanie (bez SMTP konto jest auto-zweryfikowane).
  await expect(page).toHaveURL(/\/login/);

  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Hasło', { exact: true }).fill(password);
  await page.getByRole('button', { name: /zaloguj/i }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

  await page.getByRole('button', { name: 'Wyloguj' }).click();
  await expect(page).toHaveURL(/\/login$/);
});

test('rejects wrong password', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('E-mail').fill('nobody@example.test');
  await page.getByLabel('Hasło').fill('wrong-password');
  await page.getByRole('button', { name: /zaloguj/i }).click();

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByText(/nieprawidłow/i)).toBeVisible();
});
