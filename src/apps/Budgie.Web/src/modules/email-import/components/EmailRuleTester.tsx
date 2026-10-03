import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ChevronDownIcon, FlaskConicalIcon, Loader2Icon } from "lucide-react";
import { usePreviewRule } from "../hooks/useEmailRules";
import { ConfidenceBadge } from "./ConfidenceBadge";
import { FieldConfidenceBadges } from "./FieldConfidenceBadges";
import type { EmailImportRule, EmailRuleDraftBody, PreviewRuleResponse } from "../types";

const DRAFT_VALUE = "__draft__";

/**
 * Probador de reglas (spec §7): pega el correo y comprueba qué extraería la regla
 * seleccionada —guardada o el borrador del formulario— sin escribir nada en la
 * base de datos. Devuelve el texto plano (lo que realmente leen las regex), los
 * campos, la confianza por campo y la explicación literal del backend.
 */
export function EmailRuleTester({
  rules,
  draft,
}: {
  rules: EmailImportRule[];
  /** Borrador actual del formulario; null = el formulario aún no es testeable. */
  draft: EmailRuleDraftBody | null;
}) {
  const { t } = useTranslation();

  const [selected, setSelected] = useState<string | null>(() =>
    draft != null ? DRAFT_VALUE : null,
  );
  const [rawEmail, setRawEmail] = useState("");
  const [fromAddress, setFromAddress] = useState("");
  const [subject, setSubject] = useState("");
  const [result, setResult] = useState<PreviewRuleResponse | null>(null);

  const previewMutation = usePreviewRule();

  const usingDraft = selected === DRAFT_VALUE;
  const canRun = !!rawEmail.trim() && (usingDraft ? draft != null : !!selected);

  const ruleItems = useMemo(() => {
    const draftItem = { label: t("emailImport.rules.tester.draft"), value: DRAFT_VALUE };
    const savedItems = rules.map((r) => ({ label: r.name, value: r.id }));
    return draft != null ? [draftItem, ...savedItems] : savedItems;
  }, [rules, draft, t]);

  const handleRun = () => {
    if (!canRun) return;
    previewMutation.mutate(
      {
        ruleId: usingDraft ? null : selected,
        draft: usingDraft ? draft : null,
        rawEmail,
        fromAddress: fromAddress.trim() || null,
        subject: subject.trim() || null,
      },
      {
        onSuccess: (data) => setResult(data),
        onError: (err) =>
          toast.error(t("emailImport.rules.tester.error"), {
            description: err instanceof Error ? err.message : undefined,
          }),
      },
    );
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("emailImport.rules.tester.title")}</CardTitle>
        <CardDescription>{t("emailImport.rules.tester.description")}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <FieldGroup className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field className="sm:col-span-2">
            <FieldLabel>{t("emailImport.rules.tester.rule")}</FieldLabel>
            <Select
              items={ruleItems}
              value={selected ?? ""}
              onValueChange={(v) => {
                if (v !== null) setSelected(String(v));
              }}
            >
              <SelectTrigger className="w-full">
                <SelectValue placeholder={t("emailImport.rules.tester.none")} />
              </SelectTrigger>
              <SelectContent>
                {ruleItems.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {item.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>

          <Field>
            <FieldLabel htmlFor="tester-from">{t("emailImport.rules.tester.fromAddress")}</FieldLabel>
            <Input
              id="tester-from"
              value={fromAddress}
              onChange={(e) => setFromAddress(e.target.value)}
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="tester-subject">{t("emailImport.rules.tester.subject")}</FieldLabel>
            <Input
              id="tester-subject"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
            />
          </Field>

          <Field className="sm:col-span-2">
            <FieldLabel htmlFor="tester-raw">{t("emailImport.rules.tester.rawEmail")}</FieldLabel>
            <Textarea
              id="tester-raw"
              rows={8}
              value={rawEmail}
              placeholder={t("emailImport.rules.tester.rawEmailPlaceholder")}
              onChange={(e) => setRawEmail(e.target.value)}
            />
          </Field>
        </FieldGroup>

        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" onClick={handleRun} disabled={!canRun || previewMutation.isPending}>
            {previewMutation.isPending ? (
              <Loader2Icon data-icon="inline-start" className="animate-spin" />
            ) : (
              <FlaskConicalIcon data-icon="inline-start" />
            )}
            {previewMutation.isPending
              ? t("emailImport.rules.tester.running")
              : t("emailImport.rules.tester.run")}
          </Button>
          {!canRun && (
            <p className="text-xs text-muted-foreground">{t("emailImport.rules.tester.needInput")}</p>
          )}
        </div>

        {result && <TesterResult result={result} />}
      </CardContent>
    </Card>
  );
}

function TesterResult({ result }: { result: PreviewRuleResponse }) {
  const { t } = useTranslation();
  const fieldEntries = Object.entries(result.fields ?? {});

  return (
    <div className="flex flex-col gap-3">
      <Separator />

      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">{t("emailImport.rules.tester.confidence")}</span>
        <ConfidenceBadge confidence={result.confidence} />
      </div>

      <FieldConfidenceBadges scores={result.fieldConfidence ?? null} />

      {result.skipReason && (
        <Alert variant="destructive">
          <AlertTitle>{t("emailImport.rules.tester.skipReason")}</AlertTitle>
          <AlertDescription>{skipReasonText(result.skipReason, t)}</AlertDescription>
        </Alert>
      )}

      {fieldEntries.length > 0 ? (
        <div className="flex flex-col gap-1">
          <p className="text-sm font-medium">{t("emailImport.rules.tester.fields")}</p>
          <ul className="flex flex-col gap-0.5 text-sm">
            {fieldEntries.map(([key, value]) => (
              <li key={key} className="flex items-baseline gap-2">
                <Badge variant="outline" className="shrink-0">
                  {t(`emailImport.confidence.fields.${key}`, key)}
                </Badge>
                <span className="tabular-nums break-all">{value ?? "—"}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t("emailImport.rules.tester.noFields")}</p>
      )}

      {result.explanation && result.explanation.length > 0 && (
        <div className="flex flex-col gap-1">
          <p className="text-sm font-medium">{t("emailImport.rules.tester.explanation")}</p>
          {/* Explicación literal del backend, sin reinterpretar. */}
          <ul className="list-disc space-y-0.5 pl-4 text-xs text-muted-foreground">
            {result.explanation.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </div>
      )}

      <Collapsible>
        <CollapsibleTrigger
          render={
            <Button type="button" variant="ghost" size="sm" className="text-muted-foreground" />
          }
        >
          <ChevronDownIcon data-icon="inline-start" />
          {t("emailImport.rules.tester.plainText")}
        </CollapsibleTrigger>
        <CollapsibleContent>
          <pre className="mt-1 max-h-64 overflow-auto rounded-md bg-muted p-3 text-xs whitespace-pre-wrap break-words">
            {result.plainText || "—"}
          </pre>
        </CollapsibleContent>
      </Collapsible>
    </div>
  );
}

/** Motivo de salto del probador: códigos conocidos a i18n, resto con fallback. */
function skipReasonText(reason: string, t: TFunction): string {
  switch (reason) {
    case "amount-pattern-missing":
      return t("emailImport.skipReasons.amountPatternMissing");
    case "amount-not-found":
      return t("emailImport.skipReasons.amountNotFound");
    default:
      return t("emailImport.skipReasons.unknown", { reason });
  }
}
