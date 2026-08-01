import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
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
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Field, FieldLabel, FieldError } from "@/components/ui/field";
import { Loader2, Trash2 } from "lucide-react";
import { ACCOUNT_NAME_MAX_LENGTH } from "@/modules/accounts/constants";

type AccountResponse = components["schemas"]["AccountResponse"];

function nullableNum(v: number | string | null | undefined): string {
  if (v === null || v === undefined) return "";
  return String(Number(v));
}

function nullableBool(v: boolean | undefined): boolean {
  return v ?? true;
}

const formSchema = z.object({
  name: z.string().min(1).max(ACCOUNT_NAME_MAX_LENGTH),
  creditLimit: z.string().optional(),
  creditConsumptionMode: z.string().optional(),
  billingCycleDay: z.string().optional(),
  paymentDueDay: z.string().optional(),
  annualInterestRate: z.string().optional(),
  billingCycleDayInclusive: z.boolean(),
  interestCalculationMethod: z.string().optional(),
});

type FormValues = z.infer<typeof formSchema>;

interface EditAccountFormProps {
  account: AccountResponse;
  onSuccess: () => void;
}

export function EditAccountForm({ account, onSuccess }: EditAccountFormProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const isCredit = account.type === 2;

  const updateMutation = useMutation({
    mutationFn: async (body: {
      id: string;
      name: string;
      creditLimit: number | null;
      creditConsumptionMode: number | null;
      billingCycleDay: number | null;
      paymentDueDay: number | null;
      annualInterestRate: number | null;
      billingCycleDayInclusive: boolean;
      interestCalculationMethod: number | null;
    }) => {
      const { error } = await client.PUT("/api/finances/accounts/{id}", {
        params: { path: { id: body.id } },
        body: {
          name: body.name,
          creditLimit: body.creditLimit,
          creditConsumptionMode: body.creditConsumptionMode,
          billingCycleDay: body.billingCycleDay,
          paymentDueDay: body.paymentDueDay,
          annualInterestRate: body.annualInterestRate,
          billingCycleDayInclusive: body.billingCycleDayInclusive,
          interestCalculationMethod: body.interestCalculationMethod,
        },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      onSuccess();
    },
    onError: (err) => {
      toast.error(t("accounts.saveError"), { description: err instanceof Error ? err.message : undefined });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/api/finances/accounts/{id}", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
  });

  const { control, handleSubmit } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: account.name,
      creditLimit: nullableNum(account.creditLimit),
      creditConsumptionMode: nullableNum(account.creditConsumptionMode),
      billingCycleDay: nullableNum(account.billingCycleDay),
      paymentDueDay: nullableNum(account.paymentDueDay),
      annualInterestRate: nullableNum(account.annualInterestRate),
      billingCycleDayInclusive: nullableBool(account.billingCycleDayInclusive),
      interestCalculationMethod: nullableNum(account.interestCalculationMethod),
    },
  });

  const consumptionModeItems = [
    { label: t("accounts.total"), value: "0" },
    { label: t("accounts.partial"), value: "1" },
  ];

  return (
    <form onSubmit={handleSubmit((values) => {
      updateMutation.mutate({
        id: account.id,
        name: values.name,
        creditLimit: isCredit && values.creditLimit ? parseFloat(values.creditLimit) : null,
        creditConsumptionMode: isCredit ? parseInt(values.creditConsumptionMode ?? "0", 10) : null,
        billingCycleDay: isCredit && values.billingCycleDay ? parseInt(values.billingCycleDay, 10) : null,
        paymentDueDay: isCredit && values.paymentDueDay ? parseInt(values.paymentDueDay, 10) : null,
        annualInterestRate: isCredit && values.annualInterestRate ? parseFloat(values.annualInterestRate) : null,
        billingCycleDayInclusive: isCredit ? values.billingCycleDayInclusive : true,
        interestCalculationMethod: isCredit && values.annualInterestRate ? (values.interestCalculationMethod ? parseInt(values.interestCalculationMethod, 10) : 0) : null,
      });
    })}>
      <DialogHeader>
        <DialogTitle>{t("accounts.edit")}</DialogTitle>
        <DialogDescription>{t("accounts.editDesc")}</DialogDescription>
      </DialogHeader>
      <div className="grid gap-4 py-4">
        <Controller
          name="name"
          control={control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="edit-name">{t("accounts.accountName")}</FieldLabel>
              <Input {...field} id="edit-name" maxLength={ACCOUNT_NAME_MAX_LENGTH} aria-invalid={fieldState.invalid} />
              {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
            </Field>
          )}
        />
        {isCredit && (
          <>
            <p className="text-sm font-medium text-muted-foreground -mb-2">{t("accounts.creditFields")}</p>
            <div className="grid grid-cols-2 gap-4">
              <Controller
                name="creditLimit"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-credit-limit">{t("accounts.creditLimit")}</FieldLabel>
                    <Input {...field} id="edit-credit-limit" type="number" step="0.01" min="0" aria-invalid={fieldState.invalid} />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
              <Controller
                name="creditConsumptionMode"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-consumption-mode">{t("accounts.creditConsumptionMode")}</FieldLabel>
                    <Select items={consumptionModeItems} value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id="edit-consumption-mode" aria-invalid={fieldState.invalid}>
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
            <div className="grid grid-cols-2 gap-4">
              <Controller
                name="billingCycleDay"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-cycle-day">{t("accounts.billingCycleDay")}</FieldLabel>
                    <Input {...field} id="edit-cycle-day" type="number" min="1" max="28" aria-invalid={fieldState.invalid} />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
              <Controller
                name="paymentDueDay"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-due-day">{t("accounts.paymentDueDay")}</FieldLabel>
                    <Input {...field} id="edit-due-day" type="number" min="1" max="31" aria-invalid={fieldState.invalid} />
                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                  </Field>
                )}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <Controller
                name="annualInterestRate"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-interest-rate">{t("accounts.annualInterestRate")}</FieldLabel>
                    <Input {...field} id="edit-interest-rate" type="number" step="0.01" min="0" aria-invalid={fieldState.invalid} />
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
                        id="edit-cycle-inclusive"
                        checked={field.value}
                        onCheckedChange={field.onChange}
                      />
                      <Label htmlFor="edit-cycle-inclusive" className="text-sm font-normal cursor-pointer">
                        {t("accounts.billingCycleDayInclusive")}
                      </Label>
                    </div>
                  )}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <Controller
                name="interestCalculationMethod"
                control={control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-interest-method">{t("accounts.interestCalculationMethod")}</FieldLabel>
                    <Select items={[
                      { label: t("accounts.frenchAmortization"), value: "0" },
                      { label: t("accounts.simpleInterest"), value: "1" },
                    ]} value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id="edit-interest-method" aria-invalid={fieldState.invalid}>
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
      <DialogFooter className="sm:justify-between">
        <AlertDialog>
          <AlertDialogTrigger render={<Button variant="destructive" size="sm" />}>
            <Trash2 className="mr-1 size-4" />
            {t("accounts.delete")}
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t("accounts.deleteConfirmTitle")}</AlertDialogTitle>
              <AlertDialogDescription>{t("accounts.deleteConfirmDesc")}</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t("accounts.cancel")}</AlertDialogCancel>
              <AlertDialogAction
                onClick={async () => {
                  await deleteMutation.mutateAsync(account.id)
                  queryClient.invalidateQueries({ queryKey: ["accounts"] })
                  onSuccess()
                }}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
                {t("accounts.delete")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
        <div className="flex gap-2">
          <Button type="button" variant="outline" onClick={onSuccess}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" disabled={updateMutation.isPending}>
            {updateMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
            {t("accounts.save")}
          </Button>
        </div>
      </DialogFooter>
    </form>
  );
}
