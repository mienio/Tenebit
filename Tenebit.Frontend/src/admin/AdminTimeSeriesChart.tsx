import { useId, useMemo, useState } from 'react';
import type { AdminSeries } from './adminApi';

// Fixed viewBox geometry: constant across renders, so it lives outside the component rather than
// being rebuilt each time and dragged through the memo dependency list.
const width = 720;
const padding = { top: 14, right: 14, bottom: 26, left: 40 };

interface Point extends AdminSeriesPointLike {
  x: number;
  y: number;
}

interface AdminSeriesPointLike {
  day: string;
  count: number;
}

/**
 * Inline SVG chart for a daily count series. Hand-rolled rather than pulling in a charting dependency:
 * the project ships no chart library (only the two hand-written components in src/components/charts),
 * and one series of daily integers does not justify adding one.
 *
 * The curve uses a monotone cubic fit rather than a plain Catmull-Rom spline. Both look smooth, but a
 * generic spline overshoots around sharp changes - it would draw counts dipping below zero after a
 * spike, inventing days that never happened. Monotone interpolation keeps the curve inside the range of
 * the real data points, so the picture cannot imply numbers the database does not contain.
 */
export function AdminTimeSeriesChart({
  series,
  color = 'var(--brand)',
  height = 190,
}: {
  series: AdminSeries;
  color?: string;
  height?: number;
}) {
  const [hover, setHover] = useState<number | null>(null);
  const gradientId = useId();

  const { points, max, linePath, areaPath } = useMemo(() => {
    const values = series.points;
    const maxValue = Math.max(...values.map(p => p.count), 1);
    const innerWidth = width - padding.left - padding.right;
    const innerHeight = height - padding.top - padding.bottom;
    // A single data point has no span to divide by; place it at the left edge instead of dividing by zero.
    const step = values.length > 1 ? innerWidth / (values.length - 1) : 0;

    const mapped: Point[] = values.map((point, index) => ({
      ...point,
      x: padding.left + index * step,
      y: padding.top + innerHeight - (point.count / maxValue) * innerHeight,
    }));

    const line = buildSmoothPath(mapped);
    const baseline = padding.top + innerHeight;
    const filled = mapped.length
      ? `${line} L${mapped[mapped.length - 1].x.toFixed(2)},${baseline.toFixed(2)} L${mapped[0].x.toFixed(2)},${baseline.toFixed(2)} Z`
      : '';

    return { points: mapped, max: maxValue, linePath: line, areaPath: filled };
  }, [series, height]);

  // Counts are integers, so on a small range (max of 1 or 2) several gridlines round to the same
  // number and the axis renders as "1, 1, 0". Label each distinct value once instead.
  const gridLines = useMemo(() => {
    const seen = new Set<number>();
    return [0, 0.25, 0.5, 0.75, 1].map(fraction => {
      const value = Math.round(max * (1 - fraction));
      const isCandidate = fraction === 0 || fraction === 0.5 || fraction === 1;
      const showLabel = isCandidate && !seen.has(value);
      if (showLabel) seen.add(value);
      return { fraction, value, showLabel };
    });
  }, [max]);

  const total = series.points.reduce((sum, p) => sum + p.count, 0);
  const peak = series.points.reduce((best, p) => (p.count > best.count ? p : best), { day: '', count: 0 });
  const active = hover !== null ? points[hover] : null;
  const slotWidth = points.length > 1 ? (width - padding.left - padding.right) / (points.length - 1) : 32;

  return (
    <figure className="adminChart">
      <figcaption className="adminChart__caption">
        <span>{series.label}</span>
        <strong>{total.toLocaleString('pl-PL')}</strong>
      </figcaption>

      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="adminChart__svg"
        role="img"
        aria-label={`${series.label}: ${total} łącznie w wybranym zakresie`}
        onMouseLeave={() => setHover(null)}
      >
        <defs>
          <linearGradient id={gradientId} x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity="0.30" />
            <stop offset="100%" stopColor={color} stopOpacity="0" />
          </linearGradient>
        </defs>

        {gridLines.map(({ fraction, value, showLabel }) => {
          const y = padding.top + (height - padding.top - padding.bottom) * fraction;
          const isEdge = fraction === 0 || fraction === 1;
          return (
            <g key={fraction}>
              <line
                x1={padding.left}
                x2={width - padding.right}
                y1={y}
                y2={y}
                stroke="var(--border)"
                strokeWidth={isEdge ? 1 : 0.5}
              />
              {showLabel ? (
                <text x={padding.left - 8} y={y + 3.5} textAnchor="end" className="adminChart__axis">
                  {value}
                </text>
              ) : null}
            </g>
          );
        })}

        {areaPath ? <path d={areaPath} fill={`url(#${gradientId})`} /> : null}
        {linePath ? (
          <path d={linePath} fill="none" stroke={color} strokeWidth="2.25" strokeLinejoin="round" strokeLinecap="round" />
        ) : null}

        {active ? (
          <g>
            <line
              x1={active.x}
              x2={active.x}
              y1={padding.top}
              y2={height - padding.bottom}
              stroke={color}
              strokeWidth="1"
              strokeDasharray="3 3"
            />
            <circle cx={active.x} cy={active.y} r="4.5" fill="var(--surface)" stroke={color} strokeWidth="2.5" />
          </g>
        ) : null}

        {/* One hit area per point so hovering works without a chart library's event layer. */}
        {points.map((point, index) => (
          <rect
            key={point.day}
            x={point.x - slotWidth / 2}
            y={padding.top}
            width={slotWidth}
            height={height - padding.top - padding.bottom}
            fill="transparent"
            onMouseEnter={() => setHover(index)}
          />
        ))}

        {points.length > 0 ? (
          <>
            <text x={padding.left} y={height - 8} className="adminChart__axis">{formatDay(points[0].day)}</text>
            <text x={width - padding.right} y={height - 8} textAnchor="end" className="adminChart__axis">
              {formatDay(points[points.length - 1].day)}
            </text>
          </>
        ) : null}
      </svg>

      <p className="adminChart__hint">
        {active
          ? `${formatDay(active.day, true)}: ${active.count}`
          : peak.count > 0
            ? `Szczyt: ${formatDay(peak.day, true)} — ${peak.count}`
            : 'Brak zdarzeń w tym zakresie'}
      </p>
    </figure>
  );
}

/**
 * Monotone cubic interpolation (Fritsch-Carlson tangents). Produces a smooth curve that never overshoots
 * the data: between two points the curve stays within their values, so a spike cannot render as a dip
 * below zero.
 */
function buildSmoothPath(points: Point[]): string {
  if (points.length === 0) return '';
  if (points.length === 1) return `M${points[0].x.toFixed(2)},${points[0].y.toFixed(2)}`;
  if (points.length === 2) {
    return `M${points[0].x.toFixed(2)},${points[0].y.toFixed(2)} L${points[1].x.toFixed(2)},${points[1].y.toFixed(2)}`;
  }

  const n = points.length;
  const slopes: number[] = [];
  for (let i = 0; i < n - 1; i++) {
    const dx = points[i + 1].x - points[i].x;
    slopes.push(dx === 0 ? 0 : (points[i + 1].y - points[i].y) / dx);
  }

  // Tangent at each point, then damped so neighbouring segments cannot bulge past the samples.
  const tangents: number[] = new Array(n);
  tangents[0] = slopes[0];
  tangents[n - 1] = slopes[n - 2];
  for (let i = 1; i < n - 1; i++) {
    tangents[i] = slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2;
  }
  for (let i = 0; i < n - 1; i++) {
    if (slopes[i] === 0) {
      tangents[i] = 0;
      tangents[i + 1] = 0;
      continue;
    }
    const a = tangents[i] / slopes[i];
    const b = tangents[i + 1] / slopes[i];
    const magnitude = Math.hypot(a, b);
    if (magnitude > 3) {
      const scale = 3 / magnitude;
      tangents[i] = scale * a * slopes[i];
      tangents[i + 1] = scale * b * slopes[i];
    }
  }

  let path = `M${points[0].x.toFixed(2)},${points[0].y.toFixed(2)}`;
  for (let i = 0; i < n - 1; i++) {
    const dx = points[i + 1].x - points[i].x;
    const c1x = points[i].x + dx / 3;
    const c1y = points[i].y + (tangents[i] * dx) / 3;
    const c2x = points[i + 1].x - dx / 3;
    const c2y = points[i + 1].y - (tangents[i + 1] * dx) / 3;
    path += ` C${c1x.toFixed(2)},${c1y.toFixed(2)} ${c2x.toFixed(2)},${c2y.toFixed(2)} ${points[i + 1].x.toFixed(2)},${points[i + 1].y.toFixed(2)}`;
  }

  return path;
}

function formatDay(day: string, long = false) {
  const parsed = new Date(day);
  if (Number.isNaN(parsed.getTime())) return day;
  return parsed.toLocaleDateString('pl-PL', long
    ? { day: '2-digit', month: 'long', year: 'numeric' }
    : { day: '2-digit', month: '2-digit' });
}
