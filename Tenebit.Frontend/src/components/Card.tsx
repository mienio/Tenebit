import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <section className={`card ${className}`}>{children}</section>;
}

export function MetricCard({ label, value, hint, icon, to }: { label: string; value: string | number; hint?: string; icon?: ReactNode; to?: string }) {
  const content = (
    <>
      <div className="metricCard__top">
        <span>{label}</span>
        {icon ? <span className="metricCard__icon">{icon}</span> : null}
      </div>
      <div className="metricCard__body">
        <strong>{value}</strong>
        {hint ? <small>{hint}</small> : null}
      </div>
    </>
  );

  if (to) {
    return <Link to={to} className="card metricCard metricCard--link">{content}</Link>;
  }

  return <Card className="metricCard">{content}</Card>;
}
