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

/** Full phrase, used for the tooltip and for screen readers. */
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
    return t('maintenance.inMonths', { months: String(Math.round(daysRemaining / 30)) });
  };
}

/**
 * The two or three characters that go inside the ring: "9d", "8m", "0d".
 *
 * Kept this short on purpose - the number lives inside the ring rather than in its own column, so the
 * remaining time and the progress that produced it read as one object instead of two things sitting
 * apart on opposite sides of the row.
 */
function ringValue(daysRemaining: number): string {
  const magnitude = Math.abs(daysRemaining);
  if (magnitude < 31) return `${magnitude}d`;
  return `${Math.round(magnitude / 30)}m`;
}

const RING_SIZE = 40;
const RING_STROKE = 3;
const RING_RADIUS = (RING_SIZE - RING_STROKE) / 2;
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS;

/**
 * One schedule as a single compact line: a ring holding the remaining time, then what it is.
 *
 * Everything sits flush to the left in reading order. An earlier version stretched the label so the
 * time landed against the right edge, which left a dead gap across the middle of every row.
 */
export function MaintenanceDueRow({ item, onComplete }: { item: MaintenanceScheduleItem; onComplete?: (item: MaintenanceScheduleItem) => void }) {
  const { t } = useI18n();
  const dueLabel = useDueLabel();
  const urgency = urgencyOf(item.daysRemaining);
  const clamped = Math.max(0, Math.min(item.cycleProgress, 100));
  const filled = (clamped / 100) * RING_CIRCUMFERENCE;

  return (
    <div className={`dueRow dueRow--${urgency}`} title={`${item.name} — ${dueLabel(item.daysRemaining)}`}>
      <span className="dueRing" aria-label={dueLabel(item.daysRemaining)}>
        <svg width={RING_SIZE} height={RING_SIZE} viewBox={`0 0 ${RING_SIZE} ${RING_SIZE}`} aria-hidden="true">
          <circle cx={RING_SIZE / 2} cy={RING_SIZE / 2} r={RING_RADIUS} fill="none" className="dueRing__track" strokeWidth={RING_STROKE} />
          <circle
            cx={RING_SIZE / 2}
            cy={RING_SIZE / 2}
            r={RING_RADIUS}
            fill="none"
            className="dueRing__value"
            strokeWidth={RING_STROKE}
            strokeLinecap="round"
            strokeDasharray={`${filled} ${RING_CIRCUMFERENCE - filled}`}
            transform={`rotate(-90 ${RING_SIZE / 2} ${RING_SIZE / 2})`}
          />
        </svg>
        <span className="dueRing__text" aria-hidden="true">{ringValue(item.daysRemaining)}</span>
      </span>

      <span className="dueRow__text">
        <strong>{item.name}</strong>
        <span className="dueRow__sep">·</span>
        <span className="dueRow__asset">{item.assetName}</span>
        {urgency === 'overdue' ? <span className="dueRow__flag">{t('maintenance.overdueFlag')}</span> : null}
      </span>

      {onComplete ? (
        <button type="button" className="dueRow__action" onClick={() => onComplete(item)}>
          {t('maintenance.markDone')}
        </button>
      ) : null}
    </div>
  );
}
