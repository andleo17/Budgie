import { useState } from "react";
import { useTranslation } from "react-i18next";
import { PlusCircle, ChevronDown, ArrowUpRight, ArrowLeftRight, TrendingDown, RotateCcw } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
} from "@/components/ui/dropdown-menu";

export type FormType = "income-expense" | "transfer" | "loan" | "collection";

interface TransactionTypeDropdownProps {
  onSelect: (type: FormType) => void;
}

const items: { type: FormType; labelKey: string; icon: typeof ArrowUpRight }[] = [
  { type: "income-expense", labelKey: "transactions.incomeExpense", icon: ArrowUpRight },
  { type: "transfer", labelKey: "transactions.transfer", icon: ArrowLeftRight },
  { type: "loan", labelKey: "transactions.loan", icon: TrendingDown },
  { type: "collection", labelKey: "transactions.collection", icon: RotateCcw },
];

export function TransactionTypeDropdown({ onSelect }: TransactionTypeDropdownProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <div className="flex shrink-0">
      <button
        onClick={() => onSelect("income-expense")}
        className="inline-flex h-10 items-center justify-center gap-2 rounded-l-lg border border-transparent bg-primary px-4 text-sm font-medium text-primary-foreground whitespace-nowrap transition-all hover:bg-primary/80 active:translate-y-px"
      >
        <PlusCircle className="size-4 shrink-0" />
        {t("transactions.addTransaction")}
      </button>
      <DropdownMenu open={open} onOpenChange={setOpen}>
        <DropdownMenuTrigger className="inline-flex h-10 w-8 items-center justify-center rounded-r-lg border-l border-primary-foreground/20 bg-primary text-primary-foreground transition-all hover:bg-primary/80 active:translate-y-px">
          <ChevronDown className="size-4" />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="min-w-45">
          {items.map((item) => {
            const Icon = item.icon;
            return (
              <DropdownMenuItem
                key={item.type}
                onClick={() => {
                  onSelect(item.type);
                  setOpen(false);
                }}
                className="gap-2"
              >
                <Icon className="size-4 shrink-0" />
                <span>{t(item.labelKey)}</span>
              </DropdownMenuItem>
            );
          })}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
