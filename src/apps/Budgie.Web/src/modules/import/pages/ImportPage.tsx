import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  Alert,
  AlertAction,
  AlertDescription,
  AlertTitle,
} from "@/components/ui/alert";
import { TriangleAlert, PartyPopper, FileUp, RotateCcw } from "lucide-react";
import { useImport } from "@/modules/import/hooks/useImport";
import { ImportUpload } from "@/modules/import/components/ImportUpload";
import { ImportPreview } from "@/modules/import/components/ImportPreview";

/** Página dedicada `/import`: subir .mmbak → preview → confirmar commit. */
export default function ImportPage() {
  const { t } = useTranslation();
  const {
    file,
    previewData,
    commitData,
    isPreviewing,
    isPreviewError,
    previewError,
    isCommitting,
    startPreview,
    confirmCommit,
    reset,
  } = useImport();
  const [overwrite, setOverwrite] = useState(false);

  const successTitleRef = useRef<HTMLDivElement>(null);

  // Al pasar a la pantalla de éxito, mover el foco al título para que el
  // lector de pantalla y el teclado no queden en el botón de confirmar.
  useEffect(() => {
    if (commitData) successTitleRef.current?.focus();
  }, [commitData]);

  const busy = isPreviewing || isCommitting;
  const previewErrorMessage = previewError instanceof Error ? previewError.message : undefined;

  function handleStartOver() {
    setOverwrite(false);
    reset();
  }

  return (
    <div className="w-full space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">{t("import.title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("import.description")}</p>
      </div>

      {commitData ? (
        <Card>
          <CardHeader>
            <CardTitle
              ref={successTitleRef}
              tabIndex={-1}
              className="flex items-center gap-2"
            >
              <PartyPopper className="size-5 text-primary" />
              {t("import.commit.success")}
            </CardTitle>
            <CardDescription>{t("import.commit.successDesc")}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
              {[
                { label: t("import.commit.createdAccounts"), value: commitData.createdAccounts },
                { label: t("import.commit.skippedAccounts"), value: commitData.skippedAccounts },
                { label: t("import.commit.createdCategories"), value: commitData.createdCategories },
                { label: t("import.commit.skippedCategories"), value: commitData.skippedCategories },
                { label: t("import.commit.createdTransactions"), value: commitData.createdTransactions },
                { label: t("import.commit.skippedTransactions"), value: commitData.skippedTransactions },
                { label: t("import.commit.createdRecurring"), value: commitData.createdRecurring },
              ].map((item) => (
                <div key={item.label} className="flex flex-col gap-1 rounded-lg border bg-card p-4">
                  <span className="text-xs text-muted-foreground">{item.label}</span>
                  <span className="text-xl font-bold tabular-nums tracking-tight">{item.value}</span>
                </div>
              ))}
            </div>

            {commitData.warnings.length > 0 && (
              <Alert>
                <TriangleAlert />
                <AlertTitle>{t("import.preview.warnings")}</AlertTitle>
                <AlertDescription>
                  <ul className="list-disc space-y-1 pl-5">
                    {commitData.warnings.map((warn, i) => (
                      <li key={i}>{warn}</li>
                    ))}
                  </ul>
                </AlertDescription>
              </Alert>
            )}

            <Button onClick={handleStartOver} variant="outline">
              <FileUp className="mr-1 size-4" />
              {t("import.commit.startOver")}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle>{t("import.upload.title")}</CardTitle>
              <CardDescription>{t("import.upload.description")}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {isPreviewError && (
                <Alert variant="destructive">
                  <TriangleAlert />
                  <AlertTitle>{t("import.preview.error")}</AlertTitle>
                  {previewErrorMessage && (
                    <AlertDescription>{previewErrorMessage}</AlertDescription>
                  )}
                  <AlertAction>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-9"
                      onClick={() => file && startPreview(file)}
                      disabled={busy}
                    >
                      <RotateCcw className="size-3.5" />
                      {t("import.preview.retry")}
                    </Button>
                  </AlertAction>
                </Alert>
              )}
              <ImportUpload onFileSelected={startPreview} disabled={busy} />
            </CardContent>
          </Card>

          {(isPreviewing || previewData) && (
            <ImportPreview
              data={previewData ?? undefined}
              loading={isPreviewing}
              committing={isCommitting}
              overwrite={overwrite}
              onOverwriteChange={setOverwrite}
              onConfirm={() => confirmCommit(overwrite)}
              onCancel={() => {
                setOverwrite(false);
                reset();
              }}
            />
          )}
        </>
      )}
    </div>
  );
}
