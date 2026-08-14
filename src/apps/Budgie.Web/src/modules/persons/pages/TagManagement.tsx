import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import client from "@/lib/api-client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
import { Card, CardContent } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Loader2, Plus, Pencil, Trash2, Tags } from "lucide-react";
import type { components } from "@/lib/api-types";

type TagResponse = components["schemas"]["TagResponse"];

const COLOR_OPTIONS = [
  { name: "gray", hex: "#6b7280" },
  { name: "red", hex: "#ef4444" },
  { name: "orange", hex: "#f97316" },
  { name: "yellow", hex: "#eab308" },
  { name: "green", hex: "#22c55e" },
  { name: "teal", hex: "#14b8a6" },
  { name: "blue", hex: "#3b82f6" },
  { name: "purple", hex: "#a855f7" },
  { name: "pink", hex: "#ec4899" },
];

function isCustomColor(value: string): boolean {
  return !COLOR_OPTIONS.some((c) => c.name === value) && /^#[0-9a-fA-F]{6}$/.test(value);
}

function TagForm({ onSave, onCancel, initial }: { onSave: (name: string, color: string | null) => void; onCancel?: () => void; initial: { name: string; color: string } }) {
  const { t } = useTranslation();
  const [name, setName] = useState(initial.name);
  const [color, setColor] = useState(initial.color);
  const [useCustom, setUseCustom] = useState(isCustomColor(initial.color));

  function handleColorSelect(c: string) {
    setColor(c);
    setUseCustom(false);
  }

  function handleColorPicker(e: React.ChangeEvent<HTMLInputElement>) {
    setColor(e.target.value);
    setUseCustom(true);
  }

  return (
    <div className="grid gap-4 py-4">
      <div className="grid gap-2">
        <Label htmlFor="tag-name">{t("persons.tagName")}</Label>
        <Input
          id="tag-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t("persons.tagNamePlaceholder")}
        />
      </div>
      <div className="grid gap-2">
        <Label>{t("persons.tagColor")}</Label>
        <div className="flex gap-2 flex-wrap items-center">
          {COLOR_OPTIONS.map((c) => (
            <button
              key={c.name}
              type="button"
              onClick={() => handleColorSelect(c.name)}
              className={`size-8 rounded-full border-2 transition-all ${
                !useCustom && color === c.name ? "border-foreground scale-110 ring-2 ring-foreground/30" : "border-transparent"
              }`}
              style={{ backgroundColor: c.hex }}
              title={c.name}
            />
          ))}
          <label
            className={`size-8 rounded-full border-2 cursor-pointer flex items-center justify-center text-xs transition-all ${
              useCustom ? "border-foreground scale-110 ring-2 ring-foreground/30" : "border-dashed border-muted-foreground/40"
            }`}
            style={useCustom ? { backgroundColor: color } : undefined}
            title="Custom color"
          >
            <input
              type="color"
              value={isCustomColor(color) ? color : "#3b82f6"}
              onChange={handleColorPicker}
              className="sr-only"
            />
            {!useCustom && <span className="text-muted-foreground/60">+</span>}
          </label>
        </div>
      </div>
      <DialogFooter>
        {onCancel && (
          <Button type="button" variant="outline" onClick={onCancel}>
            {t("persons.cancel")}
          </Button>
        )}
        <Button
          type="button"
          onClick={() => onSave(name, color === "gray" && !useCustom ? null : color)}
          disabled={!name.trim()}
        >
          {t("persons.save")}
        </Button>
      </DialogFooter>
    </div>
  );
}

function TagManagement() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [editTag, setEditTag] = useState<TagResponse | null>(null);
  const [deleteTag, setDeleteTag] = useState<TagResponse | null>(null);
  const [, setNewName] = useState("");
  const [, setNewColor] = useState("gray");

  const { data: tags, isLoading } = useQuery({
    queryKey: ["tags"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/tags");
      if (error) throw error;
      return data;
    },
  });

  const sorted = tags ? [...tags].sort((a, b) => a.name.localeCompare(b.name)) : [];

  const createMutation = useMutation({
    mutationFn: async (body: { name: string; color?: string | null }) => {
      const { data, error } = await client.POST("/api/relationships/tags", {
        body: { name: body.name, color: body.color ?? null },
      });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
      setCreateOpen(false);
      setNewName("");
      setNewColor("gray");
    },
    onError: (err) => toast.error("Failed to create tag", { description: err instanceof Error ? err.message : undefined }),
  });

  const updateMutation = useMutation({
    mutationFn: async (body: { id: string; name: string; color?: string | null }) => {
      const { error } = await client.PUT("/api/relationships/tags/{id}", {
        params: { path: { id: body.id } },
        body: { name: body.name, color: body.color ?? null },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
      setEditTag(null);
    },
    onError: (err) => toast.error("Failed to update tag", { description: err instanceof Error ? err.message : undefined }),
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/api/relationships/tags/{id}", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tags"] });
      setDeleteTag(null);
    },
    onError: (err) => toast.error("Failed to delete tag", { description: err instanceof Error ? err.message : undefined }),
  });

  return (
    <div className="w-full">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{t("persons.tagsTitle")}</h1>
          <p className="text-muted-foreground mt-1">{t("persons.tagsDescription")}</p>
        </div>
        <Button size="sm" onClick={() => { setNewName(""); setNewColor("gray"); setCreateOpen(true); }}>
          <Plus className="mr-1.5 size-3.5" />
          {t("persons.newTag")}
        </Button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="size-8 animate-spin text-muted-foreground" />
        </div>
      ) : sorted.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Tags className="mb-4 size-12 text-muted-foreground/60" />
          <p className="text-lg font-medium">{t("persons.tagCreateFirst")}</p>
        </div>
      ) : (
        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {sorted.map((tag) => {
            const hex = COLOR_OPTIONS.find((c) => c.name === tag.color)?.hex ?? tag.color ?? "#6b7280";
            return (
              <Card key={tag.id} className="overflow-hidden">
                <CardContent className="flex items-center justify-between p-2.5">
                  <div className="flex items-center gap-2.5">
                    <div className="size-4 rounded-full shrink-0" style={{ backgroundColor: hex }} />
                    <span className="text-sm font-medium">{tag.name}</span>
                  </div>
                  <div className="flex gap-0.5">
                    <Button
                      variant="ghost"
                      size="icon-xs"
                      onClick={() => {
                        setEditTag(tag);
                        setNewName(tag.name);
                        setNewColor(tag.color ?? "gray");
                      }}
                    >
                      <Pencil className="size-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon-xs"
                      className="text-destructive"
                      onClick={() => setDeleteTag(tag)}
                    >
                      <Trash2 className="size-3.5" />
                    </Button>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {/* Create Dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("persons.newTag")}</DialogTitle>
            <DialogDescription>{t("persons.addDesc")}</DialogDescription>
          </DialogHeader>
          <TagForm
            initial={{ name: "", color: "gray" }}
            onSave={(name, color) => createMutation.mutate({ name, color })}
          />
        </DialogContent>
      </Dialog>

      {/* Edit Dialog */}
      <Dialog open={editTag !== null} onOpenChange={(v) => { if (!v) setEditTag(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("persons.editTag")}</DialogTitle>
            <DialogDescription>{t("persons.editDesc")}</DialogDescription>
          </DialogHeader>
          {editTag && (
            <TagForm
              initial={{ name: editTag.name, color: editTag.color ?? "gray" }}
              onSave={(name, color) => updateMutation.mutate({ id: editTag.id, name, color })}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation */}
      <AlertDialog
        open={deleteTag !== null}
        onOpenChange={(v) => { if (!v) setDeleteTag(null); }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("persons.deleteTag")}</AlertDialogTitle>
            <AlertDialogDescription>{t("persons.deleteTagConfirm")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("persons.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => deleteTag && deleteMutation.mutate(deleteTag.id)}
            >
              {deleteMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
              {t("persons.deleteTag")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

export default TagManagement;
