import { defineConfig } from '@playwright/test';

// Requires a reachable Postgres the backend is allowed to migrate/seed against — set
// TENEBIT_E2E_DB_CONNECTION (same convention as Tenebit.Tests' TENEBIT_TEST_DB_CONNECTION).
// Defaults to the local dev pattern documented in the repo's scratchpad notes: a throwaway
// Postgres cluster on port 5433 with a trust-auth "postgres" role.
const dbConnection =
  process.env.TENEBIT_E2E_DB_CONNECTION ?? 'Host=localhost;Port=5433;Database=tenebit_e2e;Username=postgres;Password=postgres';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  // Serial: specs share one backend process behind a per-IP auth rate limiter (10 req/min on
  // /api/auth/*, audit P1-SEC-004) — parallel workers would burst past it from the same IP.
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    // The app defaults its UI language to the browser's language, and Polish is the
    // canonical/source language for every string in the backend (see ErrorMessageTranslator) —
    // pin it so selectors and error-message assertions don't depend on the CI runner's locale.
    locale: 'pl-PL',
  },
  webServer: [
    {
      command: 'dotnet run --project ../Tenebit.Backend/Tenebit.Api --no-launch-profile',
      url: 'http://localhost:8080/api/health',
      reuseExistingServer: true,
      timeout: 120_000,
      env: {
        ConnectionStrings__TenebitDb: dbConnection,
        Database__AutoCreate: 'true',
        Seed__Enabled: 'false',
        Auth__SigningKey: 'e2e-test-signing-key-not-for-prod-32chars',
        Alerts__Enabled: 'false',
        ASPNETCORE_ENVIRONMENT: 'Development',
        // Every page load triggers an /api/auth/refresh call (AuthProvider bootstrap), which
        // shares the "auth" rate-limit bucket (10/min in prod) with login/register — a short E2E
        // run does more page loads than that. Raised here only, not in the shipped default.
        RateLimiting__AuthPermitLimit: '1000',
      },
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
      timeout: 60_000,
    },
  ],
});
