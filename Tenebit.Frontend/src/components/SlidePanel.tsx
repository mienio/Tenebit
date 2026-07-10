import { X } from 'lucide-react';
import { useEffect, useRef } from 'react';

interface SlidePanelProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  width?: 'default' | 'wide';
}

export function SlidePanel({ open, onClose, title, description, children, width = 'default' }: SlidePanelProps) {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
      panelRef.current?.focus();
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [open]);

  useEffect(() => {
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && open) {
        onClose();
      }
    };
    window.addEventListener('keydown', handleEsc);
    return () => window.removeEventListener('keydown', handleEsc);
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
        aria-labelledby="slide-panel-title"
        tabIndex={-1}
      >
        <div className="slide-panel-header">
          <div>
            <h2 id="slide-panel-title">{title}</h2>
            {description && <p>{description}</p>}
          </div>
          <button
            onClick={onClose}
            className="slide-panel-close"
            aria-label="Zamknij panel"
          >
            <X size={20} />
          </button>
        </div>
        <div className="slide-panel-body">{children}</div>
      </div>
    </div>
  );
}
