import { ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useI18n } from '../i18n/I18nProvider';

export function BackButton({ to }: { to: string }) {
  const navigate = useNavigate();
  const { t } = useI18n();
  return (
    <button type="button" className="authIcon" aria-label={t('common.back')} title={t('common.back')} onClick={() => navigate(to)}>
      <ArrowLeft size={24} />
    </button>
  );
}
