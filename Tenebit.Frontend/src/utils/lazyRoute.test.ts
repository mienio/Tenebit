import { describe, expect, it } from 'vitest';
import { RELOAD_GUARD_MS, shouldReloadForStaleBuild } from './lazyRoute';

describe('shouldReloadForStaleBuild', () => {
  it('reloads the first time a chunk fails to load', () => {
    expect(shouldReloadForStaleBuild(1_000_000, null)).toBe(true);
  });

  it('does not reload again right after one that did not help', () => {
    const now = 1_000_000;
    expect(shouldReloadForStaleBuild(now, now - 1_000)).toBe(false);
    expect(shouldReloadForStaleBuild(now, now - RELOAD_GUARD_MS)).toBe(false);
  });

  // Otherwise a single failure would disarm the recovery for the rest of the session, and the next
  // deploy would put the user right back on the dead "Try again" screen.
  it('reloads again for a failure well after the last attempt', () => {
    const now = 1_000_000;
    expect(shouldReloadForStaleBuild(now, now - RELOAD_GUARD_MS - 1)).toBe(true);
  });

  // A marker from the future (clock moved back) must not lock the page out of recovering.
  it('ignores a marker newer than now', () => {
    expect(shouldReloadForStaleBuild(1_000_000, 2_000_000)).toBe(true);
  });
});
