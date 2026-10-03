import { useTranslation } from "react-i18next";
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
import { Loader2Icon } from "lucide-react";

/** Confirmación de borrado de regla. El caller deshabilita la acción mientras muta. */
export function DeleteRuleDialog({
  open,
  ruleName,
  isDeleting,
  onOpenChange,
  onConfirm,
}: {
  open: boolean;
  ruleName: string | null;
  isDeleting: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}) {
  const { t } = useTranslation();

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("emailImport.rules.deleteTitle")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("emailImport.rules.deleteDesc", { name: ruleName ?? "" })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isDeleting}>
            {t("emailImport.rules.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            disabled={isDeleting}
            className="bg-destructive text-white hover:bg-destructive/90"
          >
            {isDeleting && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
            {t("emailImport.rules.delete")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
