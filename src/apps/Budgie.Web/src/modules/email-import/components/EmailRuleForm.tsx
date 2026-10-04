import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import client from "@/lib/api-client";
import { useAccounts } from "@/lib/hooks/useAccounts";
import { CurrencySelector } from "@/components/currency-selector";
import { enrichCurrencies } from "@/lib/currencies";
import { getRegexError } from "@/lib/regex-hint";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel, FieldError } from "@/components/ui/field";
import { CategoryCombobox } from "@/modules/categories/components/CategoryCombobox";
import { Loader2Icon, SaveIcon } from "lucide-react";
import { useCreateRule, useUpdateRule } from "../hooks/useEmailRules";
import { EmailImportApiError } from "../api";
import { GENERIC_REGEX_ADAPTER } from "../types";
import { AccountSelect, TypeSelect, type FieldPatterns } from "./rule-fields";
import type { EmailImportRule, EmailRuleBody, EmailRuleDraftBody } from "../types";
import { describedBy } from "@/lib/aria";

/** Prefill del ciclo aprender-del-uso (fila descartada → nueva regla). */
export interface RulePrefill {
  name?: string;
  fromAddress?: string;
  subjectContains?: string;
}

// Solo lo consume parsePatterns: se queda aquí (ver nota en rule-fields.tsx).
const EMPTY_PATTERNS: FieldPatterns = { amount: "", date: "", merchant: "", direction: "", symbol: "" };

const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Parsea FieldPatternsJson de forma segura (jsonb normaliza el string al
 * almacenar, así que solo se compara JSON parseado, nunca el string crudo).
 */
function parsePatterns(json: string | null): FieldPatterns {
  if (!json) return { ...EMPTY_PATTERNS };
  try {
    const parsed: unknown = JSON.parse(json);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return { ...EMPTY_PATTERNS };
    const obj = parsed as Record<string, unknown>;
    const str = (v: unknown) => (typeof v === "string" ? v : "");
    return {
      amount: str(obj.amount),
      date: str(obj.date),
      merchant: str(obj.merchant),
      direction: str(obj.direction),
      symbol: str(obj.symbol),
    };
  } catch {
    return { ...EMPTY_PATTERNS };
  }
}

function buildPatternsJson(p: FieldPatterns): string {
  return JSON.stringify({
    amount: p.amount.trim() || null,
    date: p.date.trim() || null,
    merchant: p.merchant.trim() || null,
    direction: p.direction.trim() || null,
    symbol: p.symbol.trim() || null,
  });
}

interface FormState {
  name: string;
  priority: string;
  isEnabled: boolean;
  accountId: string;
  categoryId: string | undefined;
  transactionType: string;
  currency: string;
  exchangeRate: string;
  fromAddress: string;
  subjectContains: string;
  bodyRegex: string;
  autoCommit: boolean;
}

interface FormErrors {
  name?: string;
  accountId?: string;
  patterns?: string;
}

/**
 * Aviso inline de regex inválida en el navegador. NO bloquea el guardado: la
 * sintaxis JS no es la de .NET y el servidor es el juez (ver el doc de
 * `getRegexError` en `lib/regex-hint.ts`). El `id` del párrafo lo referencia el
 * input con `aria-describedby`, para que el lector de pantalla lo anuncie.
 */
function RegexHint({ htmlFor, pattern }: { htmlFor: string; pattern: string }) {
  const { t } = useTranslation();
  const error = getRegexError(pattern);
  if (!error) return null;
  return (
    <p id={`${htmlFor}-hint`} className="text-xs text-destructive">
      {t("emailImport.rules.regexHint", { error })}
    </p>
  );
}

/**
 * Borrador para el probador: solo es utilizable si la forma es mínimamente
 * válida (nombre, cuenta GUID y patrón de importe) — el backend rechazaría el resto.
 */
function buildDraft(
  state: FormState,
  patterns: FieldPatterns,
  fallbackCurrency: string,
): EmailRuleDraftBody | null {
  if (!state.name.trim() || !GUID_RE.test(state.accountId) || !patterns.amount.trim()) return null;
  return {
    name: state.name.trim(),
    adapterKey: GENERIC_REGEX_ADAPTER,
    priority: Number(state.priority) || 0,
    accountId: state.accountId,
    categoryId: state.categoryId || null,
    transactionType: Number(state.transactionType),
    currency: state.currency || fallbackCurrency,
    exchangeRate: state.exchangeRate.trim() === "" ? null : Number(state.exchangeRate),
    fromAddress: state.fromAddress.trim() || null,
    subjectContains: state.subjectContains.trim() || null,
    bodyRegex: state.bodyRegex.trim() || null,
    fieldPatternsJson: buildPatternsJson(patterns),
    autoCommit: state.autoCommit,
  };
}

/**
 * Formulario crear/editar regla de importación (spec §8). Auto-commit se explica
 * con el umbral de la conexión (solo lectura aquí: el umbral lo guarda el backend).
 * El padre (RulesTab) lo monta con `key={formKey}` y lo remonta al cambiar de
 * destino para que el estado local se reinicie desde las props.
 */
export function EmailRuleForm({
  rule,
  prefill,
  threshold,
  onDraftChange,
  onSaved,
  onCancel,
}: {
  rule: EmailImportRule | null;
  prefill: RulePrefill | null;
  /** Umbral de auto-commit de la conexión (para la descripción del switch). */
  threshold: number | undefined;
  /** Publica el borrador actual para que el probador lo use (null si no es testeable). */
  onDraftChange?: (draft: EmailRuleDraftBody | null) => void;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const { t, i18n } = useTranslation();
  const { data: accounts } = useAccounts();

  const { data: settingsData } = useQuery({
    queryKey: ["settings"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/users/settings");
      if (error) throw error;
      return data;
    },
  });
  const currencies = useMemo(() => {
    const supported = settingsData?.supportedCurrencies ?? [];
    return enrichCurrencies(supported.length ? supported : [settingsData?.mainCurrency ?? "EUR"], i18n.language);
  }, [settingsData, i18n.language]);

  // Inicializador perezoso: parsePatterns solo se consume en el estado inicial.
  // DEPENDENCIA QUE NO HAY QUE ROMPER: el padre monta este formulario con
  // `key={formKey}` (RulesTab) y lo remonta al cambiar de regla, así que en cada
  // montaje `rule` es la regla correcta. Si esto se "optimiza" a un valor
  // computado en cada render o a un efecto, el formulario deja de reflejar la
  // regla que se está editando.
  const [patterns, setPatterns] = useState<FieldPatterns>(() =>
    parsePatterns(rule?.fieldPatternsJson ?? null),
  );
  const [state, setState] = useState<FormState>(() => ({
    name: rule?.name ?? prefill?.name ?? "",
    priority: String(rule?.priority ?? 0),
    isEnabled: rule?.isEnabled ?? true,
    accountId: rule?.accountId ?? "",
    categoryId: rule?.categoryId ?? undefined,
    transactionType: String(rule?.transactionType ?? 1),
    // Vacío = aún sin elegir; effectiveCurrency resuelve contra settings al construir.
    currency: rule?.currency ?? "",
    exchangeRate: rule?.exchangeRate != null ? String(rule.exchangeRate) : "",
    fromAddress: rule?.fromAddress ?? prefill?.fromAddress ?? "",
    subjectContains: rule?.subjectContains ?? prefill?.subjectContains ?? "",
    bodyRegex: rule?.bodyRegex ?? "",
    autoCommit: rule?.autoCommit ?? true,
  }));
  const [errors, setErrors] = useState<FormErrors>({});

  const createMutation = useCreateRule();
  const updateMutation = useUpdateRule();
  const saving = createMutation.isPending || updateMutation.isPending;

  // La moneda del formulario sigue la principal del usuario hasta que elija otra
  // (settings carga después del primer render; sin estado en effect).
  const effectiveCurrency = state.currency || settingsData?.mainCurrency || "EUR";

  const update = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    const next: FormState = { ...state, [key]: value };
    setState(next);
    onDraftChange?.(buildDraft(next, patterns, effectiveCurrency));
  };

  const updatePattern = (key: keyof FieldPatterns, value: string) => {
    const next: FieldPatterns = { ...patterns, [key]: value };
    setPatterns(next);
    onDraftChange?.(buildDraft(state, next, effectiveCurrency));
  };

  const validate = (): FormErrors => {
    const next: FormErrors = {};
    if (!state.name.trim()) next.name = t("emailImport.rules.nameRequired");
    if (!GUID_RE.test(state.accountId)) next.accountId = t("emailImport.rules.accountRequired");
    if (!patterns.amount.trim()) next.patterns = t("emailImport.rules.patternRequired");
    return next;
  };

  const handleSave = () => {
    const nextErrors = validate();
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    const body: EmailRuleBody = {
      name: state.name.trim(),
      adapterKey: GENERIC_REGEX_ADAPTER,
      priority: Math.max(0, Number(state.priority) || 0),
      isEnabled: state.isEnabled,
      accountId: state.accountId,
      categoryId: state.categoryId || null,
      transactionType: Number(state.transactionType),
      currency: effectiveCurrency,
      exchangeRate: state.exchangeRate.trim() === "" ? null : Number(state.exchangeRate),
      fromAddress: state.fromAddress.trim() || null,
      subjectContains: state.subjectContains.trim() || null,
      bodyRegex: state.bodyRegex.trim() || null,
      fieldPatternsJson: buildPatternsJson(patterns),
      autoCommit: state.autoCommit,
    };

    const onSuccess = () => {
      toast.success(t("emailImport.rules.saved"));
      onSaved();
    };
    const onError = (err: unknown) => {
      // El 400 EmailImport.InvalidRule viene sin indicar qué campo falló; el grupo
      // de patrones es el único con validación de regex del lado servidor, así que
      // el aviso se pinta ahí, junto a los campos de regex (el early feedback del
      // navegador puede no haberlo visto: sintaxis JS ≠ sintaxis .NET).
      if (err instanceof EmailImportApiError && err.code === "EmailImport.InvalidRule") {
        setErrors((prev) => ({ ...prev, patterns: t("emailImport.rules.regexHintServer") }));
      }
      toast.error(t("emailImport.rules.saveError"), {
        description: err instanceof Error ? err.message : undefined,
      });
    };

    if (rule) updateMutation.mutate({ id: rule.id, body }, { onSuccess, onError });
    else createMutation.mutate(body, { onSuccess, onError });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{rule ? t("emailImport.rules.editTitle") : t("emailImport.rules.createTitle")}</CardTitle>
        <CardDescription>{t("emailImport.rules.description")}</CardDescription>
      </CardHeader>
      <CardContent>
        <FieldGroup className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {/*
            PATRÓN NUEVO EN EL REPO (plan deuda §7.15.1): FieldError lleva `id` y el
            control lo referencia con `aria-describedby`, para que el lector de
            pantalla anuncie el error junto al campo. Es la primera vez que se hace
            aquí (antes había cero `aria-describedby` en la app): el resto de
            formularios deben copiar este patrón a partir de ahora.
          */}
          <Field data-invalid={!!errors.name}>
            <FieldLabel htmlFor="rule-name">{t("emailImport.rules.name")}</FieldLabel>
            <Input
              id="rule-name"
              value={state.name}
              aria-invalid={!!errors.name}
              aria-describedby={describedBy(errors.name ? "rule-name-error" : undefined)}
              placeholder={t("emailImport.rules.namePlaceholder")}
              onChange={(e) => update("name", e.target.value)}
            />
            {errors.name && <FieldError id="rule-name-error">{errors.name}</FieldError>}
          </Field>

          <Field>
            <FieldLabel>{t("emailImport.rules.adapter")}</FieldLabel>
            <div className="flex h-9 items-center">
              <Badge variant="secondary">{GENERIC_REGEX_ADAPTER}</Badge>
            </div>
            <p className="text-xs text-muted-foreground">{t("emailImport.rules.adapterDesc")}</p>
          </Field>

          <Field data-invalid={!!errors.accountId}>
            <FieldLabel htmlFor="rule-account">{t("emailImport.rules.account")}</FieldLabel>
            <AccountSelect
              id="rule-account"
              value={state.accountId}
              onChange={(id) => update("accountId", id)}
              accounts={accounts ?? []}
              describedBy={describedBy(errors.accountId ? "rule-account-error" : undefined)}
              invalid={!!errors.accountId}
            />
            {errors.accountId && <FieldError id="rule-account-error">{errors.accountId}</FieldError>}
          </Field>

          <Field>
            <FieldLabel htmlFor="rule-category">{t("emailImport.rules.category")}</FieldLabel>
            <CategoryCombobox
              id="rule-category"
              value={state.categoryId}
              onValueChange={(id) => update("categoryId", id)}
              placeholder={t("transactions.uncategorized")}
              showNoneOption
              type={Number(state.transactionType)}
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="rule-type">{t("emailImport.rules.type")}</FieldLabel>
            <TypeSelect
              id="rule-type"
              value={state.transactionType}
              onChange={(v) => update("transactionType", v)}
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="rule-currency">{t("emailImport.rules.currency")}</FieldLabel>
            <CurrencySelector
              id="rule-currency"
              value={effectiveCurrency}
              onValueChange={(code) => update("currency", code)}
              currencies={currencies}
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="rule-priority">{t("emailImport.rules.priority")}</FieldLabel>
            <Input
              id="rule-priority"
              type="number"
              min={0}
              step={1}
              value={state.priority}
              className="tabular-nums"
              onChange={(e) => update("priority", e.target.value)}
            />
            <p className="text-xs text-muted-foreground">{t("emailImport.rules.priorityDesc")}</p>
          </Field>

          <Field>
            <FieldLabel htmlFor="rule-rate">{t("emailImport.rules.exchangeRate")}</FieldLabel>
            <Input
              id="rule-rate"
              type="number"
              min={0}
              step="any"
              inputMode="decimal"
              value={state.exchangeRate}
              className="tabular-nums"
              placeholder={t("emailImport.rules.exchangeRatePlaceholder")}
              onChange={(e) => update("exchangeRate", e.target.value)}
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="rule-from">{t("emailImport.rules.fromAddress")}</FieldLabel>
            <Input
              id="rule-from"
              value={state.fromAddress}
              placeholder={t("emailImport.rules.fromAddressPlaceholder")}
              onChange={(e) => update("fromAddress", e.target.value)}
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="rule-subject">{t("emailImport.rules.subjectContains")}</FieldLabel>
            <Input
              id="rule-subject"
              value={state.subjectContains}
              placeholder={t("emailImport.rules.subjectPlaceholder")}
              onChange={(e) => update("subjectContains", e.target.value)}
            />
          </Field>

          <Field className="sm:col-span-2">
            <FieldLabel htmlFor="rule-body">{t("emailImport.rules.bodyRegex")}</FieldLabel>
            <Input
              id="rule-body"
              value={state.bodyRegex}
              aria-describedby={describedBy(
                getRegexError(state.bodyRegex) ? "rule-body-hint" : undefined,
              )}
              placeholder={t("emailImport.rules.bodyPlaceholder")}
              onChange={(e) => update("bodyRegex", e.target.value)}
            />
            <RegexHint htmlFor="rule-body" pattern={state.bodyRegex} />
          </Field>

          {/* Patrones de extracción (FieldPatternsJson) */}
          <div className="flex flex-col gap-2 sm:col-span-2">
            <p className="text-sm font-medium">{t("emailImport.rules.patterns.title")}</p>
            <FieldGroup className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field data-invalid={!!errors.patterns} className="sm:col-span-2">
                <FieldLabel htmlFor="rule-pattern-amount">
                  {t("emailImport.rules.patterns.amount")}
                </FieldLabel>
                <Input
                  id="rule-pattern-amount"
                  value={patterns.amount}
                  aria-invalid={!!errors.patterns}
                  aria-describedby={describedBy(
                    errors.patterns ? "rule-pattern-amount-error" : undefined,
                    getRegexError(patterns.amount) ? "rule-pattern-amount-hint" : undefined,
                  )}
                  placeholder={t("emailImport.rules.patterns.amountPlaceholder")}
                  onChange={(e) => updatePattern("amount", e.target.value)}
                />
                {errors.patterns && (
                  <FieldError id="rule-pattern-amount-error">{errors.patterns}</FieldError>
                )}
                <RegexHint htmlFor="rule-pattern-amount" pattern={patterns.amount} />
              </Field>
              <Field>
                <FieldLabel htmlFor="rule-pattern-date">
                  {t("emailImport.rules.patterns.date")}
                </FieldLabel>
                <Input
                  id="rule-pattern-date"
                  value={patterns.date}
                  aria-describedby={describedBy(
                    getRegexError(patterns.date) ? "rule-pattern-date-hint" : undefined,
                  )}
                  placeholder={t("emailImport.rules.patterns.datePlaceholder")}
                  onChange={(e) => updatePattern("date", e.target.value)}
                />
                <RegexHint htmlFor="rule-pattern-date" pattern={patterns.date} />
              </Field>
              <Field>
                <FieldLabel htmlFor="rule-pattern-merchant">
                  {t("emailImport.rules.patterns.merchant")}
                </FieldLabel>
                <Input
                  id="rule-pattern-merchant"
                  value={patterns.merchant}
                  aria-describedby={describedBy(
                    getRegexError(patterns.merchant) ? "rule-pattern-merchant-hint" : undefined,
                  )}
                  placeholder={t("emailImport.rules.patterns.merchantPlaceholder")}
                  onChange={(e) => updatePattern("merchant", e.target.value)}
                />
                <RegexHint htmlFor="rule-pattern-merchant" pattern={patterns.merchant} />
              </Field>
              <Field>
                <FieldLabel htmlFor="rule-pattern-direction">
                  {t("emailImport.rules.patterns.direction")}
                </FieldLabel>
                <Input
                  id="rule-pattern-direction"
                  value={patterns.direction}
                  aria-describedby={describedBy(
                    getRegexError(patterns.direction) ? "rule-pattern-direction-hint" : undefined,
                  )}
                  placeholder={t("emailImport.rules.patterns.directionPlaceholder")}
                  onChange={(e) => updatePattern("direction", e.target.value)}
                />
                <RegexHint htmlFor="rule-pattern-direction" pattern={patterns.direction} />
              </Field>
              <Field>
                <FieldLabel htmlFor="rule-pattern-symbol">
                  {t("emailImport.rules.patterns.symbol")}
                </FieldLabel>
                <Input
                  id="rule-pattern-symbol"
                  value={patterns.symbol}
                  aria-describedby={describedBy(
                    getRegexError(patterns.symbol) ? "rule-pattern-symbol-hint" : undefined,
                  )}
                  placeholder={t("emailImport.rules.patterns.symbolPlaceholder")}
                  onChange={(e) => updatePattern("symbol", e.target.value)}
                />
                <RegexHint htmlFor="rule-pattern-symbol" pattern={patterns.symbol} />
              </Field>
            </FieldGroup>
          </div>

          <Field className="sm:col-span-2">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <FieldLabel htmlFor="rule-enabled">{t("emailImport.rules.enabled")}</FieldLabel>
                <p className="text-xs text-muted-foreground">{t("emailImport.rules.enabledDesc")}</p>
              </div>
              <Switch
                id="rule-enabled"
                checked={state.isEnabled}
                onCheckedChange={(v) => update("isEnabled", v)}
                disabled={saving}
              />
            </div>
          </Field>

          <Field className="sm:col-span-2">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <FieldLabel htmlFor="rule-autocommit">{t("emailImport.rules.autoCommit")}</FieldLabel>
                <p className="text-xs text-muted-foreground">
                  {threshold != null
                    ? t("emailImport.rules.autoCommitDescWithThreshold", { threshold: String(threshold) })
                    : t("emailImport.rules.autoCommitDesc")}
                </p>
              </div>
              <Switch
                id="rule-autocommit"
                checked={state.autoCommit}
                onCheckedChange={(v) => update("autoCommit", v)}
                disabled={saving}
              />
            </div>
          </Field>
        </FieldGroup>

        <div className="mt-4 flex flex-wrap items-center gap-2">
          <Button type="button" onClick={handleSave} disabled={saving}>
            {saving ? (
              <Loader2Icon data-icon="inline-start" className="animate-spin" />
            ) : (
              <SaveIcon data-icon="inline-start" />
            )}
            {t("emailImport.rules.save")}
          </Button>
          <Button type="button" variant="ghost" onClick={onCancel} disabled={saving}>
            {t("emailImport.rules.cancel")}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
