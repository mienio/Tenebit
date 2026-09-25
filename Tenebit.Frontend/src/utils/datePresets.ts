// Arytmetyka na datach w formacie pola <input type="date"> ("2026-09-25"). Liczymy na składowych
// lokalnych, nie przez toISOString(): ten dałby datę UTC, czyli w Polsce tuż po północy jeszcze wczorajszą.

const pad = (value: number) => String(value).padStart(2, '0');

function parse(iso: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  if (!match) return null;
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

function format(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function todayIsoLocal(now = new Date()): string {
  return format(now);
}

export function addDays(iso: string, days: number): string {
  const date = parse(iso);
  if (!date) return '';
  date.setDate(date.getDate() + days);
  return format(date);
}

/// +N miesięcy z przycięciem do końca miesiąca: 31 stycznia + 1 miesiąc to 28/29 lutego, a nie
/// 3 marca, jak zrobiłby to goły Date.setMonth.
export function addMonths(iso: string, months: number): string {
  const date = parse(iso);
  if (!date) return '';
  const day = date.getDate();
  date.setDate(1);
  date.setMonth(date.getMonth() + months);
  const lastDay = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
  date.setDate(Math.min(day, lastDay));
  return format(date);
}

export function endOfMonth(iso: string): string {
  const date = parse(iso);
  if (!date) return '';
  return format(new Date(date.getFullYear(), date.getMonth() + 1, 0));
}
