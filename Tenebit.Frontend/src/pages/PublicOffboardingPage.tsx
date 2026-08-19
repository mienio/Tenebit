import { Camera, CheckCircle2, ChevronRight, PackageCheck, Upload } from 'lucide-react';
import { useMemo, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ErrorState, LoadingState } from '../components/StateViews';
import { PublicFooter } from '../components/PublicFooter';
import { useAsyncData } from '../hooks/useAsyncData';
import { usePublicCapabilitySession } from '../hooks/usePublicCapabilitySession';
import { useI18n } from '../i18n/I18nProvider';

const responseValues = ['HaveWillReturn', 'AlreadyReturned', 'DontHave', 'Damaged'] as const;

export function PublicOffboardingPage() {
  const { t } = useI18n();
  const capability = usePublicCapabilitySession('offboarding');
  const loader = useMemo(() => () => capability === 'ready' ? api.publicOffboarding() : Promise.resolve(null), [capability]);
  const { data, error, isLoading, reload } = useAsyncData(loader, [loader]);
  const [step, setStep] = useState<'form' | 'review' | 'submitted'>('form');
  const [answers, setAnswers] = useState<Record<string, { response: string; comment: string; uploading: boolean }>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function upload(itemId: string, file: File) {
    setAnswers(current => ({ ...current, [itemId]: { response: current[itemId]?.response ?? 'Damaged', comment: current[itemId]?.comment ?? '', uploading: true } }));
    try {
      await api.uploadPublicOffboardingEvidence(itemId, file);
      setMessage(null);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('publicOffboarding.uploadFailed'));
    } finally {
      setAnswers(current => ({ ...current, [itemId]: { response: current[itemId]?.response ?? 'Damaged', comment: current[itemId]?.comment ?? '', uploading: false } }));
    }
  }

  async function submit() {
    if (!data) return;
    setSubmitting(true);
    setMessage(null);
    try {
      await api.submitPublicOffboardingResponse({
        answers: data.items.map(item => ({
          itemId: item.id,
          response: answers[item.id]?.response ?? 'HaveWillReturn',
          comment: answers[item.id]?.comment || null
        }))
      });
      await reload();
      setStep('submitted');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('publicOffboarding.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  }

  if (capability === 'loading') return <LoadingState title={t('publicOffboarding.loadingTitle')} description={t('publicOffboarding.loadingDesc')} />;
  if (capability === 'error') return <ErrorState message={t('publicOffboarding.invalidLink')} />;
  if (isLoading && !data) return <LoadingState title={t('publicOffboarding.loadingTitle')} description={t('publicOffboarding.loadingDesc')} />;
  if (error || !data) return <ErrorState message={error ?? t('publicOffboarding.invalidLink')} onRetry={reload} />;

  return (
    <main className="authShell">
      <section className="authCard" style={{ width: 'min(680px, 100%)' }}>
        <div className="authTop"><div className="authIcon"><PackageCheck size={24} /></div></div>
        <h1>{t('publicOffboarding.title')}</h1>
        <p>{t('publicOffboarding.intro', { org: data.organizationName, dueDate: new Date(data.returnDueDate).toLocaleDateString() })}</p>
        {data.defaultReturnLocation ? <p className="muted">{t('publicOffboarding.returnLocation', { location: data.defaultReturnLocation })}</p> : null}
        {data.notes ? <p className="muted">{data.notes}</p> : null}

        {message ? <p className="formMessage formMessage--error">{message}</p> : null}

        {step === 'submitted' ? (
          <div className="formActions formActions--split">
            <p style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--success)' }}><CheckCircle2 size={18} /> {t('publicOffboarding.submitted')}</p>
          </div>
        ) : step === 'review' ? (
          <>
            <div className="formSectionTitle">{t('publicOffboarding.reviewTitle')}</div>
            <div className="listRows">
              {data.items.map(item => (
                <div className="listRow" key={item.id}>
                  <div>
                    <strong>{item.label}</strong>
                    <small>{item.assetTag ?? '-'}</small>
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <strong>{t(`publicOffboarding.response.${answers[item.id]?.response ?? 'HaveWillReturn'}`)}</strong>
                    <small>{answers[item.id]?.comment || '-'}</small>
                  </div>
                </div>
              ))}
            </div>
            <Card className="card--flat">
              <p className="muted">{t('publicOffboarding.privacyNotice')}</p>
            </Card>
            <div className="formActions formActions--split">
              <Button type="button" variant="ghost" onClick={() => setStep('form')}>{t('common.back')}</Button>
              <Button disabled={submitting} onClick={submit}>{submitting ? t('publicOffboarding.submitting') : t('publicOffboarding.submitButton')}</Button>
            </div>
          </>
        ) : (
          <>
            <div className="formSectionTitle">{t('publicOffboarding.itemsTitle')}</div>
            <div className="listRows">
              {data.items.map(item => {
                const current = answers[item.id] ?? { response: item.employeeResponse ?? 'HaveWillReturn', comment: item.employeeComment ?? '', uploading: false };
                return (
                  <div className="listRow" key={item.id} style={{ alignItems: 'stretch' }}>
                    <div style={{ flex: 1 }}>
                      <strong>{item.label}</strong>
                      <small>{item.assetTag ?? '-'}</small>
                      <select value={current.response} onChange={event => setAnswers(state => ({ ...state, [item.id]: { ...current, response: event.target.value } }))} style={{ marginTop: '10px' }}>
                        {responseValues.map(value => <option key={value} value={value}>{t(`publicOffboarding.response.${value}`)}</option>)}
                      </select>
                      <textarea
                        value={current.comment}
                        onChange={event => setAnswers(state => ({ ...state, [item.id]: { ...current, comment: event.target.value } }))}
                        placeholder={t('publicOffboarding.commentPlaceholder')}
                        rows={3}
                        style={{ marginTop: '10px', width: '100%' }}
                      />
                    </div>
                    {current.response === 'Damaged' ? (
                      <label className="button button--secondary" style={{ alignSelf: 'center' }}>
                        <span className="button__icon">{current.uploading ? <Upload size={16} /> : <Camera size={16} />}</span>
                        <span>{current.uploading ? t('publicOffboarding.uploading') : t('publicOffboarding.uploadPhoto')}</span>
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
            <Card className="card--flat">
              <p className="muted">{t('publicOffboarding.privacyNotice')}</p>
            </Card>
            <div className="formActions formActions--split">
              <span />
              <Button type="button" onClick={() => setStep('review')} icon={<ChevronRight size={16} />}>{t('publicOffboarding.reviewButton')}</Button>
            </div>
          </>
        )}
      </section>
      <PublicFooter compact />
    </main>
  );
}
