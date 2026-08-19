import { useEffect, useMemo, useState } from 'react';
import { Save, Send } from 'lucide-react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { Pagination } from '../components/Pagination';
import { EmptyState, ErrorState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';
import type { AlertDeliveryMode, AlertDigestFrequency, AlertRecipientMode, AlertRule, AlertType, SaveAlertDigestSettingsRequest, SaveAlertRuleRequest } from '../types/domain';
import { formatDateTime, toNullable } from '../utils/format';
import { alertDeliveryModeValues, alertDigestFrequencyValues, alertRecipientModeValues, alertTypeValues } from '../utils/labels';

type Message = { type: 'success' | 'error'; text: string } | null;

type RuleDraft = {
  isEnabled: boolean;
  thresholds: string;
  deliveryMode: AlertDeliveryMode;
  recipientMode: AlertRecipientMode;
  customEmails: string;
  cooldownDays: number;
};

type DigestDraft = {
  frequency: AlertDigestFrequency;
  dayOfWeek: string;
  localTime: string;
  quietHoursStart: string;
  quietHoursEnd: string;
  businessDays: number;
  includeEmptyDigest: boolean;
};

const HISTORY_PAGE_SIZE = 25;

const WEEKDAYS: { value: string; bit: number; key: string }[] = [
  { value: 'Monday', bit: 1, key: 'monday' },
  { value: 'Tuesday', bit: 2, key: 'tuesday' },
  { value: 'Wednesday', bit: 4, key: 'wednesday' },
  { value: 'Thursday', bit: 8, key: 'thursday' },
  { value: 'Friday', bit: 16, key: 'friday' },
  { value: 'Saturday', bit: 32, key: 'saturday' },
  { value: 'Sunday', bit: 64, key: 'sunday' }
];

const BUSINESS_DAY_BITS: Record<string, number> = {
  None: 0, Monday: 1, Tuesday: 2, Wednesday: 4, Thursday: 8, Friday: 16, Saturday: 32, Sunday: 64, Weekdays: 31, All: 127
};

function toRuleDraft(rule: AlertRule): RuleDraft {
  return {
    isEnabled: rule.isEnabled,
    thresholds: rule.thresholdDays.join(', '),
    deliveryMode: rule.deliveryMode,
    recipientMode: rule.recipientMode,
    customEmails: rule.customEmails ?? '',
    cooldownDays: rule.cooldownDays
  };
}

// Backend serializuje flags (businessDays) jako napis ("Weekdays" lub "Monday, Tuesday, ..."); akceptujemy
// też liczbę, bo przy zapisie wysyłamy bitmaskę numeryczną.
function parseBusinessDays(raw: string | number | null | undefined): number {
  if (typeof raw === 'number') return raw;
  if (!raw) return 31;
  let value = 0;
  for (const part of raw.split(',')) {
    const bit = BUSINESS_DAY_BITS[part.trim()];
    if (bit !== undefined) value |= bit;
  }
  return value;
}

function toTimeInput(value: string | null | undefined): string {
  if (!value) return '';
  return value.slice(0, 5);
}

function parseThresholds(text: string): number[] | null {
  const parts = text.split(',').map(p => p.trim()).filter(Boolean);
  if (parts.length > 5) return null;
  const days: number[] = [];
  for (const part of parts) {
    if (!/^\d+$/.test(part)) return null;
    const value = Number(part);
    if (value > 365) return null;
    days.push(value);
  }
  return days;
}

export function AlertsSettings() {
  const { t } = useI18n();
  const rules = useAsyncData(api.alertRules, []);
  const digest = useAsyncData(api.alertDigest, []);
  const [historyPage, setHistoryPage] = useState(1);
  const history = useAsyncData(() => api.alertHistory(historyPage, HISTORY_PAGE_SIZE), [historyPage]);
  const [message, setMessage] = useState<Message>(null);

  const [drafts, setDrafts] = useState<Record<AlertType, RuleDraft>>({} as Record<AlertType, RuleDraft>);
  const [savingType, setSavingType] = useState<AlertType | null>(null);
  const [testingType, setTestingType] = useState<AlertType | null>(null);

  const [digestDraft, setDigestDraft] = useState<DigestDraft | null>(null);
  const [digestSaving, setDigestSaving] = useState(false);

  useEffect(() => {
    if (!rules.data) return;
    setDrafts(current => {
      const next = { ...current };
      for (const rule of rules.data!) {
        next[rule.type] = current[rule.type] ?? toRuleDraft(rule);
      }
      return next;
    });
  }, [rules.data]);

  useEffect(() => {
    if (!digest.data) return;
    setDigestDraft(current => current ?? {
      frequency: digest.data!.frequency,
      dayOfWeek: digest.data!.dayOfWeek ?? '',
      localTime: toTimeInput(digest.data!.localTime),
      quietHoursStart: toTimeInput(digest.data!.quietHoursStart),
      quietHoursEnd: toTimeInput(digest.data!.quietHoursEnd),
      businessDays: parseBusinessDays(digest.data!.businessDays),
      includeEmptyDigest: digest.data!.includeEmptyDigest
    });
  }, [digest.data]);

  function updateDraft(type: AlertType, patch: Partial<RuleDraft>) {
    setDrafts(current => ({ ...current, [type]: { ...current[type], ...patch } }));
  }

  async function saveRule(type: AlertType) {
    const draft = drafts[type];
    if (!draft) return;
    const days = parseThresholds(draft.thresholds);
    if (days === null) {
      setMessage({ type: 'error', text: t('alerts.thresholdsInvalid') });
      return;
    }
    setSavingType(type);
    const body: SaveAlertRuleRequest = {
      isEnabled: draft.isEnabled,
      thresholdDays: days,
      deliveryMode: draft.deliveryMode,
      recipientMode: draft.recipientMode,
      customEmails: draft.recipientMode === 'Custom' ? toNullable(draft.customEmails) : null,
      cooldownDays: draft.cooldownDays
    };
    try {
      await api.saveAlertRule(type, body);
      setMessage({ type: 'success', text: t('alerts.ruleSaved') });
      await rules.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('alerts.ruleSaveFailed') });
    } finally {
      setSavingType(null);
    }
  }

  async function sendTest(type: AlertType) {
    setTestingType(type);
    try {
      await api.sendTestAlert(type);
      setMessage({ type: 'success', text: t('alerts.testSent') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('alerts.testFailed') });
    } finally {
      setTestingType(null);
    }
  }

  async function saveDigest() {
    if (!digestDraft) return;
    if (digestDraft.frequency === 'Weekly' && !digestDraft.dayOfWeek) {
      setMessage({ type: 'error', text: t('alerts.dayOfWeekRequired') });
      return;
    }
    if (Boolean(digestDraft.quietHoursStart) !== Boolean(digestDraft.quietHoursEnd)) {
      setMessage({ type: 'error', text: t('alerts.quietHoursPair') });
      return;
    }
    setDigestSaving(true);
    const body: SaveAlertDigestSettingsRequest = {
      frequency: digestDraft.frequency,
      dayOfWeek: digestDraft.frequency === 'Weekly' ? digestDraft.dayOfWeek : null,
      localTime: digestDraft.localTime || '08:00',
      quietHoursStart: toNullable(digestDraft.quietHoursStart),
      quietHoursEnd: toNullable(digestDraft.quietHoursEnd),
      businessDays: digestDraft.businessDays,
      holidayCalendarCountryCode: digest.data?.holidayCalendarCountryCode ?? null,
      includeEmptyDigest: digestDraft.includeEmptyDigest
    };
    try {
      await api.saveAlertDigest(body);
      setMessage({ type: 'success', text: t('alerts.digestSaved') });
      await digest.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('alerts.digestSaveFailed') });
    } finally {
      setDigestSaving(false);
    }
  }

  const historyItems = useMemo(() => history.data?.items ?? [], [history.data]);

  return (
    <div className="pageStack">
      {message ? <p className={`formMessage formMessage--${message.type}`} aria-live="polite">{message.text}</p> : null}

      <Card>
        <div className="sectionTitle"><div><h2>{t('alerts.rulesTitle')}</h2><p>{t('alerts.rulesHint')}</p></div></div>
        {rules.isLoading && !rules.data ? <p className="muted">{t('common.loading')}</p> : rules.error ? <ErrorState message={rules.error} onRetry={rules.reload} /> : (
          <div className="pageStack">
            {alertTypeValues.map(type => {
              const draft = drafts[type];
              if (!draft) return null;
              const typeLabel = t(`alerts.type.${type}`);
              return (
                <div className="alertRule" key={type}>
                  <div className="alertRule__head">
                    <h3>{typeLabel}</h3>
                    <label className="toggleSwitch">
                      <input type="checkbox" checked={draft.isEnabled} onChange={event => updateDraft(type, { isEnabled: event.target.checked })} aria-label={typeLabel} />
                      <span className="toggleSwitch__track" />
                      <span className="toggleSwitch__thumb" />
                    </label>
                  </div>
                  <div className="formGrid">
                    <Field label={t('alerts.thresholdDays')} info={t('alerts.thresholdDaysHint')}>
                      <TextInput value={draft.thresholds} onChange={event => updateDraft(type, { thresholds: event.target.value })} placeholder="30, 7" />
                    </Field>
                    <Field label={t('alerts.deliveryMode')}>
                      <SelectInput value={draft.deliveryMode} onChange={event => updateDraft(type, { deliveryMode: event.target.value as AlertDeliveryMode })}>
                        {alertDeliveryModeValues.map(value => <option key={value} value={value}>{t(`alerts.delivery.${value}`)}</option>)}
                      </SelectInput>
                    </Field>
                    <Field label={t('alerts.recipientMode')}>
                      <SelectInput value={draft.recipientMode} onChange={event => updateDraft(type, { recipientMode: event.target.value as AlertRecipientMode })}>
                        {alertRecipientModeValues.map(value => <option key={value} value={value}>{t(`alerts.recipient.${value}`)}</option>)}
                      </SelectInput>
                    </Field>
                    {draft.recipientMode === 'Custom' ? (
                      <Field label={t('alerts.customEmails')} info={t('alerts.customEmailsHint')}>
                        <TextInput value={draft.customEmails} onChange={event => updateDraft(type, { customEmails: event.target.value })} placeholder="a@x.com, b@x.com" />
                      </Field>
                    ) : null}
                    <Field label={t('alerts.cooldownDays')} info={t('alerts.cooldownDaysHint')}>
                      <TextInput type="number" min={0} max={14} value={draft.cooldownDays} onChange={event => updateDraft(type, { cooldownDays: Number(event.target.value) || 0 })} />
                    </Field>
                  </div>
                  <p className="muted">{t('alerts.preview', { type: typeLabel, days: draft.thresholds || '-' })}</p>
                  <div className="formActions formActions--split">
                    <Button type="button" variant="secondary" disabled={testingType === type} icon={<Send size={16} />} onClick={() => sendTest(type)}>{testingType === type ? t('common.saving') : t('alerts.sendTest')}</Button>
                    <Button type="button" disabled={savingType === type} icon={<Save size={16} />} onClick={() => saveRule(type)}>{savingType === type ? t('common.saving') : t('common.save')}</Button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Card>

      <Card>
        <div className="sectionTitle"><div><h2>{t('alerts.digestTitle')}</h2><p>{t('alerts.digestHint')}</p></div></div>
        {digest.isLoading && !digest.data ? <p className="muted">{t('common.loading')}</p> : digest.error ? <ErrorState message={digest.error} onRetry={digest.reload} /> : digestDraft ? (
          <>
            <div className="formGrid">
              <Field label={t('alerts.frequency')}>
                <SelectInput value={digestDraft.frequency} onChange={event => setDigestDraft(current => current ? { ...current, frequency: event.target.value as AlertDigestFrequency } : current)}>
                  {alertDigestFrequencyValues.map(value => <option key={value} value={value}>{t(`alerts.frequency.${value}`)}</option>)}
                </SelectInput>
              </Field>
              {digestDraft.frequency === 'Weekly' ? (
                <Field label={t('alerts.dayOfWeek')}>
                  <SelectInput value={digestDraft.dayOfWeek} onChange={event => setDigestDraft(current => current ? { ...current, dayOfWeek: event.target.value } : current)}>
                    <option value="">{t('alerts.dayOfWeekPlaceholder')}</option>
                    {WEEKDAYS.map(day => <option key={day.value} value={day.value}>{t(`alerts.weekday.${day.key}`)}</option>)}
                  </SelectInput>
                </Field>
              ) : null}
              <Field label={t('alerts.localTime')}>
                <TextInput type="time" value={digestDraft.localTime} onChange={event => setDigestDraft(current => current ? { ...current, localTime: event.target.value } : current)} />
              </Field>
              <Field label={t('alerts.quietHoursStart')}>
                <TextInput type="time" value={digestDraft.quietHoursStart} onChange={event => setDigestDraft(current => current ? { ...current, quietHoursStart: event.target.value } : current)} />
              </Field>
              <Field label={t('alerts.quietHoursEnd')}>
                <TextInput type="time" value={digestDraft.quietHoursEnd} onChange={event => setDigestDraft(current => current ? { ...current, quietHoursEnd: event.target.value } : current)} />
              </Field>
            </div>
            <fieldset className="checkboxGroup">
              <legend>{t('alerts.businessDays')}</legend>
              {WEEKDAYS.map(day => (
                <label key={day.value}>
                  <input type="checkbox" checked={(digestDraft.businessDays & day.bit) === day.bit} onChange={() => setDigestDraft(current => current ? { ...current, businessDays: current.businessDays ^ day.bit } : current)} />
                  {t(`alerts.weekday.${day.key}`)}
                </label>
              ))}
            </fieldset>
            <label className="checkField">
              <input type="checkbox" checked={digestDraft.includeEmptyDigest} onChange={event => setDigestDraft(current => current ? { ...current, includeEmptyDigest: event.target.checked } : current)} />
              {t('alerts.includeEmptyDigest')}
            </label>
            <div className="formActions"><Button disabled={digestSaving} icon={<Save size={16} />} onClick={saveDigest}>{digestSaving ? t('common.saving') : t('common.save')}</Button></div>
          </>
        ) : null}
      </Card>

      <Card>
        <div className="sectionTitle"><div><h2>{t('alerts.historyTitle')}</h2><p>{t('alerts.historyHint')}</p></div></div>
        {history.isLoading && !history.data ? <p className="muted">{t('common.loading')}</p> : history.error ? <ErrorState message={history.error} onRetry={history.reload} /> : !historyItems.length ? (
          <EmptyState title={t('alerts.historyEmptyTitle')} description={t('alerts.historyEmptyDesc')} />
        ) : (
          <>
            <div className="tableWrap"><table>
              <thead><tr><th>{t('alerts.colType')}</th><th>{t('alerts.colEntity')}</th><th>{t('alerts.colRecipient')}</th><th>{t('alerts.colStatus')}</th><th>{t('alerts.colCreatedAt')}</th><th>{t('alerts.colSentAt')}</th><th>{t('alerts.colError')}</th></tr></thead>
              <tbody>
                {historyItems.map(item => (
                  <tr key={item.id}>
                    <td data-label={t('alerts.colType')}>{t(`alerts.type.${item.type}`)}</td>
                    <td data-label={t('alerts.colEntity')}><code>{item.entityId.slice(0, 8)}</code></td>
                    <td data-label={t('alerts.colRecipient')}>{item.recipientEmail}</td>
                    <td data-label={t('alerts.colStatus')}>{t(`alerts.status.${item.status}`)}</td>
                    <td data-label={t('alerts.colCreatedAt')}><small>{formatDateTime(item.createdAt)}</small></td>
                    <td data-label={t('alerts.colSentAt')}><small>{formatDateTime(item.sentAt)}</small></td>
                    <td data-label={t('alerts.colError')}>{item.lastError ?? '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table></div>
            <Pagination page={history.data?.page ?? 1} total={history.data?.total ?? 0} pageSize={HISTORY_PAGE_SIZE} onPageChange={setHistoryPage} />
          </>
        )}
      </Card>
    </div>
  );
}
