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
 * "in 87 days" forces the reader to do arithmetic to learn something they only needed roughly.
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

const RING_SIZE = 30;
const RING_STROKE = 3;
const RING_RADIUS = (RING_SIZE - RING_STROKE) / 2;
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS;

/**
 * Cycle progress as a ring rather than a bar.
 *
 * A bar needs a fixed track width, which left a dead gap between the label and the value and made
 * every row look ragged. A ring is a single compact glyph that sits in the text flow, so the row can
 * be laid out as icon - label - value with nothing floating in between.
 */
function ProgressRing({ progress, urgency }: { progress: number; urgency: Urgency }) {
  const clamped = Math.max(0, Math.min(progress, 100));
  // Drawn from 12 o'clock clockwise: rotating the whole SVG is simpler than recomputing the arc.
  const filled = (clamped / 100) * RING_CIRCUMFERENCE;

  return (
    <svg
      className={`dueRing dueRing--${urgency}`}
      width={RING_SIZE}
      height={RING_SIZE}
      viewBox={`0 0 ${RING_SIZE} ${RING_SIZE}`}
      aria-hidden="true"
    >
      <circle
        cx={RING_SIZE / 2}
        cy={RING_SIZE / 2}
        r={RING_RADIUS}
        fill="none"
        className="dueRing__track"
        strokeWidth={RING_STROKE}
      />
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
  );
}

/**
 * One schedule as a single compact line: ring, what it is, and the time left. Every element has a
 * fixed size so the same thing lands in the same place on every row.
 */
export function MaintenanceDueRow({ item, onComplete }: { item: MaintenanceScheduleItem; onComplete?: (item: MaintenanceScheduleItem) => void }) {
  const { t } = useI18n();
  const dueLabel = useDueLabel();
  const urgency = urgencyOf(item.daysRemaining);

  return (
    <div className={`dueRow dueRow--${urgency}`} title={t('maintenance.nextDue', { date: item.nextDueOn })}>
      <ProgressRing progress={item.cycleProgress} urgency={urgency} />

      <span className="dueRow__text">
        <strong>{item.name}</strong>
        <span className="dueRow__sep">·</span>
        <span className="dueRow__asset">{item.assetName}</span>
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
