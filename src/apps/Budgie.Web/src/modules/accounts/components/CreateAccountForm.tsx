import { useCallback, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm, Controller, useWatch } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import client from "@/lib/api-client";
import { CurrencySelector } from "@/components/currency-selector";
import { enrichCurrencies } from "@/lib/currencies";
import { formatLocal } from "@/lib/date-locale";
import { DateTimePicker } from "@/components/ui/date-time-picker";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
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
import { Field, FieldLabel, FieldContent, FieldError } from "@/components/ui/field";
import { Loader2 } from "lucide-react";
import { ACCOUNT_NAME_MAX_LENGTH, CURRENCY_MAX_LENGTH } from "@/modules/accounts/constants";

const formSchema = z.object({
  name: z.string().min(1).max(ACCOUNT_NAME_MAX_LENGTH),
  type: z.string().min(1),
  currency: z.string().min(1).max(CURRENCY_MAX_LENGTH),
  initialBalance: z.string().min(1),
  creditLimit: z.string().optional(),
  creditConsumptionMode: z.string().optional(),
  billingCycleDay: z.string().optional(),
  paymentDueDay: z.string().optional(),
  annualInterestRate: z.string().optional(),
  billingCycleDayInclusive: z.boolean(),
  interestCalculationMethod: z.string().optional(),
  createdAt: z.string().min(1),
});

type FormValues = z.infer<typeof formSchema>;

interface CreateAccountFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

export function CreateAccountForm({ onSuccess, onCancel }: CreateAccountFormProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();

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

  const mutation = useMutation({
    mutationFn: async (body: {
      name: string;
      currency: string;
      initialBalance: number;
      type: number;
      creditLimit?: number | null;
      creditConsumptionMode?: number | null;
      billingCycleDay?: number | null;
      paymentDueDay?: number | null;
      annualInterestRate?: number | null;
      billingCycleDayInclusive: boolean;
      interestCalculationMethod?: number | null;
      createdAt?: string | null;
    }) => {
      const { data, error } = await client.POST("/api/finances/accounts", { body });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      onSuccess();
    },
    onError: (err) => {
      toast.error(t("accounts.saveError"), { description: err instanceof Error ? err.message : undefined });
    },
  });

  const { control, handleSubmit, setValue } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: "",
      type: "0",
      currency: settings?.mainCurrency || "USD",
      initialBalance: "0",
      creditLimit: "",
      creditConsumptionMode: "0",
      billingCycleDay: "",
      paymentDueDay: "",
      annualInterestRate: "",
      billingCycleDayInclusive: true,
      interestCalculationMethod: "0",
      createdAt: formatLocal(new Date()),
    },
  });

  useEffect(() => {
    if (settings?.mainCurrency) {
      setValue("currency", settings.mainCurrency);
    }
  }, [settings, setValue]);

  const selectedType = useWatch({ control, name: "type" });
  const isCredit = selectedType === "2";

  const onSubmit = useCallback(
    (values: FormValues) => {
      mutation.mutate({
        name: values.name,
        currency: values.currency,
        initialBalance: parseFloat(values.initialBalance),
        type: parseInt(values.type, 10),
        creditLimit: isCredit && values.creditLimit ? parseFloat(values.creditLimit) : null,
        creditConsumptionMode: isCredit ? parseInt(values.creditConsumptionMode ?? "0", 10) : null,
        billingCycleDay: isCredit && values.billingCycleDay ? parseInt(values.billingCycleDay, 10) : null,
        paymentDueDay: isCredit && values.paymentDueDay ? parseInt(values.paymentDueDay, 10) : null,
        annualInterestRate: isCredit && values.annualInterestRate ? parseFloat(values.annualInterestRate) : null,
        billingCycleDayInclusive: isCredit ? values.billingCycleDayInclusive : true,
        interestCalculationMethod: isCredit && values.annualInterestRate ? (values.interestCalculationMethod ? parseInt(values.interestCalculationMethod, 10) : 0) : null,
        createdAt: values.createdAt,
      });
    },
    [mutation, isCredit],
  );

  const typeItems = [
    { label: t("accounts.checking"), value: "0" },
    { label: t("accounts.savings"), value: "1" },
    { label: t("accounts.credit"), value: "2" },
  ];

  const consumptionModeItems = [
    { label: t("accounts.total"), value: "0" },
    { label: t("accounts.partial"), value: "1" },
  ];

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <DialogHeader>
        <DialogTitle>{t("accounts.addNew")}</DialogTitle>
        <DialogDescription>{t("accounts.addDesc")}</DialogDescription>
      </DialogHeader>
      <div className="grid gap-4 py-4">
        <Controller
          name="name"
          control={control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="create-name">{t("accounts.accountName")}</FieldLabel>
              <Input {...field} id="create-name" placeholder={t("accounts.accountNamePlaceholder")} maxLength={ACCOUNT_NAME_MAX_LENGTH} aria-invalid={fieldState.invalid} />
              {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
            </Field>
          )}
        />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Controller
            name="type"
            control={control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="create-type">{t("accounts.accountType")}</FieldLabel>
                <Select items={typeItems} value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="create-type" aria-invalid={fieldState.invalid}>
                    <SelectValue placeholder={t("accounts.selectType")} />
                  </SelectTrigger>
                  <SelectContent>
                    {typeItems.map((item) => (
                      <SelectItem key={item.value} value={item.value}>
                        {item.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
              </Field>
            )}
          />
          <Controller
            name="currency"
            control={control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="create-currency">{t("accounts.currency")}</FieldLabel>
                <CurrencySelector
                  id="create-currency"
                  value={field.value}
                  onValueChange={field.onChange}
                  invalid={fieldState.invalid}
                  currencies={enrichCurrencies(settings?.supportedCurrencies ?? ["USD"], i18n.language)}
                />
                {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
              </Field>
            )}
          />
        </div>
        <Controller
          name="createdAt"
          control={control}
          render={({ field }) => (
            <Field>
              <FieldLabel>{t("accounts.date")}</FieldLabel>
              <FieldContent>
                <DateTimePicker value={field.value} onChange={field.onChange} />
              </FieldContent>
            </Field>
          )}
        />
        <Controller
          name="initialBalance"
          control={control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="create-balance">{t("accounts.initialBalance")}</FieldLabel>
              <Input {...field} id="create-balance" type="number" step="0.01" aria-invalid={fieldState.invalid} />
              {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
            </Field>
          )}
        />
        {isCredit && (
          <>
            <p className="text-sm font-medium text-muted-foreground -mb-2">{t("accounts.creditFields")}</p>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Controller
                name="creditLimit"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="create-credit-limit">{t("accounts.creditLimit")}</FieldLabel>
                    <Input {...field} id="create-credit-limit" type="number" step="0.01" min="0" aria-invalid={fieldState.invalid} />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
              <Controller
                name="creditConsumptionMode"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="create-consumption-mode">{t("accounts.creditConsumptionMode")}</FieldLabel>
                    <Select items={consumptionModeItems} value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id="create-consumption-mode" aria-invalid={fieldState.invalid}>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {consumptionModeItems.map((item) => (
                          <SelectItem key={item.value} value={item.value}>
                            {item.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Controller
                name="billingCycleDay"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="create-cycle-day">{t("accounts.billingCycleDay")}</FieldLabel>
                    <Input {...field} id="create-cycle-day" type="number" min="1" max="28" aria-invalid={fieldState.invalid} />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
              <Controller
                name="paymentDueDay"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="create-due-day">{t("accounts.paymentDueDay")}</FieldLabel>
                    <Input {...field} id="create-due-day" type="number" min="1" max="31" aria-invalid={fieldState.invalid} />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Controller
                name="annualInterestRate"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="create-interest-rate">{t("accounts.annualInterestRate")}</FieldLabel>
                    <Input {...field} id="create-interest-rate" type="number" step="0.01" min="0" aria-invalid={fieldState.invalid} />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
              <div className="flex items-end pb-2">
                <Controller
                  name="billingCycleDayInclusive"
                  control={control}
                  render={({ field }) => (
                    <div className="flex items-center gap-2">
                      <Checkbox
                        id="create-cycle-inclusive"
                        checked={field.value}
                        onCheckedChange={field.onChange}
                      />
                      <Label htmlFor="create-cycle-inclusive" className="text-sm font-normal cursor-pointer">
                        {t("accounts.billingCycleDayInclusive")}
                      </Label>
                    </div>
                  )}
                />
              </div>
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Controller
                name="interestCalculationMethod"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="create-interest-method">{t("accounts.interestCalculationMethod")}</FieldLabel>
                    <Select items={[
                      { label: t("accounts.frenchAmortization"), value: "0" },
                      { label: t("accounts.simpleInterest"), value: "1" },
                    ]} value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id="create-interest-method" aria-invalid={fieldState.invalid}>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="0">{t("accounts.frenchAmortization")}</SelectItem>
                        <SelectItem value="1">{t("accounts.simpleInterest")}</SelectItem>
                      </SelectContent>
                    </Select>
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
              <div />
            </div>
          </>
        )}
      </div>
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onCancel}>
          {t("accounts.cancel")}
        </Button>
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
          {t("accounts.save")}
        </Button>
      </DialogFooter>
    </form>
  );
}
