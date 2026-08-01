import { format, type Locale } from "date-fns";

export interface PeriodRange {
  from: string;
  to: string;
  label: string;
  key: string;
}

export function getMonthRange(year: number, month: number, locale?: Locale): PeriodRange {
  const start = new Date(year, month, 1);
  const end = new Date(year, month + 1, 0);
  return {
    from: format(start, "yyyy-MM-dd"),
    to: format(end, "yyyy-MM-dd"),
    label: format(start, "MMMM yyyy", { locale }),
    key: `${year}-${String(month + 1).padStart(2, "0")}`,
  };
}

export function navigateMonth(
  year: number,
  month: number,
  direction: number,
): { year: number; month: number } {
  const d = new Date(year, month + direction, 1);
  return { year: d.getFullYear(), month: d.getMonth() };
}

function clampDay(day: number): number {
  return Math.min(Math.max(day, 1), 28);
}

export function getBillingPeriodRange(
  refDate: Date,
  cycleDay: number,
  inclusive: boolean,
  locale?: Locale,
): PeriodRange {
  const cd = clampDay(cycleDay);
  const day = refDate.getDate();
  let periodEnd: Date;
  if (day >= cd) {
    periodEnd = new Date(refDate.getFullYear(), refDate.getMonth() + 1, cd);
  } else {
    periodEnd = new Date(refDate.getFullYear(), refDate.getMonth(), cd);
  }
  const periodStart = new Date(periodEnd);
  periodStart.setMonth(periodStart.getMonth() - 1);
  if (!inclusive) {
    periodEnd.setDate(periodEnd.getDate() - 1);
  }
  const fromLabel = format(periodStart, "MMM d", { locale });
  const toLabel = format(periodEnd, "MMM d, yyyy", { locale });
  const displayMonth = format(periodEnd, "MMMM yyyy", { locale });
  return {
    from: format(periodStart, "yyyy-MM-dd"),
    to: format(periodEnd, "yyyy-MM-dd"),
    label: `${displayMonth} (${fromLabel} – ${toLabel})`,
    key: `${refDate.getFullYear()}-${String(refDate.getMonth() + 1).padStart(2, "0")}-${String(refDate.getDate()).padStart(2, "0")}`,
  };
}

export function navigateBillingPeriod(
  refDate: Date,
  _cycleDay: number,
  direction: number,
): Date {
  const d = new Date(refDate);
  d.setMonth(d.getMonth() + direction);
  return d;
}

export function findCurrentBillingPeriod(
  now: Date,
  cycleDay: number,
  inclusive: boolean,
  locale?: Locale,
): PeriodRange {
  return getBillingPeriodRange(now, cycleDay, inclusive, locale);
}
