import { useTranslation } from "react-i18next";
import { TransactionType } from "@/lib/constants";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

/**
 * Campos reutilizables del formulario de regla de importación (plan deuda §7.17.3).
 * Son patrones "campo del formulario de regla", no componentes de dominio: aquí no
 * hay lógica de negocio. La validación (`validate`) y la construcción del body
 * (`handleSave`) viven en `EmailRuleForm`, que es el corazón del formulario.
 */

/** Patrones de extracción por campo (serializados en `FieldPatternsJson`). */
export interface FieldPatterns {
  amount: string;
  date: string;
  merchant: string;
  direction: string;
  symbol: string;
}

// NOTA (desviación documentada del plan §7.17.3): `EMPTY_PATTERNS` se quedó en
// `EmailRuleForm.tsx` en vez de vivir aquí. `react-refresh/only-export-components`
// (preset vite) prohíbe exportar objetos desde archivos de componentes —solo
// permite constantes literales—, y el único consumidor de la constante es
// `parsePatterns`, que permanece en el formulario.

export function AccountSelect({
  id,
  value,
  onChange,
  accounts,
  describedBy,
  invalid,
}: {
  /** id del trigger, para asociar el FieldLabel (WCAG 1.3.1). */
  id?: string;
  value: string;
  onChange: (id: string) => void;
  accounts: { id: string; name: string }[];
  /** aria-describedby del trigger (p. ej. el FieldError de cuenta obligatoria). */
  describedBy?: string;
  /**
   * `aria-invalid` del trigger. El `data-invalid` del `Field`wrapper sólo repinta el texto de la
   * etiqueta: sin esto, el elemento interactivo no sabe que está inválido y el lector de
   * pantalla no lo dice. Es el tercer elemento del patrón `htmlFor` + `id` + `aria-invalid` que
   * el módulo replica de `CreateAccountForm`.
   */
  invalid?: boolean;
}) {
  const { t } = useTranslation();
  const items = [
    { label: t("emailImport.rules.selectAccount"), value: "" },
    ...accounts.map((a) => ({ label: a.name, value: a.id })),
  ];
  return (
    <Select
      items={items}
      value={value}
      onValueChange={(v) => {
        if (v !== null) onChange(String(v));
      }}
    >
      <SelectTrigger
        id={id}
        aria-describedby={describedBy}
        aria-invalid={invalid || undefined}
        className="w-full"
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {items.map((item) => (
          <SelectItem key={item.value} value={item.value}>
            {item.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export function TypeSelect({
  id,
  value,
  onChange,
}: {
  /** id del trigger, para asociar el FieldLabel (WCAG 1.3.1). */
  id?: string;
  value: string;
  onChange: (v: string) => void;
}) {
  const { t } = useTranslation();
  // Valores desde el enum, no literales "0"/"1": el mismo enum se usa en PendingTransactionRow
  // y dos fuentes para el mismo concepto es la forma de que se desincronicen.
  const items = [
    { label: t("transactions.income"), value: String(TransactionType.Income) },
    { label: t("transactions.expense"), value: String(TransactionType.Expense) },
  ];
  return (
    <Select
      items={items}
      value={value}
      onValueChange={(v) => {
        if (v !== null) onChange(String(v));
      }}
    >
      <SelectTrigger id={id} className="w-full">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {items.map((item) => (
          <SelectItem key={item.value} value={item.value}>
            {item.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
