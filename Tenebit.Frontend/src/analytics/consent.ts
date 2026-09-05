import { loadGoogleAnalytics } from './googleAnalytics';

export type ConsentChoice = 'accepted' | 'rejected';

const CONSENT_KEY = 'tenebit_cookie_consent';

export function getStoredConsent(): ConsentChoice | null {
  try {
    const value = window.localStorage.getItem(CONSENT_KEY);
    return value === 'accepted' || value === 'rejected' ? value : null;
  } catch {
    return null;
  }
}

export function storeConsent(choice: ConsentChoice) {
  try { window.localStorage.setItem(CONSENT_KEY, choice); } catch { /* storage can be unavailable */ }
  if (choice === 'accepted') loadGoogleAnalytics();
}

export function clearConsent() {
  try { window.localStorage.removeItem(CONSENT_KEY); } catch { /* storage can be unavailable */ }
}

// Called once on app start so returning visitors who already accepted don't need to see the banner again.
export function initGoogleAnalyticsIfConsented() {
  if (getStoredConsent() === 'accepted') loadGoogleAnalytics();
}
