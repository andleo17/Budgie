import { useTranslation } from "react-i18next";
import { ArrowDown, ArrowUp, Wallet } from "lucide-react";
import { formatCurrency } from "@/lib/format";

interface CurrencySummaryData {
  income: number;
  expenses: number;
  balance: number;
  currency: string;
}

interface TransactionSummaryBarProps {
  currencies: CurrencySummaryData[];
  mainCurrency: string;
  approximateIncome?: number;
  approximateExpenses?: number;
  isLoading?: boolean;
  endingBalance?: { value: number; currency: string } | null;
}

export function TransactionSummaryBar({
  currencies,
  mainCurrency,
  approximateIncome,
  approximateExpenses,
  isLoading,
  endingBalance,
}: TransactionSummaryBarProps) {
  const { t } = useTranslation();

  if (isLoading) {
    return (
      <div className="grid grid-cols-2 gap-3">
        {[1, 2, 3].map((i) => (
          <div key={i} className="h-24 animate-pulse rounded-xl bg-muted" />
        ))}
      </div>
    );
  }

  const filtered = currencies.filter((c) => c.income !== 0 || c.expenses !== 0 || c.balance !== 0);
  const multiCurrency = filtered.length > 1;
  const netApprox = (approximateIncome ?? 0) - (approximateExpenses ?? 0);

  const incomeTotal = multiCurrency ? (approximateIncome ?? 0) : (filtered[0]?.income ?? 0);
  const expensesTotal = multiCurrency ? (approximateExpenses ?? 0) : (filtered[0]?.expenses ?? 0);

  const cards = [
    {
      label: t("transactions.summaryIncome"),
      total: incomeTotal,
      icon: ArrowUp,
      color: "text-emerald-400",
      bg: "bg-emerald-500/10",
      items: filtered.map((c) => ({ currency: c.currency, value: c.income, positive: true })),
    },
    {
      label: t("transactions.summaryExpenses"),
      total: expensesTotal,
      icon: ArrowDown,
      color: "text-red-400",
      bg: "bg-red-500/10",
      items: filtered.map((c) => ({ currency: c.currency, value: c.expenses, positive: false })),
    },
    {
      label: t("transactions.summaryNet"),
      total: netApprox,
      icon: netApprox >= 0 ? ArrowUp : ArrowDown,
      color: netApprox >= 0 ? "text-emerald-400" : "text-red-400",
      bg: netApprox >= 0 ? "bg-emerald-500/10" : "bg-red-500/10",
      items: filtered.map((c) => ({ currency: c.currency, value: c.balance, positive: c.balance >= 0 })),
      approximate: multiCurrency,
    },
  ];

  if (endingBalance) {
    cards.push({
      label: t("transactions.summaryEndingBalance"),
      total: endingBalance.value,
      icon: Wallet,
      color: "text-violet-400",
      bg: "bg-violet-500/10",
      items: [{ currency: endingBalance.currency, value: endingBalance.value, positive: endingBalance.value >= 0 }],
    });
  }

  return (
    <div className={`grid gap-3 sm:grid-cols-2 ${cards.length === 4 ? "lg:grid-cols-4" : "lg:grid-cols-3"}`}>
      {cards.map((card) => {
        const nonZeroItems = card.items.filter((i) => i.value !== 0);
        const hasBreakdown = multiCurrency && nonZeroItems.length > 0;
        return (
          <div key={card.label} className={`rounded-xl border bg-card p-3 flex flex-col h-full ${hasBreakdown ? "" : "justify-center"}`}>
            <div className="flex items-start gap-3">
              <div className={`flex size-9 shrink-0 items-center justify-center rounded-lg ${card.bg}`}>
                <card.icon className={`size-4 ${card.color}`} />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-xs text-muted-foreground">{card.label}</p>
                <p className={`text-lg font-bold tabular-nums leading-tight ${card.color}`}>
                  {card.approximate && <span className="text-muted-foreground mr-0.5">~</span>}
                  {formatCurrency(Math.abs(card.total), mainCurrency)}
                </p>
              </div>
            </div>
            {hasBreakdown && (
              <div className="mt-2 flex flex-wrap items-center gap-x-0 gap-y-0.5 text-xs text-muted-foreground/60 leading-tight">
                {nonZeroItems.map((i, idx) => (
                  <span key={i.currency}>
                    {idx > 0 && <span className="mx-1 text-muted-foreground/30">|</span>}
                    {i.positive ? "+" : "-"}{formatCurrency(Math.abs(i.value), i.currency)}
                  </span>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
