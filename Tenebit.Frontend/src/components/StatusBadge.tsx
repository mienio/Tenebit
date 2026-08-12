import type { CSSProperties } from 'react';
import { useI18n } from '../i18n/I18nProvider';

export function StatusBadge({ status, label, color, backgroundColor }: { status: string; label?: string; color?: string; backgroundColor?: string }) {
  const { t } = useI18n();
  const style: CSSProperties | undefined = color ? { background: backgroundColor ?? 'var(--surface)', color } : undefined;
  return <span className={`status status--${status}`} style={style}>{label ?? t(`status.${status}`)}</span>;
}
