import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { formatCurrency } from "@/lib/format";
import type { components } from "@/lib/api-types";
import { Loader2, TrendingUp } from "lucide-react";
import { Link } from "react-router-dom";

type GoalStatus = components["schemas"]["GoalStatus"];

interface GoalsWidgetCardProps {
  goals: GoalStatus[];
  currency?: string;
  isLoading: boolean;
  t: (key: string) => string;
}

export function GoalsWidgetCard({
  goals,
  currency,
  isLoading,
  t,
}: GoalsWidgetCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("dashboard.financialGoals")}</CardTitle>
        <CardDescription>{t("dashboard.goalsDesc")}</CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : goals && goals.length > 0 ? (
          <div className="space-y-8">
            {goals.map((goal) => (
              <div key={goal.accountId} className="space-y-2">
                <div className="flex items-center justify-between text-sm">
                  <span className="font-medium">{goal.accountName}</span>
                  <span className="text-muted-foreground">
                    {formatCurrency(Number(goal.currentBalance), currency)} /{" "}
                    {formatCurrency(Number(goal.targetAmount), currency)}
                  </span>
                </div>
                <Progress
                  value={Number(goal.progressPercentage)}
                  className="h-2"
                />
                <p className="text-xs text-muted-foreground text-right">
                  {Number(goal.progressPercentage).toFixed(1)}
                  {t("dashboard.percentCompleted")}
                </p>
              </div>
            ))}
          </div>
        ) : (
          <div className="flex flex-col items-center justify-center py-10 text-center gap-3">
            <div className="rounded-full bg-muted p-3">
              <TrendingUp className="h-6 w-6 text-muted-foreground" />
            </div>
            <div className="space-y-1">
              <p className="text-sm font-medium">
                {t("dashboard.noActiveGoals")}
              </p>
              <p className="text-xs text-muted-foreground">
                {t("dashboard.noActiveGoalsDesc")}
              </p>
            </div>
            <Link
              to="/goals"
              className="inline-flex h-7 items-center justify-center gap-1 rounded-lg border border-border bg-background px-2.5 text-xs font-medium text-foreground transition-all hover:bg-muted mt-2"
            >
              {t("dashboard.createSavingsGoal")}
            </Link>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
