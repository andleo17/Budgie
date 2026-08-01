import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Camera, Trash2, Loader2 } from "lucide-react";

interface AvatarUploadProps {
  personId: string;
  name: string;
  photoUrl: string | null | undefined;
}

function getInitials(name: string): string {
  return name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

export function AvatarUpload({ personId, name, photoUrl }: AvatarUploadProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const fileRef = useRef<HTMLInputElement>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const uploadMutation = useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData();
      formData.append("file", file);
      const response = await fetch(`/api/relationships/persons/${personId}/photo`, {
        method: "POST",
        headers: { Authorization: `Bearer ${localStorage.getItem("token")}` },
        body: formData,
      });
      if (!response.ok) throw new Error("Upload failed");
      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["persons"] });
      toast.success(t("persons.uploadPhoto") || "Photo uploaded");
    },
    onError: () => {
      setPreviewUrl(null);
      toast.error("Failed to upload photo");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async () => {
      const response = await fetch(`/api/relationships/persons/${personId}/photo`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${localStorage.getItem("token")}` },
      });
      if (!response.ok) throw new Error("Delete failed");
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["persons"] });
    },
    onError: () => {
      toast.error("Failed to remove photo");
    },
  });

  const displayUrl = previewUrl ?? photoUrl ?? undefined;

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setPreviewUrl(URL.createObjectURL(file));
    uploadMutation.mutate(file);
    e.target.value = "";
  }

  return (
    <div className="flex items-center gap-3">
      <div className="relative group">
        <Avatar className="size-16">
          <AvatarImage src={displayUrl} alt={name} />
          <AvatarFallback className="text-lg">{getInitials(name)}</AvatarFallback>
        </Avatar>
        {uploadMutation.isPending && (
          <div className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40">
            <Loader2 className="size-5 animate-spin text-white" />
          </div>
        )}
        {!uploadMutation.isPending && (
          <div className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity">
            <Button
              type="button"
              variant="ghost"
              size="icon-xs"
              className="text-white"
              onClick={() => fileRef.current?.click()}
            >
              <Camera className="size-5" />
            </Button>
          </div>
        )}
      </div>
      <input
        ref={fileRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={handleFileChange}
      />
      <div className="flex flex-col gap-1">
        <Button
          type="button"
          variant="outline"
          size="xs"
          onClick={() => fileRef.current?.click()}
          disabled={uploadMutation.isPending}
        >
          {uploadMutation.isPending ? (
            <Loader2 className="mr-1 size-3 animate-spin" />
          ) : (
            <Camera className="mr-1 size-3" />
          )}
          {photoUrl ? t("persons.changePhoto") : t("persons.uploadPhoto")}
        </Button>
        {photoUrl && (
          <Button
            type="button"
            variant="ghost"
            size="xs"
            className="text-destructive"
            onClick={() => deleteMutation.mutate()}
            disabled={uploadMutation.isPending}
          >
            <Trash2 className="mr-1 size-3" />
            {t("persons.removePhoto")}
          </Button>
        )}
      </div>
    </div>
  );
}
