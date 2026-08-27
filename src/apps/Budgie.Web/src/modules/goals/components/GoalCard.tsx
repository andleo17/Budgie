import { useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { Badge } from "@/components/ui/badge";
import {
  Loader2,
  CalendarDays,
  CheckCircle2,
  Pencil,
  Trash2,
} from "lucide-react";
import { format, isPast } from "date-fns";
import { formatCurrency } from "@/lib/format";
import { getDateLocale } from "@/lib/date-locale";
import { getProgress, getDaysLeft, getGoalStatus, type GoalAccount } from "./goal-utils";
import { cn } from "@/lib/utils";

export default function GoalCard({
  goal,
  onEdit,
  t,
  i18n,
}: {
  goal: GoalAccount;
  onEdit: (g: GoalAccount) => void;
  t: (key: string) => string;
  i18n: { language: string };
}) {
  const dateLocale = getDateLocale(i18n.language);
  const queryClient = useQueryClient();
  const pct = getProgress(goal.balance, goal.targetAmount ?? 0);
  const daysLeft = getDaysLeft(goal.targetDate);
  const status = getGoalStatus(pct, daysLeft, t);
  const StatusIcon = status.icon;
  const isComplete = pct >= 100;

  const deleteMutation = useMutation({
    mutationFn: async (params: { id: string }) => {
      const { error } = await client.DELETE("/api/finances/goals/{id}", {
        params: { path: { id: params.id } },
      });
      if (error) throw error;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["goals"] }),
  });

  return (
    <div
      className={cn(
        "group relative rounded-2xl border bg-card p-5 transition-all duration-200 active:scale-[0.995] active:bg-muted/30 hover:shadow-xl hover:shadow-black/30 hover:-translate-y-0.5 overflow-hidden",
        status.bg,
      )}
    >
      {isComplete && (
        <div className="pointer-events-none absolute inset-0 bg-linear-to-br from-emerald-400/5 via-transparent to-transparent rounded-2xl" />
      )}

      <div className="flex items-start justify-between gap-3 mb-4">
        <div className="flex items-center gap-3 min-w-0">
          <div
            className={cn(
              "flex h-10 w-10 shrink-0 items-center justify-center rounded-xl",
              status.bg,
            )}
          >
            <StatusIcon className={cn("h-5 w-5", status.color)} />
          </div>
          <div className="min-w-0">
            <h3 className="font-semibold text-foreground truncate leading-tight">
              {goal.name}
            </h3>
            <Badge
              variant="secondary"
              className="mt-0.5 text-xs font-normal opacity-70"
            >
              {t("goals.savingsGoal")}
            </Badge>
          </div>
        </div>

        <div className="flex gap-1 opacity-100 sm:opacity-0 sm:group-hover:opacity-100 transition-opacity duration-150 shrink-0">
          <button
            onClick={() => onEdit(goal)}
            aria-label={`${t("goals.editAria")} ${goal.name}`}
            className="flex h-7 w-7 cursor-pointer items-center justify-center rounded-lg bg-muted/60 hover:bg-muted transition-colors"
          >
            <Pencil className="h-3.5 w-3.5 text-muted-foreground" />
          </button>
          <button
            onClick={() => {
              if (confirm(`${t("goals.deleteConfirm")} "${goal.name}"?`))
                deleteMutation.mutate({ id: goal.id });
            }}
            aria-label={`${t("goals.deleteAria")} ${goal.name}`}
            className="flex h-7 w-7 cursor-pointer items-center justify-center rounded-lg bg-destructive/10 hover:bg-destructive/20 transition-colors"
          >
            {deleteMutation.isPending ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin text-destructive" />
            ) : (
              <Trash2 className="h-3.5 w-3.5 text-destructive" />
            )}
          </button>
        </div>
      </div>

      <div className="mb-4">
        <div className="flex flex-wrap items-baseline gap-1.5">
          <span
            className={cn(
              "text-3xl font-bold tabular-nums min-w-0 truncate",
              isComplete ? "text-emerald-400" : "text-foreground",
            )}
          >
            {formatCurrency(goal.balance, goal.currency)}
          </span>
          <span className="text-sm text-muted-foreground">
            /{" "}
            {goal.targetAmount
              ? formatCurrency(goal.targetAmount, goal.currency)
              : "—"}
          </span>
        </div>
        {goal.targetAmount && (
          <p className={cn("text-xs mt-0.5 font-medium", status.color)}>
            {isComplete
              ? t("goals.goalReached")
              : `${formatCurrency(goal.targetAmount - goal.balance, goal.currency)} ${t("goals.remaining")}`}
          </p>
        )}
      </div>

      {goal.targetAmount && (
        <div className="mb-4 space-y-1.5">
          <div className="flex justify-between text-xs text-muted-foreground">
            <span>
              {pct.toFixed(1)}
              {t("goals.percentSaved")}
            </span>
            {daysLeft !== null && (
              <span className={cn(status.color, "font-medium")}>
                {daysLeft < 0
                  ? `${Math.abs(daysLeft)} ${t("goals.daysOverdue")}`
                  : daysLeft === 0
                    ? t("goals.dueToday")
                    : `${daysLeft} ${t("goals.daysLeft")}`}
              </span>
            )}
          </div>
          <div className="relative h-2 w-full overflow-hidden rounded-full bg-muted/60">
            <div
              className="h-full rounded-full transition-all duration-700 ease-out"
              style={{
                width: `${pct}%`,
                background: `linear-gradient(90deg, ${status.ring} 0%, ${isComplete ? "hsl(160 84% 60%)" : status.ring} 100%)`,
                boxShadow: `0 0 8px ${status.ring}60`,
              }}
            />
          </div>
        </div>
      )}

      {goal.targetDate && (
        <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <CalendarDays className="h-3.5 w-3.5 shrink-0" />
          <span>
            {t("goals.targetLabel")}{" "}
            <span
              className={cn(
                "font-medium",
                isPast(new Date(goal.targetDate)) && !isComplete
                  ? "text-red-400"
                  : "text-foreground/70",
              )}
            >
              {format(new Date(goal.targetDate), "MMM d, yyyy", { locale: dateLocale })}
            </span>
          </span>
          {isComplete && (
            <CheckCircle2 className="ml-auto h-4 w-4 text-emerald-400" />
          )}
        </div>
      )}
    </div>
  );
}
