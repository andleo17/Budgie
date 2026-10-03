import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAccounts } from "@/lib/hooks/useAccounts";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
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

const DEFAULT_THRESHOLD = 0.85;

/** Extrae el remitente del raw (cabeceras MIME) para prellenar la regla. */
function extractFromHeader(rawBody: string | null): string | null {
  if (!rawBody) return null;
  const match = /^(?:From|De):\s*(.+)$/im.exec(rawBody);
  return match?.[1]?.trim() ?? null;
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

  // Flujo aprender-del-uso: descartar → ¿guardar como regla? → form prellenado.
  const [draftCandidate, setDraftCandidate] = useState<PendingEmailTransaction | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<EmailImportRule | null>(null);
  const [formPrefill, setFormPrefill] = useState<RulePrefill | null>(null);
  const [formKey, setFormKey] = useState(0);
  const [ruleDraft, setRuleDraft] = useState<EmailRuleDraftBody | null>(null);

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
  const threshold = connection?.autoCommitMinConfidence ?? DEFAULT_THRESHOLD;
  const pageSize = inbox?.pageSize ?? 50;
  const totalPages = Math.max(1, Math.ceil((inbox?.total ?? 0) / pageSize));
  const statusItems = useMemo(
    () => [
      { label: t("emailImport.pending.filterPending"), value: String(PendingEmailStatus.Pending) },
      { label: t("emailImport.pending.filterFailed"), value: String(PendingEmailStatus.Failed) },
    ],
    [t],
  );

  const openCreateForm = () => {
    setEditingRule(null);
    setFormPrefill(null);
    setRuleDraft(null);
    setFormOpen(true);
    setFormKey((k) => k + 1);
  };

  const openEditForm = (rule: EmailImportRule) => {
    setEditingRule(rule);
    setFormPrefill(null);
    setRuleDraft(null);
    setFormOpen(true);
    setFormKey((k) => k + 1);
  };

  const closeForm = () => {
    setFormOpen(false);
    setEditingRule(null);
    setFormPrefill(null);
    setRuleDraft(null);
  };

  const handleSaveAsRule = () => {
    const candidate = draftCandidate;
    setDraftCandidate(null);
    if (!candidate) return;
    setEditingRule(null);
    setFormPrefill({
      name: candidate.subject ?? "",
      fromAddress: extractFromHeader(candidate.rawBody) ?? undefined,
      subjectContains: candidate.subject ?? undefined,
    });
    setRuleDraft(null);
    setFormOpen(true);
    setFormKey((k) => k + 1);
    setTab("rules");
  };

  const handleConfirmBatch = () => {
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
              <SelectTrigger className="w-44">
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

            {status === PendingEmailStatus.Pending && (count ?? 0) > 0 && (
              <Button type="button" variant="outline" onClick={() => setBatchOpen(true)}>
                <ListChecksIcon data-icon="inline-start" />
                {t("emailImport.pending.approveBatch")}
              </Button>
            )}
          </div>

          {inboxLoading ? (
            <div className="flex flex-col gap-3">
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
              <Button
                type="button"
                variant="outline"
                size="sm"
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
                size="sm"
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
        <TabsContent value="rules" className="flex flex-col gap-4">
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
              threshold={connection?.autoCommitMinConfidence}
              onDraftChange={setRuleDraft}
              onSaved={closeForm}
              onCancel={closeForm}
            />
          )}

          <EmailRuleList
            rules={rules ?? []}
            isLoading={rulesLoading}
            onEdit={openEditForm}
          />

          <EmailRuleTester rules={rules ?? []} draft={ruleDraft} />
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
            <AlertDialogCancel disabled={approveBatchMutation.isPending}>
              {t("emailImport.rules.cancel")}
            </AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmBatch} disabled={approveBatchMutation.isPending}>
              {t("emailImport.pending.approveBatchConfirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
