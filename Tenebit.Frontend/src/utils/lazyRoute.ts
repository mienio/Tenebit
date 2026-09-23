import { lazy, type ComponentType } from 'react';

/**
 * Every route in App.tsx is its own content-hashed chunk, and a deploy replaces those files. A tab left
 * open across one - the "I come back to the app after a while and it says SOMETHING WENT WRONG" case -
 * still holds the previous index.html, so navigating to /assets or /settings asks for a chunk filename
 * that no longer exists on the server. The dynamic import rejects and the error boundary paints over the
 * whole page.
 *
 * React.lazy caches that rejection forever, so re-rendering the same element can never recover from it:
 * the boundary's "Try again" button was, by construction, dead for exactly this failure. Only a full
 * document load fixes it, because only that re-fetches index.html (served no-store, see nginx.conf) and
 * with it the current chunk names - which is why F5 always worked.
 *
 * So the reload happens by itself, once, on the first failed chunk load. The timestamp guard is what
 * keeps a genuinely broken deploy (the chunk is still missing after reloading) from looping forever: the
 * second failure inside the window falls through to the boundary and shows a real error.
 */
const RELOAD_MARKER = 'tenebit:stale-build-reload';

/** A reload that did not fix the import within this window means the build itself is broken, not stale. */
export const RELOAD_GUARD_MS = 30_000;

export function shouldReloadForStaleBuild(now: number, lastReloadAt: number | null): boolean {
  return lastReloadAt === null || now - lastReloadAt > RELOAD_GUARD_MS || now < lastReloadAt;
}

function readLastReload(): number | null {
  try {
    const raw = sessionStorage.getItem(RELOAD_MARKER);
    if (raw === null) return null;
    const parsed = Number(raw);
    return Number.isFinite(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

/** Reloads the document unless that was already tried just now. Returns whether a reload is under way.
 * A browser that refuses sessionStorage gets no reload at all rather than an unstoppable loop of them:
 * without the marker there is nothing to tell the first attempt from the hundredth. */
function reloadOnce(): boolean {
  const now = Date.now();
  if (!shouldReloadForStaleBuild(now, readLastReload())) return false;

  try {
    sessionStorage.setItem(RELOAD_MARKER, String(now));
  } catch {
    return false;
  }

  window.location.reload();
  return true;
}

/** Drop-in replacement for React.lazy for route-level chunks; see the note above. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function lazyRoute<T extends ComponentType<any>>(load: () => Promise<{ default: T }>) {
  return lazy(() => load().catch((error: unknown) => {
    if (!reloadOnce()) throw error;
    // The document is already on its way out - resolving now would flash a half-rendered route first.
    return new Promise<{ default: T }>(() => {});
  }));
}

/** Vite reports a failed chunk *preload* (a router prefetch, or a module preload hint) on the window
 * rather than through the importing component, so that path needs the same recovery. Cancelling the
 * event stops Vite from rethrowing into an unhandled rejection while the reload is in flight. */
export function installStaleBuildRecovery(): void {
  window.addEventListener('vite:preloadError', (event: Event) => {
    if (reloadOnce()) event.preventDefault();
  });
}
