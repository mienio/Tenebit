import { PieChart } from 'lucide-react';
import { useI18n } from '../../i18n/I18nProvider';

export interface DonutChartSegment {
  label: string;
  value: number;
  color: string;
}

export function DonutChart({ segments, size = 160, emptyLabel }: { segments: DonutChartSegment[]; size?: number; emptyLabel?: string }) {
  const { t } = useI18n();
  const total = segments.reduce((sum, item) => sum + item.value, 0);
  const radius = size / 2 - 12;
  const circumference = 2 * Math.PI * radius;
  let offset = 0;

  if (total === 0) {
    return (
      <div className="chartEmpty">
        <PieChart size={26} />
        <p>{emptyLabel ?? t('chart.empty')}</p>
      </div>
    );
  }

  return (
    <div className="donutChart">
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--border)" strokeWidth={16} />
        {segments.map(segment => {
          const length = (segment.value / total) * circumference;
          const dashArray = `${length} ${circumference - length}`;
          const dashOffset = -offset;
          offset += length;
          return (
            <circle
              key={segment.label}
              cx={size / 2}
              cy={size / 2}
              r={radius}
              fill="none"
              stroke={segment.color}
              strokeWidth={16}
              strokeDasharray={dashArray}
              strokeDashoffset={dashOffset}
              transform={`rotate(-90 ${size / 2} ${size / 2})`}
            />
          );
        })}
        <text x={size / 2} y={size / 2} textAnchor="middle" dominantBaseline="middle" fontSize={22} fontWeight={700} fill="var(--brand)">
          {total}
        </text>
      </svg>
      <ul className="donutChart__legend">
        {segments.map(segment => (
          <li key={segment.label}>
            <span className="donutChart__swatch" style={{ background: segment.color }} />
            <span>{segment.label}</span>
            <strong>{segment.value}</strong>
          </li>
        ))}
      </ul>
    </div>
  );
}
