import { useEffect, useId, useRef } from 'react';
import { AlertTriangle, Rocket } from 'lucide-react';
import { Button } from './Button';
import { useI18n } from '../i18n/I18nProvider';
import { useScrollLock } from '../hooks/useScrollLock';

const focusableSelector = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export function ConfirmDialog({ open, title, description, confirmLabel, confirmDisabled, variant = 'warning', onConfirm, onClose, children }: { open: boolean; title: string; description: string; confirmLabel?: string; confirmDisabled?: boolean; variant?: 'warning' | 'positive'; onConfirm: () => void; onClose: () => void; children?: React.ReactNode }) {
  const { t } = useI18n();
  const titleId = useId();
  const descriptionId = useId();
  const panelRef = useRef<HTMLElement | null>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  useScrollLock(open);

  useEffect(() => {
    if (!open) return;
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const focusTimer = window.setTimeout(() => {
      panelRef.current?.querySelector<HTMLElement>(focusableSelector)?.focus();
    }, 0);

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
      if (event.key !== 'Tab' || !panelRef.current) return;
      const focusable = Array.from(panelRef.current.querySelectorAll<HTMLElement>(focusableSelector));
      if (!focusable.length) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => {
      window.clearTimeout(focusTimer);
      document.removeEventListener('keydown', onKeyDown);
      previousFocusRef.current?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;
  return (
    <div className="modalOverlay" role="presentation">
      <button className="modalBackdrop" type="button" aria-label={t('common.cancel')} onClick={onClose} />
      <section className="confirmDialog" role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={descriptionId} ref={panelRef}>
        <div className={`confirmIcon${variant === 'positive' ? ' confirmIcon--positive' : ''}`}>{variant === 'positive' ? <Rocket size={20} /> : <AlertTriangle size={20} />}</div>
        <h2 id={titleId}>{title}</h2>
        <p id={descriptionId}>{description}</p>
        {children}
        <div className="formActions formActions--split"><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant={variant === 'positive' ? 'primary' : 'danger'} disabled={confirmDisabled} onClick={onConfirm}>{confirmLabel ?? t('common.confirmDelete')}</Button></div>
      </section>
    </div>
  );
}
