import { Camera, CheckCircle2, ChevronRight, ClipboardCheck, Upload } from 'lucide-react';
import { useMemo, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { ErrorState, LoadingState } from '../components/StateViews';
import { PublicFooter } from '../components/PublicFooter';
import { useAsyncData } from '../hooks/useAsyncData';
import { usePublicCapabilitySession } from '../hooks/usePublicCapabilitySession';
import { useI18n } from '../i18n/I18nProvider';
import type { AssetAuditResponse } from '../types/domain';

const responseValues: AssetAuditResponse[] = ['Confirmed', 'Missing', 'Damaged', 'WrongOwner'];

export function PublicAssetAuditPage() {
  const { t } = useI18n();
  const capability = usePublicCapabilitySession('asset-audit');
  const loader = useMemo(() => () => capability === 'ready' ? api.publicAssetAudit() : Promise.resolve(null), [capability]);
  const { data, error, isLoading, reload } = useAsyncData(loader, [loader]);
  const [step, setStep] = useState<'form' | 'review' | 'submitted'>('form');
  const [answers, setAnswers] = useState<Record<string, { response: AssetAuditResponse; comment: string; uploading: boolean }>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function upload(itemId: string, file: File) {
    setAnswers(current => ({ ...current, [itemId]: { response: current[itemId]?.response ?? 'Missing', comment: current[itemId]?.comment ?? '', uploading: true } }));
    try {
      await api.uploadPublicAssetAuditEvidence(itemId, file);
      setMessage(null);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('publicAssetAudit.uploadFailed'));
    } finally {
      setAnswers(current => ({ ...current, [itemId]: { response: current[itemId]?.response ?? 'Missing', comment: current[itemId]?.comment ?? '', uploading: false } }));
    }
  }

  async function submit() {
    if (!data) return;
    setSubmitting(true);
    setMessage(null);
    try {
      for (const item of data.items) {
        const answer = answers[item.id];
        if (!answer) continue;
        await api.submitPublicAssetAuditItemResponse(item.id, { response: answer.response, comment: answer.comment || null });
      }
      await api.submitPublicAssetAudit();
      await reload();
      setStep('submitted');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('publicAssetAudit.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  }

  if (capability === 'loading') return <LoadingState title={t('publicAssetAudit.loadingTitle')} description={t('publicAssetAudit.loadingDesc')} />;
  if (capability === 'error') return <ErrorState message={t('publicAssetAudit.invalidLink')} />;
  if (isLoading && !data) return <LoadingState title={t('publicAssetAudit.loadingTitle')} description={t('publicAssetAudit.loadingDesc')} />;
  if (error || !data) return <ErrorState message={error ?? t('publicAssetAudit.invalidLink')} onRetry={reload} />;

  return (
    <main className="authShell">
      <section className="authCard" style={{ width: 'min(680px, 100%)' }}>
        <div className="authTop"><div className="authIcon"><ClipboardCheck size={24} /></div></div>
        <h1>{t('publicAssetAudit.title')}</h1>
        <p>{t('publicAssetAudit.intro', { org: data.organizationName, campaign: data.campaignName, dueDate: new Date(data.dueDate).toLocaleDateString() })}</p>

        {message ? <p className="formMessage formMessage--error">{message}</p> : null}

        {step === 'submitted' ? (
          <div className="formActions formActions--split">
            <p style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--success)' }}><CheckCircle2 size={18} /> {t('publicAssetAudit.submitted')}</p>
          </div>
        ) : step === 'review' ? (
          <>
            <div className="formSectionTitle">{t('publicAssetAudit.reviewTitle')}</div>
            <div className="listRows">
              {data.items.map(item => (
                <div className="listRow" key={item.id}>
                  <div>
                    <strong>{item.assetName}</strong>
                    <small>{item.assetTag}</small>
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <strong>{t(`publicAssetAudit.response.${answers[item.id]?.response ?? 'Confirmed'}`)}</strong>
                    <small>{answers[item.id]?.comment || '-'}</small>
                  </div>
                </div>
              ))}
            </div>
            <div className="formActions formActions--split">
              <Button type="button" variant="ghost" onClick={() => setStep('form')}>{t('common.back')}</Button>
              <Button disabled={submitting} onClick={submit}>{submitting ? t('publicAssetAudit.submitting') : t('publicAssetAudit.submitButton')}</Button>
            </div>
          </>
        ) : (
          <>
            <div className="formSectionTitle">{t('publicAssetAudit.itemsTitle')}</div>
            <div className="listRows">
              {data.items.map(item => {
                const current = answers[item.id] ?? { response: item.response ?? 'Confirmed', comment: item.comment ?? '', uploading: false };
                return (
                  <div className="listRow" key={item.id} style={{ alignItems: 'stretch' }}>
                    <div style={{ flex: 1 }}>
                      <strong>{item.assetName}</strong>
                      <small>{item.assetTag}{item.model ? ` · ${item.model}` : ''}</small>
                      <select
                        value={current.response}
                        disabled={data.readOnly}
                        onChange={event => setAnswers(state => ({ ...state, [item.id]: { ...current, response: event.target.value as AssetAuditResponse } }))}
                        style={{ marginTop: '10px' }}
                      >
                        {responseValues.map(value => <option key={value} value={value}>{t(`publicAssetAudit.response.${value}`)}</option>)}
                      </select>
                      <textarea
                        value={current.comment}
                        disabled={data.readOnly}
                        onChange={event => setAnswers(state => ({ ...state, [item.id]: { ...current, comment: event.target.value } }))}
                        placeholder={t('publicAssetAudit.commentPlaceholder')}
                        rows={3}
                        style={{ marginTop: '10px', width: '100%' }}
                      />
                    </div>
                    {current.response !== 'Confirmed' && !data.readOnly ? (
                      <label className="button button--secondary" style={{ alignSelf: 'center' }}>
                        <span className="button__icon">{current.uploading ? <Upload size={16} /> : <Camera size={16} />}</span>
                        <span>{current.uploading ? t('publicAssetAudit.uploading') : t('publicAssetAudit.uploadPhoto')}</span>
                        <input type="file" accept="image/*" hidden onChange={event => {
                          const file = event.target.files?.[0];
                          if (file) void upload(item.id, file);
                        }} />
                      </label>
                    ) : null}
                  </div>
                );
              })}
            </div>
            <div className="formActions formActions--split">
              <span />
              <Button type="button" disabled={data.readOnly} onClick={() => setStep('review')} icon={<ChevronRight size={16} />}>{t('publicAssetAudit.reviewButton')}</Button>
            </div>
          </>
        )}
      </section>
      <PublicFooter compact />
    </main>
  );
}
