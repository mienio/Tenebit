import { Check, Plus, Trash2, X } from 'lucide-react';
import { FormEvent, useCallback, useEffect, useState } from 'react';
import { api } from '../../api/endpoints';
import { Button } from '../../components/Button';
import { Field, TextInput } from '../../components/FormFields';
import { useDueLabel, urgencyOf, type MaintenanceScheduleItem } from '../../components/MaintenanceDue';
import { useI18n } from '../../i18n/I18nProvider';
import './assetMaintenance.css';

const RING_SIZE = 34;
const RING_STROKE = 3;
const RING_RADIUS = (RING_SIZE - RING_STROKE) / 2;
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS;

function ringValue(daysRemaining: number): string {
  const magnitude = Math.abs(daysRemaining);
  return magnitude < 31 ? `${magnitude}d` : `${Math.round(magnitude / 30)}m`;
}

/**
 * Recurring maintenance for one asset, shown inside its detail panel.
 *
 * Deliberately lives here rather than in a top-level section: a schedule is a property of a piece of
 * equipment, so it belongs where you already are when looking at that equipment - adding a separate
 * area for it would just grow the navigation for something nobody browses on its own.
 */
export function AssetMaintenanceSection({ assetId, onChanged }: { assetId: string; onChanged?: () => void }) {
  const { t } = useI18n();
  const dueLabel = useDueLabel();

  const [items, setItems] = useState<MaintenanceScheduleItem[] | null>(null);
  const [adding, setAdding] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [justDone, setJustDone] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);

  const load = useCallback(async () => {
    const all = await api.maintenance();
    setItems(all.filter(item => item.assetId === assetId));
  }, [assetId]);

  useEffect(() => {
    let cancelled = false;
    setItems(null);
    api.maintenance()
      .then(all => { if (!cancelled) setItems(all.filter(item => item.assetId === assetId)); })
      .catch(() => { if (!cancelled) setItems([]); });
    return () => { cancelled = true; };
  }, [assetId]);

  async function complete(item: MaintenanceScheduleItem) {
    setBusyId(item.id);
    try {
      await api.completeMaintenance(item.id, {});
      // Held briefly so finishing something is visibly acknowledged before the row resets its date.
      setJustDone(item.id);
      window.setTimeout(async () => {
        setJustDone(null);
        await load();
        onChanged?.();
      }, 1300);
    } finally {
      setBusyId(null);
    }
  }

  async function remove(item: MaintenanceScheduleItem) {
    setBusyId(item.id);
    try {
      await api.deleteMaintenance(item.id);
      setConfirmDelete(null);
      await load();
      onChanged?.();
    } finally {
      setBusyId(null);
    }
  }

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await api.createMaintenance({
      assetId,
      name: String(form.get('name') ?? ''),
      intervalMonths: Number(form.get('intervalMonths') ?? 12),
      nextDueOn: String(form.get('nextDueOn') ?? ''),
    });
    setAdding(false);
    await load();
    onChanged?.();
  }

  const todayIso = new Date().toISOString().slice(0, 10);

  return (
    <>
      <div className="formSectionTitle">{t('maintenance.title')}</div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '8px' }}>
        <Button variant="secondary" onClick={() => setAdding(open => !open)} icon={adding ? <X size={16} /> : <Plus size={16} />}>
          {adding ? t('common.cancel') : t('maintenance.add')}
        </Button>
      </div>

      {adding ? (
        <form className="amAdd" onSubmit={create}>
          <Field label={t('maintenance.nameLabel')}>
            <TextInput name="name" required maxLength={160} placeholder={t('maintenance.namePlaceholder')} />
          </Field>
          <Field label={t('maintenance.intervalLabel')}>
            <TextInput name="intervalMonths" type="number" min={1} max={120} defaultValue={12} required />
          </Field>
          <Field label={t('maintenance.firstDueLabel')}>
            <TextInput name="nextDueOn" type="date" defaultValue={todayIso} required />
          </Field>
          <Button type="submit" icon={<Check size={16} />}>{t('common.save')}</Button>
        </form>
      ) : null}

      {!items ? (
        <p className="muted">{t('common.loading')}</p>
      ) : items.length === 0 ? (
        <p className="muted">{t('maintenance.noneForAsset')}</p>
      ) : (
        <ul className="amList">
          {items.map(item => {
            const urgency = urgencyOf(item.daysRemaining);
            const clamped = Math.max(0, Math.min(item.cycleProgress, 100));
            const filled = (clamped / 100) * RING_CIRCUMFERENCE;
            const done = justDone === item.id;

            return (
              <li key={item.id} className={`amRow amRow--${done ? 'done' : urgency}`}>
                <span className="amRing">
                  {done ? (
                    <span className="amRing__done"><Check size={16} /></span>
                  ) : (
                    <>
                      <svg width={RING_SIZE} height={RING_SIZE} viewBox={`0 0 ${RING_SIZE} ${RING_SIZE}`} aria-hidden="true">
                        <circle cx={RING_SIZE / 2} cy={RING_SIZE / 2} r={RING_RADIUS} fill="none" className="amRing__track" strokeWidth={RING_STROKE} />
                        <circle
                          cx={RING_SIZE / 2} cy={RING_SIZE / 2} r={RING_RADIUS} fill="none"
                          className="amRing__value" strokeWidth={RING_STROKE} strokeLinecap="round"
                          strokeDasharray={`${filled} ${RING_CIRCUMFERENCE - filled}`}
                          transform={`rotate(-90 ${RING_SIZE / 2} ${RING_SIZE / 2})`}
                        />
                      </svg>
                      <span className="amRing__text">{ringValue(item.daysRemaining)}</span>
                    </>
                  )}
                </span>

                <span className="amName" title={item.name}>{item.name}</span>
                <span className="amEvery">{t('maintenance.everyMonths', { months: String(item.intervalMonths) })}</span>
                <span className="amDue">
                  {done ? t('maintenance.doneNext', { months: String(item.intervalMonths) }) : dueLabel(item.daysRemaining)}
                </span>

                <span className="amActions">
                  {done ? null : confirmDelete === item.id ? (
                    <>
                      <button type="button" className="amBtn amBtn--danger" disabled={busyId === item.id} onClick={() => remove(item)}>
                        {t('maintenance.deleteConfirm')}
                      </button>
                      <button type="button" className="amBtn" onClick={() => setConfirmDelete(null)}>{t('common.cancel')}</button>
                    </>
                  ) : (
                    <>
                      <button type="button" className="amBtn" disabled={busyId === item.id} onClick={() => complete(item)}>
                        <Check size={13} /> {t('maintenance.markDone')}
                      </button>
                      <button type="button" className="amBtn amBtn--icon" aria-label={t('maintenance.delete')} onClick={() => setConfirmDelete(item.id)}>
                        <Trash2 size={13} />
                      </button>
                    </>
                  )}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </>
  );
}
