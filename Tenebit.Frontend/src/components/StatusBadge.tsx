import { useI18n } from '../i18n/I18nProvider';

export function StatusBadge({ status }: { status: string }) {
  const { t } = useI18n();
  return <span className={`status status--${status}`}>{t(`status.${status}`)}</span>;
}
