import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useForm, Controller, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { transferSchema, type TransferFormValues } from "../schemas";
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
} from "@/components/ui/field";
import { Loader2 } from "lucide-react";

interface TransferFormProps {
  defaultAccountId?: string;
  onSuccess: () => void;
  onCancel: () => void;
}

export function TransferForm({ defaultAccountId, onSuccess, onCancel }: TransferFormProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const { data: accounts } = useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data;
    },
  });

  const form = useForm<TransferFormValues>({
    resolver: zodResolver(transferSchema),
    defaultValues: {
      sourceAccountId: "",
      destinationAccountId: "",
      amount: "",
      currency: "USD",
      exchangeRate: "1",
      convertedAmount: "",
      date: formatLocal(new Date()),
      notes: "",
    },
  });

  useEffect(() => {
    if (accounts) {
      const defaultAccount = accounts.find((a) => a.id === defaultAccountId);
      if (defaultAccount) {
        form.setValue("sourceAccountId", defaultAccount.id);
        form.setValue("currency", defaultAccount.currency);
      }
    }
  }, [accounts, defaultAccountId, form]);

  const createMutation = useMutation({
    mutationFn: async (body: components["schemas"]["TransferCommand"]) => {
      const { data, error } = await client.POST("/api/finances/transactions/transfer", { body });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["transactionSummary"] });
      queryClient.invalidateQueries({ queryKey: ["dashboardSummary"] });
      onSuccess();
    },
  });

  const isPending = createMutation.isPending;
  const sourceAccountId = useWatch({ control: form.control, name: "sourceAccountId" });
  const destinationAccountId = useWatch({ control: form.control, name: "destinationAccountId" });
  const sourceAccount = accounts?.find((a) => a.id === sourceAccountId);
  const destinationAccount = accounts?.find((a) => a.id === destinationAccountId);
  const showExchangeRate = sourceAccount && destinationAccount && sourceAccount.currency !== destinationAccount.currency;

  const handleSubmit = (values: TransferFormValues) => {
    const exchangeRate = parseFloat(values.exchangeRate ?? "1") || 1;
    const amount = parseFloat(values.amount);
    const useCurrency = sourceAccount?.currency ?? "USD";

    createMutation.mutate({
      fromAccountId: values.sourceAccountId,
      toAccountId: values.destinationAccountId,
      amount,
      fee: 0,
      currency: useCurrency,
      exchangeRate,
      date: values.date,
      notes: values.notes || null,
    });
  };

  useEffect(() => {
    if (sourceAccount) {
      form.setValue("currency", sourceAccount.currency);
    }
  }, [sourceAccount, form]);

  return (
    <form onSubmit={form.handleSubmit(handleSubmit)} className="grid gap-4 py-4">
      <Field>
        <FieldLabel>{t("transactions.sourceAccount")} <span className="text-destructive">*</span></FieldLabel>
        <Controller
          name="sourceAccountId"
          control={form.control}
          render={({ field }) => {
            const sorted = accounts ?? [];
            const selected = sorted.find((a) => a.id === field.value) ?? null;
            return (
              <Combobox
                items={sorted}
                itemToStringValue={(a) => `${a.name} (${a.currency})`}
                itemToStringLabel={(a) => `${a.name} (${a.currency})`}
                value={selected}
                onValueChange={(a) => field.onChange(a?.id ?? "")}
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
            );
          }}
        />
      </Field>

      <Field>
        <FieldLabel>{t("transactions.destinationAccount")} <span className="text-destructive">*</span></FieldLabel>
        <Controller
          name="destinationAccountId"
          control={form.control}
          render={({ field }) => {
            const sorted = accounts ?? [];
            const selected = sorted.find((a) => a.id === field.value) ?? null;
            return (
              <Combobox
                items={sorted}
                itemToStringValue={(a) => `${a.name} (${a.currency})`}
                itemToStringLabel={(a) => `${a.name} (${a.currency})`}
                value={selected}
                onValueChange={(a) => field.onChange(a?.id ?? "")}
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
            );
          }}
        />
      </Field>

      <div className="grid grid-cols-2 gap-3">
        <Field>
          <FieldLabel>{t("transactions.amount")} <span className="text-destructive">*</span></FieldLabel>
          <Controller name="amount" control={form.control} render={({ field }) => (
            <Input type="number" step="0.01" min="0" placeholder="0.00" value={field.value ?? ""} onChange={(e) => field.onChange(e.target.value)} />
          )} />
        </Field>
        <Field>
          <FieldLabel>{t("transactions.currency")}</FieldLabel>
          <div className="flex h-10 items-center rounded-lg border bg-muted/30 px-3 text-sm font-medium text-muted-foreground">
            {sourceAccount?.currency || form.watch("currency") || "USD"}
          </div>
        </Field>
      </div>

      {showExchangeRate && (
        <div className="grid grid-cols-2 gap-3 rounded-lg border bg-muted/30 p-3">
          <Field>
            <FieldLabel>{t("transactions.exchangeRate")} <small className="text-muted-foreground">({sourceAccount?.currency} → {destinationAccount?.currency})</small></FieldLabel>
            <Controller name="exchangeRate" control={form.control} render={({ field }) => (
              <Input type="number" step="0.0001" min="0" placeholder="1.0000" value={field.value ?? "1"} onChange={(e) => field.onChange(e.target.value)} />
            )} />
          </Field>
          <Field>
            <FieldLabel>{t("transactions.convertedAmount", { currency: destinationAccount?.currency ?? "" })}</FieldLabel>
            <Controller name="convertedAmount" control={form.control} render={({ field }) => (
              <Input type="number" step="0.01" min="0" placeholder="0.00" value={field.value ?? ""} onChange={(e) => field.onChange(e.target.value)} />
            )} />
          </Field>
        </div>
      )}

      <Field>
        <FieldLabel>{t("transactions.date")}</FieldLabel>
        <Controller name="date" control={form.control} render={({ field }) => (
          <DateTimePicker value={field.value} onChange={field.onChange} />
        )} />
      </Field>

      <Field>
        <FieldLabel>{t("transactions.notes")}</FieldLabel>
        <Controller name="notes" control={form.control} render={({ field }) => (
          <Input value={field.value ?? ""} onChange={field.onChange} placeholder={t("transactions.notesPlaceholder")} />
        )} />
      </Field>

      <div className="flex justify-end gap-2 pt-2">
        <Button type="button" variant="outline" onClick={onCancel}>{t("transactions.cancel")}</Button>
        <Button type="submit" disabled={isPending}>
          {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {t("transactions.save")}
        </Button>
      </div>
    </form>
  );
}
