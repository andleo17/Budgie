import { useState, useMemo, useCallback, useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import {
  format,
  startOfWeek,
  isToday,
  isYesterday,
} from "date-fns";
import type { Locale } from "date-fns";
import { getDateLocale, parseDbDate } from "@/lib/date-locale";
import { formatCurrency } from "@/lib/format";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { TransactionRow } from "@/modules/transactions/components/TransactionRow";
import { TransactionFilters } from "@/modules/transactions/components/TransactionFilters";
import { TransactionFormDialog } from "@/modules/transactions/components/TransactionFormDialog";
import {
  TransactionTypeDropdown,
  type FormType,
} from "@/modules/transactions/components/TransactionTypeDropdown";
import { DeleteTransactionDialog } from "@/modules/transactions/components/DeleteTransactionDialog";
import { TransactionEmptyState } from "@/modules/transactions/components/TransactionEmptyState";
import { ApplyFromTemplateDialog } from "@/modules/transactions/components/ApplyFromTemplateDialog";
import { PageSkeleton } from "@/components/page-skeleton";
import { MetricStrip, type MetricItem } from "@/components/shared/MetricStrip";
import { ChevronLeft, ChevronRight, FileText } from "lucide-react";
import { Button } from "@/components/ui/button";
import { parseViewMode, type ViewMode, TransactionType } from "@/lib/constants";
import { getMonthRange, getBillingPeriodRange } from "../period-utils";
import { useTransactions, useTransactionSummary, useSettings } from "@/lib/hooks/useTransactions";

type TransactionResponse = components["schemas"]["TransactionResponse"];

interface TransactionGroup {
  label: string;
  date: Date;
  transactions: TransactionResponse[];
}

function getGroupLabel(
  date: Date,
  view: ViewMode,
  t: (key: string) => string,
  locale: Locale,
): string {
  if (view === "daily") {
    if (isToday(date)) return t("transactions.today");
    if (isYesterday(date)) return t("transactions.yesterday");
    return format(date, "EEEE, MMMM d, yyyy", { locale });
  }
  if (view === "weekly") {
    const weekStart = startOfWeek(date, { weekStartsOn: 1 });
    const weekEnd = new Date(weekStart);
    weekEnd.setDate(weekEnd.getDate() + 6);
    return `${format(weekStart, "MMM d", { locale })} – ${format(weekEnd, "MMM d, yyyy", { locale })}`;
  }
  if (view === "monthly") {
    return format(date, "MMMM yyyy", { locale });
  }
  return format(date, "yyyy");
}

function groupTransactions(
  transactions: TransactionResponse[],
  view: ViewMode,
): TransactionGroup[] {
  if (!transactions.length) return [];

  const sorted = [...transactions].sort(
    (a, b) => parseDbDate(b.date).getTime() - parseDbDate(a.date).getTime(),
  );

  const groups: TransactionGroup[] = [];

  for (const tx of sorted) {
    const date = parseDbDate(tx.date);
    let groupKey: string;

    if (view === "daily") {
      groupKey = format(date, "yyyy-MM-dd");
    } else if (view === "weekly") {
      const ws = startOfWeek(date, { weekStartsOn: 1 });
      groupKey = format(ws, "yyyy-MM-dd");
    } else if (view === "monthly") {
      groupKey = format(date, "yyyy-MM");
    } else {
      groupKey = format(date, "yyyy");
    }

    const existing = groups.find((g) => g.label === groupKey);
    if (existing) {
      existing.transactions.push(tx);
    } else {
      groups.push({ label: groupKey, date, transactions: [tx] });
    }
  }

  return groups;
}

export default function Transactions() {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  const [searchParams] = useSearchParams();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogTx, setDialogTx] = useState<TransactionResponse | null>(null);
  const [dialogFormType, setDialogFormType] =
    useState<FormType>("income-expense");
  const [templateDialogOpen, setTemplateDialogOpen] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<components["schemas"]["RecurringTransactionResponse"] | null>(null);
  const [copySource, setCopySource] = useState<TransactionResponse | null>(null);
  const [deletingTx, setDeletingTx] = useState<TransactionResponse | null>(null);
  const [page, setPage] = useState(1);

  const accounts = searchParams.get("accounts") ?? "";
  const categories = searchParams.get("categories") ?? "";
  const types = searchParams.get("types") ?? "";
  const from = searchParams.get("from");
  const to = searchParams.get("to");
  const periodParam = searchParams.get("period");
  const search = searchParams.get("search") ?? "";
  const personIds = searchParams.get("personIds") ?? "";
  const tagIds = searchParams.get("tagIds") ?? "";
  const view = parseViewMode(searchParams.get("view"));

  const filterKey = `${accounts}|${categories}|${types}|${from}|${to}|${periodParam}|${search}|${personIds}|${tagIds}|${view}`;

  const prevFilterKey = useRef(filterKey);
  const isFirstRender = useRef(true);

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    prevFilterKey.current = filterKey;
    setPage(1);
  }, [filterKey]);

  const { data: settings } = useSettings();

  const hasSingleAccount = accounts.split(",").filter(Boolean).length === 1;
  const selectedAccountId = hasSingleAccount ? accounts.split(",")[0] : null;

  const { data: allAccounts } = useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data;
    },
    enabled: hasSingleAccount,
    select: (data) => data?.find((a) => a.id === selectedAccountId),
  });

  const dateRange = useMemo(() => {
    if (from && to) return { from, to };
    if (periodParam) {
      if (periodParam.length === 7) {
        const parts = periodParam.split("-");
        const y = parseInt(parts[0], 10);
        const m = parseInt(parts[1], 10) - 1;
        if (!isNaN(y) && !isNaN(m)) {
          const r = getMonthRange(y, m);
          return { from: r.from, to: r.to };
        }
      } else if (
        periodParam.length === 10 &&
        allAccounts?.billingCycleDay != null
      ) {
        const ref = new Date(periodParam + "T12:00:00");
        if (!isNaN(ref.getTime())) {
          const cd = Number(allAccounts.billingCycleDay);
          const cdi = allAccounts.billingCycleDayInclusive !== false;
          const bp = getBillingPeriodRange(ref, cd, cdi);
          return { from: bp.from, to: bp.to };
        }
      }
    }
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth(), 1);
    const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    return {
      from: format(start, "yyyy-MM-dd"),
      to: format(end, "yyyy-MM-dd"),
    };
  }, [from, to, periodParam, allAccounts]);

  const { data: pagedData, isLoading } = useTransactions({
    accounts,
    categories,
    types,
    from: dateRange.from,
    to: dateRange.to,
    search,
    personIds,
    tagIds,
    hasSingleAccount,
    page,
    pageSize: Number(settings?.transactionPageSize ?? 100),
    enabled: !!settings,
  });

  const { data: summary, isLoading: summaryLoading } = useTransactionSummary({
    from: dateRange.from,
    to: dateRange.to,
    accountIds: accounts,
  });

  const transactions = useMemo(() => pagedData?.items ?? [], [pagedData]);
  const totalCount = Number(pagedData?.totalCount ?? 0);
  const pageSize = Math.max(1, Number(settings?.transactionPageSize ?? 100));
  const totalPages = Math.ceil(totalCount / pageSize);
  const showPagination = totalPages > 1;

  const groups = useMemo(
    () => groupTransactions(transactions, view),
    [transactions, view],
  );

  const { data: rawCategories } = useQuery({
    queryKey: ["categories", "includeSystem"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/categories");
      if (error) throw error;
      return data;
    },
  });

  const categoryColorMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const c of rawCategories ?? []) {
      if (c.color?.startsWith("#")) map.set(c.id, c.color);
    }
    return map;
  }, [rawCategories]);

  const stripMetrics = useMemo<MetricItem[]>(() => {
    const mainCurrency = hasSingleAccount && allAccounts
      ? allAccounts.currency
      : (summary?.mainCurrency ?? settings?.mainCurrency ?? "USD");
    const currencies = summary?.currencies ?? [];
    const multiCurrency = currencies.length > 1;

    const income = multiCurrency
      ? Number(summary?.approximateIncome ?? 0)
      : Number(currencies[0]?.income ?? 0);
    const expenses = multiCurrency
      ? Number(summary?.approximateExpenses ?? 0)
      : Number(currencies[0]?.expenses ?? 0);
    const net = multiCurrency
      ? Number(summary?.approximateIncome ?? 0) - Number(summary?.approximateExpenses ?? 0)
      : Number(currencies[0]?.balance ?? 0);

    const metrics: MetricItem[] = [
      { label: t("transactions.summaryIncome"), value: formatCurrency(income, mainCurrency), tone: "positive" },
      { label: t("transactions.summaryExpenses"), value: formatCurrency(expenses, mainCurrency), tone: "negative" },
      { label: t("transactions.summaryNet"), value: formatCurrency(Math.abs(net), mainCurrency), tone: net >= 0 ? "positive" : "negative" },
    ];

    if (hasSingleAccount && allAccounts) {
      metrics.push({
        label: t("accounts.currentBalance"),
        value: formatCurrency(allAccounts.balance, allAccounts.currency),
        tone: "neutral",
      });
    }
    return metrics;
  }, [summary, settings, hasSingleAccount, allAccounts, t]);

  const groupBalancesMap = useMemo(() => {
    const map = new Map<string, { currency: string; balance: number }[]>();
    for (const group of groups) {
      const byCurrency = new Map<string, number>();
      for (const tx of group.transactions) {
        if (tx.parentTransactionId && tx.type === TransactionType.Loan) continue;
        const amount = Number(tx.amount);
        const delta = tx.type === TransactionType.Income || tx.type === TransactionType.Collection ? amount : -amount;
        byCurrency.set(tx.currency, (byCurrency.get(tx.currency) ?? 0) + delta);
      }
      map.set(group.label, Array.from(byCurrency.entries())
        .map(([currency, balance]) => ({ currency, balance }))
        .filter((c) => c.balance !== 0)
        .sort((a, b) => Math.abs(b.balance) - Math.abs(a.balance)));
    }
    return map;
  }, [groups]);

  const handleEdit = useCallback((tx: TransactionResponse) => {
    setCopySource(null);
    setDialogTx(tx);
    setDialogOpen(true);
  }, [setCopySource, setDialogTx, setDialogOpen]);

  const handleCopy = useCallback((tx: TransactionResponse) => {
    setCopySource(tx);
    setDialogTx(null);
    setSelectedTemplate(null);
    setDialogFormType("income-expense");
    setDialogOpen(true);
  }, [setCopySource, setDialogTx, setSelectedTemplate, setDialogFormType, setDialogOpen]);

  const handleDelete = useCallback((tx: TransactionResponse) => {
    setDeletingTx(tx);
  }, [setDeletingTx]);

  const handleTemplateApply = useCallback((tmpl: components["schemas"]["RecurringTransactionResponse"]) => {
    setSelectedTemplate(tmpl);
    const templateToFormType: FormType = tmpl.type === TransactionType.Transfer ? "transfer" : tmpl.type === TransactionType.Loan ? "loan" : tmpl.type === TransactionType.Collection ? "collection" : "income-expense";
    setDialogFormType(templateToFormType);
    setTemplateDialogOpen(false);
    setDialogOpen(true);
  }, [setSelectedTemplate, setDialogFormType, setTemplateDialogOpen, setDialogOpen]);

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold tracking-tight md:text-3xl">
            {allAccounts ? allAccounts.name : t("transactions.title")}
          </h1>
          <p className="text-muted-foreground">
            {allAccounts
              ? t("transactions.description")
              : t("transactions.description")}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2 shrink-0">
          <Button variant="outline" className="h-10" onClick={() => setTemplateDialogOpen(true)}>
            <FileText className="size-4 mr-1" />
            {t("templates.fromTemplate")}
          </Button>
          <TransactionTypeDropdown
            onSelect={(type) => {
              setDialogTx(null);
              setCopySource(null);
              setDialogFormType(type);
              setDialogOpen(true);
            }}
          />
        </div>
      </div>

      <MetricStrip metrics={stripMetrics} isLoading={summaryLoading} />

      <TransactionFilters />

      {isLoading ? (
        <PageSkeleton header={false} cards={0} rows={8} />
      ) : !transactions.length ? (
        <TransactionEmptyState />
      ) : (
        <div className="rounded-xl border bg-card overflow-hidden">
          <div className="divide-y divide-border/30">
            {groups.map((group) => {
              const groupDate = group.transactions[0]?.date
                ? parseDbDate(group.transactions[0].date)
                : new Date();
              const groupLabel = getGroupLabel(groupDate, view, t, dateLocale);

              const mainCurrency = settings?.mainCurrency || "USD";
              const groupBalances = groupBalancesMap.get(group.label) ?? [];

              return (
                <div key={group.label}>
                  <div className="sticky top-0 z-10 flex flex-wrap items-center justify-between gap-x-2 gap-y-0.5 border-b border-border/30 bg-muted/50 px-4 py-1.5 backdrop-blur-sm">
                    <span className="min-w-0 text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                      {groupLabel}
                    </span>
                    <span className="flex min-w-0 flex-wrap items-center gap-2 text-xs font-semibold tabular-nums">
                      {groupBalances.map((cb) => (
                        <span
                          key={cb.currency}
                          className={
                            cb.balance >= 0
                              ? "text-emerald-400"
                              : "text-red-400"
                          }
                        >
                          {cb.balance >= 0 ? "+" : ""}
                          {formatCurrency(cb.balance, cb.currency)}
                        </span>
                      ))}
                    </span>
                  </div>
                  <div className="divide-y divide-border/10">
                    {group.transactions.map((tx) => (
                      <TransactionRow
                        key={tx.id}
                        transaction={tx}
                        showRunningBalance={hasSingleAccount}
                        onEdit={handleEdit}
                        mainCurrency={mainCurrency}
                        categoryColor={tx.categoryId ? categoryColorMap.get(tx.categoryId) : undefined}
                      />
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {showPagination && (
        <div className="flex items-center justify-center gap-2 pt-4">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            <ChevronLeft className="size-4" />
          </Button>
          <span className="text-sm text-muted-foreground min-w-20 text-center">
            {page} / {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            <ChevronRight className="size-4" />
          </Button>
        </div>
      )}

      <TransactionFormDialog
        open={dialogOpen}
        onOpenChange={(v) => {
          setDialogOpen(v);
          if (!v) { setSelectedTemplate(null); setCopySource(null); }
        }}
        transaction={dialogTx}
        copySource={copySource}
        defaultAccountId={hasSingleAccount ? accounts : undefined}
        formType={dialogTx ? undefined : dialogFormType}
        onDelete={handleDelete}
        onCopy={handleCopy}
        template={dialogTx ? undefined : selectedTemplate}
      />

      <DeleteTransactionDialog
        open={!!deletingTx}
        onOpenChange={(v) => {
          if (!v) setDeletingTx(null);
        }}
        transactionId={deletingTx?.id ?? ""}
        isPayment={deletingTx?.type === TransactionType.Collection}
      />

      <ApplyFromTemplateDialog
        open={templateDialogOpen}
        onClose={() => setTemplateDialogOpen(false)}
        onApply={handleTemplateApply}
      />
    </>
  );
}
