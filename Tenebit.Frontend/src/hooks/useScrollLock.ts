import { useEffect } from 'react';

// Shared across every overlay (Modal, ConfirmDialog, SlidePanel, ...) so nested or
// out-of-order open/close never leaves body scroll stuck locked or unlocked - each
// active overlay holds one "vote" and the lock only releases once every vote is gone.
let lockCount = 0;
let originalOverflow = '';

export function useScrollLock(active: boolean) {
  useEffect(() => {
    if (!active) return;
    if (lockCount === 0) {
      originalOverflow = document.body.style.overflow;
      document.body.style.overflow = 'hidden';
    }
    lockCount++;
    return () => {
      lockCount--;
      if (lockCount === 0) {
        document.body.style.overflow = originalOverflow;
      }
    };
  }, [active]);
}
