import { useTranslation } from "react-i18next";
import { useState } from "react";
import { format } from "date-fns";
import { formatCurrency } from "@/lib/format";
import { getDateLocale } from "@/lib/date-locale";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { useCategoryBudgets } from "@/lib/hooks/useBudgets";
import { AlertCircle, CalendarIcon, TrendingUp } from "lucide-react";
import type { components } from "@/lib/api-types";
import { cn } from "@/lib/utils";

type CategoryBudget = components["schemas"]["CategoryBudget"];
type BudgetThresholdStatus = components["schemas"]["BudgetThresholdStatus"];

interface BudgetsWidgetProps {
  currency?: string;
}

function statusMeta(status: BudgetThresholdStatus | undefined, t: (k: string) => string) {
  switch (status) {
    case 1:
      return {
        label: t("budgets.statusWarning"),
        badge: "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400 border-transparent",
        bar: "bg-amber-500",
      };
    case 2:
      return {
        label: t("budgets.statusExceeded"),
        badge: "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400 border-transparent",
        bar: "bg-destructive",
      };
    default:
      return {
        label: t("budgets.statusNormal"),
        badge: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400 border-transparent",
        bar: "bg-emerald-500",
      };
  }
}

function BudgetRow({ budget, currency }: { budget: CategoryBudget; currency?: string }) {
  const { t } = useTranslation();
  const pct = Math.min(Number(budget.percentage), 100);
  const meta = statusMeta(budget.status, t);

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-2">
        <span className="inline-flex min-w-0 items-center gap-2 text-sm">
          <span
            className="size-2.5 shrink-0 rounded-full"
            style={{ backgroundColor: budget.color ?? "#e5e7eb" }}
          />
          <span className="truncate font-medium">{budget.categoryName}</span>
          <Badge variant="outline" className={cn("text-[10px] px-1.5 py-0", meta.badge)}>
            {meta.label}
          </Badge>
        </span>
        <span className="shrink-0 text-xs text-muted-foreground">
          {pct.toFixed(0)}%
        </span>
      </div>
      <Progress value={pct} aria-label={budget.categoryName} />
      <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-0.5 text-xs text-muted-foreground">
        <span>
          {formatCurrency(Number(budget.spent), currency)}
        </span>
        <span className="truncate">
          {t("budgets.of")} {formatCurrency(Number(budget.monthlyBudget), currency)}
        </span>
        <span className={Number(budget.remaining) < 0 ? "font-semibold text-destructive" : ""}>
          {formatCurrency(Number(budget.remaining), currency)} {t("budgets.remaining")}
        </span>
      </div>
    </div>
  );
}

export function BudgetsWidget({ currency = "USD" }: BudgetsWidgetProps) {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  const [month, setMonth] = useState(() => {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}`;
  });

  const { data: budgets, isLoading } = useCategoryBudgets(month);

  const monthDate = new Date(`${month}-01T00:00:00`);
  const selectedDay = new Date(`${month}-15T00:00:00`);

  return (
    <Card className="w-full">
      <CardHeader>
        <div className="flex items-center justify-between gap-2">
          <div>
            <CardTitle className="text-base">{t("budgets.title")}</CardTitle>
            <CardDescription>{t("budgets.description")}</CardDescription>
          </div>
          <Popover>
            <PopoverTrigger render={
              <Button variant="outline" className="h-8 gap-1.5 px-2.5 text-xs font-normal" aria-label={t("budgets.selectMonth")}>
                <CalendarIcon className="size-3.5" />
                {format(monthDate, "MMMM yyyy", { locale: dateLocale })}
              </Button>
            } />
            <PopoverContent className="w-auto p-3" align="end">
              <Calendar
                mode="single"
                locale={dateLocale}
                selected={selectedDay}
                defaultMonth={selectedDay}
                onSelect={(date) => {
                  if (!date) return;
                  const y = date.getFullYear();
                  const m = String(date.getMonth() + 1).padStart(2, "0");
                  setMonth(`${y}-${m}`);
                }}
                captionLayout="dropdown"
                startMonth={new Date(new Date().getFullYear() - 10, 0, 1)}
                endMonth={new Date(new Date().getFullYear() + 1, 11, 31)}
              />
            </PopoverContent>
          </Popover>
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="flex flex-col gap-3">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-5/6" />
          </div>
        ) : !budgets || budgets.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-2 py-8 text-center">
            <TrendingUp className="size-8 text-muted-foreground/50" />
            <p className="text-sm text-muted-foreground">{t("budgets.empty")}</p>
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            {budgets.map((b) => (
              <BudgetRow key={b.categoryId} budget={b} currency={currency} />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

export function BudgetAlertBanner() {
  const { t } = useTranslation();
  const { data: budgets, isLoading } = useCategoryBudgets();

  if (isLoading) return null;
  const exceeded = (budgets ?? []).filter((b) => b.status === 2);

  if (exceeded.length === 0) return null;

  return (
    <div className="flex items-center gap-2 rounded-2xl border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
      <AlertCircle className="size-4 shrink-0" />
      <span>{t("budgets.exceededBanner", { count: exceeded.length })}</span>
    </div>
  );
}
