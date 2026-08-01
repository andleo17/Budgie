import { useEffect } from "react";
import { useMutation, useQueryClient, useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { createGoalSchema, type CreateGoalFormValues } from "../schemas";
import { Input } from "@/components/ui/input";
import {
  Field,
  FieldLabel,
  FieldContent,
} from "@/components/ui/field";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from "@/components/ui/combobox";
import { Loader2 } from "lucide-react";
import { type GoalAccount } from "./goal-utils";

export default function GoalFormDialog({
  open,
  onOpenChange,
  editGoal,
  t,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  editGoal: GoalAccount | null;
  t: (key: string) => string;
}) {
  const queryClient = useQueryClient();
  const isEditing = !!editGoal;

  const { data: accounts } = useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data ?? [];
    },
    enabled: !isEditing && open,
  });

  const form = useForm<CreateGoalFormValues>({
    resolver: zodResolver(createGoalSchema),
    defaultValues: {
      targetAmount: "",
      targetDate: "",
    },
  });

  useEffect(() => {
    if (editGoal) {
      form.reset({
        targetAmount: editGoal.targetAmount
          ? String(editGoal.targetAmount)
          : "",
        targetDate: editGoal.targetDate ? editGoal.targetDate.slice(0, 10) : "",
      });
    } else {
      form.reset({
        targetAmount: "",
        targetDate: "",
      });
    }
  }, [editGoal, open, form]);

  const createMutation = useMutation({
    mutationFn: async (body: {
      accountId: string;
      targetAmount: number;
      targetDate: string | null;
    }) => {
      const { data, error } = await client.POST("/api/finances/goals", {
        body,
      });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["goals"] });
      onOpenChange(false);
    },
  });

  const updateMutation = useMutation({
    mutationFn: async ({
      id,
      ...body
    }: {
      id: string;
      targetAmount: number;
      targetDate: string | null;
    }) => {
      const { error } = await client.PUT("/api/finances/goals/{id}", {
        params: { path: { id } },
        body,
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["goals"] });
      onOpenChange(false);
    },
  });

  const isPending = createMutation.isPending || updateMutation.isPending;
  const error = createMutation.error ?? updateMutation.error;

  const onSubmit = (data: CreateGoalFormValues) => {
    if (isEditing) {
      updateMutation.mutate({
        id: editGoal.id,
        targetAmount: data.targetAmount ? parseFloat(data.targetAmount) : 0,
        targetDate: data.targetDate
          ? new Date(data.targetDate).toISOString()
          : null,
      });
    } else {
      createMutation.mutate({
        accountId: data.accountId ?? "",
        targetAmount: data.targetAmount ? parseFloat(data.targetAmount) : 0,
        targetDate: data.targetDate
          ? new Date(data.targetDate).toISOString()
          : null,
      });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={form.handleSubmit(onSubmit)}>
          <DialogHeader>
            <DialogTitle>
              {isEditing ? t("goals.editGoal") : t("goals.newGoal")}
            </DialogTitle>
            <DialogDescription>
              {isEditing ? t("goals.editGoalDesc") : t("goals.newGoalDesc")}
            </DialogDescription>
          </DialogHeader>

          {error && (
            <div className="my-3 rounded-lg bg-destructive/10 border border-destructive/20 px-3 py-2.5 text-sm text-destructive">
              {error?.message ?? t("goals.somethingWentWrong")}
            </div>
          )}

          <div className="grid gap-4 py-4">
            {!isEditing && (
              <Field>
                <FieldLabel>{t("goals.account")}</FieldLabel>
                <FieldContent>
                  <Controller
                    name="accountId"
                    control={form.control}
                    render={({ field }) => {
                      const sorted = accounts ?? [];
                      const selected = sorted.find((a) => a.id === field.value) ?? null;
                      return (
                        <Combobox
                          items={sorted}
                          itemToStringValue={(a) => `${a.name} (${a.currency})`}
                          value={selected}
                          onValueChange={(a) => field.onChange(a?.id ?? "")}
                        >
                          <ComboboxInput placeholder={t("goals.selectAccount")} />
                          <ComboboxContent>
                            <ComboboxEmpty>{t("goals.noResults")}</ComboboxEmpty>
                            <ComboboxList>
                              {(a) => (
                                <ComboboxItem key={a.id} value={a}>
                                  {a.name} ({a.currency})
                                </ComboboxItem>
                              )}
                            </ComboboxList>
                          </ComboboxContent>
                        </Combobox>
                      );
                    }}
                  />
                </FieldContent>
              </Field>
            )}

            <Controller
              name="targetAmount"
              control={form.control}
              render={({ field }) => (
                <Field>
                  <FieldLabel>
                    {t("goals.targetAmount")}{" "}
                    <span className="text-muted-foreground font-normal">
                      {t("goals.optional")}
                    </span>
                  </FieldLabel>
                  <FieldContent>
                    <Input
                      {...field}
                      type="number"
                      step="0.01"
                      min="0"
                      placeholder={t("goals.targetAmountPlaceholder")}
                    />
                  </FieldContent>
                </Field>
              )}
            />

            <Controller
              name="targetDate"
              control={form.control}
              render={({ field }) => (
                <Field>
                  <FieldLabel>
                    {t("goals.targetDate")}{" "}
                    <span className="text-muted-foreground font-normal">
                      {t("goals.optional")}
                    </span>
                  </FieldLabel>
                  <FieldContent>
                    <Input
                      {...field}
                      type="date"
                      min={new Date().toISOString().slice(0, 10)}
                    />
                  </FieldContent>
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
              {t("goals.cancel")}
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {isEditing ? t("goals.saveChanges") : t("goals.createGoal")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
