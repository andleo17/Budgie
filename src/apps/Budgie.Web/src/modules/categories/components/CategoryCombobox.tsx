import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { buildCategoryTree, type CategoryTreeItem } from "@/modules/categories/utils";
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxTrigger,
  ComboboxValue,
  ComboboxList,
  ComboboxItem,
} from "@/components/ui/combobox";
import { Button } from "@/components/ui/button";

type CategoryResponse = components["schemas"]["CategoryResponse"];

interface CategoryComboboxProps {
  value: string | undefined;
  onValueChange: (id: string | undefined) => void;
  placeholder?: string;
  includeSystem?: boolean;
  excludeCategoryId?: string;
  type?: number;
  categories?: CategoryResponse[];
  showNoneOption?: boolean;
}

export function CategoryCombobox({
  value,
  onValueChange,
  placeholder,
  includeSystem,
  excludeCategoryId,
  type,
  categories: propCategories,
  showNoneOption,
}: CategoryComboboxProps) {
  const { t } = useTranslation();

  const { data: fetchedCategories } = useQuery({
    queryKey: ["categories", "includeSystem"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/categories");
      if (error) throw error;
      return data;
    },
    enabled: !propCategories,
  });

  const categories = useMemo(() => propCategories ?? fetchedCategories ?? [], [propCategories, fetchedCategories]);

  const items = useMemo(() => {
    const tree = buildCategoryTree(categories, {
      excludeCategoryId,
      includeSystem,
      type,
    });

    if (showNoneOption) {
      return [
        { id: "", name: t("categories.noParent"), depth: 0, path: t("categories.noParent") },
        ...tree,
      ];
    }

    return tree;
  }, [categories, excludeCategoryId, includeSystem, type, showNoneOption, t]);

  const current = items.find((i) => i.id === value) ?? null;

  return (
    <Combobox
      items={items}
      value={current}
      onValueChange={(v: CategoryTreeItem | null) =>
        onValueChange(v?.id ?? undefined)
      }
      itemToStringLabel={(item: CategoryTreeItem) => item.id ? item.path : item.name}
    >
      <ComboboxTrigger
        render={
          <Button
            variant="outline"
            className="w-full justify-between font-normal"
          />
        }
      >
        <ComboboxValue placeholder={placeholder ?? t("categories.noParent")} />
      </ComboboxTrigger>
      <ComboboxContent>
        <ComboboxInput
          showTrigger={false}
          placeholder={t("categories.searchParent")}
        />
        <ComboboxEmpty>{t("categories.noParentFound")}</ComboboxEmpty>
        <ComboboxList>
          {(item: CategoryTreeItem) => (
            <ComboboxItem key={item.id} value={item}>
              <span
                className="flex items-center gap-1"
                style={{ paddingLeft: `${item.depth * 16}px` }}
              >
                <span className={item.id === "" ? "text-muted-foreground/50 italic" : ""}>
                  {item.name}
                </span>
                {item.isSystem && (
                  <span className="text-xs text-muted-foreground">
                    ({t("categories.system")})
                  </span>
                )}
              </span>
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}
