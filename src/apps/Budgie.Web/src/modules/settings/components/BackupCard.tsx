import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Alert } from "@/components/ui/alert";
import { Loader2, Download, Upload, DatabaseBackup } from "lucide-react";

type SnapshotData = components["schemas"]["SnapshotData"];

export function BackupCard() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [parsedSnapshot, setParsedSnapshot] = useState<SnapshotData | null>(null);
  const [parseError, setParseError] = useState<string | null>(null);

  const exportMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await client.GET("/api/finances/export/snapshot");
      if (error) throw error;
      return data;
    },
    onSuccess: (snapshot) => {
      const blob = new Blob([JSON.stringify(snapshot, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `budgie-backup-${new Date().toISOString().slice(0, 10)}.json`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      toast.success(t("backup.exported"));
    },
    onError: (err) => {
      toast.error(t("backup.exportError"), { description: err instanceof Error ? err.message : undefined });
    },
  });

  const restoreMutation = useMutation({
    mutationFn: async (snapshot: SnapshotData) => {
      const { data, error } = await client.POST("/api/finances/export/snapshot", { body: { snapshot } });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries();
      setFileName(null);
      setParsedSnapshot(null);
      setParseError(null);
      toast.success(t("backup.restored"));
    },
    onError: (err) => {
      toast.error(t("backup.restoreError"), { description: err instanceof Error ? err.message : undefined });
    },
  });

  async function handleFile(file: File) {
    setFileName(file.name);
    setParseError(null);
    setParsedSnapshot(null);
    try {
      const text = await file.text();
      const parsed = JSON.parse(text) as SnapshotData;
      if (!parsed || typeof parsed !== "object" || !Array.isArray(parsed.accounts) || !parsed.version) {
        setParseError(t("backup.invalidFile"));
        return;
      }
      setParsedSnapshot(parsed);
    } catch {
      setParseError(t("backup.invalidFile"));
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <DatabaseBackup className="size-4" />
          {t("backup.title")}
        </CardTitle>
        <CardDescription>{t("backup.description")}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-col gap-2 rounded-lg border bg-muted/20 p-3">
          <p className="text-sm text-muted-foreground">{t("backup.exportDesc")}</p>
          <div className="flex items-center gap-2">
            <Button onClick={() => exportMutation.mutate()} disabled={exportMutation.isPending}>
              {exportMutation.isPending ? <Loader2 className="mr-1 size-4 animate-spin" /> : <Download className="mr-1 size-4" />}
              {t("backup.export")}
            </Button>
          </div>
        </div>

        <div className="flex flex-col gap-2 rounded-lg border bg-muted/20 p-3">
          <p className="text-sm text-muted-foreground">{t("backup.restoreDesc")}</p>
          <input
            ref={fileInputRef}
            type="file"
            accept="application/json,.json"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) handleFile(file);
              e.target.value = "";
            }}
          />
          {!fileName ? (
            <Button variant="outline" onClick={() => fileInputRef.current?.click()}>
              <Upload className="mr-1 size-4" />
              {t("backup.chooseFile")}
            </Button>
          ) : (
            <div className="flex flex-col gap-2">
              <p className="text-xs text-muted-foreground truncate">{fileName}</p>
              {parseError ? (
                <Alert>{parseError}</Alert>
              ) : (
                <div className="flex gap-2">
                  <Button
                    onClick={() => restoreMutation.mutate(parsedSnapshot!)}
                    disabled={restoreMutation.isPending || !parsedSnapshot}
                  >
                    {restoreMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
                    {t("backup.restore")}
                  </Button>
                  <Button variant="ghost" onClick={() => { setFileName(null); setParsedSnapshot(null); setParseError(null); }}>
                    {t("backup.cancel")}
                  </Button>
                </div>
              )}
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
