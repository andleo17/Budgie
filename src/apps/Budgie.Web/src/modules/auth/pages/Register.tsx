import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, Link } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { registerSchema, type RegisterFormValues } from "@/modules/auth/schemas";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, FieldLabel, FieldError } from "@/components/ui/field";
import { Loader2, CheckCircle2 } from "lucide-react";
import { AuthLayout } from "../components/AuthLayout";
import { AuthErrorBanner } from "../components/AuthErrorBanner";

export default function Register() {
  const { t } = useTranslation();
  const [done, setDone] = useState(false);
  const form = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
  });
  const navigate = useNavigate();
  const mutation = useMutation({
    mutationFn: async (body: {
      name: string;
      email: string;
      password: string;
    }) => {
      const { data, error } = await client.POST("/api/users/register", {
        body,
      });
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      setDone(true);
      setTimeout(() => navigate("/login"), 2000);
    },
  });

  const onSubmit = (data: RegisterFormValues) => {
    mutation.mutate({
      name: data.name,
      email: data.email,
      password: data.password,
    });
  };

  const error =
    mutation.error &&
    !form.formState.errors.name &&
    !form.formState.errors.email &&
    !form.formState.errors.password
      ? mutation.error?.message || t("auth.registrationFailed")
      : "";

  return (
    <AuthLayout tagline={t("auth.taglineRegister")}>
      {done ? (
        <div className="flex flex-col items-center gap-3 py-6 text-center">
          <CheckCircle2 className="h-10 w-10 text-emerald-400" />
          <p className="font-semibold">{t("auth.accountCreated")}</p>
          <p className="text-sm text-muted-foreground">
            {t("auth.redirecting")}
          </p>
        </div>
      ) : (
        <>
          <div className="mb-6">
            <h2 className="text-xl font-semibold">{t("auth.createAccount")}</h2>
            <p className="text-sm text-muted-foreground mt-1">
              {t("auth.getStartedFree")}
            </p>
          </div>

          <AuthErrorBanner error={error} />

          <form
            onSubmit={form.handleSubmit(onSubmit)}
            className="grid gap-4"
          >
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor={field.name}>{t("auth.name")}</FieldLabel>
                  <Input
                    {...field}
                    id={field.name}
                    placeholder={t("auth.namePlaceholder")}
                    autoComplete="name"
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid && (
                    <FieldError errors={[fieldState.error]} />
                  )}
                </Field>
              )}
            />
            <Controller
              name="email"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor={field.name}>{t("auth.email")}</FieldLabel>
                  <Input
                    {...field}
                    id={field.name}
                    type="email"
                    placeholder={t("auth.emailPlaceholder")}
                    autoComplete="email"
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid && (
                    <FieldError errors={[fieldState.error]} />
                  )}
                </Field>
              )}
            />
            <Controller
              name="password"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor={field.name}>{t("auth.password")}</FieldLabel>
                  <Input
                    {...field}
                    id={field.name}
                    type="password"
                    autoComplete="new-password"
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid && (
                    <FieldError errors={[fieldState.error]} />
                  )}
                </Field>
              )}
            />
            <Button
              type="submit"
              className="mt-2 w-full"
              disabled={mutation.isPending}
            >
              {mutation.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              {t("auth.createAccountBtn")}
            </Button>
          </form>

          <p className="mt-5 text-center text-sm text-muted-foreground">
            {t("auth.alreadyHaveAccount")}{" "}
            <Link
              to="/login"
              className="text-primary hover:underline underline-offset-4 font-medium"
            >
              {t("auth.signInLink")}
            </Link>
          </p>
        </>
      )}
    </AuthLayout>
  );
}
