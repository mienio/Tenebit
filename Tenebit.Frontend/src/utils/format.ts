export function formatMoney(value: number | null | undefined, currency = 'PLN') {
  return new Intl.NumberFormat('pl-PL', { style: 'currency', currency }).format(value ?? 0);
}

export function formatDate(value?: string | null) {
  if (!value) return '—';
  const normalized = value.length === 10 ? `${value}T00:00:00` : value;
  const date = new Date(normalized);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('pl-PL').format(date);
}

export function formatDateTime(value?: string | null) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('pl-PL', { dateStyle: 'short', timeStyle: 'short' }).format(date);
}

export function toNullable(value: string) {
  return value.trim() ? value.trim() : null;
}

/** Quotes a CSV cell per RFC4180 and defuses leading =/+/-/@ so Excel/Sheets can't
 * interpret user-entered data (asset/person names, locations, ...) as a formula
 * when the export is opened later (CSV/formula injection, CWE-1236). */
export function csvCell(value: string | number) {
  const text = String(value);
  const safe = /^[=+\-@\t\r]/.test(text) ? `'${text}` : text;
  return `"${safe.replace(/"/g, '""')}"`;
}
