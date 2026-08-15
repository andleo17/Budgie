import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { DateTimePicker } from "@/components/ui/date-time-picker";
import { formatLocal } from "@/lib/date-locale";
import { formatCurrency } from "@/lib/format";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
import { Loader2, HandCoins, Wallet } from "lucide-react";

type DebtResponse = components["schemas"]["DebtResponse"];
type AccountResponse = components["schemas"]["AccountResponse"];
type PersonResponse = components["schemas"]["PersonResponse"];

interface SettleDebtDialogProps {
  person: PersonResponse | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}

export function SettleDebtDialog({ person, open, onOpenChange }: SettleDebtDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [amount, setAmount] = useState("");
  const [date, setDate] = useState(() => formatLocal(new Date()));
  const [notes, setNotes] = useState("");
  const [destinationAccountId, setDestinationAccountId] = useState<string>("");
  const [selectedDebts, setSelectedDebts] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);

  const personId = person?.id;

  const { data: debts } = useQuery({
    queryKey: ["personDebts", personId],
    queryFn: async () => {
      if (!personId) return [] as DebtResponse[];
      const { data, error } = await client.GET(
        "/api/finances/persons/{personId}/debts",
        { params: { path: { personId } } },
      );
      if (error) throw error;
      return (data ?? []) as DebtResponse[];
    },
    enabled: open && !!personId,
  });

  const { data: accounts } = useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data;
    },
  });

  const totalPending = useMemo(
    () => (debts ?? []).reduce((s, d) => s + Number(d.remainingAmount), 0),
    [debts],
  );

  const selectedTotal = useMemo(
    () =>
      (debts ?? [])
        .filter((d) => selectedDebts.has(d.participantId))
        .reduce((s, d) => s + Number(d.remainingAmount), 0),
    [debts, selectedDebts],
  );

  const currency = debts?.[0]?.currency ?? "USD";
  const selectAll = (debts ?? []).length > 0 && selectedDebts.size === (debts ?? []).length;

  function toggleAll() {
    if (selectAll) setSelectedDebts(new Set());
    else setSelectedDebts(new Set((debts ?? []).map((d) => d.participantId)));
  }

  function toggleOne(id: string) {
    setSelectedDebts((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  const settleMutation = useMutation({
    mutationFn: async (body: {
      personId: string;
      destinationAccountId: string;
      amount: number;
      currency: string;
      date: string;
      notes?: string | null;
      specificParticipantIds?: string[] | null;
    }) => {
      const { data, error } = await client.POST("/api/finances/debts/settle", { body });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["personDebts"] });
      queryClient.invalidateQueries({ queryKey: ["personSummary"] });
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      toast.success(t("debts.settled"));
      reset();
      onOpenChange(false);
    },
    onError: (err) => {
      setError(err instanceof Error ? err.message : t("debts.settleError"));
    },
  });

  function reset() {
    setAmount("");
    setDate(formatLocal(new Date()));
    setNotes("");
    setSelectedDebts(new Set());
    setDestinationAccountId("");
    setError(null);
  }

  function handleSettle() {
    const value = parseFloat(amount);
    if (!destinationAccountId) { setError(t("debts.selectAccountError")); return; }
    if (!value || value <= 0) { setError(t("debts.invalidAmount")); return; }

    const selected = selectedDebts.size > 0;
    const ids = selected ? [...selectedDebts] : null;
    const max = selected ? selectedTotal : totalPending;
    if (value > max) { setError(t("debts.amountTooLarge")); return; }

    settleMutation.mutate({
      personId: personId!,
      destinationAccountId,
      amount: value,
      currency,
      date,
      notes: notes || null,
      specificParticipantIds: ids,
    });
  }

  const selectedAccount = (accounts ?? []).find((a) => a.id === destinationAccountId);

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) reset(); onOpenChange(v); }}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <HandCoins className="size-5" />
            {t("debts.settleTitle", { person: person?.name ?? "" })}
          </DialogTitle>
          <DialogDescription>
            {t("debts.settleDesc")}
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4 py-2">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-muted/20 px-3 py-2.5">
            <span className="text-sm text-muted-foreground">{t("debts.totalPending")}</span>
            <span className="text-lg font-bold">
              {formatCurrency(totalPending, currency)}
            </span>
          </div>

          {(debts ?? []).length > 0 && (
            <div className="flex flex-col gap-2">
              <div className="flex items-center justify-between">
                <Label className="text-sm font-medium">{t("debts.selectDebts")}</Label>
                <label className="flex items-center gap-2 text-xs text-muted-foreground cursor-pointer">
                  <Checkbox checked={selectAll} onCheckedChange={toggleAll} />
                  {t("debts.selectAll")}
                </label>
              </div>
              <div className="max-h-44 overflow-y-auto flex flex-col gap-1.5 pr-1">
                {(debts ?? []).map((d) => (
                  <label
                    key={d.participantId}
                    className="flex items-center gap-2 rounded-md border bg-card px-2.5 py-2 cursor-pointer hover:bg-muted/40"
                  >
                    <Checkbox
                      checked={selectedDebts.has(d.participantId)}
                      onCheckedChange={() => toggleOne(d.participantId)}
                    />
                    <span className="flex-1 text-sm truncate">
                      {formatLocal(new Date(d.date)).slice(0, 10)}
                      {d.notes ? ` · ${d.notes}` : ""}
                    </span>
                    <Badge variant="secondary" className="shrink-0">
                      {formatCurrency(Number(d.remainingAmount), d.currency)}
                    </Badge>
                  </label>
                ))}
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field>
              <FieldLabel>{t("debts.amount")}</FieldLabel>
              <FieldContent>
                <Input
                  type="number"
                  step="0.01"
                  min="0"
                  placeholder="0.00"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                />
              </FieldContent>
              {selectedDebts.size > 0 && (
                <FieldError errors={[{ message: t("debts.selectedTotal", { amount: formatCurrency(selectedTotal, currency) }) }]} />
              )}
            </Field>
            <Field>
              <FieldLabel>{t("debts.date")}</FieldLabel>
              <FieldContent>
                <DateTimePicker value={date} onChange={setDate} />
              </FieldContent>
            </Field>
          </div>

          <Field>
            <FieldLabel>{t("debts.destinationAccount")}</FieldLabel>
            <FieldContent>
              <Combobox<AccountResponse>
                items={accounts ?? []}
                itemToStringValue={(a: AccountResponse) => a.name}
                itemToStringLabel={(a: AccountResponse) => a.name}
                value={selectedAccount ?? null}
                onValueChange={(a: AccountResponse | null) => setDestinationAccountId(a?.id ?? "")}
              >
                <ComboboxInput placeholder={t("debts.selectAccount")} className="pl-8" />
                <ComboboxContent>
                  <ComboboxEmpty>{t("transactions.noResults")}</ComboboxEmpty>
                  <ComboboxList>
                    {(a) => (
                      <ComboboxItem key={a.id} value={a}>
                        <Wallet className="size-3.5 mr-1.5" />
                        {a.name}
                      </ComboboxItem>
                    )}
                  </ComboboxList>
                </ComboboxContent>
              </Combobox>
            </FieldContent>
          </Field>

          <Field>
            <FieldLabel>{t("debts.notes")}</FieldLabel>
            <FieldContent>
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder={t("debts.notesPlaceholder")} />
            </FieldContent>
          </Field>

          {error && (
            <p className="text-sm text-destructive rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2">
              {error}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => { reset(); onOpenChange(false); }}>
            {t("debts.cancel")}
          </Button>
          <Button type="button" onClick={handleSettle} disabled={settleMutation.isPending || totalPending <= 0}>
            {settleMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
            {t("debts.settle")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
