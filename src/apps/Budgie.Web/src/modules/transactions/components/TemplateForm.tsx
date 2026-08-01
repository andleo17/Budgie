import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { CurrencySelector } from "@/components/currency-selector";
import { enrichCurrencies } from "@/lib/currencies";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { DateTimePicker } from "@/components/ui/date-time-picker";
import { formatLocal } from "@/lib/date-locale";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
import { Loader2 } from "lucide-react";
import { toast } from "sonner";

type RecurringTransactionResponse = components["schemas"]["RecurringTransactionResponse"];

const templateSchema = z.object({
  name: z.string().min(1, "Name is required"),
  type: z.string().min(1),
  accountId: z.string().min(1, "Account is required"),
  amount: z.string().min(1, "Amount is required"),
  currency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  exchangeRate: z.string().optional(),
  categoryId: z.string().optional(),
  notes: z.string().optional(),
  installments: z.string().optional(),
  isInterestFree: z.boolean().optional(),
  isRecurring: z.boolean(),
  applicability: z.string().optional(),
  frequency: z.string().optional(),
  customIntervalDays: z.string().optional(),
  startDate: z.string().optional(),
  endType: z.string().optional(),
  endAfterCount: z.string().optional(),
  endDate: z.string().optional(),
});

type TemplateFormValues = z.infer<typeof templateSchema>;

interface TemplateFormProps {
  open: boolean;
  onClose: () => void;
  template?: RecurringTransactionResponse | null;
}

export function TemplateForm({ open, onClose, template }: TemplateFormProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const isEdit = !!template;

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
      defaultApplicability: d?.defaultRecurringApplicability ?? 1,
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

  const form = useForm<TemplateFormValues>({
    resolver: zodResolver(templateSchema),
    defaultValues: {
      name: "",
      type: "1",
      accountId: "",
      amount: "",
      currency: "",
      exchangeRate: "1",
      categoryId: "",
      notes: "",
      installments: "1",
      isInterestFree: true,
      isRecurring: false,
      applicability: String(settings?.defaultApplicability ?? 1),
      frequency: "3",
      customIntervalDays: "30",
      startDate: formatLocal(new Date()),
      endType: "0",
      endAfterCount: "5",
      endDate: "",
    },
  });

  useEffect(() => {
    if (template) {
      form.reset({
        name: template.name,
        type: String(template.type),
        accountId: template.accountId,
        amount: String(template.amount),
        currency: template.currency,
        exchangeRate: String(template.exchangeRate),
        categoryId: template.categoryId ?? "",
        notes: template.notes ?? "",
        installments: String(template.installments ?? 1),
        isInterestFree: template.isInterestFree ?? true,
        isRecurring: template.frequency != null,
        applicability: String(template.applicability),
        frequency: String(template.frequency ?? 3),
        customIntervalDays: String(template.customIntervalDays ?? 30),
        startDate: template.startDate ? template.startDate.slice(0, 19) : formatLocal(new Date()),
        endType: String(template.endType ?? 0),
        endAfterCount: String(template.endAfterCount ?? 5),
        endDate: template.endDate ? template.endDate.slice(0, 19) : "",
      });
    } else if (settings?.mainCurrency) {
      form.reset({
        name: "",
        type: "1",
        accountId: "",
        amount: "",
        currency: settings.mainCurrency,
        exchangeRate: "1",
        categoryId: "",
        notes: "",
        installments: "1",
        isInterestFree: true,
        isRecurring: false,
        applicability: String(settings.defaultApplicability ?? 1),
        frequency: "3",
        customIntervalDays: "30",
        startDate: formatLocal(new Date()),
        endType: "0",
        endAfterCount: "5",
        endDate: "",
      });
    }
  }, [template, settings, form]);

  const createMutation = useMutation({
    mutationFn: async (body: components["schemas"]["CreateRecurringTransactionCommand"]) => {
      const { data, error } = await client.POST("/api/finances/transactions/templates", { body });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["recurring-transactions"] });
      toast.success(t("templates.saveSuccess"));
      onClose();
    },
    onError: (err) => {
      toast.error(t("accounts.saveError"), { description: err instanceof Error ? err.message : undefined });
    },
  });

  const updateMutation = useMutation({
    mutationFn: async ({ id, body }: { id: string; body: components["schemas"]["UpdateRecurringTransactionCommand"] }) => {
      const { error } = await client.PUT("/api/finances/transactions/templates/{id}", {
        params: { path: { id } },
        body,
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["recurring-transactions"] });
      toast.success(t("templates.saveSuccess"));
      onClose();
    },
    onError: (err) => {
      toast.error(t("accounts.saveError"), { description: err instanceof Error ? err.message : undefined });
    },
  });

  const isPending = createMutation.isPending || updateMutation.isPending;
  const isRecurring = form.watch("isRecurring");
  const selectedFrequency = form.watch("frequency");
  const selectedEndType = form.watch("endType");

  const typeItems = [
    { label: t("transactions.income"), value: "0" },
    { label: t("transactions.expense"), value: "1" },
    { label: t("transactions.transfer"), value: "2" },
    { label: t("transactions.loan"), value: "3" },
  ];

  const applicabilityItems = [
    { label: t("templates.automatic"), value: "1" },
    { label: t("templates.manual"), value: "2" },
  ];

  const frequencyItems = [
    { label: t("templates.daily"), value: "0" },
    { label: t("templates.weekly"), value: "1" },
    { label: t("templates.biweekly"), value: "2" },
    { label: t("templates.monthly"), value: "3" },
    { label: t("templates.yearly"), value: "4" },
    { label: t("templates.custom"), value: "5" },
  ];

  const endTypeItems = [
    { label: t("templates.endNever"), value: "0" },
    { label: t("templates.endAfter"), value: "1" },
    { label: t("templates.endAtDate"), value: "2" },
  ];

  const onSubmit = (values: TemplateFormValues) => {
    const body: components["schemas"]["CreateRecurringTransactionCommand"] = {
      name: values.name,
      accountId: values.accountId,
      categoryId: values.categoryId || null,
      amount: parseFloat(values.amount),
      currency: values.currency,
      exchangeRate: parseFloat(values.exchangeRate ?? "1") || 1,
      type: parseInt(values.type, 10) as components["schemas"]["TransactionType"],
      notes: values.notes || null,
      installments: parseInt(values.installments ?? "1", 10) || 1,
      isInterestFree: values.isInterestFree ?? true,
      applicability: (values.isRecurring ? parseInt(values.applicability ?? "1", 10) : 0) as components["schemas"]["RecurringApplicability"],
      frequency: values.isRecurring ? (parseInt(values.frequency ?? "3", 10) as components["schemas"]["RecurringFrequency"]) : null,
      customIntervalDays: values.isRecurring && values.frequency === "5" ? parseInt(values.customIntervalDays ?? "30", 10) : null,
      endType: values.isRecurring ? (parseInt(values.endType ?? "0", 10) as components["schemas"]["RecurringEndType"]) : null,
      endAfterCount: values.isRecurring && values.endType === "1" ? parseInt(values.endAfterCount ?? "5", 10) : null,
      endDate: values.isRecurring && values.endType === "2" ? values.endDate || null : null,
      startDate: values.isRecurring ? values.startDate || null : null,
      participants: [],
    };

    if (isEdit && template) {
      updateMutation.mutate({
        id: template.id,
        body: { ...body, id: template.id } as never,
      });
    } else {
      createMutation.mutate(body);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isEdit ? t("templates.editTemplate") : t("templates.createTemplate")}</DialogTitle>
          <DialogDescription>{t("templates.description")}</DialogDescription>
        </DialogHeader>
        <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4 py-4">
          <Controller
            name="name"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel>{t("templates.name")} <span className="text-destructive">*</span></FieldLabel>
                <FieldContent>
                  <Input {...field} placeholder={t("templates.name")} />
                </FieldContent>
                {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
              </Field>
            )}
          />

          <div className="grid grid-cols-2 gap-3">
            <Controller
              name="type"
              control={form.control}
              render={({ field }) => (
                <Field>
                  <FieldLabel>{t("transactions.type")}</FieldLabel>
                  <FieldContent>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger>
                        <SelectValue>
                          {() => typeItems.find(i => i.value === field.value)?.label}
                        </SelectValue>
                      </SelectTrigger>
                      <SelectContent>
                        {typeItems.map((item) => (
                          <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FieldContent>
                </Field>
              )}
            />
            <Controller
              name="accountId"
              control={form.control}
              render={({ field, fieldState }) => {
                const sorted = accounts ?? [];
                const selected = sorted.find((a) => a.id === field.value) ?? null;
                return (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel>{t("transactions.account")} <span className="text-destructive">*</span></FieldLabel>
                    <FieldContent>
                      <Combobox
                        items={sorted}
                        itemToStringValue={(a) => a.name}
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
                    </FieldContent>
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                );
              }}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Controller
              name="amount"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel>{t("transactions.amount")} <span className="text-destructive">*</span></FieldLabel>
                  <FieldContent>
                    <Input type="number" step="0.01" min="0" {...field} />
                  </FieldContent>
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
            <Controller
              name="currency"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel>{t("transactions.currency")}</FieldLabel>
                  <FieldContent>
                    <CurrencySelector value={field.value} onValueChange={field.onChange} invalid={fieldState.invalid} currencies={enrichCurrencies(settings?.supportedCurrencies ?? ["USD"], i18n.language)} />
                  </FieldContent>
                </Field>
              )}
            />
          </div>

          <Controller
            name="categoryId"
            control={form.control}
            render={({ field }) => (
              <Field>
                <FieldLabel>{t("transactions.category")}</FieldLabel>
                <FieldContent>
                  <CategoryCombobox value={field.value} onValueChange={(id) => field.onChange(id ?? "")} placeholder={t("transactions.uncategorized")} showNoneOption />
                </FieldContent>
              </Field>
            )}
          />

          <Controller
            name="notes"
            control={form.control}
            render={({ field }) => (
              <Field>
                <FieldLabel>{t("transactions.notes")}</FieldLabel>
                <FieldContent>
                  <Input {...field} value={field.value ?? ""} />
                </FieldContent>
              </Field>
            )}
          />

          <div className="flex items-center gap-2 pt-2 border-t">
            <Controller
              name="isRecurring"
              control={form.control}
              render={({ field }) => (
                <div className="flex items-center gap-2">
                  <Checkbox
                    id="tmpl-recurring"
                    checked={field.value}
                    onCheckedChange={(v) => field.onChange(v === true)}
                  />
                  <Label htmlFor="tmpl-recurring" className="text-sm font-medium cursor-pointer">
                    {t("templates.isRecurring")}
                  </Label>
                </div>
              )}
            />
          </div>

          {isRecurring && (
            <div className="grid gap-4 rounded-lg border bg-muted/30 p-4">
              <div className="grid grid-cols-2 gap-3">
                <Controller
                  name="applicability"
                  control={form.control}
                  render={({ field }) => (
                    <Field>
                      <FieldLabel>{t("templates.applicability")}</FieldLabel>
                      <FieldContent>
                        <Select items={applicabilityItems} value={field.value} onValueChange={field.onChange}>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {applicabilityItems.map((item) => (
                              <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FieldContent>
                    </Field>
                  )}
                />
                <Controller
                  name="frequency"
                  control={form.control}
                  render={({ field }) => (
                    <Field>
                      <FieldLabel>{t("templates.frequency")}</FieldLabel>
                      <FieldContent>
                        <Select items={frequencyItems} value={field.value} onValueChange={field.onChange}>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {frequencyItems.map((item) => (
                              <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FieldContent>
                    </Field>
                  )}
                />
              </div>

              {selectedFrequency === "5" && (
                <Controller
                  name="customIntervalDays"
                  control={form.control}
                  render={({ field }) => (
                    <Field>
                      <FieldLabel>{t("templates.customDays")}</FieldLabel>
                      <FieldContent>
                        <Input type="number" min="1" step="1" {...field} />
                      </FieldContent>
                    </Field>
                  )}
                />
              )}

              <Controller
                name="startDate"
                control={form.control}
                render={({ field }) => (
                  <Field>
                    <FieldLabel>{t("templates.startDate")}</FieldLabel>
                    <FieldContent>
                      <DateTimePicker value={field.value ?? ""} onChange={field.onChange} />
                    </FieldContent>
                  </Field>
                )}
              />

              <Controller
                name="endType"
                control={form.control}
                render={({ field }) => (
                  <Field>
                    <FieldLabel>{t("templates.end")}</FieldLabel>
                    <FieldContent>
                      <Select items={endTypeItems} value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {endTypeItems.map((item) => (
                            <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FieldContent>
                  </Field>
                )}
              />

              {selectedEndType === "1" && (
                <Controller
                  name="endAfterCount"
                  control={form.control}
                  render={({ field }) => (
                    <Field>
                      <FieldLabel>{t("templates.endAfterCount")}</FieldLabel>
                      <FieldContent>
                        <Input type="number" min="1" step="1" {...field} />
                      </FieldContent>
                    </Field>
                  )}
                />
              )}

              {selectedEndType === "2" && (
                <Controller
                  name="endDate"
                  control={form.control}
                  render={({ field }) => (
                    <Field>
                      <FieldLabel>{t("templates.endDateLabel")}</FieldLabel>
                      <FieldContent>
                        <DateTimePicker value={field.value ?? ""} onChange={field.onChange} />
                      </FieldContent>
                    </Field>
                  )}
                />
              )}
            </div>
          )}

          <DialogFooter className="pt-4">
            <Button type="button" variant="outline" onClick={onClose}>
              {t("transactions.cancel")}
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {isEdit ? t("common.save") : t("templates.createTemplate")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
