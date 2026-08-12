import { ReactNode, SyntheticEvent, useEffect, useId, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { ConfirmDialog } from './ConfirmDialog';
import { useI18n } from '../i18n/I18nProvider';
import { useScrollLock } from '../hooks/useScrollLock';

interface ModalProps {
  open: boolean;
  title: string;
  description?: string;
  children: ReactNode;
  onClose: () => void;
  width?: 'normal' | 'wide';
  closeDisabled?: boolean;
}

const focusableSelector = 'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

export function keepFocusInside(event: KeyboardEvent, panel: HTMLElement) {
  if (event.key !== 'Tab') return;
  const focusable = Array.from(panel.querySelectorAll<HTMLElement>(focusableSelector)).filter(element => element.offsetParent !== null || element === document.activeElement);
  if (focusable.length === 0) {
    event.preventDefault();
    panel.focus();
    return;
  }

  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}

export function Modal({ open, title, description, children, onClose, width = 'normal', closeDisabled = false }: ModalProps) {
  const { t } = useI18n();
  const titleId = useId();
  const descriptionId = useId();
  const panelRef = useRef<HTMLElement | null>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);
  const dirtyRef = useRef(false);
  const [confirmDiscard, setConfirmDiscard] = useState(false);

  useScrollLock(open);

  useEffect(() => {
    if (!open) return;
    dirtyRef.current = false;
    setConfirmDiscard(false);
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const focusTimer = window.setTimeout(() => {
      const panel = panelRef.current;
      if (!panel) return;
      const firstFocusable = panel.querySelector<HTMLElement>(focusableSelector);
      (firstFocusable ?? panel).focus();
    }, 0);

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !closeDisabled) {
        if (dirtyRef.current) setConfirmDiscard(true);
        else onClose();
      }
      const panel = panelRef.current;
      if (panel) keepFocusInside(event, panel);
    };

    document.addEventListener('keydown', onKeyDown);
    return () => {
      window.clearTimeout(focusTimer);
      document.removeEventListener('keydown', onKeyDown);
      previousFocusRef.current?.focus();
    };
  }, [open, onClose, closeDisabled]);

  if (!open) return null;

  function requestClose() {
    if (closeDisabled) return;
    if (dirtyRef.current) setConfirmDiscard(true);
    else onClose();
  }

  function markDirty(event: SyntheticEvent<HTMLDivElement>) {
    const target = event.target as HTMLInputElement;
    if ((target.type === 'checkbox' || target.type === 'radio') && target.checked === target.defaultChecked) return;
    dirtyRef.current = true;
  }

  return (
    <div className="modalOverlay" role="presentation">
      <button className="modalBackdrop" type="button" aria-label={t('common.close')} onClick={requestClose} style={closeDisabled ? { cursor: 'not-allowed' } : undefined} />
      <section
        className={width === 'wide' ? 'modalPanel modalPanel--wide' : 'modalPanel'}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        tabIndex={-1}
        ref={panelRef}
      >
        <header className="modalHeader">
          <div>
            <h2 id={titleId}>{title}</h2>
            {description ? <p id={descriptionId}>{description}</p> : null}
          </div>
          <button className="iconButton" type="button" aria-label={t('common.close')} title={closeDisabled ? t('common.closeDisabledHint') : undefined} disabled={closeDisabled} onClick={requestClose}><X size={18} /></button>
        </header>
        <div className="modalBody" onInput={markDirty} onChange={markDirty}>{children}</div>
      </section>

      <ConfirmDialog
        open={confirmDiscard}
        title={t('common.discardTitle')}
        description={t('common.discardDesc')}
        confirmLabel={t('common.discardConfirm')}
        onConfirm={() => { setConfirmDiscard(false); onClose(); }}
        onClose={() => setConfirmDiscard(false)}
      />
    </div>
  );
}
