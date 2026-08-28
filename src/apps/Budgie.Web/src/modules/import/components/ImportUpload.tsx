import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Upload, FileUp, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Alert } from "@/components/ui/alert";

const ALLOWED_EXTENSIONS = [".mmbak", ".db", ".sqlite"];
const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

interface ImportUploadProps {
  onFileSelected: (file: File) => void;
  disabled?: boolean;
}

/**
 * Dropzone de subida del respaldo .mmbak con validación client-side
 * (extensión .mmbak/.db/.sqlite y ≤10 MB) antes de llamar a preview.
 */
export function ImportUpload({ onFileSelected, disabled }: ImportUploadProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragActive, setIsDragActive] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<{ name: string; size: number } | null>(null);

  function openPicker() {
    if (disabled) return;
    inputRef.current?.click();
  }

  function handleFile(file: File) {
    if (disabled) return;
    const ext = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      setError(t("import.upload.invalidExtension"));
      return;
    }
    if (file.size > MAX_FILE_SIZE_BYTES) {
      setError(t("import.upload.tooLarge"));
      return;
    }
    setError(null);
    setSelected({ name: file.name, size: file.size });
    onFileSelected(file);
  }

  return (
    <div
      role="button"
      tabIndex={disabled ? -1 : 0}
      aria-label={
        selected ? t("import.upload.removeLabel") : t("import.upload.chooseFile")
      }
      aria-disabled={disabled || undefined}
      onClick={openPicker}
      onKeyDown={(e) => {
        if (disabled) return;
        // Ignorar eventos que burbujean desde controles internos (p.ej. el
        // botón "Quitar"): solo el dropzone activa el selector de archivos.
        if (e.target !== e.currentTarget) return;
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          openPicker();
        }
      }}
      onDragOver={(e) => {
        if (disabled) return;
        e.preventDefault();
        setIsDragActive(true);
      }}
      onDragLeave={() => setIsDragActive(false)}
      onDrop={(e) => {
        if (disabled) return;
        e.preventDefault();
        setIsDragActive(false);
        const file = e.dataTransfer.files?.[0];
        if (file) handleFile(file);
      }}
      className={`flex min-h-40 cursor-pointer flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed p-6 text-center outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50 ${
        isDragActive
          ? "border-primary bg-primary/5"
          : "border-border hover:border-primary/60 hover:bg-muted/40"
      } ${disabled ? "pointer-events-none opacity-60" : ""}`}
    >
      <input
        ref={inputRef}
        type="file"
        accept=".mmbak,.db,.sqlite"
        className="sr-only"
        aria-hidden="true"
        tabIndex={-1}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) handleFile(file);
          e.target.value = "";
        }}
      />

      {selected ? (
        <div className="flex flex-col items-center gap-3" aria-live="polite">
          <span className="inline-flex h-11 w-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <FileUp className="size-5" />
          </span>
          <div>
            <p className="text-sm font-medium">{selected.name}</p>
            <p className="text-xs text-muted-foreground">{formatBytes(selected.size)}</p>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-11 sm:h-8"
            onClick={(e) => {
              e.stopPropagation();
              setSelected(null);
              setError(null);
            }}
          >
            <X className="mr-1 size-4" />
            {t("import.upload.remove")}
          </Button>
        </div>
      ) : (
        <>
          <span className="inline-flex h-11 w-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Upload className="size-5" />
          </span>
          <div>
            <p className="text-sm font-medium">{t("import.upload.chooseFile")}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t("import.upload.dragHint")}</p>
          </div>
          <span className="rounded-full border bg-card px-2.5 py-1 text-xs text-muted-foreground">
            .mmbak · .db · .sqlite
          </span>
        </>
      )}

      {error && <Alert variant="destructive">{error}</Alert>}
    </div>
  );
}
