import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatCurrency } from "@/lib/format";
import { formatLocal, parseDbDate, getDateLocale } from "@/lib/date-locale";
import { format } from "date-fns";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from "@/components/ui/combobox";
import {
  Field,
  FieldLabel,
} from "@/components/ui/field";
import { Loader2, ArrowRight } from "lucide-react";
import { usePersonDebts } from "@/lib/hooks/usePersonDebts";

interface CollectionPaymentFormProps {
  defaultAccountId?: string;
  onSuccess: () => void;
  onCancel: () => void;
}

export function CollectionPaymentForm({ defaultAccountId, onSuccess, onCancel }: CollectionPaymentFormProps) {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  const queryClient = useQueryClient();
  const [selectedPersonId, setSelectedPersonId] = useState("");
  const [selectedDebtIds, setSelectedDebtIds] = useState<Set<string>>(new Set());
  const [paymentAmount, setPaymentAmount] = useState("");
  const [accountId, setAccountId] = useState(defaultAccountId ?? "");
  const [exchangeRate, setExchangeRate] = useState("1");
  const convertedAmount = useMemo(() => {
    const rate = parseFloat(exchangeRate) || 1;
    const amount = parseFloat(paymentAmount) || 0;
    if (amount === 0) return "";
    return String(amount * rate);
  }, [paymentAmount, exchangeRate]);
  const [distribution, setDistribution] = useState<Map<string, number>>(new Map());

  const { data: persons } = useQuery({
    queryKey: ["persons"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/persons");
      if (error) throw error;
      return data;
    },
  });

  const { data: accounts } = useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data;
    },
  });

  const { data: debts, isLoading: debtsLoading } = usePersonDebts(selectedPersonId);

  const selectedAccount = accounts?.find((a) => a.id === accountId);
  const accountCurrency = selectedAccount?.currency ?? "USD";

  const handlePersonChange = (personId: string) => {
    setSelectedPersonId(personId);
    setSelectedDebtIds(new Set());
    setPaymentAmount("");
    setExchangeRate("1");
    setDistribution(new Map());
  };

  const debtsByCurrency = useMemo(() => {
    const groups = new Map<string, components["schemas"]["DebtResponse"][]>();
    for (const d of debts ?? []) {
      const c = d.currency || "USD";
      if (!groups.has(c)) groups.set(c, []);
      groups.get(c)!.push(d);
    }
    return groups;
  }, [debts]);

  const selectedDebts = useMemo(() =>
    (debts ?? []).filter((d) => selectedDebtIds.has(d.participantId)),
    [debts, selectedDebtIds]
  );

  const totalSelectedDebt = useMemo(() =>
    selectedDebts.reduce((s, d) => s + Number(d.remainingAmount), 0),
    [selectedDebts]
  );

  const totalAllDebt = useMemo(() =>
    (debts ?? []).reduce((s, d) => s + Number(d.remainingAmount), 0),
    [debts]
  );

  const totalAllByCurrency = useMemo(() => {
    const byCurr = new Map<string, number>();
    for (const d of debts ?? []) {
      const c = d.currency || "USD";
      byCurr.set(c, (byCurr.get(c) ?? 0) + Number(d.remainingAmount));
    }
    return byCurr;
  }, [debts]);

  const totalSelectedByCurrency = useMemo(() => {
    const byCurr = new Map<string, number>();
    for (const d of selectedDebts) {
      const c = d.currency || "USD";
      byCurr.set(c, (byCurr.get(c) ?? 0) + Number(d.remainingAmount));
    }
    return byCurr;
  }, [selectedDebts]);

  const totalDistributed = useMemo(() =>
    Array.from(distribution.values()).reduce((s, v) => s + v, 0),
    [distribution]
  );

  const remainingAfterPayment = useMemo(() =>
    Math.max(0, totalSelectedDebt - totalDistributed),
    [totalSelectedDebt, totalDistributed]
  );

  const globalAfterPayment = useMemo(() =>
    Math.max(0, totalAllDebt - totalDistributed),
    [totalAllDebt, totalDistributed]
  );

  const showExchangeRate = selectedAccount && debtsByCurrency.size > 0 &&
    (debtsByCurrency.size > 1 || !debtsByCurrency.has(accountCurrency));

  const debtCurrency = selectedDebts[0]?.currency ?? "USD";

  const repayMutation = useMutation({
    mutationFn: async (body: components["schemas"]["BulkRepayCommand"]) => {
      const { data, error } = await client.POST("/api/finances/transactions/bulk-repay", { body });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["transactionSummary"] });
      queryClient.invalidateQueries({ queryKey: ["dashboardSummary"] });
      queryClient.invalidateQueries({ queryKey: ["personDebts"] });
      queryClient.invalidateQueries({ queryKey: ["personSummary"] });
      onSuccess();
    },
  });

  const isPending = repayMutation.isPending;

  const handleDistribute = () => {
    const amount = parseFloat(paymentAmount) || 0;
    if (amount <= 0 || !debts || debts.length === 0) return;

    const sorted = [...debts].sort(
      (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime()
    );

    let remaining = amount;
    const newDistribution = new Map<string, number>();
    const newSelected = new Set<string>();

    for (const debt of sorted) {
      if (remaining <= 0) break;
      const debtRemaining = Number(debt.remainingAmount);
      const toPay = Math.min(debtRemaining, remaining);
      newDistribution.set(debt.participantId, toPay);
      newSelected.add(debt.participantId);
      remaining -= toPay;
    }

    setDistribution(newDistribution);
    setSelectedDebtIds(newSelected);
  };

  const handleSubmit = () => {
    const distAmount = Array.from(distribution.values()).reduce((s, v) => s + v, 0);
    if (distAmount <= 0 || !accountId) return;

    const debtPayments: { participantId: string; amount: number; transactionId: string }[] = [];
    for (const [participantId, amount] of distribution.entries()) {
      if (amount <= 0) continue;
      const debt = debts?.find((d) => d.participantId === participantId);
      if (debt) {
        debtPayments.push({ participantId, amount, transactionId: debt.transactionId });
      }
    }

    repayMutation.mutate({
      accountId,
      amount: distAmount,
      currency: accountCurrency,
      exchangeRate: parseFloat(exchangeRate) || 1,
      date: formatLocal(new Date()),
      notes: null,
      debts: debtPayments,
    });
  };

  const toggleDebt = (participantId: string) => {
    const next = new Set(selectedDebtIds);
    if (next.has(participantId)) next.delete(participantId);
    else next.add(participantId);
    setSelectedDebtIds(next);
    // Recalculate distribution when toggling
    const amount = parseFloat(paymentAmount) || 0;
    if (amount > 0) {
      recalculateDistribution(amount, next, debts ?? []);
    } else {
      setDistribution(new Map());
    }
  };

  const recalculateDistribution = (amount: number, selected: Set<string>, allDebts: components["schemas"]["DebtResponse"][]) => {
    const sorted = allDebts
      .filter((d) => selected.has(d.participantId))
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

    let remaining = amount;
    const newDistribution = new Map<string, number>();
    for (const debt of sorted) {
      if (remaining <= 0) break;
      const debtRemaining = Number(debt.remainingAmount);
      const toPay = Math.min(debtRemaining, remaining);
      newDistribution.set(debt.participantId, toPay);
      remaining -= toPay;
    }
    setDistribution(newDistribution);
  };

  const handlePaymentAmountChange = (value: string) => {
    setPaymentAmount(value);
    const amount = parseFloat(value) || 0;
    if (amount > 0 && selectedDebtIds.size > 0) {
      recalculateDistribution(amount, selectedDebtIds, debts ?? []);
    } else {
      setDistribution(new Map());
    }
  };

  const hasDebts = debts && debts.length > 0;

  return (
    <div className="grid gap-4 py-4">
      <Field>
        <FieldLabel>{t("transactions.person")} <span className="text-destructive">*</span></FieldLabel>
        <Combobox
          items={persons ?? []}
          itemToStringValue={(p) => p.name}
          itemToStringLabel={(p) => p.name}
          value={(persons ?? []).find((p) => p.id === selectedPersonId) ?? null}
          onValueChange={(p) => p && handlePersonChange(p.id)}
        >
          <ComboboxInput placeholder={t("transactions.selectPerson")} />
          <ComboboxContent>
            <ComboboxEmpty>{t("transactions.noResults")}</ComboboxEmpty>
            <ComboboxList>
              {(p) => (
                <ComboboxItem key={p.id} value={p}>
                  {p.name}
                </ComboboxItem>
              )}
            </ComboboxList>
          </ComboboxContent>
        </Combobox>
      </Field>

      {selectedPersonId && (
        <>
          {debtsLoading ? (
            <div className="flex justify-center py-4">
              <Loader2 className="size-5 animate-spin text-muted-foreground" />
            </div>
          ) : hasDebts ? (
            <>
              <Field>
                <FieldLabel>{t("transactions.account")} <span className="text-destructive">*</span></FieldLabel>
                <Combobox
                  items={accounts ?? []}
                  itemToStringValue={(a) => `${a.name} (${a.currency})`}
                  itemToStringLabel={(a) => `${a.name} (${a.currency})`}
                  value={selectedAccount ?? null}
                  onValueChange={(a) => a && setAccountId(a.id)}
                >
                  <ComboboxInput placeholder={t("transactions.selectAccount")} />
                  <ComboboxContent>
                    <ComboboxEmpty>{t("transactions.noResults")}</ComboboxEmpty>
                    <ComboboxList>
                      {(a) => (
                        <ComboboxItem key={a.id} value={a}>
                          {a.name} ({a.currency})
                        </ComboboxItem>
                      )}
                    </ComboboxList>
                  </ComboboxContent>
                </Combobox>
              </Field>

              <div className="text-xs text-muted-foreground space-y-0.5">
                {Array.from(totalAllByCurrency.entries()).map(([currency, amount]) => (
                  <div key={currency} className="flex justify-between">
                    <span>{t("transactions.totalDebt")} ({currency})</span>
                    <span className="font-medium">{formatCurrency(amount, currency)}</span>
                  </div>
                ))}
              </div>

              <div className="space-y-1 rounded-lg border max-h-60 overflow-y-auto">
                {Array.from(debtsByCurrency.entries()).map(([currency, currencyDebts]) => (
                  <div key={currency}>
                    <div className="sticky top-0 bg-muted/50 px-3 py-1 text-xs font-semibold text-muted-foreground backdrop-blur-sm">
                      {currency}
                    </div>
                    {currencyDebts.map((debt) => {
                      const isSelected = selectedDebtIds.has(debt.participantId);
                      const payAmount = distribution.get(debt.participantId) ?? 0;
                      const debtDate = parseDbDate(debt.date);
                      const remaining = Number(debt.remainingAmount);
                      return (
                        <div
                          key={debt.participantId}
                          className={`flex items-center gap-2 px-3 py-2.5 text-sm cursor-pointer transition-colors ${
                            isSelected ? "bg-muted/30" : "hover:bg-muted/10"
                          }`}
                          onClick={() => toggleDebt(debt.participantId)}
                        >
                          <Checkbox checked={isSelected} onCheckedChange={() => toggleDebt(debt.participantId)} />
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-xs text-muted-foreground shrink-0">
                                {format(debtDate, "MMM d, HH:mm", { locale: dateLocale })}
                              </span>
                              {debt.notes && (
                                <span className="text-xs text-muted-foreground truncate">
                                  {debt.notes}
                                </span>
                              )}
                            </div>
                          </div>
                          <div className="flex items-center gap-1 shrink-0">
                            {payAmount > 0 && payAmount < remaining && (
                              <span className="text-[11px] text-emerald-400 font-medium">
                                -{formatCurrency(payAmount, currency)}
                              </span>
                            )}
                            <span className={`font-medium tabular-nums ${payAmount >= remaining && payAmount > 0 ? "text-emerald-400 line-through" : ""}`}>
                              {formatCurrency(remaining, currency)}
                            </span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                ))}
              </div>

              {selectedDebts.length > 0 && (
                <div className="text-xs text-muted-foreground space-y-0.5">
                  {Array.from(totalSelectedByCurrency.entries()).map(([currency, amount]) => (
                    <div key={currency} className="flex justify-between">
                      <span>{t("transactions.selectedDebt")} ({currency})</span>
                      <span className="font-medium">{formatCurrency(amount, currency)}</span>
                    </div>
                  ))}
                </div>
              )}

              {showExchangeRate && (
                <div className="grid grid-cols-2 gap-3 rounded-lg border bg-muted/30 p-3">
                  <div>
                    <FieldLabel>{t("transactions.exchangeRate")} <small className="text-muted-foreground">({debtCurrency} → {accountCurrency})</small></FieldLabel>
                    <Input
                      type="number"
                      step="0.0001"
                      min="0"
                      value={exchangeRate}
                      onChange={(e) => setExchangeRate(e.target.value)}
                      placeholder="1.0000"
                    />
                  </div>
                  <div>
                    <FieldLabel>{t("transactions.convertedAmount", { currency: accountCurrency })}</FieldLabel>
                    <Input
                      type="number"
                      step="0.01"
                      min="0"
                      value={convertedAmount}
                      readOnly
                      placeholder="0.00"
                    />
                  </div>
                </div>
              )}

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">
                    {t("transactions.paymentAmount")} ({accountCurrency})
                  </label>
                  <Input
                    type="number"
                    step="0.01"
                    min="0"
                    value={paymentAmount}
                    onChange={(e) => handlePaymentAmountChange(e.target.value)}
                    placeholder="0.00"
                  />
                </div>
                <div className="flex items-end pb-px">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={handleDistribute}
                    disabled={!paymentAmount || parseFloat(paymentAmount) <= 0}
                    className="text-xs"
                  >
                    <ArrowRight className="size-3 mr-1" />
                    {t("transactions.distribute")}
                  </Button>
                </div>
              </div>

              <div className="rounded-lg border bg-muted/20 p-3 text-xs space-y-1.5">
                {Array.from(totalSelectedByCurrency.entries()).map(([currency, amount]) => (
                  <div key={currency} className="flex justify-between">
                    <span className="text-muted-foreground">{t("transactions.currentDebt")} ({currency})</span>
                    <span className="font-semibold">{formatCurrency(amount, currency)}</span>
                  </div>
                ))}
                <div className="flex justify-between">
                  <span className="text-muted-foreground">{t("transactions.afterPayment")}</span>
                  <span className="font-semibold text-emerald-400">{formatCurrency(remainingAfterPayment, accountCurrency)}</span>
                </div>
                <div className="flex justify-between border-t border-border/30 pt-1">
                  <span className="text-muted-foreground">{t("transactions.globalAfterPayment")}</span>
                  <span className="font-semibold text-muted-foreground">{formatCurrency(globalAfterPayment, accountCurrency)}</span>
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <Button type="button" variant="outline" onClick={onCancel}>{t("transactions.cancel")}</Button>
                <Button
                  type="button"
                  onClick={handleSubmit}
                  disabled={isPending || totalDistributed <= 0 || !accountId}
                >
                  {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  {t("transactions.registerPayment")}
                </Button>
              </div>
            </>
          ) : (
            <p className="text-sm text-muted-foreground py-4 text-center">
              {t("transactions.noDebts")}
            </p>
          )}
        </>
      )}
    </div>
  );
}
