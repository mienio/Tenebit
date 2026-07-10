import { AlertCircle, Inbox, Loader2, RefreshCw } from 'lucide-react';
import { Button } from './Button';
import { useI18n } from '../i18n/I18nProvider';

export function LoadingState({ title, description }: { title?: string; description?: string }) {
  const { t } = useI18n();
  return <div className="stateBox" aria-live="polite"><Loader2 className="spin" size={30} /><h2>{title ?? t('common.loading')}</h2>{description ? <p>{description}</p> : null}</div>;
}

export function ErrorState({ title, message, onRetry }: { title?: string; message: string; onRetry?: () => void }) {
  const { t } = useI18n();
  return <div className="stateBox stateBox--error" role="alert"><AlertCircle size={30} /><h2>{title ?? t('common.loadError')}</h2><p>{message}</p>{onRetry ? <Button variant="secondary" onClick={onRetry} icon={<RefreshCw size={16} />}>{t('common.retry')}</Button> : null}</div>;
}

export function EmptyState({ title, description, action }: { title: string; description: string; action?: React.ReactNode }) {
  return <div className="stateBox stateBox--empty"><Inbox size={30} /><h3>{title}</h3><p>{description}</p>{action ? <div>{action}</div> : null}</div>;
}
