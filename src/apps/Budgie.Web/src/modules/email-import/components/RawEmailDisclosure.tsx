import { useTranslation } from "react-i18next";
import { ChevronDownIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";

/**
 * "Ver correo original": despliega el RawBody del staging como texto plano
 * preformateado (spec §8.1). Sin RawBody no se renderiza nada.
 */
export function RawEmailDisclosure({ rawBody }: { rawBody: string | null }) {
  const { t } = useTranslation();

  if (!rawBody) return null;

  return (
    <Collapsible>
      <CollapsibleTrigger
        render={
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="text-muted-foreground"
          />
        }
      >
        <ChevronDownIcon data-icon="inline-start" />
        {t("emailImport.pending.viewRaw")}
      </CollapsibleTrigger>
      <CollapsibleContent>
        <pre className="mt-1 max-h-64 overflow-auto rounded-md bg-muted p-3 text-xs whitespace-pre-wrap break-words">
          {rawBody}
        </pre>
      </CollapsibleContent>
    </Collapsible>
  );
}
