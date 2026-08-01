import { useTranslation } from "react-i18next";
import { formatCurrency } from "@/lib/format";
import { Wallet, TrendingUp, TrendingDown, Activity } from "lucide-react";
import { SummaryStatCard } from "../components/SummaryStatCard";
import { SpendingChartCard } from "../components/SpendingChartCard";
import { GoalsWidgetCard } from "../components/GoalsWidgetCard";
import { useDashboardSummary, useGoalsStatus, useCategorySpending } from "@/lib/hooks/useDashboard";

export default function Dashboard() {
  const { t } = useTranslation();
  const { data: summary, isLoading: isLoadingSummary } = useDashboardSummary();
  const { data: goals, isLoading: isLoadingGoals } = useGoalsStatus();
  const { data: spending, isLoading: isLoadingSpending } = useCategorySpending();

  const netFlow = Number(summary?.netFlow ?? 0);

  return (
    <>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight md:text-3xl">
          {t("dashboard.title")}
        </h1>
      </div>

      <div className="grid gap-4 md:grid-cols-2 md:gap-8 lg:grid-cols-4">
        <SummaryStatCard
          label={t("dashboard.totalBalance")}
          value={formatCurrency(summary?.totalBalance ?? 0, summary?.currency)}
          subLabel={t("dashboard.acrossAllAccounts")}
          icon={Wallet}
          iconColor="text-muted-foreground"
          isLoading={isLoadingSummary}
        />
        <SummaryStatCard
          label={t("dashboard.monthlyIncome")}
          value={formatCurrency(summary?.monthlyIncome ?? 0, summary?.currency)}
          subLabel={t("dashboard.thisMonth")}
          icon={TrendingUp}
          iconColor="text-emerald-500"
          isLoading={isLoadingSummary}
        />
        <SummaryStatCard
          label={t("dashboard.monthlyExpenses")}
          value={formatCurrency(summary?.monthlyExpenses ?? 0, summary?.currency)}
          subLabel={t("dashboard.thisMonth")}
          icon={TrendingDown}
          iconColor="text-destructive"
          isLoading={isLoadingSummary}
        />
        <SummaryStatCard
          label={t("dashboard.netMonthly")}
          value={formatCurrency(netFlow, summary?.currency)}
          subLabel={t("dashboard.incomeMinusExpenses")}
          icon={Activity}
          iconColor={netFlow >= 0 ? "text-emerald-500" : "text-destructive"}
          isLoading={isLoadingSummary}
        />
      </div>

      <div className="grid gap-4 md:gap-8 lg:grid-cols-2 xl:grid-cols-3">
        <SpendingChartCard
          spending={spending ?? []}
          currency={summary?.currency}
          isLoading={isLoadingSpending}
          t={t}
        />
        <GoalsWidgetCard
          goals={goals ?? []}
          currency={summary?.currency}
          isLoading={isLoadingGoals}
          t={t}
        />
      </div>
    </>
  );
}
