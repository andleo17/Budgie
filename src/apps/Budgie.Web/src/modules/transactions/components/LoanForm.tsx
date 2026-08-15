import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useForm, Controller, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { loanSchema, type LoanFormValues } from "../schemas";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { DateTimePicker } from "@/components/ui/date-time-picker";
import { formatLocal } from "@/lib/date-locale";
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
  FieldContent,
} from "@/components/ui/field";
import { Loader2, AlertTriangle, Trash2 } from "lucide-react";
import { formatCurrency } from "@/lib/format";
import { getApiError } from "@/lib/format";
import { usePersonDebts } from "@/lib/hooks/usePersonDebts";

type TransactionResponse = components["schemas"]["TransactionResponse"];

interface LoanFormProps {
  transaction?: TransactionResponse | null;
  defaultAccountId?: string;
  onSuccess: () => void;
  onCancel: () => void;
  onDelete?: (tx: TransactionResponse) => void;
}

export function LoanForm({ transaction, defaultAccountId, onSuccess, onCancel, onDelete }: LoanFormProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const isEdit = !!transaction;
  const participant = transaction?.participants?.[0];
  const [selectedPersonId, setSelectedPersonId] = useState(() => participant?.personId ?? "");
  const { data: debts } = usePersonDebts(selectedPersonId);
  const totalDebt = debts?.reduce((s, d) => s + Number(d.remainingAmount), 0) ?? 0;
  const [debtWarningAcknowledged, setDebtWarningAcknowledged] = useState(() => !!participant);

  const { data: settings } = useQuery({
    queryKey: ["settings"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/users/settings");
      if (error) throw error;
      return data;
    },
    select: (d) => ({
      mainCurrency: d?.mainCurrency ?? "USD",
      supportedCurrencies: d?.supportedCurrencies?.length ? d.supportedCurrencies : [d?.mainCurrency ?? "USD"],
    }),
  });

  const { data: accounts } = useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data;
    },
  });

  const { data: persons } = useQuery({
    queryKey: ["persons"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/persons");
      if (error) throw error;
      return data;
    },
  });

  const defaultValues = isEdit && transaction
    ? {
        personId: participant?.personId ?? "",
        accountId: transaction.accountId,
        amount: String(Math.abs(Number(transaction.amount))),
        currency: transaction.currency,
        exchangeRate: String(Number(transaction.exchangeRate)),
        convertedAmount: String(Math.abs(Number(transaction.amount)) * Number(transaction.exchangeRate)),
        date: transaction.date?.slice(0, 19) ?? formatLocal(new Date()),
        dueDate: "",
        notes: transaction.notes ?? "",
      }
    : {
        personId: "",
        accountId: defaultAccountId ?? "",
        amount: "",
        currency: settings?.mainCurrency ?? "USD",
        exchangeRate: "1",
        convertedAmount: "",
        date: formatLocal(new Date()),
        dueDate: "",
        notes: "",
      };

  const form = useForm<LoanFormValues>({
    resolver: zodResolver(loanSchema),
    defaultValues,
  });

  useEffect(() => {
    if (!isEdit && settings?.mainCurrency && accounts && !defaultAccountId) {
      form.setValue("currency", settings.mainCurrency);
    }
  }, [isEdit, settings, accounts, defaultAccountId, form]);

  const createMutation = useMutation({
    mutationFn: async (body: components["schemas"]["CreateTransactionCommand"]) => {
      const { data, error } = await client.POST("/api/finances/transactions", { body });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["transactionSummary"] });
      queryClient.invalidateQueries({ queryKey: ["dashboardSummary"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["personDebts"] });
      queryClient.invalidateQueries({ queryKey: ["personSummary"] });
      onSuccess();
    },
    onError: (err) => setFormError(getApiError(err)),
  });

  const remainingAmount = participant ? Number(participant.remainingAmount) : 0;
  const contributedAmount = participant ? Number(participant.contributedAmount) : 0;
  const isFullyPaid = isEdit && remainingAmount <= 0;
  const isPartiallyPaid = isEdit && remainingAmount > 0 && remainingAmount < contributedAmount;
  const personName = isEdit && participant
    ? (persons?.find((p) => p.id === participant.personId)?.name ?? participant.personId.slice(0, 8))
    : "";

  const isPending = createMutation.isPending;
  const [formError, setFormError] = useState<string | null>(null);
  const watchedCurrency = useWatch({ control: form.control, name: "currency" });
  const mainCurrency = settings?.mainCurrency ?? "USD";
  const showExchangeRate = watchedCurrency && watchedCurrency !== mainCurrency;

  const handleSubmit = (values: LoanFormValues) => {
    if (!isEdit && totalDebt > 0 && !debtWarningAcknowledged) return;
    if (isEdit) { onCancel(); return; }

    const exchangeRate = parseFloat(values.exchangeRate ?? "1") || 1;
    const amount = parseFloat(values.amount);

    createMutation.mutate({
      accountId: values.accountId,
      categoryId: null,
      amount,
      currency: values.currency,
      exchangeRate,
      date: values.date,
      notes: values.notes || null,
      type: 3 as components["schemas"]["TransactionType"],
      installments: 1,
      participants: [{
        personId: values.personId,
        contributedAmount: amount,
        remainingAmount: amount,
        isTrackingOnly: false,
        destinationAccountId: null,
      }],
      isInitialBalance: false,
    });
  };

  return (
    <>
      {selectedPersonId && totalDebt > 0 && !debtWarningAcknowledged && !isEdit && (
        <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 mb-4">
          <div className="flex items-start gap-2">
            <AlertTriangle className="size-4 text-amber-400 mt-0.5 shrink-0" />
            <div>
              <p className="text-sm text-amber-400">
                {t("transactions.debtWarningPerson", {
                  person: persons?.find((p) => p.id === selectedPersonId)?.name ?? "",
                  amount: totalDebt.toFixed(2),
                  currency: watchedCurrency,
                })}
              </p>
              <div className="flex gap-2 mt-2">
                <Button type="button" size="sm" variant="outline" onClick={() => setSelectedPersonId("")}>
                  {t("transactions.cancel")}
                </Button>
                <Button type="button" size="sm" onClick={() => setDebtWarningAcknowledged(true)}>
                  {t("transactions.continue")}
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}

      {isEdit && (
        <div className={`rounded-lg border p-3 mb-2 text-sm space-y-1 ${
          isFullyPaid ? "border-emerald-500/30 bg-emerald-500/10" : "border-amber-500/30 bg-amber-500/10"
        }`}>
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground text-xs">{t("transactions.person")}</span>
            <span className="font-medium">{personName}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground text-xs">{t("transactions.amount")}</span>
            <span className="font-medium">{formatCurrency(contributedAmount, transaction?.currency ?? "USD")}</span>
          </div>
          <div className="flex items-center justify-between pt-1 border-t border-border/30 mt-1">
            <span className="text-muted-foreground text-xs">{t("transactions.debtStatus")}</span>
            <span className={`font-semibold flex items-center gap-1 ${
              isFullyPaid ? "text-emerald-400" : isPartiallyPaid ? "text-amber-400" : "text-red-400"
            }`}>
              <span className={`size-2 rounded-full ${
                isFullyPaid ? "bg-emerald-400" : isPartiallyPaid ? "bg-amber-400" : "bg-red-400"
              }`} />
              {isFullyPaid
                ? t("transactions.debtPaid")
                : isPartiallyPaid
                  ? t("transactions.debtPartial", { amount: formatCurrency(remainingAmount, transaction?.currency ?? "USD") })
                  : t("transactions.debtUnpaid", { amount: formatCurrency(remainingAmount, transaction?.currency ?? "USD") })
              }
            </span>
          </div>
        </div>
      )}

      <form onSubmit={form.handleSubmit(handleSubmit)} className="grid gap-4 py-4">
        {formError && (
          <p className="text-sm text-red-400 rounded-lg border border-red-400/30 bg-red-400/10 px-3 py-2">{formError}</p>
        )}
        <Field>
          <FieldLabel>{t("transactions.person")} {!isEdit && <span className="text-destructive">*</span>}</FieldLabel>
          <FieldContent>
            {isEdit ? (
              <div className="flex h-10 items-center rounded-lg border bg-muted/30 px-3 text-sm font-medium text-muted-foreground">
                {personName}
              </div>
            ) : (
              <Controller
                name="personId"
                control={form.control}
                render={({ field }) => {
                  const sorted = persons ?? [];
                  const selected = sorted.find((p) => p.id === field.value) ?? null;
                  return (
                    <Combobox
                      items={sorted}
                      itemToStringValue={(p) => p.name}
                      itemToStringLabel={(p) => p.name}
                      value={selected}
                      onValueChange={(p) => {
                        if (!p) return;
                        field.onChange(p.id);
                        setSelectedPersonId(p.id);
                        setDebtWarningAcknowledged(false);
                      }}
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
                  );
                }}
              />
            )}
          </FieldContent>
        </Field>

        <Field>
          <FieldLabel>{t("transactions.account")} {!isEdit && <span className="text-destructive">*</span>}</FieldLabel>
          <FieldContent>
            {isEdit ? (
              <div className="flex h-10 items-center rounded-lg border bg-muted/30 px-3 text-sm font-medium text-muted-foreground">
                {accounts?.find((a) => a.id === transaction?.accountId)?.name ?? transaction?.accountId.slice(0, 8)}
              </div>
            ) : (
              <Controller
                name="accountId"
                control={form.control}
                render={({ field }) => {
                  const sorted = accounts ?? [];
                  const selected = sorted.find((a) => a.id === field.value) ?? null;
                  return (
                    <Combobox
                      items={sorted}
                      itemToStringValue={(a) => a.name}
                      itemToStringLabel={(a) => a.name}
                      value={selected}
                      onValueChange={(a) => field.onChange(a?.id ?? "")}
                    >
                      <ComboboxInput placeholder={t("transactions.selectAccount")} />
                      <ComboboxContent>
                        <ComboboxEmpty>{t("transactions.noResults")}</ComboboxEmpty>
                        <ComboboxList>
                          {(a) => (
                            <ComboboxItem key={a.id} value={a}>
                              {a.name}
                            </ComboboxItem>
                          )}
                        </ComboboxList>
                      </ComboboxContent>
                    </Combobox>
                  );
                }}
              />
            )}
          </FieldContent>
        </Field>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field>
            <FieldLabel>{t("transactions.amount")} {!isEdit && <span className="text-destructive">*</span>}</FieldLabel>
            <FieldContent>
              <Controller name="amount" control={form.control} render={({ field }) => (
                <Input type="number" step="0.01" min="0" placeholder="0.00" value={field.value ?? ""} onChange={(e) => field.onChange(e.target.value)} disabled={isEdit} />
              )} />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel>{t("transactions.currency")}</FieldLabel>
            <FieldContent>
              <Controller name="currency" control={form.control} render={({ field }) => (
                <div className="flex h-10 items-center rounded-lg border bg-muted/30 px-3 text-sm font-medium text-muted-foreground">
                  {field.value || "USD"}
                </div>
              )} />
            </FieldContent>
          </Field>
        </div>

        {showExchangeRate && !isEdit && (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 rounded-lg border bg-muted/30 p-3">
            <Field>
              <FieldLabel>{t("transactions.exchangeRate")}</FieldLabel>
              <FieldContent>
                <Controller name="exchangeRate" control={form.control} render={({ field }) => (
                  <Input type="number" step="0.0001" min="0" placeholder="1.0000" value={field.value ?? "1"} onChange={(e) => field.onChange(e.target.value)} />
                )} />
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel>{t("transactions.convertedAmount", { currency: mainCurrency })}</FieldLabel>
              <FieldContent>
                <Controller name="convertedAmount" control={form.control} render={({ field }) => (
                  <Input type="number" step="0.01" min="0" placeholder="0.00" value={field.value ?? ""} onChange={(e) => field.onChange(e.target.value)} />
                )} />
              </FieldContent>
            </Field>
          </div>
        )}

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field>
            <FieldLabel>{t("transactions.date")}</FieldLabel>
            <FieldContent>
              <Controller name="date" control={form.control} render={({ field }) => (
                <DateTimePicker value={field.value} onChange={field.onChange} disabled={isEdit} />
              )} />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel>{t("transactions.dueDate")}</FieldLabel>
            <FieldContent>
              <Controller name="dueDate" control={form.control} render={({ field }) => (
                <DateTimePicker value={field.value ?? ""} onChange={(v) => field.onChange(v.slice(0, 10))} disabled={isEdit} />
              )} />
            </FieldContent>
          </Field>
        </div>

        <Field>
          <FieldLabel>{t("transactions.notes")}</FieldLabel>
          <FieldContent>
            <Controller name="notes" control={form.control} render={({ field }) => (
              <Input value={field.value ?? ""} onChange={field.onChange} placeholder={t("transactions.notesPlaceholder")} disabled={isEdit} />
            )} />
          </FieldContent>
        </Field>

        <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
          <div>
            {isEdit && onDelete && transaction && (
              <Button type="button" variant="outline" className="text-red-400 border-red-400/30 hover:bg-red-400/10 hover:text-red-300" onClick={() => { onDelete(transaction); onCancel(); }}>
                <Trash2 className="size-3.5 mr-1.5" />
                {t("accounts.delete")}
              </Button>
            )}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" onClick={onCancel}>{t("transactions.cancel")}</Button>
            {!isEdit && (
              <Button type="submit" disabled={isPending}>
                {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {t("transactions.save")}
              </Button>
            )}
          </div>
        </div>
      </form>
    </>
  );
}
