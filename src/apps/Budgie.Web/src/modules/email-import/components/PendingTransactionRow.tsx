import { useState } from "react";
import { useTranslation } from "react-i18next";
import { format } from "date-fns";
import { toast } from "sonner";
import type { components } from "@/lib/api-types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
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
  ComboboxTrigger,
  ComboboxValue,
} from "@/components/ui/combobox";
import { CategoryCombobox } from "@/modules/categories/components/CategoryCombobox";
import { DateTimePicker } from "@/components/ui/date-time-picker";
import { HelpCircleIcon, Loader2Icon, ThumbsUpIcon, XCircleIcon } from "lucide-react";
import { getDateLocale, formatLocal, parseDbDate } from "@/lib/date-locale";
import { TransactionType } from "@/lib/constants";
import { useApprovePending, useDismissPending } from "../hooks/usePendingEmails";
import { usePreviewRule } from "../hooks/useEmailRules";
import { parseFieldConfidence } from "../confidence";
import { ConfidenceBadge } from "./ConfidenceBadge";
import { FieldConfidenceBadges } from "./FieldConfidenceBadges";
import { RawEmailDisclosure } from "./RawEmailDisclosure";
import type { PendingEmailTransaction } from "../types";

type AccountResponse = components["schemas"]["AccountResponse"];

/**
 * Fila editable de la bandeja de revisión (spec §8.1). Es donde el usuario descubre
 * por qué algo quedó pendiente y lo corrige antes de aprobar.
 *
 * El estado editable vive en `useState` con inicialización perezosa desde props —
 * nunca en un `useEffect` — y el padre monta cada fila con `key={row.id}` para que
 * un refresco de la lista reinicie los valores al estado del servidor.
 */
export function PendingTransactionRow({
  row,
  accounts,
  accountsLoading,
  onDismissed,
}: {
  row: PendingEmailTransaction;
  accounts: AccountResponse[] | undefined;
  accountsLoading: boolean;
  onDismissed: (row: PendingEmailTransaction) => void;
}) {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);

  const scores = parseFieldConfidence(row.fieldConfidenceJson);

  // ── Estado editable local, inicializado desde la fila del servidor ──────────
  const [amount, setAmount] = useState(() => String(row.amount));
  const [date, setDate] = useState(() => formatLocal(parseDbDate(row.date)));
  const [merchant, setMerchant] = useState(() => row.merchant ?? "");
  const [notes, setNotes] = useState(() => row.notes ?? "");
  const [type, setType] = useState(() => String(row.transactionType));
  // undefined = conservar lo que diga la regla; string = override del usuario.
  const [accountId, setAccountId] = useState<string | undefined>(undefined);
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined);
  const [explanation, setExplanation] = useState<string[] | null>(null);

  const approveMutation = useApprovePending();
  const dismissMutation = useDismissPending();
  const explainMutation = usePreviewRule();

  const parsedAmount = Number(amount.replace(",", "."));
  const amountValid = Number.isFinite(parsedAmount) && parsedAmount > 0;
  // Sin regla que aporte cuenta y sin override: aprobar fallaría en el backend.
  const accountResolvable = !!accountId || !!row.ruleId;
  const busy = approveMutation.isPending || dismissMutation.isPending;

  const handleApprove = () => {
    if (!amountValid || !accountResolvable) return;
    approveMutation.mutate(
      {
        id: row.id,
        body: {
          amount: parsedAmount,
          date,
          merchant: merchant.trim() || null,
          notes: notes.trim() || null,
          accountId: accountId ?? null,
          categoryId: categoryId ?? null,
          type: Number(type),
        },
      },
      {
        onSuccess: () => toast.success(t("emailImport.pending.approved")),
        onError: (err) =>
          toast.error(t("emailImport.pending.approveError"), {
            description: err instanceof Error ? err.message : undefined,
          }),
      },
    );
  };

  const handleDismiss = () => {
    dismissMutation.mutate(row.id, {
      onSuccess: () => {
        toast.success(t("emailImport.pending.dismissed"));
        onDismissed(row);
      },
      onError: (err) =>
        toast.error(t("emailImport.pending.dismissError"), {
          description: err instanceof Error ? err.message : undefined,
        }),
    });
  };

  const handleExplain = () => {
    if (!row.rawBody || !row.ruleId) return;
    explainMutation.mutate(
      { ruleId: row.ruleId, rawEmail: row.rawBody },
      {
        onSuccess: (data) => setExplanation(data.explanation),
        onError: (err) =>
          toast.error(t("emailImport.pending.whyError"), {
            description: err instanceof Error ? err.message : undefined,
          }),
      },
    );
  };

  const typeItems = [
    { label: t("transactions.income"), value: String(TransactionType.Income) },
    { label: t("transactions.expense"), value: String(TransactionType.Expense) },
  ];

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        {/* Cabecera: asunto + confianza total + fecha de recepción */}
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0 flex-1">
            <p className="truncate font-medium">
              {row.subject ?? t("emailImport.pending.noSubject")}
            </p>
            <p className="text-xs text-muted-foreground tabular-nums">
              {format(parseDbDate(row.createdAt), "PPp", { locale: dateLocale })}
            </p>
          </div>
          <ConfidenceBadge confidence={row.confidence} />
        </div>

        <FieldConfidenceBadges scores={scores} />

        {row.failureReason && (
          <Alert variant="destructive">
            <AlertTitle>{t("emailImport.pending.failureReason")}</AlertTitle>
            <AlertDescription>{row.failureReason}</AlertDescription>
          </Alert>
        )}

        {/* Campos editables */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`account-${row.id}`}>
              {t("emailImport.pending.fields.account")}
            </Label>
            {accountsLoading ? (
              <Skeleton className="h-9 w-full" />
            ) : (
              <AccountCombobox
                id={`account-${row.id}`}
                accounts={accounts ?? []}
                value={accountId}
                onChange={setAccountId}
                placeholder={t("emailImport.pending.fields.accountFromRule")}
              />
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`category-${row.id}`}>
              {t("emailImport.pending.fields.category")}
            </Label>
            <CategoryCombobox
              value={categoryId}
              onValueChange={(id) => setCategoryId(id)}
              placeholder={t("emailImport.pending.fields.categoryFromRule")}
              showNoneOption
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`amount-${row.id}`}>{t("emailImport.pending.fields.amount")}</Label>
            <div className="flex items-center gap-1.5">
              <Input
                id={`amount-${row.id}`}
                value={amount}
                inputMode="decimal"
                className="tabular-nums"
                aria-invalid={!amountValid}
                onChange={(e) => setAmount(e.target.value)}
              />
              <span className="shrink-0 text-sm text-muted-foreground">{row.currency}</span>
            </div>
            {!amountValid && (
              <p className="text-xs text-destructive">{t("emailImport.pending.amountInvalid")}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`date-${row.id}`}>{t("emailImport.pending.fields.date")}</Label>
            <DateTimePicker value={date} onChange={setDate} disabled={busy} />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`merchant-${row.id}`}>{t("emailImport.pending.fields.merchant")}</Label>
            <Input
              id={`merchant-${row.id}`}
              value={merchant}
              onChange={(e) => setMerchant(e.target.value)}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`type-${row.id}`}>{t("emailImport.pending.fields.type")}</Label>
            <Select
              items={typeItems}
              value={type}
              onValueChange={(v) => {
                if (v !== null) setType(String(v));
              }}
            >
              <SelectTrigger id={`type-${row.id}`} className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {typeItems.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {item.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2 lg:col-span-4">
            <Label htmlFor={`notes-${row.id}`}>{t("emailImport.pending.fields.notes")}</Label>
            <Textarea
              id={`notes-${row.id}`}
              value={notes}
              rows={2}
              placeholder={t("transactions.notesPlaceholder")}
              onChange={(e) => setNotes(e.target.value)}
            />
          </div>
        </div>

        <RawEmailDisclosure rawBody={row.rawBody} />

        {/* Explicación literal del scorer, traída on-demand con la misma regla + correo */}
        {explanation && (
          <Alert>
            <AlertTitle>{t("emailImport.pending.explanation")}</AlertTitle>
            <AlertDescription>
              <ul className="list-disc space-y-0.5 pl-4">
                {explanation.map((line) => (
                  <li key={line} className="text-xs">
                    {line}
                  </li>
                ))}
              </ul>
            </AlertDescription>
          </Alert>
        )}

        {!accountResolvable && (
          <p className="text-xs text-muted-foreground">
            {t("emailImport.pending.accountRequiredHint")}
          </p>
        )}

        {/* Acciones */}
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            onClick={handleApprove}
            disabled={busy || !amountValid || !accountResolvable}
          >
            {approveMutation.isPending ? (
              <Loader2Icon data-icon="inline-start" className="animate-spin" />
            ) : (
              <ThumbsUpIcon data-icon="inline-start" />
            )}
            {approveMutation.isPending
              ? t("emailImport.pending.approving")
              : t("emailImport.pending.approve")}
          </Button>
          <Button type="button" variant="outline" onClick={handleDismiss} disabled={busy}>
            <XCircleIcon data-icon="inline-start" />
            {t("emailImport.pending.dismiss")}
          </Button>
          {row.ruleId && row.rawBody && (
            <Button
              type="button"
              variant="ghost"
              onClick={handleExplain}
              disabled={explainMutation.isPending}
            >
              {explainMutation.isPending ? (
                <Loader2Icon data-icon="inline-start" className="animate-spin" />
              ) : (
                <HelpCircleIcon data-icon="inline-start" />
              )}
              {t("emailImport.pending.why")}
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/** Combobox de cuentas (patrón de CategoryCombobox) sin query propia: recibe la lista. */
function AccountCombobox({
  id,
  accounts,
  value,
  onChange,
  placeholder,
}: {
  id: string;
  accounts: AccountResponse[];
  value: string | undefined;
  onChange: (id: string | undefined) => void;
  placeholder: string;
}) {
  const { t } = useTranslation();
  const current = accounts.find((a) => a.id === value) ?? null;

  return (
    <Combobox
      items={accounts}
      value={current}
      onValueChange={(v: AccountResponse | null) => onChange(v?.id)}
      itemToStringLabel={(item: AccountResponse) => item.name}
    >
      <ComboboxTrigger
        id={id}
        render={
          <Button
            type="button"
            variant="outline"
            className="w-full justify-between font-normal"
          />
        }
      >
        <ComboboxValue placeholder={placeholder} />
      </ComboboxTrigger>
      <ComboboxContent>
        <ComboboxInput showTrigger={false} placeholder={t("accounts.searchPlaceholder")} />
        <ComboboxEmpty>{t("accounts.noAccounts")}</ComboboxEmpty>
        <ComboboxList>
          {(item: AccountResponse) => (
            <ComboboxItem key={item.id} value={item}>
              {item.name}
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}
