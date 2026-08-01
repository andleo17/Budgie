import { useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { useForm, Controller, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { toast } from "sonner";
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
import { CategoryCombobox } from "@/modules/categories/components/CategoryCombobox";
import { Field, FieldLabel, FieldError } from "@/components/ui/field";
import { Loader2 } from "lucide-react";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

type CategoryResponse = components["schemas"]["CategoryResponse"];

const TYPE_LABEL_KEYS = [
  "income",
  "expense",
  "transfer",
  "loan",
  "repayment",
] as const;
const TRANSACTION_TYPES = [0, 1, 2, 3, 4] as const;

const formSchema = z.object({
  name: z.string().min(1),
  color: z.string().optional(),
  icon: z.string().optional(),
  type: z.number().int().min(0).max(4),
  parentId: z.string().optional(),
});

type FormValues = z.infer<typeof formSchema>;

interface CategoryFormDialogProps {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  category?: CategoryResponse | null;
  categories?: CategoryResponse[];
}

export function CategoryFormDialog({
  open,
  onOpenChange,
  category,
  categories,
}: CategoryFormDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const isEditing = !!category;

  const mutation = useMutation({
    mutationFn: async (
      body: components["schemas"]["CreateCategoryCommand"],
    ) => {
      if (isEditing && category) {
        const { error } = await client.PUT("/api/finances/categories/{id}", {
          params: { path: { id: category.id } },
          body,
        });
        if (error) throw error;
      } else {
        const { error } = await client.POST("/api/finances/categories", {
          body,
        });
        if (error) throw error;
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      onOpenChange(false);
    },
    onError: (err: unknown) => {
      let message: string | undefined;
      if (Array.isArray(err)) {
        const first = err.find(Boolean);
        if (first && typeof first === 'object' && 'description' in first && typeof first.description === 'string') {
          message = first.description;
        }
      } else if (err && typeof err === 'object' && 'errors' in err) {
        const errors = err.errors;
        if (typeof errors === 'object' && errors) {
          for (const key in errors) {
            const msgs: unknown = (errors as Record<string, unknown>)[key];
            if (Array.isArray(msgs) && typeof msgs[0] === 'string') {
              message = msgs[0];
              break;
            }
          }
        }
      }
      toast.error(message || t("categories.error"));
    },
  });

  const defaultValues = useMemo<FormValues>(
    () => ({
      name: category?.name ?? "",
      color: category?.color ?? "",
      icon: category?.icon ?? "",
      type: Number(category?.type ?? 1),
      parentId: category?.parentId ?? undefined,
    }),
    [category],
  );

  const { control, handleSubmit, reset } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues,
  });

  useEffect(() => {
    if (open) {
      reset(defaultValues);
    }
  }, [open, defaultValues, reset]);

  const watchedType = useWatch({ control, name: "type" });



  const typeItems = useMemo(
    () => [
      { label: t("categories.selectType"), value: null },
      ...TRANSACTION_TYPES.map((type) => ({
        label: t(`transactions.${TYPE_LABEL_KEYS[type]}`),
        value: String(type),
      })),
    ],
    [t],
  );

  const onSubmit = (values: FormValues) => {
    mutation.mutate({
      name: values.name,
      color: values.color || "",
      icon: values.icon || "",
      type: values.type,
      parentId: values.parentId || null,
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <form onSubmit={handleSubmit(onSubmit)}>
          <DialogHeader>
            <DialogTitle>
              {isEditing
                ? t("categories.editTitle")
                : t("categories.createTitle")}
            </DialogTitle>
            <DialogDescription>
              {isEditing
                ? t("categories.editDesc")
                : t("categories.createDesc")}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <Controller
              name="name"
              control={control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="cat-name">
                    {t("categories.name")}
                  </FieldLabel>
                  <Input
                    id="cat-name"
                    {...field}
                    placeholder={t("categories.namePlaceholder")}
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid && (
                    <FieldError errors={[fieldState.error]} />
                  )}
                </Field>
              )}
            />
            <Controller
              name="type"
              control={control}
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="cat-type">
                    {t("categories.type")}
                  </FieldLabel>
                  <Select
                    items={typeItems}
                    value={String(field.value)}
                    onValueChange={(v) => field.onChange(Number(v))}
                  >
                    <SelectTrigger id="cat-type">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectGroup>
                        {typeItems.map((item) => (
                          <SelectItem key={item.value} value={item.value}>
                            {item.label}
                          </SelectItem>
                        ))}
                      </SelectGroup>
                    </SelectContent>
                  </Select>
                </Field>
              )}
            />
            <Controller
              name="parentId"
              control={control}
              render={({ field }) => (
                <Field>
                  <FieldLabel>{t("categories.parent")}</FieldLabel>
                  <CategoryCombobox
                    value={field.value}
                    onValueChange={(id) => field.onChange(id)}
                    excludeCategoryId={category?.id}
                    type={watchedType}
                    categories={categories}
                    showNoneOption
                  />
                </Field>
              )}
            />
            <Controller
              name="color"
              control={control}
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="cat-color">
                    {t("categories.color")}
                  </FieldLabel>
                  <div className="flex items-center gap-2">
                    <Input
                      id="cat-color"
                      type="color"
                      value={field.value || "#000000"}
                      onChange={(e) => field.onChange(e.target.value)}
                      className="h-8 w-12 cursor-pointer p-0.5"
                    />
                    <span className="font-mono text-xs text-muted-foreground">
                      {field.value || "#000000"}
                    </span>
                  </div>
                </Field>
              )}
            />
            <Controller
              name="icon"
              control={control}
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="cat-icon">
                    {t("categories.icon")}
                  </FieldLabel>
                  <Input
                    id="cat-icon"
                    {...field}
                    placeholder={t("categories.iconPlaceholder")}
                  />
                </Field>
              )}
            />
          </div>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
            >
              {t("categories.cancel")}
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              {isEditing ? t("categories.save") : t("categories.create")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
