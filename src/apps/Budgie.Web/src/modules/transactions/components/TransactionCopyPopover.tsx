import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { format } from "date-fns";
import { Copy, Search } from "lucide-react";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { formatCurrency } from "@/lib/format";
import { parseDbDate, getDateLocale } from "@/lib/date-locale";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";

type TransactionResponse = components["schemas"]["TransactionResponse"];

interface TransactionCopyPopoverProps {
  onSelect: (tx: TransactionResponse) => void;
}

export function TransactionCopyPopover({ onSelect }: TransactionCopyPopoverProps) {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");

  const { data, isLoading } = useQuery({
    queryKey: ["transactions", "copy", search],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/transactions", {
        params: {
          query: {
            transactionTypes: "0,1",
            search: search || undefined,
            pageSize: 100,
            page: 1,
          },
        },
      });
      if (error) throw error;
      return data;
    },
    enabled: open,
  });

  const items = (data?.items ?? []).filter(
    (tx) => tx.type === 0 || tx.type === 1,
  );

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <Button type="button" variant="outline" size="sm" className="text-xs">
            <Copy className="mr-1 size-3.5" />
            {t("transactions.copyFromTransaction")}
          </Button>
        }
      />
      <PopoverContent className="w-80 p-2" align="start">
        <div className="relative mb-2">
          <Search className="absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("transactions.searchTransactions")}
            className="pl-8 h-8 text-xs"
            autoFocus
          />
        </div>
        <div className="max-h-72 overflow-y-auto flex flex-col gap-1">
          {isLoading && (
            <p className="text-xs text-muted-foreground px-1 py-2">{t("transactions.loading")}</p>
          )}
          {!isLoading && items.length === 0 && (
            <p className="text-xs text-muted-foreground px-1 py-2">{t("transactions.noResults")}</p>
          )}
          {items.map((tx) => (
            <button
              key={tx.id}
              type="button"
              className="flex flex-col items-start gap-0.5 rounded-md border bg-card px-2.5 py-1.5 text-left text-xs transition-colors hover:bg-accent"
              onClick={() => {
                onSelect(tx);
                setOpen(false);
                setSearch("");
              }}
            >
              <div className="flex w-full items-center justify-between gap-2">
                <span className="truncate font-medium">
                  {tx.categoryName ?? tx.accountName}
                </span>
                <span className="shrink-0 font-semibold">
                  {formatCurrency(Number(tx.amount), tx.currency)}
                </span>
              </div>
              <div className="flex w-full items-center justify-between gap-2 text-muted-foreground">
                <span className="truncate">{tx.accountName}</span>
                <span className="shrink-0">
                  {format(parseDbDate(tx.date), "MMM d, yyyy", { locale: dateLocale })}
                </span>
              </div>
            </button>
          ))}
        </div>
      </PopoverContent>
    </Popover>
  );
}
