export interface BarChartItem {
  label: string;
  value: number;
}

export function BarChart({ items, formatValue, onItemClick }: { items: BarChartItem[]; formatValue?: (value: number) => string; onItemClick?: (item: BarChartItem) => void }) {
  const max = Math.max(...items.map(item => item.value), 1);

  return (
    <div className="barList">
      {items.map(item => (
        onItemClick ? (
          <button type="button" className="barItem barItem--clickable" key={item.label} onClick={() => onItemClick(item)}>
            <div><span>{item.label}</span><strong>{formatValue ? formatValue(item.value) : item.value}</strong></div>
            <span><i style={{ width: `${(item.value / max) * 100}%` }} /></span>
          </button>
        ) : (
          <div className="barItem" key={item.label}>
            <div><span>{item.label}</span><strong>{formatValue ? formatValue(item.value) : item.value}</strong></div>
            <span><i style={{ width: `${(item.value / max) * 100}%` }} /></span>
          </div>
        )
      ))}
    </div>
  );
}
