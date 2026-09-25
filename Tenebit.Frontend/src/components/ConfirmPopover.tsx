import { useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Button } from './Button';
import { useI18n } from '../i18n/I18nProvider';

/**
 * Lekkie potwierdzenie przy przycisku akcji zmieniającej stan (np. akceptacja wydania w wierszu tabeli).
 * Pełny modal byłby tu za ciężki, a brak potwierdzenia kończył się przypadkową akceptacją przy próbie
 * kliknięcia sąsiedniej ikony podglądu. Renderowane przez portal z pozycją `fixed`, bo tabela ma
 * `overflow-x: auto` i przycięłaby dymek. Zamyka się Anuluj, Escape, kliknięciem obok i przewinięciem.
 */
export function ConfirmPopover({ trigger, message, confirmLabel, onConfirm }: {
  trigger(props: { onClick(): void; 'aria-expanded': boolean; 'aria-haspopup': 'dialog'; ref: React.Ref<HTMLButtonElement> }): ReactNode;
  message: string;
  confirmLabel: string;
  onConfirm(): void | Promise<void>;
}) {
  const { t } = useI18n();
  const titleId = useId();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [position, setPosition] = useState<{ top: number; left: number } | null>(null);

  useLayoutEffect(() => {
    if (!open || !triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    const width = 280;
    const left = Math.max(8, Math.min(rect.right - width, window.innerWidth - width - 8));
    const below = rect.bottom + 8;
    const panelHeight = panelRef.current?.offsetHeight ?? 120;
    const top = below + panelHeight > window.innerHeight - 8 ? Math.max(8, rect.top - panelHeight - 8) : below;
    setPosition({ top, left });
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const focusTimer = window.setTimeout(() => panelRef.current?.querySelector<HTMLElement>('[data-cancel]')?.focus(), 0);
    const close = () => setOpen(false);
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.stopPropagation();
      setOpen(false);
      triggerRef.current?.focus();
    };
    const onPointer = (event: PointerEvent) => {
      const target = event.target as Node;
      if (panelRef.current?.contains(target) || triggerRef.current?.contains(target)) return;
      setOpen(false);
    };
    document.addEventListener('keydown', onKey, true);
    document.addEventListener('pointerdown', onPointer, true);
    window.addEventListener('scroll', close, true);
    window.addEventListener('resize', close);
    return () => {
      window.clearTimeout(focusTimer);
      document.removeEventListener('keydown', onKey, true);
      document.removeEventListener('pointerdown', onPointer, true);
      window.removeEventListener('scroll', close, true);
      window.removeEventListener('resize', close);
    };
  }, [open]);

  async function confirm() {
    setBusy(true);
    try {
      await onConfirm();
      setOpen(false);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      {trigger({ onClick: () => setOpen(value => !value), 'aria-expanded': open, 'aria-haspopup': 'dialog', ref: triggerRef })}
      {open ? createPortal(
        <div
          ref={panelRef}
          className="confirmPopover"
          role="dialog"
          aria-modal="false"
          aria-labelledby={titleId}
          style={position ? { top: position.top, left: position.left } : { visibility: 'hidden' }}
        >
          <p id={titleId}>{message}</p>
          <div className="confirmPopover__actions">
            <Button type="button" variant="ghost" data-cancel onClick={() => { setOpen(false); triggerRef.current?.focus(); }}>{t('common.cancel')}</Button>
            <Button type="button" disabled={busy} onClick={() => void confirm()}>{busy ? t('common.saving') : confirmLabel}</Button>
          </div>
        </div>,
        document.body
      ) : null}
    </>
  );
}
