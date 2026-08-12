import { X } from 'lucide-react';
import { useEffect, useId, useRef } from 'react';
import { keepFocusInside } from './Modal';
import { useI18n } from '../i18n/I18nProvider';
import { useScrollLock } from '../hooks/useScrollLock';

interface SlidePanelProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  width?: 'default' | 'wide';
}

export function SlidePanel({ open, onClose, title, description, children, width = 'default' }: SlidePanelProps) {
  const { t } = useI18n();
  const titleId = useId();
  const panelRef = useRef<HTMLDivElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  useScrollLock(open);

  useEffect(() => {
    if (!open) return;
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const focusTimer = window.setTimeout(() => panelRef.current?.focus(), 0);

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
      if (panelRef.current) keepFocusInside(event, panelRef.current);
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
    <div className="slide-panel-overlay" onClick={onClose}>
      <div
        ref={panelRef}
        className={`slide-panel ${width === 'wide' ? 'slide-panel--wide' : ''}`}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
      >
        <div className="slide-panel-header">
          <div>
            <h2 id={titleId}>{title}</h2>
            {description && <p>{description}</p>}
          </div>
          <button
            onClick={onClose}
            className="slide-panel-close"
            aria-label={t('common.close')}
          >
            <X size={20} />
          </button>
        </div>
        <div className="slide-panel-body">{children}</div>
      </div>
    </div>
  );
}
