import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAccounts } from "@/lib/hooks/useAccounts";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Field, FieldLabel } from "@/components/ui/field";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { ChevronLeftIcon, ChevronRightIcon, InboxIcon, ListChecksIcon, PlusIcon } from "lucide-react";
import { useEmailImportNotice } from "../hooks/useEmailImportNotice";
import { useEmailConnections } from "../hooks/useEmailConnections";
import { useEmailRules } from "../hooks/useEmailRules";
import { useApproveBatch, usePendingEmails, usePendingCount } from "../hooks/usePendingEmails";
import { PendingEmailStatus } from "../types";
import type { PendingEmailStatusValue, EmailImportRule, EmailRuleDraftBody, PendingEmailTransaction } from "../types";
import { PendingTransactionRow } from "../components/PendingTransactionRow";
import { EmailRuleList } from "../components/EmailRuleList";
import { EmailRuleForm } from "../components/EmailRuleForm";
import type { RulePrefill } from "../components/EmailRuleForm";
import { EmailRuleTester } from "../components/EmailRuleTester";
import { RuleDraftPrompt } from "../components/RuleDraftPrompt";

/**
 * Umbral que usa el botón "aprobar en lote".
 *
 * Deliberadamente SIN valor por defecto. Antes caía a 0.85 cuando la query de conexiones aún no
 * había resuelto, y como el botón sólo depende del contador de pendientes ya está visible en
 * ese instante: el lote se despachaba con 0.85 por muy editable que tuviera el umbral, y
 * aprobaba filas por debajo del valor que el usuario eligió. `undefined` significa "todavía no lo
 * sabemos", y el botón permanece inhábil hasta que la conexión responda. El umbral real lo
 * aplica el servidor, que es quien lo persiste.
 */

/** Referencia estable para "sin reglas": `[]` literal en JSX rompería React.memo. */
const NO_RULES: EmailImportRule[] = [];

/** Extrae el remitente del raw (cabeceras MIME) para prellenar la regla. */
function extractFromHeader(rawBody: string | null): string | null {
  if (!rawBody) return null;
  const match = /^(?:From|De):\s*(.+)$/im.exec(rawBody);
  return match?.[1]?.trim() ?? null;
}

/**
 * Petición de apertura del formulario de regla, originada FUERA de la pestaña
 * (el prompt "guardar como regla" de la bandeja de revisión). Vive en la página
 * porque nace ahí; `RulesTab` la consume y es la dueña del estado del formulario.
 *
 * Sin un contador de "nonce": la comparación que la consume es por REFERENCIA
 * (`openRequest !== prevOpenRequest`) y cada apertura construye un objeto nuevo,
 * así que ya nunca coinciden dos peticiones distintas. El contador que había era
 * ruido, y su comentario describía un efecto que el código no tenía.
 */
interface RuleFormRequest {
  rule: EmailImportRule | null;
  prefill: RulePrefill | null;
}

/**
 * Pestaña "Reglas": formulario (crear/editar), tabla y probador.
 *
 * Es dueña de `ruleDraft` y `formKey` (plan deuda §7.17.2): el borrador del
 * formulario cambia en CADA tecla y, si viviera en la página, re-renderizaría la
 * bandeja entera (las N filas pendientes) con cada pulsación. Con el estado aquí,
 * escribir en el formulario solo re-renderiza esta pestaña.
 */
function RulesTab({
  rules,
  rulesLoading,
  threshold,
  openRequest,
}: {
  rules: EmailImportRule[];
  rulesLoading: boolean;
  /** Umbral de auto-commit de la conexión (descripción del switch de auto-commit). */
  threshold: number | undefined;
  openRequest: RuleFormRequest | null;
}) {
  const { t } = useTranslation();

  const [formOpen, setFormOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<EmailImportRule | null>(null);
  const [formPrefill, setFormPrefill] = useState<RulePrefill | null>(null);
  const [formKey, setFormKey] = useState(0);
  const [ruleDraft, setRuleDraft] = useState<EmailRuleDraftBody | null>(null);

  // Ajuste de estado en render (patrón de React: "storing information from previous
  // renders"), NO un useEffect: `react-hooks/set-state-in-effect` prohíbe el
  // setState síncrono en efectos, y aquí no hay nada que sincronizar con el mundo
  // externo — la prop ya llegó. El bloque es condicional (compara con el valor
  // previo), que es exactamente lo que permite `react-hooks/set-state-in-render`.
  const [prevOpenRequest, setPrevOpenRequest] = useState<RuleFormRequest | null>(null);
  if (openRequest !== null && openRequest !== prevOpenRequest) {
    setPrevOpenRequest(openRequest);
    setEditingRule(openRequest.rule);
    setFormPrefill(openRequest.prefill);
    setRuleDraft(null);
    setFormOpen(true);
    setFormKey((k) => k + 1);
  }

  const openCreateForm = () => {
    setEditingRule(null);
    setFormPrefill(null);
    setRuleDraft(null);
    setFormOpen(true);
    setFormKey((k) => k + 1);
  };

  // useCallback + el `memo` de EmailRuleList: sin esto la referencia cambia en cada render y la
  // tabla se repinta en cada tecla del formulario (plan deuda §7.17.2).
  const openEditForm = useCallback((rule: EmailImportRule) => {
    setEditingRule(rule);
    setFormPrefill(null);
    setRuleDraft(null);
    setFormOpen(true);
    setFormKey((k) => k + 1);
  }, []);

  const closeForm = () => {
    setFormOpen(false);
    setEditingRule(null);
    setFormPrefill(null);
    setRuleDraft(null);
  };

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">{t("emailImport.rules.tabDescription")}</p>
        <Button type="button" onClick={openCreateForm}>
          <PlusIcon data-icon="inline-start" />
          {t("emailImport.rules.create")}
        </Button>
      </div>

      {formOpen && (
        <EmailRuleForm
          key={formKey}
          rule={editingRule}
          prefill={formPrefill}
          threshold={threshold}
          onDraftChange={setRuleDraft}
          onSaved={closeForm}
          onCancel={closeForm}
        />
      )}

      <EmailRuleList rules={rules} isLoading={rulesLoading} onEdit={openEditForm} />

      <EmailRuleTester rules={rules} draft={ruleDraft} />
    </>
  );
}

/**
 * /email-import — bandeja de revisión + reglas de importación (spec §8).
 * Una sola página con dos pestañas; la conexión vive en Ajustes.
 */
export default function EmailImportInbox() {
  const { t } = useTranslation();
  useEmailImportNotice();

  const [tab, setTab] = useState<string>("pending");
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<PendingEmailStatusValue>(PendingEmailStatus.Pending);
  const [batchOpen, setBatchOpen] = useState(false);

  // Flujo aprender-del-uso: descartar → ¿guardar como regla? → RulesTab abre el form.
  const [draftCandidate, setDraftCandidate] = useState<PendingEmailTransaction | null>(null);
  const [ruleFormRequest, setRuleFormRequest] = useState<RuleFormRequest | null>(null);

  const { data: accounts, isFetching: accountsFetching } = useAccounts();
  const { data: connections } = useEmailConnections();
  const { data: rules, isLoading: rulesLoading } = useEmailRules();
  const { data: count } = usePendingCount();
  const { data: inbox, isLoading: inboxLoading, isFetching: inboxFetching } = usePendingEmails(
    page,
    status,
  );
  const approveBatchMutation = useApproveBatch();

  const connection = connections?.[0];
  const threshold = connection?.autoCommitMinConfidence;
  const pageSize = inbox?.pageSize ?? 50;
  const totalPages = Math.max(1, Math.ceil((inbox?.total ?? 0) / pageSize));
  const statusItems = [
    { label: t("emailImport.pending.filterPending"), value: String(PendingEmailStatus.Pending) },
    { label: t("emailImport.pending.filterFailed"), value: String(PendingEmailStatus.Failed) },
  ];

  const handleSaveAsRule = () => {
    const candidate = draftCandidate;
    setDraftCandidate(null);
    if (!candidate) return;
    setRuleFormRequest({
      rule: null,
      prefill: {
        name: candidate.subject ?? "",
        fromAddress: extractFromHeader(candidate.rawBody) ?? undefined,
        subjectContains: candidate.subject ?? undefined,
      },
    });
    setTab("rules");
  };

  const handleConfirmBatch = () => {
    // Doble barrera: el botón está deshabilitado sin umbral, pero el handler también se
    // abstiene. Un diálogo que quedara abierto al cambiar la conexión podría despachar con
    // `undefined`, que el servidor leería como "sin mínimo".
    if (threshold === undefined) return;

    approveBatchMutation.mutate(
      { minConfidence: threshold, take: 100 },
      {
        onSuccess: (result) => {
          setBatchOpen(false);
          const failures =
            result.failures.length > 0
              ? " " + t("emailImport.pending.approveBatchFailures", {
                  failures: result.failures.slice(0, 3).join("; "),
                })
              : "";
          toast.success(
            t("emailImport.pending.approveBatchResult", {
              approved: String(result.approved),
              failed: String(result.failed),
            }) + failures,
          );
        },
        onError: (err) => {
          setBatchOpen(false);
          toast.error(t("emailImport.pending.approveBatchError"), {
            description: err instanceof Error ? err.message : undefined,
          });
        },
      },
    );
  };

  return (
    <div className="w-full space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">{t("emailImport.title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("emailImport.description")}</p>
      </div>

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="pending">
            <InboxIcon data-icon="inline-start" />
            {t("emailImport.tabs.pending")}
            {count != null && ` (${count})`}
          </TabsTrigger>
          <TabsTrigger value="rules">
            <ListChecksIcon data-icon="inline-start" />
            {t("emailImport.tabs.rules")}
          </TabsTrigger>
        </TabsList>

        {/* ── Pendientes ─────────────────────────────────────────────────── */}
        <TabsContent value="pending" className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            {/* Filtro de estado: etiqueta accesible asociada (slice 15.4). Antes el
                Select no tenía ningún <label> en absoluto. Patrón de
                CreateAccountForm: htmlFor + id en el SelectTrigger. */}
            <Field>
              <FieldLabel htmlFor="inbox-status" className="sr-only">
                {t("emailImport.pending.statusLabel")}
              </FieldLabel>
              <Select
                items={statusItems}
                value={String(status)}
                onValueChange={(v) => {
                  if (v !== null) {
                    setStatus(Number(v) as PendingEmailStatusValue);
                    setPage(1);
                  }
                }}
              >
                <SelectTrigger id="inbox-status" className="w-44">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {statusItems.map((item) => (
                    <SelectItem key={item.value} value={item.value}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>

            {status === PendingEmailStatus.Pending && (count ?? 0) > 0 && (
              <Button
                type="button"
                variant="outline"
                onClick={() => setBatchOpen(true)}
                // Sin umbral conocido no se puede aprobar en lote: el mínimo de confianza es
                // una decisión del usuario y mandarlo con un valor por defecto aprobaría filas
                // que él eligió revisar. El botón aparece pero no hace nada hasta que la
                // conexión responde.
                disabled={threshold === undefined}
                title={threshold === undefined ? t("emailImport.pending.approveBatchPending") : undefined}
              >
                <ListChecksIcon data-icon="inline-start" />
                {t("emailImport.pending.approveBatch")}
              </Button>
            )}
          </div>

          {inboxLoading ? (
            // role="status": anuncia la carga de la bandeja (slice 15.5). Skeleton
            // hace spread de props; la primitiva no se toca.
            <div role="status" className="flex flex-col gap-3">
              <span className="sr-only">{t("emailImport.loading.pending")}</span>
              <Skeleton className="h-40 w-full" />
              <Skeleton className="h-40 w-full" />
            </div>
          ) : inbox && inbox.items.length > 0 ? (
            <div className="flex flex-col gap-3">
              {inbox.items.map((row) => (
                <PendingTransactionRow
                  key={row.id}
                  row={row}
                  accounts={accounts}
                  accountsLoading={accountsFetching && !accounts}
                  refetching={inboxFetching}
                  onDismissed={(dismissed) => setDraftCandidate(dismissed)}
                />
              ))}
            </div>
          ) : (
            <Alert>
              <AlertTitle>{t("emailImport.pending.empty")}</AlertTitle>
              <AlertDescription>{t("emailImport.pending.emptyDesc")}</AlertDescription>
            </Alert>
          )}

          {totalPages > 1 && (
            <div className="flex items-center justify-between gap-2">
              {/* size="lg" (h-9, 36 px) y no "sm" (28 px): son el objetivo táctil de
                  la página principal de revisión (plan deuda §7.18.4). El techo del
                  design system (lg = 36 px) sigue por debajo del objetivo de 44 px —
                  añadir un tamaño xl sería una decisión de plataforma, no de esta
                  página. */}
              <Button
                type="button"
                variant="outline"
                size="lg"
                disabled={page <= 1 || inboxFetching}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <ChevronLeftIcon data-icon="inline-start" />
                {t("emailImport.pending.prev")}
              </Button>
              <span className="text-sm text-muted-foreground tabular-nums">
                {t("emailImport.pending.page", { page: String(page), total: String(totalPages) })}
              </span>
              <Button
                type="button"
                variant="outline"
                size="lg"
                disabled={page >= totalPages || inboxFetching}
                onClick={() => setPage((p) => p + 1)}
              >
                {t("emailImport.pending.next")}
                <ChevronRightIcon data-icon="inline-end" />
              </Button>
            </div>
          )}
        </TabsContent>

        {/* ── Reglas ─────────────────────────────────────────────────────── */}
        {/* `keepMounted` en la pestaña de reglas, y no en la de pendientes, por un motivo concreto:
            la petición "guardar como regla" se consume dentro de RulesTab (su memoria es local y se
            pierde al desmontar). Base UI desmonta el panel inactivo por defecto, así que al
            volver a Reglas RulesTab se remontaba con `prevOpenRequest = null`, el guard volvía a
            ser cierto y el formulario se REABRÍA solo con el prefill viejo, aunque ya estuviera
            cerrado. Montarlo una vez y conservarlo hace que "ya lo he procesado" sobreviva al
            cambio de pestaña, y de paso preserva el borrador al alternar entre pestañas.

            El panel de pendientes sí se desmonta: no guarda estado de una sola vez y sus filas
            son la lista más pesada de la página. */}
        <TabsContent value="rules" className="flex flex-col gap-4" keepMounted>
          <RulesTab
            rules={rules ?? NO_RULES}
            rulesLoading={rulesLoading}
            threshold={connection?.autoCommitMinConfidence}
            openRequest={ruleFormRequest}
          />
        </TabsContent>
      </Tabs>

      {/* Descartar → ¿guardar como regla? */}
      <RuleDraftPrompt
        open={draftCandidate !== null}
        subject={draftCandidate?.subject ?? null}
        fromAddress={extractFromHeader(draftCandidate?.rawBody ?? null)}
        onSaveAsRule={handleSaveAsRule}
        onDiscard={() => setDraftCandidate(null)}
      />

      {/* Aprobar en lote */}
      <AlertDialog open={batchOpen} onOpenChange={setBatchOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("emailImport.pending.approveBatchTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("emailImport.pending.approveBatchDesc", { threshold: String(threshold) })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            {/* Sin disabled en AlertDialogAction/Cancel: la primitiva es un Button
                normal que CIERRA el diálogo al hacer clic, así que un
                disabled={isPending} sería feedback muerto —el estado nunca se ve—
                y en Cancel además impediría cancelar. El toast de resultado tras la
                mutación es el feedback real (plan deuda §7.18.1). DeleteRuleDialog
                sí empareja disabled+Loader2, pero porque usa Dialog y no
                AlertDialog: no copiar ese patrón sin cambiar de primitiva. */}
            <AlertDialogCancel>{t("emailImport.rules.cancel")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmBatch}>
              {t("emailImport.pending.approveBatchConfirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
