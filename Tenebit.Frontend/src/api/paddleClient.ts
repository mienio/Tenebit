// Paddle Billing has no server-generated hosted checkout redirect URL (unlike the old Stripe Checkout
// Session) - Paddle.js runs client-side and opens the checkout overlay itself. This loads the script once
// from Paddle's own CDN (required - see developer.paddle.com/paddlejs/include-paddlejs) and initializes
// it with the public client-side token the backend hands out via GET /api/subscription/paddle-config.

import type { Language } from '../i18n/translations';

export interface PaddleCheckoutOpenOptions {
  /** Mutually exclusive with `transactionId` - Paddle rejects a checkout carrying both. */
  items?: { priceId: string; quantity: number }[];
  /** An existing transaction to pay, used by the public /checkout page for payment links Paddle itself
   * sends out (invoice "Pay" buttons, failed-payment recovery mails). */
  transactionId?: string;
  customer?: { id: string };
  discountId?: string | null;
  /** Echoed back verbatim on the transaction.completed webhook - carries the affiliate attribution
   * resolved server-side in GET /api/subscription/checkout-params (see CheckoutParams.affiliateCode). */
  customData?: Record<string, string>;
  settings?: {
    successUrl?: string;
    displayMode?: 'overlay' | 'inline';
    /** Tenebit plans are per-organization asset limits, not per-seat licenses - there is no meaning to
     * buying "3 Starters", so the quantity stepper Paddle shows by default must be hidden. */
    allowQuantity?: boolean;
    locale?: string;
  };
}

interface PaddleEvent {
  name: string;
}

interface PaddleInitializeOptions {
  token: string;
  eventCallback?: (event: PaddleEvent) => void;
  checkout?: { settings?: { locale?: string } };
  /** Paddle Retain. Must be the Paddle customer id (ctm_...) - Retain looks the subscriber up by it, so
   * our own organization/user id or an email address here silently matches nobody. */
  pwCustomer?: { id: string };
}

interface PaddleGlobal {
  Environment: { set: (environment: 'sandbox' | 'production') => void };
  Initialize: (options: PaddleInitializeOptions) => void;
  Checkout: { open: (options: PaddleCheckoutOpenOptions) => void };
}

declare global {
  interface Window {
    Paddle?: PaddleGlobal;
  }
}

/** Handlers for Paddle's global checkout events. `onClosed` fires when the shopper dismisses the overlay
 * without paying - and, on some flows, again right after a successful payment, so a caller that navigates
 * away on `onCompleted` has to ignore the trailing close itself. */
export interface PaddleCheckoutHandlers {
  onCompleted?: () => void;
  onClosed?: () => void;
}

// Paddle.Initialize may only run once per page load, but the handlers have to follow whichever page is
// currently driving a checkout (the pricing page reloads the subscription; /checkout navigates away).
// The one eventCallback registered at Initialize therefore dispatches through this mutable slot instead
// of capturing the first caller's closures forever.
let handlers: PaddleCheckoutHandlers = {};
let readyPromise: Promise<PaddleGlobal> | null = null;

/** Locales Paddle Checkout renders in (developer.paddle.com - Paddle.Checkout.open, settings.locale).
 * Anything outside this list has to be left unset so Paddle falls back to the browser's own locale
 * rather than being handed a code it does not know. */
const PADDLE_LOCALES = new Set([
  'ar', 'zh-Hans', 'zh-TW', 'da', 'nl', 'en', 'fr', 'de', 'it', 'ja',
  'ko', 'no', 'pl', 'pt', 'pt-BR', 'tr', 'ru', 'es', 'sv'
]);

/** The overlay is the last thing a buyer reads before paying, so it has to speak the language the rest of
 * the app is speaking. Every language Tenebit ships today maps 1:1 onto a Paddle locale; a future one that
 * does not returns undefined, which is Paddle's documented "use the browser locale" behaviour. */
export function paddleLocaleFor(language: Language | string): string | undefined {
  return PADDLE_LOCALES.has(language) ? language : undefined;
}

/** Paddle appends its transaction id to the Default payment link as `_ptxn` (Checkout settings in the
 * Paddle dashboard). Returns null for a visitor who reached /checkout without one. */
export function readTransactionId(search: string): string | null {
  const value = new URLSearchParams(search).get('_ptxn');
  return value && value.trim() !== '' ? value : null;
}

/** Drops `_ptxn` from the address bar. Paddle.js reads it during Initialize and opens the overlay by
 * itself; /checkout opens the checkout explicitly instead, so that it can pass the shopper's locale and
 * its own event handlers. Without this the two would race and the overlay could open twice. */
export function stripTransactionId(url: string): string {
  const parsed = new URL(url);
  parsed.searchParams.delete('_ptxn');
  return parsed.pathname + (parsed.search ? parsed.search : '') + parsed.hash;
}

function loadScript(): Promise<void> {
  if (window.Paddle) return Promise.resolve();
  const existing = document.querySelector<HTMLScriptElement>('script[data-paddle-js]');
  return new Promise((resolve, reject) => {
    if (existing) {
      existing.addEventListener('load', () => resolve());
      existing.addEventListener('error', () => reject(new Error('Nie udało się załadować Paddle.js.')));
      return;
    }
    const script = document.createElement('script');
    script.src = 'https://cdn.paddle.com/paddle/v2/paddle.js';
    script.async = true;
    script.dataset.paddleJs = 'true';
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('Nie udało się załadować Paddle.js.'));
    document.head.appendChild(script);
  });
}

/**
 * Idempotent - safe to call on every page load; only the first call actually loads/initializes the
 * script, later ones just swap in the current page's handlers. `onCompleted` is wired into Paddle's
 * global eventCallback (there is no per-open callback - Paddle Billing overlay checkout never navigates
 * the browser on its own after a successful payment, unlike a classic hosted-redirect flow, so without
 * this the app is left showing whatever state it had before the purchase until the user manually
 * hard-refreshes).
 */
export function ensurePaddleReady(
  clientToken: string,
  environment: 'sandbox' | 'production',
  checkoutHandlers: PaddleCheckoutHandlers = {},
  locale?: string,
  paddleCustomerId?: string | null
): Promise<PaddleGlobal> {
  handlers = checkoutHandlers;
  if (!readyPromise) {
    readyPromise = loadScript().then(() => {
      if (!window.Paddle) throw new Error('Paddle.js nie zostało poprawnie załadowane.');
      if (environment === 'sandbox') window.Paddle.Environment.set('sandbox');
      window.Paddle.Initialize({
        token: clientToken,
        checkout: locale ? { settings: { locale } } : undefined,
        // Retain identifies the subscriber here and nowhere else. Initialize runs once per page load, so a
        // customer id that only arrives later cannot be attached afterwards - callers that have one must
        // hold off on the first call until they do (see PricingPage). Omitted entirely when unknown, which
        // is the honest state for the public /checkout page and for an org that has never paid.
        pwCustomer: paddleCustomerId ? { id: paddleCustomerId } : undefined,
        eventCallback: event => {
          if (event.name === 'checkout.completed') handlers.onCompleted?.();
          if (event.name === 'checkout.closed') handlers.onClosed?.();
        }
      });
      return window.Paddle;
    });
  }
  return readyPromise;
}

export function openPaddleCheckout(paddle: PaddleGlobal, options: PaddleCheckoutOpenOptions): void {
  paddle.Checkout.open(options);
}
