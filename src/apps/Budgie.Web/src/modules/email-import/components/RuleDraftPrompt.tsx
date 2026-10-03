import { useTranslation } from "react-i18next";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { FilePlus2Icon } from "lucide-react";

/**
 * Tras descartar un correo: "¿Guardar como regla?" (spec §8.1, ciclo
 * aprender-del-uso). Al aceptar, el padre abre EmailRuleForm prellenado con el
 * remitente y el asunto capturados de la fila descartada.
 */
export function RuleDraftPrompt({
  open,
  subject,
  fromAddress,
  onSaveAsRule,
  onDiscard,
}: {
  open: boolean;
  subject: string | null;
  fromAddress: string | null;
  onSaveAsRule: () => void;
  onDiscard: () => void;
}) {
  const { t } = useTranslation();

  return (
    <Dialog
      open={open}
      onOpenChange={(isOpen) => {
        if (!isOpen) onDiscard();
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("emailImport.rules.draftPrompt.title")}</DialogTitle>
          <DialogDescription>
            {t("emailImport.rules.draftPrompt.description")}
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-1 rounded-md bg-muted p-3 text-xs">
          <p className="truncate">
            <span className="font-medium">{t("auth.email")}: </span>
            {fromAddress ?? t("emailImport.rules.draftPrompt.noFrom")}
          </p>
          <p className="truncate">
            <span className="font-medium">{t("emailImport.rules.draftPrompt.subject")}: </span>
            {subject ?? t("emailImport.pending.noSubject")}
          </p>
        </div>

        <DialogFooter>
          <Button type="button" variant="ghost" onClick={onDiscard}>
            {t("emailImport.rules.draftPrompt.discard")}
          </Button>
          <Button type="button" onClick={onSaveAsRule}>
            <FilePlus2Icon data-icon="inline-start" />
            {t("emailImport.rules.draftPrompt.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
