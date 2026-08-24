import { CalendarRange } from 'lucide-react';

export interface DateRange {
  from: string;
  to: string;
}

const PRESETS: { label: string; days: number }[] = [
  { label: '7 dni', days: 7 },
  { label: '30 dni', days: 30 },
  { label: '90 dni', days: 90 },
  { label: '365 dni', days: 365 },
];

export function todayIso(): string {
  return toIso(new Date());
}

export function daysAgoIso(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - (days - 1));
  return toIso(date);
}

/** Local calendar date, not UTC: `toISOString()` would shift the day for anyone east of Greenwich. */
function toIso(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function defaultRange(days = 30): DateRange {
  return { from: daysAgoIso(days), to: todayIso() };
}

export function AdminDateRange({ value, onChange }: { value: DateRange; onChange: (range: DateRange) => void }) {
  const today = todayIso();

  function applyPreset(days: number) {
    onChange({ from: daysAgoIso(days), to: today });
  }

  function activePreset(): number | null {
    if (value.to !== today) return null;
    return PRESETS.find(preset => daysAgoIso(preset.days) === value.from)?.days ?? null;
  }

  const active = activePreset();

  return (
    <div className="adminDateRange">
      <div className="adminRange">
        {PRESETS.map(preset => (
          <button
            key={preset.days}
            type="button"
            className={`adminRange__button${active === preset.days ? ' adminRange__button--active' : ''}`}
            onClick={() => applyPreset(preset.days)}
          >
            {preset.label}
          </button>
        ))}
      </div>

      <label className="adminDateRange__field">
        <CalendarRange size={15} />
        <span className="adminDateRange__label">od</span>
        <input
          type="date"
          className="input adminDateRange__input"
          value={value.from}
          max={value.to}
          onChange={event => onChange({ ...value, from: event.target.value || value.from })}
        />
      </label>

      <label className="adminDateRange__field">
        <span className="adminDateRange__label">do</span>
        <input
          type="date"
          className="input adminDateRange__input"
          value={value.to}
          min={value.from}
          max={today}
          onChange={event => onChange({ ...value, to: event.target.value || value.to })}
        />
      </label>
    </div>
  );
}
