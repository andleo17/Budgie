import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { format } from "date-fns";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Slider } from "@/components/ui/slider";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
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
import { useState } from "react";
import {
  ArrowRightIcon,
  Link2Icon,
  Loader2Icon,
  MailIcon,
  RefreshCwIcon,
  UnlinkIcon,
} from "lucide-react";
import { getDateLocale } from "@/lib/date-locale";
import {
  useAuthorizeEmailConnection,
  useDisconnectEmailConnection,
  useEmailConnections,
  useSyncNow,
} from "@/modules/email-import/hooks/useEmailConnections";
import { useEmailImportNotice } from "@/modules/email-import/hooks/useEmailImportNotice";
import { EmailImportApiError } from "@/modules/email-import/api";
import { EmailProvider } from "@/modules/email-import/types";

const PROVIDER_LABEL_KEYS: Record<number, string> = {
  [EmailProvider.Gmail]: "emailImport.settings.gmail",
};

/**
 * Tarjeta de Ajustes de importación por correo (spec §8): conectar (redirect
 * OAuth), estado de la conexión, umbral de auto-commit (solo lectura: lo fija el
 * backend/operador), "Sincronizar ahora" y enlace a /email-import.
 */
export function EmailImportSettingsCard() {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  useEmailImportNotice();

  const { data: connections, isLoading } = useEmailConnections();
  const authorizeMutation = useAuthorizeEmailConnection();
  const disconnectMutation = useDisconnectEmailConnection();
  const syncMutation = useSyncNow();
  const [disconnectOpen, setDisconnectOpen] = useState(false);

  const connection = connections?.[0];
  const providerKey = connection ? (PROVIDER_LABEL_KEYS[connection.provider] ?? null) : null;
  const providerLabel = providerKey ? t(providerKey) : "—";

  const handleConnect = () => {
    authorizeMutation.mutate(String(EmailProvider.Gmail), {
      onError: (err) =>
        toast.error(t("emailImport.settings.connectError"), {
          description: err instanceof Error ? err.message : undefined,
        }),
    });
  };

  const handleSyncNow = () => {
    syncMutation.mutate(undefined, {
      onSuccess: (result) => {
        toast.success(t("emailImport.settings.syncOk"), {
          description: t("emailImport.settings.syncTally", {
            autoCommitted: String(result.autoCommitted),
            staged: String(result.staged),
            skipped: String(result.skipped),
            failed: String(result.failed),
          }),
        });
      },
      onError: (err) => {
        // Con el worker activo el backend responde 400 SyncAlreadyScheduled:
        // no es un error, es el despliegue haciendo su trabajo. Aviso, no toast rojo.
        if (err instanceof EmailImportApiError && err.code === "EmailImport.SyncAlreadyScheduled") {
          toast.info(t("emailImport.settings.syncAlreadyScheduled"));
          return;
        }
        if (err instanceof EmailImportApiError && err.code === "EmailImport.ProviderNotConfigured") {
          toast.error(t("emailImport.settings.notConfigured"), {
            description: err.message,
          });
          return;
        }
        toast.error(t("emailImport.settings.syncError"), {
          description: err instanceof Error ? err.message : undefined,
        });
      },
    });
  };

  const handleDisconnect = () => {
    if (!connection) return;
    disconnectMutation.mutate(String(connection.provider), {
      onSuccess: () => {
        toast.success(t("emailImport.settings.disconnected"));
        setDisconnectOpen(false);
      },
      onError: (err) => {
        setDisconnectOpen(false);
        toast.error(t("emailImport.settings.disconnectError"), {
          description: err instanceof Error ? err.message : undefined,
        });
      },
    });
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t("emailImport.settings.title")}</CardTitle>
          <CardDescription>{t("emailImport.settings.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <Skeleton className="h-24 w-full" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("emailImport.settings.title")}</CardTitle>
        <CardDescription>{t("emailImport.settings.description")}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {!connection ? (
          <>
            <p className="text-sm text-muted-foreground">{t("emailImport.settings.connectDesc")}</p>
            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" onClick={handleConnect} disabled={authorizeMutation.isPending}>
                {authorizeMutation.isPending ? (
                  <Loader2Icon data-icon="inline-start" className="animate-spin" />
                ) : (
                  <MailIcon data-icon="inline-start" />
                )}
                {authorizeMutation.isPending
                  ? t("emailImport.settings.connecting")
                  : t("emailImport.settings.connect")}
              </Button>
              <Badge variant="outline">{t("emailImport.settings.gmail")}</Badge>
            </div>
            <Button type="button" variant="ghost" render={<Link to="/email-import" />}>
              {t("emailImport.settings.manageRules")}
              <ArrowRightIcon data-icon="inline-end" />
            </Button>
          </>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">
                <Link2Icon data-icon="inline-start" />
                {providerLabel}: {connection.email}
              </Badge>
              <Badge variant={connection.syncEnabled ? "default" : "secondary"}>
                {connection.syncEnabled
                  ? t("emailImport.settings.syncEnabled")
                  : t("emailImport.settings.syncDisabled")}
              </Badge>
            </div>

            <div className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
              <div>
                <p className="text-muted-foreground">{t("emailImport.settings.lastSync")}</p>
                <p className="tabular-nums">
                  {connection.lastSyncAt
                    ? format(new Date(connection.lastSyncAt), "PPp", { locale: dateLocale })
                    : t("emailImport.settings.never")}
                </p>
              </div>
              <div>
                <p className="text-muted-foreground">{t("emailImport.settings.lastSkippedLabel")}</p>
                <p className="tabular-nums">
                  {t("emailImport.settings.lastSkipped", {
                    count: String(connection.lastSkippedCount),
                  })}
                </p>
              </div>
            </div>

            {connection.lastError && (
              <Alert variant="destructive">
                <AlertTitle>{t("emailImport.settings.lastError")}</AlertTitle>
                <AlertDescription>{connection.lastError}</AlertDescription>
              </Alert>
            )}

            <Separator />

            {/* Umbral de auto-commit: el backend aún no expone su actualización,
                así que el slider muestra el valor de la conexión en solo lectura. */}
            <div className="flex flex-col gap-2">
              <div className="flex items-center justify-between gap-2">
                <p className="text-sm font-medium">{t("emailImport.settings.threshold")}</p>
                <span className="text-sm tabular-nums">
                  {Math.round(connection.autoCommitMinConfidence * 100)}%
                </span>
              </div>
              <Slider
                value={[Number(connection.autoCommitMinConfidence)]}
                min={0}
                max={1}
                step={0.01}
                disabled
                aria-label={t("emailImport.settings.threshold")}
              />
              <p className="text-xs text-muted-foreground">
                {t("emailImport.settings.thresholdDesc")}
              </p>
            </div>

            <div className="flex flex-col gap-1">
              <p className="text-sm font-medium">{t("emailImport.settings.interval")}</p>
              <p className="text-xs text-muted-foreground">
                {t("emailImport.settings.intervalDesc")}
              </p>
            </div>

            <Separator />

            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" onClick={handleSyncNow} disabled={syncMutation.isPending}>
                {syncMutation.isPending ? (
                  <Loader2Icon data-icon="inline-start" className="animate-spin" />
                ) : (
                  <RefreshCwIcon data-icon="inline-start" />
                )}
                {syncMutation.isPending
                  ? t("emailImport.settings.syncing")
                  : t("emailImport.settings.syncNow")}
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => setDisconnectOpen(true)}
                disabled={disconnectMutation.isPending}
              >
                <UnlinkIcon data-icon="inline-start" />
                {t("emailImport.settings.disconnect")}
              </Button>
              <Button type="button" variant="ghost" render={<Link to="/email-import" />}>
                {t("emailImport.settings.manageRules")}
                <ArrowRightIcon data-icon="inline-end" />
              </Button>
            </div>
          </>
        )}
      </CardContent>

      <AlertDialog open={disconnectOpen} onOpenChange={setDisconnectOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("emailImport.settings.disconnectTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("emailImport.settings.disconnectDesc")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={disconnectMutation.isPending}>
              {t("emailImport.rules.cancel")}
            </AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDisconnect}
              disabled={disconnectMutation.isPending}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              {t("emailImport.settings.disconnect")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}
