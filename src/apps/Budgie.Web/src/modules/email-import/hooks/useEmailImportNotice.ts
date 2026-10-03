import { useEffect, useRef } from "react";
import { useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { jwtDecode } from "jwt-decode";

/**
 * Aviso del flujo OAuth de email-import.
 *
 * El callback del backend redirige a `/settings?emailImport=connected` (éxito) o a
 * `/email-import?emailImport=error&reason=<código ErrorOr>` (fallo). Este hook:
 *  1. comprueba que el JWT de localStorage sigue presente y sin caducar — el token
 *     vive en localStorage, así que una vuelta en otra pestaña puede no tenerlo;
 *  2. muestra el mensaje correspondiente (los códigos conocidos se mapean a i18n,
 *     el resto usa un fallback genérico);
 *  3. limpia los query params para que un refresh no repita el aviso.
 */
const REASON_KEYS: Record<string, string> = {
  "EmailImport.InvalidState": "emailImport.settings.reasons.invalidState",
  "EmailImport.AlreadyConnected": "emailImport.settings.reasons.alreadyConnected",
  "EmailImport.CodeExchangeFailed": "emailImport.settings.reasons.codeExchangeFailed",
  "EmailImport.ProviderNotConfigured": "emailImport.settings.reasons.providerNotConfigured",
  "EmailImport.ProviderUnavailable": "emailImport.settings.reasons.providerUnavailable",
  "EmailImport.UnknownProvider": "emailImport.settings.reasons.unknownProvider",
  "EmailImport.NotConnected": "emailImport.settings.reasons.notConnected",
  "EmailImport.SyncAlreadyScheduled": "emailImport.settings.reasons.syncAlreadyScheduled",
  "EmailImport.RuleNotFound": "emailImport.settings.reasons.ruleNotFound",
  "EmailImport.InvalidRule": "emailImport.settings.reasons.invalidRule",
  "EmailImport.InvalidReview": "emailImport.settings.reasons.invalidReview",
  "EmailImport.TransactionLost": "emailImport.settings.reasons.transactionLost",
  "Account.NotFound": "emailImport.settings.reasons.accountNotFound",
  "Category.NotFound": "emailImport.settings.reasons.categoryNotFound",
};

function hasValidToken(): boolean {
  const token = localStorage.getItem("token");
  if (!token) return false;
  try {
    const { exp } = jwtDecode<{ exp?: number }>(token);
    // 30 s de margen: si el token caducó hace un momento, el backend lo rechazará.
    return !exp || exp * 1000 > Date.now() + 30_000;
  } catch {
    return false;
  }
}

export function useEmailImportNotice() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { t } = useTranslation();
  const handled = useRef(false);

  const result = searchParams.get("emailImport");

  useEffect(() => {
    if (!result || handled.current) return;
    handled.current = true;

    if (!hasValidToken()) {
      // Volvió sin sesión válida (otra pestaña / token caducado): a login.
      window.location.replace("/login");
      return;
    }

    if (result === "connected") {
      toast.success(t("emailImport.settings.connected"));
    } else if (result === "error") {
      const reason = searchParams.get("reason") ?? "";
      const key = REASON_KEYS[reason] ?? "emailImport.settings.reasons.unknown";
      toast.error(t("emailImport.settings.oauth.errorTitle"), { description: t(key) });
    }

    // Limpia los params sin recargar: un refresh no debe repetir el aviso.
    const next = new URLSearchParams(searchParams);
    next.delete("emailImport");
    next.delete("reason");
    setSearchParams(next, { replace: true });
  }, [result, searchParams, setSearchParams, t]);
}
