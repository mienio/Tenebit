import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Plus, Rocket } from 'lucide-react';
import { Link } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { Field, SelectInput, TextArea, TextInput } from '../components/FormFields';
import { PageHeader } from '../components/PageHeader';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { toNullable } from '../utils/format';
import { useI18n } from '../i18n/I18nProvider';

export function OnboardingPage() {
  const { t } = useI18n();
  const people = useAsyncData(() => api.people(), []);
  const assets = useAsyncData(() => api.assets({ status: 'InStock' }), []);
  const procedures = useAsyncData(() => api.procedures(), []);
  const profiles = useAsyncData(api.jobProfiles, []);
  const [selectedAssetIds, setSelectedAssetIds] = useState<string[]>([]);
  const [selectedProcedureIds, setSelectedProcedureIds] = useState<string[]>([]);
  const [issueConditions, setIssueConditions] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const [assetFilter, setAssetFilter] = useState('');

  const publishedProcedures = useMemo(() => procedures.data?.filter(item => item.status === 'Published') ?? [], [procedures.data]);
  const visibleAssets = useMemo(() => {
    const query = assetFilter.trim().toLowerCase();
    const rows = assets.data ?? [];
    return query ? rows.filter(asset => `${asset.name} ${asset.assetTag}`.toLowerCase().includes(query)) : rows;
  }, [assets.data, assetFilter]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);
  const loading = people.isLoading || assets.isLoading || procedures.isLoading;
  const error = people.error || assets.error || procedures.error;

  function toggle(list: string[], value: string) {
    return list.includes(value) ? list.filter(item => item !== value) : [...list, value];
  }

  function applyProfile(profileId: string) {
    const profile = profiles.data?.find(item => item.id === profileId);
    if (!profile) return;
    const recommendedAssets = profile.assetCategoryIds
      .map(categoryId => assets.data?.find(asset => asset.categoryId === categoryId)?.id)
      .filter((id): id is string => Boolean(id));
    const recommendedProcedures = publishedProcedures.filter(procedure => profile.procedureIds.includes(procedure.id)).map(procedure => procedure.id);
    setSelectedAssetIds(recommendedAssets);
    setSelectedProcedureIds(recommendedProcedures);
  }

  async function createPackage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const formEl = event.currentTarget;
    const form = new FormData(formEl);
    const personId = String(form.get('personId') ?? '');
    if (!personId) return setMessage({ type: 'error', text: t('onboarding.choosePersonError') });
    if (!selectedAssetIds.length) return setMessage({ type: 'error', text: t('onboarding.chooseAssetError') });

    setSaving(true);
    setMessage(null);
    try {
      const response = await api.createAssignment({
        personId,
        assets: selectedAssetIds.map(assetId => ({ assetId, issueCondition: t(`issueCondition.${issueConditions[assetId] ?? 'ok'}`) })),
        procedureIds: selectedProcedureIds,
        dueDate: toNullable(String(form.get('dueDate') ?? '')),
        notes: toNullable(String(form.get('notes') ?? ''))
      });
      formEl.reset();
      setSelectedAssetIds([]);
      setSelectedProcedureIds([]);
      setIssueConditions({});
      setMessage({ type: 'success', text: t('onboarding.created', { protocol: response.protocolNumber }) });
      await Promise.all([assets.reload(), people.reload()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('onboarding.createFailed') });
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <LoadingState title={t('onboarding.loadingTitle')} />;
  if (error) return <ErrorState message={error} onRetry={() => void Promise.all([people.reload(), assets.reload(), procedures.reload(), profiles.reload()])} />;

  return (
    <div className="pageStack pageStack--narrow">
      <PageHeader eyebrow={t('page.onboarding.eyebrow')} title={t('page.onboarding.title')} />

      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      <Card>
        <div className="sectionTitle">
          <h2>{t('onboarding.createPackage')}</h2>
        </div>

        <form className="packageFlow" onSubmit={createPackage}>
          <section className="packageStep">
            <div className="packageStep__header"><span>1</span><div><strong>{t('onboarding.step1Title')}</strong></div></div>
            <Field label={t('onboarding.step1Title')}>
              <SelectInput name="personId" required>
                <option value="">{t('onboarding.choosePerson')}</option>
                {people.data?.map(person => <option value={person.id} key={person.id}>{person.fullName} · {person.email}</option>)}
              </SelectInput>
            </Field>
            {!people.data?.length ? <p className="emptyInline">{t('onboarding.noPeople')}</p> : null}
          </section>

          <section className="packageStep">
            <div className="packageStep__header"><span>2</span><div><strong>{t('onboarding.step2Title')}</strong><small>{t('onboarding.step2Hint')}</small></div></div>
            <Field label={t('onboarding.jobProfileLabel')}>
              <SelectInput name="jobProfileId" disabled={profiles.isLoading || !!profiles.error || !profiles.data?.length} onChange={event => applyProfile(event.target.value)}>
                <option value="">{profiles.data?.length ? t('onboarding.noProfileOption') : t('onboarding.noProfilesOption')}</option>
                {profiles.data?.map(profile => <option value={profile.id} key={profile.id}>{profile.name}</option>)}
              </SelectInput>
            </Field>
            {profiles.error ? <p className="formMessage formMessage--error">{t('onboarding.profilesLoadError', { error: profiles.error })}</p> : null}
            {!profiles.isLoading && !profiles.error && !profiles.data?.length ? <Link className="inlineAction" to="/settings?tab=profiles"><Plus size={15} /> {t('onboarding.addProfileLink')}</Link> : null}
          </section>

          <section className="packageStep">
            <div className="packageStep__header"><span>3</span><div><strong>{t('onboarding.step3Title')}</strong><small>{t('onboarding.step3Hint')}</small></div></div>
            {!assets.data?.length ? <EmptyState title={t('onboarding.noAssetsTitle')} description={t('onboarding.noAssetsDesc')} /> : (
              <div className="choiceList">
                {assets.data.length > 8 && <TextInput value={assetFilter} onChange={event => setAssetFilter(event.target.value)} placeholder={t('common.filterList')} />}
                {visibleAssets.map(asset => (
                  <div key={asset.id} style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
                    <label className="choiceRow" style={{ flex: '1 1 auto' }}><input type="checkbox" checked={selectedAssetIds.includes(asset.id)} onChange={() => setSelectedAssetIds(current => toggle(current, asset.id))} /> <span><strong>{asset.name}</strong><small>{asset.assetTag} · {asset.location ?? t('common.noLocation')}</small></span></label>
                    {selectedAssetIds.includes(asset.id) && (
                      <SelectInput aria-label={t('issueCondition.label')} value={issueConditions[asset.id] ?? 'ok'} onChange={event => setIssueConditions(current => ({ ...current, [asset.id]: event.target.value }))} style={{ maxWidth: '220px' }}>
                        <option value="ok">{t('issueCondition.ok')}</option>
                        <option value="used">{t('issueCondition.used')}</option>
                        <option value="damaged">{t('issueCondition.damaged')}</option>
                      </SelectInput>
                    )}
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="packageStep">
            <div className="packageStep__header"><span>4</span><div><strong>{t('onboarding.step4Title')}</strong><small>{t('onboarding.step4Hint')}</small></div></div>
            {!publishedProcedures.length ? <p className="emptyInline">{t('onboarding.noProceduresToChoose')}</p> : (
              <div className="choiceList">
                {publishedProcedures.map(procedure => <label key={procedure.id} className="choiceRow"><input type="checkbox" checked={selectedProcedureIds.includes(procedure.id)} onChange={() => setSelectedProcedureIds(current => toggle(current, procedure.id))} /> <span><strong>{procedure.title}</strong><small>{t('onboarding.procedureVersion', { version: procedure.version })}</small></span></label>)}
              </div>
            )}
          </section>

          <section className="packageStep packageStep--compact">
            <div className="formGrid">
              <Field label={t('onboarding.dueDateLabel')}><TextInput name="dueDate" type="date" min={new Date().toISOString().slice(0, 10)} /></Field>
              <Field label={t('onboarding.notesLabel')}><TextArea name="notes" placeholder={t('onboarding.notesPlaceholder')} /></Field>
            </div>
          </section>

          <div className="formActions formActions--split"><span className="muted">{t('onboarding.selectedSummary', { assets: selectedAssetIds.length, procedures: selectedProcedureIds.length })}</span><Button disabled={saving} icon={<Rocket size={16} />}>{saving ? t('onboarding.creatingPackage') : t('onboarding.create')}</Button></div>
        </form>
      </Card>
    </div>
  );
}
