import { useTranslation } from "react-i18next";
import { format } from "date-fns";
import { getDateLocale, parseDbDate } from "@/lib/date-locale";
import { getTypeInfo } from "@/modules/transactions/components/TransactionTypeIcon";
import { formatCurrency } from "@/lib/format";
import { Wallet, Link, Users } from "lucide-react";
import type { components } from "@/lib/api-types";

type TransactionResponse = components["schemas"]["TransactionResponse"];

interface TransactionRowProps {
  transaction: TransactionResponse;
  showRunningBalance: boolean;
  onEdit: (tx: TransactionResponse) => void;
  mainCurrency: string;
}

export function TransactionRow({ transaction, showRunningBalance, onEdit, mainCurrency }: TransactionRowProps) {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  const typeInfo = getTypeInfo(transaction.type);
  const TypeIcon = transaction.isInitialBalance ? Wallet : typeInfo.icon;
  const iconColor = transaction.isInitialBalance ? "text-violet-400" : typeInfo.color;
  const isIncome = transaction.type === 0 || transaction.type === 4;
  const amount = Number(transaction.amount);
  const runningBalance = transaction.runningBalance ? Number(transaction.runningBalance) : null;
  const exchangeRate = Number(transaction.exchangeRate);
  const isForeignCurrency = transaction.currency !== mainCurrency;
  const isTransfer = transaction.type === 2;
  const isVirtualChild = transaction.parentTransactionId != null && transaction.type === 3;
  const pendingDebts = transaction.participants?.filter(
    (p) => Number(p.remainingAmount) > 0 && !p.isTrackingOnly
  ) ?? [];
  const participantCount = transaction.participants?.length ?? 0;

  return (
    <div
      className={`flex cursor-pointer items-center gap-3 border-b border-border/30 px-4 py-2.5 transition-colors hover:bg-muted/20 ${isVirtualChild ? "opacity-60" : ""}`}
      onClick={() => onEdit(transaction)}
    >
      <div className={`flex size-8 shrink-0 items-center justify-center rounded-lg ${iconColor.replace("text-", "bg-")}/10`}>
        {isVirtualChild ? <Link className={`size-4 ${iconColor}`} /> : <TypeIcon className={`size-4 ${iconColor}`} />}
      </div>
      <div className="flex min-w-0 flex-1 items-center gap-2">
        <div className="min-w-0 flex-1">
          {transaction.notes ? (
            <div className="flex items-center gap-2">
              <span className="text-sm truncate">
                {transaction.notes}
              </span>
              {transaction.installmentGroupId && (
                <span className="text-xs uppercase tracking-wider text-blue-400/70 font-medium">
                  {transaction.installmentNumber}/{transaction.totalInstallments} {t("transactions.installments")}
                </span>
              )}
              {isVirtualChild && (
                <span className="text-xs uppercase tracking-wider text-muted-foreground/60 font-medium">
                  {t("transactions.virtualDebt")}
                </span>
              )}
            </div>
          ) : isVirtualChild ? (
            <div className="flex items-center gap-2">
              <span className="text-xs uppercase tracking-wider text-muted-foreground/60 font-medium">
                {t("transactions.virtualDebt")}
              </span>
              {transaction.installmentGroupId && (
                <span className="text-xs uppercase tracking-wider text-blue-400/70 font-medium">
                  {transaction.installmentNumber}/{transaction.totalInstallments} {t("transactions.installments")}
                </span>
              )}
            </div>
          ) : transaction.installmentGroupId ? (
            <span className="text-xs uppercase tracking-wider text-blue-400/70 font-medium">
              {transaction.installmentNumber}/{transaction.totalInstallments} {t("transactions.installments")}
            </span>
          ) : null}
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            {isTransfer && transaction.transferDestinationAccountName ? (
              <span className="truncate">
                {transaction.accountName} → {transaction.transferDestinationAccountName}
              </span>
            ) : (
              <span className="truncate">{transaction.accountName}</span>
            )}
            <span aria-hidden="true">·</span>
            <span className="truncate">{transaction.categoryName ?? t("transactions.uncategorized")}</span>
          </div>
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <span>{format(parseDbDate(transaction.date), "MMM d, yyyy HH:mm", { locale: dateLocale })}</span>
            {participantCount > 0 && (
              <span className="flex items-center gap-1 text-muted-foreground/70">
                <Users className="size-3" />
                {participantCount}
              </span>
            )}
            {pendingDebts.length > 0 && (
              <span className="text-amber-400 font-medium">
                · {t("transactions.debtPending", { count: pendingDebts.length })}
              </span>
            )}
          </div>
        </div>
      </div>
        <div className="flex items-center gap-3 shrink-0">
        <div className="text-right">
          <p className={`text-sm font-semibold tabular-nums ${isIncome ? "text-emerald-400" : "text-red-400"}`}>
            {isIncome ? "+" : "-"}
            {formatCurrency(amount, transaction.currency)}
          </p>
          {isForeignCurrency && (
            <p className="text-xs text-muted-foreground tabular-nums">
              ≈ {formatCurrency(amount * exchangeRate, mainCurrency)}{exchangeRate > 0 ? ` @ ${exchangeRate}` : ""}
            </p>
          )}
          {showRunningBalance && runningBalance !== null && (
            <p className="text-xs text-muted-foreground tabular-nums">
              {t("transactions.runningBalance")}: {formatCurrency(runningBalance, transaction.currency)}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
