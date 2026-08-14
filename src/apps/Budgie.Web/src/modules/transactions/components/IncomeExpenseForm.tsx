import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useForm, Controller, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { CurrencySelector } from "@/components/currency-selector";
import { enrichCurrencies } from "@/lib/currencies";
import { incomeExpenseSchema, type IncomeExpenseFormValues, type TransactionParticipantForm } from "../schemas";
import { getApiError } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { DateTimePicker } from "@/components/ui/date-time-picker";
import { formatLocal } from "@/lib/date-locale";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
  FieldError,
} from "@/components/ui/field";
import { CategoryCombobox } from "@/modules/categories/components/CategoryCombobox";
import { ParticipantsSection } from "./ParticipantsSection";
import { Loader2, Trash2, Percent } from "lucide-react";

type TransactionResponse = components["schemas"]["TransactionResponse"];
type RecurringTransactionResponse = components["schemas"]["RecurringTransactionResponse"];

interface IncomeExpenseFormProps {
  transaction?: TransactionResponse | null;
  template?: RecurringTransactionResponse | null;
  defaultAccountId?: string;
  onSuccess: () => void;
  onCancel: () => void;
  onDelete?: (tx: TransactionResponse) => void;
}

export function IncomeExpenseForm({ transaction, template, defaultAccountId, onSuccess, onCancel, onDelete }: IncomeExpenseFormProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const isEdit = !!transaction;
  const [participants, setParticipants] = useState<TransactionParticipantForm[]>([]);

  useEffect(() => {
    if (transaction) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- form initialization from edit data
      setParticipants(
        (transaction.participants ?? []).map((p) => ({
          personId: p.personId,
          personName: "",
          contributedAmount: String(p.contributedAmount),
          paymentCondition: p.isTrackingOnly ? "tag" : Number(p.remainingAmount) > 0 ? "credit" : "cash" as const,
          destinationAccountId: p.destinationAccountId ?? undefined,
        }))
      );
    } else {
      setParticipants([]);
    }
  }, [transaction]);

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

  const form = useForm<IncomeExpenseFormValues>({
    resolver: zodResolver(incomeExpenseSchema),
    defaultValues: {
      type: "1",
      date: formatLocal(new Date()),
      accountId: "",
      amount: "",
      currency: "",
      exchangeRate: "1",
      convertedAmount: "",
      categoryId: "",
      notes: "",
      installments: "1",
      isInterestFree: true,
    },
  });

  useEffect(() => {
    if (transaction) {
      const rate = Number(transaction.exchangeRate);
      const amount = Math.abs(Number(transaction.amount));
      form.reset({
        type: String(transaction.type),
        date: transaction.date?.slice(0, 19) ?? formatLocal(new Date()),
        accountId: transaction.accountId,
        amount: String(amount),
        currency: transaction.currency,
        exchangeRate: String(rate),
        convertedAmount: String(amount * rate),
        categoryId: transaction.categoryId ?? "",
        notes: transaction.notes ?? "",
        installments: String(transaction.installments ?? 1),
        isInterestFree: false,
      });
    } else if (template) {
      const amount = Math.abs(Number(template.amount));
      const rate = Number(template.exchangeRate) || 1;
      form.reset({
        type: String(template.type),
        date: formatLocal(new Date()),
        accountId: defaultAccountId ?? template.accountId,
        amount: String(amount),
        currency: template.currency,
        exchangeRate: String(rate),
        convertedAmount: String(amount * rate),
        categoryId: template.categoryId ?? "",
        notes: template.notes ?? "",
        installments: String(template.installments ?? 1),
        isInterestFree: template.isInterestFree ?? true,
      });
      // eslint-disable-next-line react-hooks/set-state-in-effect -- form initialization from template
      setParticipants(
        (template.participants ?? []).map((p) => ({
          personId: p.personId,
          personName: "",
          contributedAmount: String(p.contributedAmount),
          paymentCondition: p.isTrackingOnly ? "tag" : Number(p.remainingAmount) > 0 ? "credit" : "cash" as const,
          destinationAccountId: p.destinationAccountId ?? undefined,
        }))
      );
    } else if (settings?.mainCurrency) {
      const defaultAccount = accounts?.find((a) => a.id === defaultAccountId);
      form.reset({
        type: "1",
        date: formatLocal(new Date()),
        accountId: defaultAccount?.id ?? "",
        amount: "",
        currency: defaultAccount?.currency ?? settings.mainCurrency,
        exchangeRate: "1",
        convertedAmount: "",
        categoryId: "",
        notes: "",
        installments: "1",
        isInterestFree: true,
      });
    }
  }, [transaction, template, settings, form, accounts, defaultAccountId]);

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
      queryClient.invalidateQueries({ queryKey: ["personSummary"] });
      onSuccess();
    },
    onError: (err) => setFormError(getApiError(err)),
  });

  const updateMutation = useMutation({
    mutationFn: async (params: { id: string; body: components["schemas"]["UpdateTransactionRequest"] }) => {
      const { error } = await client.PUT("/api/finances/transactions/{id}", {
        params: { path: { id: params.id } },
        body: params.body,
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["transactionSummary"] });
      queryClient.invalidateQueries({ queryKey: ["dashboardSummary"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["personSummary"] });
      onSuccess();
    },
    onError: (err) => setFormError(getApiError(err)),
  });

  const isPending = createMutation.isPending || updateMutation.isPending;
  const [confirmDebtWarning, setConfirmDebtWarning] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [showInstallments, setShowInstallments] = useState(false);

  const watchedCurrency = useWatch({ control: form.control, name: "currency" });
  const mainCurrency = settings?.mainCurrency ?? "USD";
  const showExchangeRate = watchedCurrency && watchedCurrency !== mainCurrency;

  const participantsPayload = useMemo(() =>
    participants
      .filter((p) => p.personId && (parseFloat(p.contributedAmount) > 0 || p.paymentCondition === "tag"))
      .map((p) => ({
        personId: p.personId,
        contributedAmount: parseFloat(p.contributedAmount),
        remainingAmount: p.paymentCondition === "credit" ? parseFloat(p.contributedAmount) : 0,
        isTrackingOnly: p.paymentCondition === "tag",
        destinationAccountId: p.paymentCondition === "cash" ? (p.destinationAccountId || null) : null,
      })),
    [participants]
  );

  const handleSubmit = async (values: IncomeExpenseFormValues) => {
    for (const p of participants) {
      if (p.paymentCondition !== "credit") continue;
      const amount = parseFloat(p.contributedAmount);
      if (amount <= 0) continue;
      const { data: debts } = await client.GET("/api/finances/persons/{personId}/debts", {
        params: { path: { personId: p.personId } },
      });
      if (debts && debts.length > 0) {
        setConfirmDebtWarning(p.personId);
        return;
      }
    }
    submitForm(values);
  };

  const submitForm = (values: IncomeExpenseFormValues) => {
    const exchangeRate = parseFloat(values.exchangeRate ?? "1") || 1;
    const amount = parseFloat(values.amount);
    const type = parseInt(values.type, 10) as components["schemas"]["TransactionType"];
    const installments = parseInt(values.installments ?? "1", 10) || 1;
    const isInterestFree = values.isInterestFree ?? true;

    if (isEdit && transaction) {
      updateMutation.mutate({
        id: transaction.id,
        body: {
          categoryId: values.categoryId || null,
          amount,
          currency: values.currency,
          exchangeRate,
          date: values.date,
          notes: values.notes || null,
          type,
          installments,
          participants: participantsPayload,
        },
      });
    } else {
      createMutation.mutate({
        accountId: values.accountId,
        categoryId: values.categoryId || null,
        amount,
        currency: values.currency,
        exchangeRate,
        date: values.date,
        notes: values.notes || null,
        type,
        installments,
        participants: participantsPayload,
        isInitialBalance: false,
        isInterestFree,
      });
    }
  };

  const transactionTypeItems = [
    { label: t("transactions.income"), value: "0" },
    { label: t("transactions.expense"), value: "1" },
  ];

  return (
    <>
      {confirmDebtWarning && (
        <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 mb-4">
          <p className="text-sm text-amber-400">
            {t("transactions.debtWarning", { person: participants.find(p => p.personId === confirmDebtWarning)?.personName ?? "" })}
          </p>
          <div className="flex gap-2 mt-2">
            <Button type="button" size="sm" variant="outline" onClick={() => setConfirmDebtWarning(null)}>
              {t("transactions.cancel")}
            </Button>
            <Button type="button" size="sm" onClick={() => { setConfirmDebtWarning(null); submitForm(form.getValues()); }}>
              {t("transactions.continue")}
            </Button>
          </div>
        </div>
      )}

      <form onSubmit={form.handleSubmit(handleSubmit)} className="grid gap-4 py-4">
        {formError && (
          <p className="text-sm text-red-400 rounded-lg border border-red-400/30 bg-red-400/10 px-3 py-2">{formError}</p>
        )}
        <div className="grid grid-cols-2 gap-3">
          <Field>
            <FieldLabel>{t("transactions.type")}</FieldLabel>
            <FieldContent>
              <Controller
                name="type"
                control={form.control}
                render={({ field }) => (
                  <Select items={transactionTypeItems} value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {transactionTypeItems.map((item) => (
                        <SelectItem key={item.value} value={item.value}>
                          {item.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel>{t("transactions.date")}</FieldLabel>
            <FieldContent>
              <Controller
                name="date"
                control={form.control}
                render={({ field }) => (
                  <DateTimePicker value={field.value} onChange={field.onChange} disabled={isEdit && !!transaction?.isInitialBalance} />
                )}
              />
              <FieldError errors={form.formState.errors.date ? [{ message: form.formState.errors.date.message }] : []} />
            </FieldContent>
          </Field>
        </div>

        <Field>
          <FieldLabel>{t("transactions.account")} <span className="text-destructive">*</span></FieldLabel>
          <FieldContent>
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
          </FieldContent>
        </Field>

        <div className="grid grid-cols-2 gap-3">
          <Field>
            <FieldLabel>{t("transactions.amount")} <span className="text-destructive">*</span></FieldLabel>
            <FieldContent>
              <Controller
                name="amount"
                control={form.control}
                render={({ field }) => (
                  <Input type="number" step="0.01" min="0" placeholder={t("transactions.amountPlaceholder")} value={field.value ?? ""} onChange={(e) => field.onChange(e.target.value)} />
                )}
              />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel>{t("transactions.currency")}</FieldLabel>
            <FieldContent>
              <Controller
                name="currency"
                control={form.control}
                render={({ field, fieldState }) => (
                  <CurrencySelector value={field.value} onValueChange={field.onChange} invalid={fieldState.invalid} currencies={enrichCurrencies(settings?.supportedCurrencies ?? ["USD"], i18n.language)} />
                )}
              />
            </FieldContent>
          </Field>
        </div>

        {showExchangeRate && (
          <div className="grid grid-cols-2 gap-3 rounded-lg border bg-muted/30 p-3">
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

        <Field>
          <FieldLabel>{t("transactions.category")}</FieldLabel>
          <FieldContent>
            <Controller name="categoryId" control={form.control} render={({ field }) => (
              <CategoryCombobox value={field.value} onValueChange={(id) => field.onChange(id ?? "")} placeholder={t("transactions.uncategorized")} showNoneOption />
            )} />
          </FieldContent>
        </Field>

        <Field>
          <FieldLabel>{t("transactions.notes")}</FieldLabel>
          <FieldContent>
            <Controller name="notes" control={form.control} render={({ field }) => (
              <Input value={field.value ?? ""} onChange={field.onChange} placeholder={t("transactions.notesPlaceholder")} />
            )} />
          </FieldContent>
        </Field>

        {!isEdit && (
          <>
            <div className="flex items-center gap-2">
              <Button type="button" variant="outline" size="sm" onClick={() => setShowInstallments(!showInstallments)} className={showInstallments ? "border-blue-400/40 text-blue-400" : ""}>
                <Percent className="size-3.5 mr-1" />
                {t("transactions.inInstallments")}
              </Button>
            </div>
            {showInstallments && (
              <div className="grid grid-cols-2 gap-3 rounded-lg border bg-muted/30 p-3">
                <Field>
                  <FieldLabel>{t("transactions.installments")}</FieldLabel>
                  <FieldContent>
                    <Controller name="installments" control={form.control} render={({ field }) => (
                      <Input type="number" min="1" step="1" value={field.value ?? "1"} onChange={(e) => field.onChange(e.target.value)} />
                    )} />
                  </FieldContent>
                </Field>
                <div className="flex items-end pb-2">
                  <Controller name="isInterestFree" control={form.control} render={({ field }) => (
                    <div className="flex items-center gap-2">
                      <Checkbox
                        id="tx-interest-free"
                        checked={field.value ?? true}
                        onCheckedChange={(v) => field.onChange(v === true)}
                      />
                      <Label htmlFor="tx-interest-free" className="text-sm font-normal cursor-pointer">
                        {t("transactions.interestFree")}
                      </Label>
                    </div>
                  )} />
                </div>
              </div>
            )}
          </>
        )}

        <ParticipantsSection
          participants={participants}
          onChange={setParticipants}
          accounts={accounts ?? []}
          currency={watchedCurrency || mainCurrency}
          defaultAccountId={useWatch({ control: form.control, name: "accountId" })}
          transactionType={useWatch({ control: form.control, name: "type" })}
        />

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
            <Button type="submit" disabled={isPending}>
              {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t("transactions.save")}
            </Button>
          </div>
        </div>
      </form>
    </>
  );
}
