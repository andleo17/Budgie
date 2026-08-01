import { differenceInDays } from "date-fns";
import { cn } from "@/lib/utils";
import { Target, TrendingUp, Trophy, Clock, Flame } from "lucide-react";
import type { components } from "@/lib/api-types";

type GeneratedGoal = components["schemas"]["GoalResponse"];

export interface GoalAccount {
  id: GeneratedGoal["id"];
  accountId: GeneratedGoal["accountId"];
  name: GeneratedGoal["accountName"];
  currency: GeneratedGoal["currency"];
  balance: number;
  targetAmount: number | null;
  targetDate: string | null;
  progressPercentage: number;
  accountName: string;
}

export function toGoalAccount(goal: GeneratedGoal): GoalAccount {
  return {
    id: goal.id,
    accountId: goal.accountId,
    name: goal.accountName,
    currency: goal.currency,
    balance: Number(goal.currentBalance),
    targetAmount: goal.targetAmount != null ? Number(goal.targetAmount) : null,
    targetDate: goal.targetDate ?? null,
    progressPercentage: Number(goal.progressPercentage),
    accountName: goal.accountName,
  };
}

export function getProgress(balance: number, target: number): number {
  if (target <= 0) return 0;
  return Math.min(100, (balance / target) * 100);
}

export function getDaysLeft(targetDate: string | null): number | null {
  if (!targetDate) return null;
  return differenceInDays(new Date(targetDate), new Date());
}

export function getGoalStatus(
  pct: number,
  daysLeft: number | null,
  t: (key: string) => string,
) {
  if (pct >= 100)
    return {
      label: t("goals.completedStatus"),
      color: "text-emerald-400",
      bg: "bg-emerald-400/10 border-emerald-400/20",
      ring: "hsl(160 84% 39%)",
      icon: Trophy,
    };
  if (daysLeft !== null && daysLeft < 0)
    return {
      label: t("goals.overdueStatus"),
      color: "text-red-400",
      bg: "bg-red-400/10 border-red-400/20",
      ring: "hsl(0 72% 51%)",
      icon: Clock,
    };
  if (daysLeft !== null && daysLeft <= 30)
    return {
      label: t("goals.dueSoonStatus"),
      color: "text-amber-400",
      bg: "bg-amber-400/10 border-amber-400/20",
      ring: "hsl(38 92% 50%)",
      icon: Flame,
    };
  if (pct >= 75)
    return {
      label: t("goals.almostThereStatus"),
      color: "text-blue-400",
      bg: "bg-blue-400/10 border-blue-400/20",
      ring: "hsl(221 83% 53%)",
      icon: TrendingUp,
    };
  return {
    label: t("goals.inProgressStatus"),
    color: "text-muted-foreground",
    bg: "bg-muted/40 border-border",
    ring: "hsl(215 20% 55%)",
    icon: Target,
  };
}

export { cn };
