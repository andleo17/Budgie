import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
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
import { Field, FieldLabel, FieldError } from "@/components/ui/field";
import { Loader2 } from "lucide-react";
import { TagSelector } from "./TagSelector";
import { AvatarUpload } from "./AvatarUpload";
import type { components } from "@/lib/api-types";

type PersonResponse = components["schemas"]["PersonResponse"];
type TagResponse = components["schemas"]["TagResponse"];

const PERSON_NAME_MAX_LENGTH = 100;

const formSchema = z.object({
  name: z.string().min(1).max(PERSON_NAME_MAX_LENGTH),
});

type FormValues = z.infer<typeof formSchema>;

interface PersonFormDialogProps {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  person?: PersonResponse | null;
}

export function PersonFormDialog({ open, onOpenChange, person }: PersonFormDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [selectedTags, setSelectedTags] = useState<TagResponse[]>(() => person?.tags ?? []);

  const createMutation = useMutation({
    mutationFn: async (body: { name: string; tagNames?: string[] }) => {
      const { data, error } = await client.POST("/api/relationships/persons", {
        body: { name: body.name, tagNames: body.tagNames?.length ? body.tagNames : null as unknown as undefined },
      });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["persons"] });
      close();
    },
    onError: (err) => {
      toast.error("Something went wrong", {
        description: err instanceof Error ? err.message : undefined,
      });
    },
  });

  const updateMutation = useMutation({
    mutationFn: async (body: { id: string; name: string; tagNames?: string[] }) => {
      const { error } = await client.PUT("/api/relationships/persons/{id}", {
        params: { path: { id: body.id } },
        body: { name: body.name, tagNames: body.tagNames?.length ? body.tagNames : null as unknown as undefined },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["persons"] });
      close();
    },
    onError: (err) => {
      toast.error("Something went wrong", {
        description: err instanceof Error ? err.message : undefined,
      });
    },
  });

  const { control, handleSubmit } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: { name: "" },
    values: { name: person?.name ?? "" },
  });

  const isEditing = !!person;

  const onSubmit = useCallback(
    (values: FormValues) => {
      const tagNames = selectedTags.map((t) => t.name);
      if (isEditing && person) {
        updateMutation.mutate({ id: person.id, name: values.name, tagNames });
      } else {
        createMutation.mutate({ name: values.name, tagNames });
      }
    },
    [isEditing, person, selectedTags, createMutation, updateMutation],
  );

  const close = () => onOpenChange(false);

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent key={person?.id ?? "create"}>
        {/* eslint-disable-next-line react-hooks/incompatible-library */}
        <form onSubmit={handleSubmit(onSubmit)}>
          <DialogHeader>
            <DialogTitle>{isEditing ? t("persons.editPerson") : t("persons.newPerson")}</DialogTitle>
            <DialogDescription>{isEditing ? t("persons.editDesc") : t("persons.addDesc")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            {isEditing && person && (
              <AvatarUpload
                personId={person.id}
                name={person.name}
                photoUrl={person.photoUrl}
              />
            )}
            <Controller
              name="name"
              control={control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="person-name">{t("persons.name")}</FieldLabel>
                  <Input
                    {...field}
                    id="person-name"
                    placeholder={t("persons.namePlaceholder")}
                    maxLength={PERSON_NAME_MAX_LENGTH}
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
            <Field>
              <FieldLabel>{t("persons.tags")}</FieldLabel>
              <TagSelector value={selectedTags} onChange={setSelectedTags} />
            </Field>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={close}>
              {t("persons.cancel")}
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
              {t("persons.save")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
