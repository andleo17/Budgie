import { useState } from "react";
import {
  Dialog,
  DialogContent,
} from "@/components/ui/dialog";
import type { components } from "@/lib/api-types";
import { CreateAccountForm } from "./CreateAccountForm";
import { EditAccountForm } from "./EditAccountForm";

type AccountResponse = components["schemas"]["AccountResponse"];

interface AccountFormDialogProps {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  account?: AccountResponse | null;
}

export function AccountFormDialog({ open, onOpenChange, account }: AccountFormDialogProps) {
  const close = () => onOpenChange(false);

  const [formAccount, setFormAccount] = useState<AccountResponse | null>(account ?? null);
  const [prevOpen, setPrevOpen] = useState(open);

  if (open !== prevOpen) {
    setPrevOpen(open);
    if (open) setFormAccount(account ?? null);
  }

  const isEditing = !!formAccount;
  const dialogKey = formAccount?.id ?? "create";

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent key={dialogKey}>
        {isEditing
          ? <EditAccountForm key={`edit-${formAccount!.id}`} account={formAccount!} onSuccess={close} />
          : <CreateAccountForm key="create" onSuccess={close} onCancel={close} />
        }
      </DialogContent>
    </Dialog>
  );
}
