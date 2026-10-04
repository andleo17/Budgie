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
import { getDateLocale, parseDbDate } from "@/lib/date-locale";
import {
  useAuthorizeEmailConnection,
  useDisconnectEmailConnection,
  useEmailConnections,
  useSyncNow,
  useUpdateAutoCommitThreshold,
} from "@/modules/email-import/hooks/useEmailConnections";
import { useEmailImportNotice } from "@/modules/email-import/hooks/useEmailImportNotice";
import { EmailImportApiError } from "@/modules/email-import/api";
import { EmailProvider } from "@/modules/email-import/types";

const PROVIDER_LABEL_KEYS: Record<number, string> = {
  [EmailProvider.Gmail]: "emailImport.settings.gmail",
};

/**
 * El Slider de Base UI entrega `number | readonly number[]` según la primitiva. Este control es
 * de un solo valor, así que se normaliza aquí en vez de repetir el `value[0] ?? 0` en dos sitios
 * (y con un type assertion que el compilador no comprobaría).
 */
function toThresholdValue(value: number | readonly number[]): number {
  return Array.isArray(value) ? (value[0] ?? 0) : (value as number);
}

/**
 * Tarjeta de Ajustes de importación por correo (spec §8): conectar (redirect
 * OAuth), estado de la conexión, umbral de auto-commit (**editable** desde el slice 1),
 * "Sincronizar ahora" y enlace a /email-import.
 */
export function EmailImportSettingsCard() {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  useEmailImportNotice();

  const { data: connections, isLoading } = useEmailConnections();
  const authorizeMutation = useAuthorizeEmailConnection();
  const disconnectMutation = useDisconnectEmailConnection();
  const syncMutation = useSyncNow();
  const thresholdMutation = useUpdateAutoCommitThreshold();
  const [disconnectOpen, setDisconnectOpen] = useState(false);

  const connection = connections?.[0];
  const providerKey = connection ? (PROVIDER_LABEL_KEYS[connection.provider] ?? null) : null;
  const providerLabel = providerKey ? t(providerKey) : "—";

  // El borrador es "una edición en curso" o null: mientras sea null, lo que se muestra y lo que
  // vale es lo que dice el servidor. Se eligió esta forma y no un draft inicializado al valor
  // del servidor con un guard de sincronización porque ese patrón NO puede absorber la primera
  // carga: en el primer render `connection` aún no existe, así que el estado nace en 0, y cuando
  // llega 0.85 el guard compara 0 contra 0 y no corre — el slider se quedaba en 0% con el
  // servidor diciendo 0.85, y el primer arrastre se revertía solo, con `onValueCommitted`
  // cortocircuitando por ser igual al valor del servidor y sin mandar el PUT.
  // Con `null` no hay estado que sincronizar: null significa "sigue lo que diga el servidor".
  const serverThreshold = connection ? Number(connection.autoCommitMinConfidence) : 0;
  const [thresholdDraft, setThresholdDraft] = useState<number | null>(null);
  const shownThreshold = thresholdDraft ?? serverThreshold;

  const saveThreshold = (value: number) => {
    if (!connection || value === serverThreshold) return;

    setThresholdDraft(value);
    thresholdMutation.mutate(
      { provider: String(connection.provider), threshold: value },
      {
        onSuccess: () => {
          // El guardado libera el borrador: a partir de aquí manda el servidor, y un refetch
          // desde otro dispositivo se refleja sin quedar desincronizado con un draft viejo.
          setThresholdDraft(null);
        },
        onError: () => {
          // Rollback: el servidor puede rechazar el valor ([0,1] lo admite, pero es quien
          // manda) y el slider no debe seguir enseñando algo no guardado.
          setThresholdDraft(null);
        },
      },
    );
  };

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
          {/* Variante única para toda la app: un contenedor `role="status"` CON TEXTO sr-only dentro.
              Se descartó la de `aria-label` sobre el propio Skeleton porque una región viva
              anuncia CAMBIOS DE CONTENIDO, no su etiqueta: un div vacío etiquetado puede no
              anunciarse nunca. Con texto real, el lector dice "cargando ajustes" al pintarse. */}
          <div role="status" className="w-full">
            <span className="sr-only">{t("emailImport.loading.settings")}</span>
            <Skeleton className="h-24 w-full" />
          </div>
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
                {/* parseDbDate, igual que la fila de la bandeja (PendingTransactionRow):
                    la convención del módulo es mostrar el timestamp tal como lo guardó
                    el backend (naive, sin zona). `new Date(...Z)` lo convertiría a la
                    zona local y con un lastSyncAt con Z las dos pantallas divergirían
                    (plan deuda §7.16.3). */}
                <p className="tabular-nums">
                  {connection.lastSyncAt
                    ? format(parseDbDate(connection.lastSyncAt), "PPp", { locale: dateLocale })
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

            {/* Umbral de auto-commit: editable (slice 1). Antes solo lectura porque el backend
                no exponía su actualización — un parámetro que parecía configurable y no lo era. */}
            <div className="flex flex-col gap-2">
              <div className="flex items-center justify-between gap-2">
                <p id="email-import-threshold-label" className="text-sm font-medium">
                  {t("emailImport.settings.threshold")}
                </p>
                <span className="text-sm tabular-nums">
                  {Math.round(shownThreshold * 100)}%
                </span>
              </div>
              <Slider
                value={[shownThreshold]}
                min={0}
                max={1}
                step={0.01}
                // Sólo bloquea mientras la petición está en vuelo: bloquear en cada movimiento
                // impediría seguir ajustando el tirador.
                disabled={thresholdMutation.isPending}
                // El primitive pone `role="group"` en la raíz y delega el rol "slider" real al
                // `<input type="range">` interno, que hereda el nombre pero es el elemento
                // enfocable: sin `aria-label` en la raíz, un lector de pantalla anunciaba
                // "slider, 0.85" sin decir QUÉ es (WCAG 4.1.2). Con `aria-labelledby` apuntando
                // al título visible, el elemento interactivo queda con nombre accesible.
                aria-labelledby="email-import-threshold-label"
                // El error del guardado se asocia al control: sin esto el `role="alert"` grita
                // al aparecer pero no queda vinculado al campo al que se refiere al navegar con
                // el teclado.
                aria-describedby={
                  thresholdMutation.isError ? "email-import-threshold-error" : undefined
                }
                onValueChange={(value) => setThresholdDraft(toThresholdValue(value))}
                onValueCommitted={(value) => saveThreshold(toThresholdValue(value))}
              />
              <p className="text-xs text-muted-foreground">
                {t("emailImport.settings.thresholdDesc")}
              </p>
              {thresholdMutation.isError && (
                <p id="email-import-threshold-error" role="alert" className="text-xs text-destructive">
                  {t("emailImport.settings.thresholdError")}
                </p>
              )}
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
            {/* Sin disabled en AlertDialogAction/Cancel: la primitiva es un Button
                normal que CIERRA el diálogo al hacer clic, así que un
                disabled={isPending} sería feedback muerto —el estado nunca se ve—
                y en Cancel además impediría cancelar. El toast de resultado
                (disconnected/disconnectError) es el feedback real (plan deuda
                §7.18.1). DeleteRuleDialog sí empareja disabled+Loader2, pero
                porque usa Dialog y no AlertDialog: no copiar ese patrón sin
                cambiar de primitiva. */}
            <AlertDialogCancel>{t("emailImport.rules.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDisconnect}
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
