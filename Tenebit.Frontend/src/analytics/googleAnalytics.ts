const GA_MEASUREMENT_ID = 'G-SZRY4K9E70';
const GA_SECONDARY_ID = 'GT-577T49PM';

declare global {
  interface Window {
    dataLayer?: unknown[];
  }
}

let loaded = false;

// Only call after consent is granted - see src/analytics/consent.ts.
export function loadGoogleAnalytics() {
  if (loaded || typeof document === 'undefined') return;
  loaded = true;

  const script = document.createElement('script');
  script.async = true;
  script.src = `https://www.googletagmanager.com/gtag/js?id=${GA_MEASUREMENT_ID}`;
  document.head.appendChild(script);

  window.dataLayer = window.dataLayer || [];
  const gtag = (...args: unknown[]) => window.dataLayer!.push(args);
  gtag('js', new Date());
  gtag('config', GA_MEASUREMENT_ID);
  gtag('config', GA_SECONDARY_ID);
}
