import { useI18n } from '../i18n/I18nProvider';
import './maintenanceDue.css';

export interface MaintenanceScheduleItem {
  id: string;
  assetId: string;
  assetName: string;
  assetTag: string | null;
  name: string;
  intervalMonths: number;
  nextDueOn: string;
  lastPerformedOn: string | null;
  lastPerformedBy: string | null;
  isActive: boolean;
  daysRemaining: number;
  cycleProgress: number;
}

type Urgency = 'overdue' | 'soon' | 'ok';

export function urgencyOf(daysRemaining: number): Urgency {
  if (daysRemaining < 0) return 'overdue';
  if (daysRemaining <= 14) return 'soon';
  return 'ok';
}

/**
 * Turns a day count into the shortest phrase that still answers "do I need to act?".
 *
 * Deliberately coarse: past a month nobody plans in days, so it rounds to months and stops. Showing
 * "za 87 dni" forces the reader to do arithmetic to learn something they only needed roughly.
 */
export function useDueLabel() {
  const { t } = useI18n();

  return (daysRemaining: number): string => {
    if (daysRemaining < 0) {
      const late = Math.abs(daysRemaining);
      return late === 1 ? t('maintenance.overdueOne') : t('maintenance.overdueMany', { days: String(late) });
    }
    if (daysRemaining === 0) return t('maintenance.today');
    if (daysRemaining === 1) return t('maintenance.tomorrow');
    if (daysRemaining < 31) return t('maintenance.inDays', { days: String(daysRemaining) });

    const months = Math.round(daysRemaining / 30);
    return t('maintenance.inMonths', { months: String(months) });
  };
}

/**
 * One schedule as a single line: what it is, a bar showing how far through the cycle we are, and the
 * remaining time. The bar carries the urgency colour so the list can be read at a glance without
 * anyone parsing dates.
 */
export function MaintenanceDueRow({ item, onComplete }: { item: MaintenanceScheduleItem; onComplete?: (item: MaintenanceScheduleItem) => void }) {
  const { t } = useI18n();
  const dueLabel = useDueLabel();
  const urgency = urgencyOf(item.daysRemaining);

  return (
    <div className={`dueRow dueRow--${urgency}`}>
      <span className="dueRow__text">
        <strong title={item.name}>{item.name}</strong>
        <span title={item.assetName}>{item.assetName}</span>
      </span>

      <span
        className="dueRow__bar"
        role="img"
        aria-label={`${item.cycleProgress}%`}
        title={t('maintenance.nextDue', { date: item.nextDueOn })}
      >
        <i style={{ width: `${Math.min(item.cycleProgress, 100)}%` }} />
      </span>

      <span className="dueRow__label">{dueLabel(item.daysRemaining)}</span>

      {onComplete ? (
        <button type="button" className="dueRow__action" onClick={() => onComplete(item)}>
          {t('maintenance.markDone')}
        </button>
      ) : null}
    </div>
  );
}
