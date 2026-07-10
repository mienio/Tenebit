import type { ReactNode } from 'react';
import { X } from 'lucide-react';

export function Drawer({ open, title, description, children, onClose }: { open: boolean; title: string; description?: string; children: ReactNode; onClose: () => void }) {
  if (!open) return null;
  return (
    <div className="drawerOverlay" role="dialog" aria-modal="true">
      <button className="drawerBackdrop" onClick={onClose} aria-label="Zamknij panel" />
      <section className="drawerPanel">
        <header className="drawerHeader">
          <div><h2>{title}</h2>{description ? <p>{description}</p> : null}</div>
          <button className="iconButton" onClick={onClose} aria-label="Zamknij"><X size={18} /></button>
        </header>
        <div className="drawerBody">{children}</div>
      </section>
    </div>
  );
}
