import { useCallback, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
import { Label } from "@/components/ui/label";
import {
  ToggleGroup,
  ToggleGroupItem,
} from "@/components/ui/toggle-group";
import { X, UserPlus, Tag, CheckCircle, Clock, SlidersHorizontal } from "lucide-react";
import { formatCurrency } from "@/lib/format";
import type { TransactionParticipantForm } from "../schemas";
import type { components } from "@/lib/api-types";

type AccountResponse = components["schemas"]["AccountResponse"];
type PersonResponse = components["schemas"]["PersonResponse"];

type SplitMode = "exact" | "equal" | "percent";

interface ParticipantsSectionProps {
  participants: TransactionParticipantForm[];
  onChange: (participants: TransactionParticipantForm[]) => void;
  accounts: AccountResponse[];
  currency?: string;
  defaultAccountId?: string;
  transactionType?: string;
  totalAmount?: number;
}

const conditions = [
  { value: "tag" as const, labelKey: "transactions.conditionTag", icon: Tag, color: "text-muted-foreground" },
  { value: "cash" as const, labelKey: "transactions.conditionCash", icon: CheckCircle, color: "text-emerald-500" },
  { value: "credit" as const, labelKey: "transactions.conditionCredit", icon: Clock, color: "text-amber-500" },
];

function round2(n: number): number {
  return Math.round(n * 100) / 100;
}

export function ParticipantsSection({ participants, onChange, accounts, currency = "USD", defaultAccountId, transactionType, totalAmount = 0 }: ParticipantsSectionProps) {
  const { t } = useTranslation();
  const [splitMode, setSplitMode] = useState<SplitMode>("exact");

  const { data: persons } = useQuery({
    queryKey: ["persons"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/persons");
      if (error) throw error;
      return data;
    },
  });

  const sortedPersons = persons ?? [];
  const availablePersons = sortedPersons.filter(
    (p) => !participants.some((pp) => pp.personId === p.id)
  );
  const sortedAccounts = accounts;

  const addPerson = useCallback((personId: string) => {
    const person = (persons ?? []).find((p) => p.id === personId);
    if (!person) return;
    const next = [
      ...participants,
      { personId: person.id, personName: person.name, contributedAmount: "0", paymentCondition: "tag" as const },
    ];
    if (splitMode === "exact") {
      onChange(next);
      return;
    }
    // Re-apply split across the new participant set
    const active = next.filter((p) => p.paymentCondition !== "tag");
    if (active.length === 0) { onChange(next); return; }
    if (splitMode === "equal") {
      const each = round2(totalAmount / active.length);
      onChange(next.map((p) => active.includes(p) ? { ...p, contributedAmount: String(each) } : p));
    } else {
      const percent = 100 / active.length;
      onChange(next.map((p) => active.includes(p)
        ? { ...p, contributedAmount: String(round2(totalAmount * percent / 100)) }
        : p));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [persons, participants, onChange, splitMode, totalAmount]);

  function updatePerson(index: number, updates: Partial<TransactionParticipantForm>) {
    onChange(participants.map((p, i) => (i === index ? { ...p, ...updates } : p)));
  }

  function removePerson(index: number) {
    onChange(participants.filter((_, i) => i !== index));
  }

  function applySplit(mode: SplitMode, total: number, list: TransactionParticipantForm[]) {
    const active = list.filter((p) => p.paymentCondition !== "tag");
    if (active.length === 0) return;

    if (mode === "equal") {
      const each = round2(total / active.length);
      onChange(list.map((p) => active.includes(p) ? { ...p, contributedAmount: String(each) } : p));
    } else if (mode === "percent") {
      const percent = 100 / active.length;
      onChange(list.map((p) => active.includes(p)
        ? { ...p, contributedAmount: String(round2(total * percent / 100)) }
        : p));
    }
  }

  const switchSplitMode = (mode: SplitMode) => {
    setSplitMode(mode);
    if (mode !== "exact") applySplit(mode, totalAmount, participants);
  };

  const summary = useMemo(() => {
    const total = participants.reduce((s, p) => s + (parseFloat(p.contributedAmount) || 0), 0);
    return {
      allocated: total,
      remaining: round2(totalAmount - total),
      count: participants.filter((p) => p.paymentCondition !== "tag").length,
    };
  }, [participants, totalAmount]);

  const showSplitControls = participants.length >= 2 && totalAmount > 0;

  return (
    <div className="rounded-lg border bg-muted/20 p-3 flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Label className="text-sm font-medium">{t("transactions.participants")}</Label>
        <div className="flex flex-wrap items-center gap-2">
          {showSplitControls && (
            <ToggleGroup
              value={[splitMode]}
              onValueChange={(v) => v && v[0] && switchSplitMode(v[0] as SplitMode)}
              size="sm"
            >
              <ToggleGroupItem value="exact">{t("transactions.splitExact")}</ToggleGroupItem>
              <ToggleGroupItem value="equal">1/{t("transactions.splitN")}</ToggleGroupItem>
              <ToggleGroupItem value="percent">%</ToggleGroupItem>
            </ToggleGroup>
          )}
          {availablePersons.length > 0 && (
            <Combobox<PersonResponse>
              items={availablePersons}
              itemToStringValue={(p: PersonResponse) => p.name}
              itemToStringLabel={(p: PersonResponse) => p.name}
              onValueChange={(p: PersonResponse | null) => p && addPerson(p.id)}
            >
              <ComboboxTrigger
                render={
                  <Button type="button" variant="outline" size="sm" className="h-7 text-xs gap-1">
                    <UserPlus className="size-3" />
                    {t("transactions.addParticipant")}
                  </Button>
                }
              >
                <ComboboxValue />
              </ComboboxTrigger>
              <ComboboxContent className="w-56" align="end">
                <ComboboxInput showTrigger={false} placeholder={t("transactions.searchPersons")} />
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
          )}
        </div>
      </div>

      {showSplitControls && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-card px-3 py-2 text-xs">
          <span className="inline-flex items-center gap-1.5 text-muted-foreground">
            <SlidersHorizontal className="size-3.5" />
            {t("transactions.splitSummary", { allocated: formatCurrency(summary.allocated, currency), total: formatCurrency(totalAmount, currency) })}
          </span>
          <span className={summary.remaining >= 0 ? "text-muted-foreground" : "font-semibold text-destructive"}>
            {summary.remaining >= 0
              ? t("transactions.splitRemaining", { amount: formatCurrency(summary.remaining, currency) })
              : t("transactions.splitOver", { amount: formatCurrency(Math.abs(summary.remaining), currency) })}
          </span>
        </div>
      )}

      {participants.length === 0 ? (
        <p className="text-xs text-muted-foreground py-1">{t("transactions.noParticipants")}</p>
      ) : (
        <div className="flex flex-col gap-2">
          {participants.map((p, i) => (
            <div key={p.personId || i} className="rounded-md border bg-card p-2 flex flex-col gap-2">
              <div className="flex items-center gap-2">
                <span className="min-w-20 text-sm font-medium truncate">
                  {p.personName || (persons ?? []).find((pp) => pp.id === p.personId)?.name || p.personId.slice(0, 8)}
                </span>
                {p.paymentCondition !== "tag" && (
                  <div className="relative flex-1">
                    <Input
                      type="number"
                      step="0.01"
                      min="0"
                      value={p.contributedAmount}
                      onChange={(e) => updatePerson(i, { contributedAmount: e.target.value })}
                      className="h-8 text-xs pl-6"
                      placeholder="0.00"
                    />
                    <span className="absolute left-2 top-1/2 -translate-y-1/2 text-xs text-muted-foreground">
                      {formatCurrency(0, currency).replace(/[\d.,\s]/g, "").trim() || currency}
                    </span>
                  </div>
                )}
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-xs"
                  className="ml-auto shrink-0"
                  onClick={() => removePerson(i)}
                >
                  <X />
                </Button>
              </div>

              <div className="flex items-center gap-1 pl-1">
                {conditions
                  .filter((c) => transactionType !== "0" || c.value === "tag")
                  .map((c) => {
                  const Icon = c.icon;
                  const isActive = p.paymentCondition === c.value;
                  return (
                    <button
                      key={c.value}
                      type="button"
                      onClick={() => updatePerson(i, {
                        paymentCondition: c.value,
                        destinationAccountId: c.value === "cash"
                          ? (p.destinationAccountId || defaultAccountId)
                          : undefined,
                      })}
                      className={`inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs transition-colors ${
                        isActive
                          ? `${c.color} bg-muted font-semibold`
                          : "text-muted-foreground hover:text-foreground"
                      }`}
                    >
                      <Icon className="size-3" />
                      {t(c.labelKey)}
                    </button>
                  );
                })}
              </div>

              {p.paymentCondition === "cash" && (
                <div className="flex items-center gap-2">
                  <span className="text-xs text-muted-foreground shrink-0">{t("transactions.toAccount")}</span>
                  <Combobox<AccountResponse>
                    items={sortedAccounts}
                    itemToStringValue={(a: AccountResponse) => a.name}
                    itemToStringLabel={(a: AccountResponse) => a.name}
                    value={sortedAccounts.find((a: AccountResponse) => a.id === p.destinationAccountId) ?? null}
                    onValueChange={(a: AccountResponse | null) => updatePerson(i, { destinationAccountId: a?.id ?? undefined })}
                  >
                    <ComboboxInput
                      placeholder={t("transactions.selectAccount")}
                      className="flex-1"
                    />
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
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
