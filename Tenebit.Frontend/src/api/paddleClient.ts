// Paddle Billing has no server-generated hosted checkout redirect URL (unlike the old Stripe Checkout
// Session) - Paddle.js runs client-side and opens the checkout overlay itself. This loads the script once
// from Paddle's own CDN (required - see developer.paddle.com/paddlejs/include-paddlejs) and initializes
// it with the public client-side token the backend hands out via GET /api/subscription/paddle-config.

export interface PaddleCheckoutOpenOptions {
  items: { priceId: string; quantity: number }[];
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
  };
}

interface PaddleEvent {
  name: string;
}

interface PaddleGlobal {
  Environment: { set: (environment: 'sandbox' | 'production') => void };
  Initialize: (options: { token: string; eventCallback?: (event: PaddleEvent) => void }) => void;
  Checkout: { open: (options: PaddleCheckoutOpenOptions) => void };
}

declare global {
  interface Window {
    Paddle?: PaddleGlobal;
  }
}

let readyPromise: Promise<PaddleGlobal> | null = null;

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
 * script. `onCheckoutCompleted` is wired into Paddle's global eventCallback (there is no per-open
 * callback - Paddle Billing overlay checkout never navigates the browser on its own after a successful
 * payment, unlike a classic hosted-redirect flow, so without this the app is left showing whatever state
 * it had before the purchase until the user manually hard-refreshes).
 */
export function ensurePaddleReady(
  clientToken: string,
  environment: 'sandbox' | 'production',
  onCheckoutCompleted?: () => void
): Promise<PaddleGlobal> {
  if (!readyPromise) {
    readyPromise = loadScript().then(() => {
      if (!window.Paddle) throw new Error('Paddle.js nie zostało poprawnie załadowane.');
      if (environment === 'sandbox') window.Paddle.Environment.set('sandbox');
      window.Paddle.Initialize({
        token: clientToken,
        eventCallback: onCheckoutCompleted
          ? event => { if (event.name === 'checkout.completed') onCheckoutCompleted(); }
          : undefined
      });
      return window.Paddle;
    });
  }
  return readyPromise;
}

export function openPaddleCheckout(paddle: PaddleGlobal, options: PaddleCheckoutOpenOptions): void {
  paddle.Checkout.open(options);
}
