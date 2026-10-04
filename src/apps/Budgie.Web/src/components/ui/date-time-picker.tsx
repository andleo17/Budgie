import { format } from "date-fns";
import { CalendarIcon, Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Input } from "@/components/ui/input";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { getDateLocale } from "@/lib/date-locale";
import { useTranslation } from "react-i18next";

interface DateTimePickerProps {
  /**
   * id del trigger, para asociar un Label externo con `htmlFor` (WCAG 1.3.1).
   * Mismo patrón que `CategoryCombobox`.
   */
  id?: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

export function DateTimePicker({ id, value, onChange, disabled }: DateTimePickerProps) {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);

  const dateStr = value?.slice(0, 10) ?? "";
  const timeStr = value?.slice(11, 19) ?? "";

  const selectedDate = dateStr ? new Date(dateStr + "T00:00:00") : undefined;

  const handleDateSelect = (date: Date | undefined) => {
    if (disabled || !date) return;
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, "0");
    const d = String(date.getDate()).padStart(2, "0");
    onChange(`${y}-${m}-${d}T${timeStr || "00:00:00"}`);
  };

  const handleMonthChange = (month: Date) => {
    if (disabled || !selectedDate) return;
    const y = month.getFullYear();
    const m = month.getMonth();
    const daysInMonth = new Date(y, m + 1, 0).getDate();
    const d = Math.min(selectedDate.getDate(), daysInMonth);
    const mm = String(m + 1).padStart(2, "0");
    const dd = String(d).padStart(2, "0");
    onChange(`${y}-${mm}-${dd}T${timeStr || "00:00:00"}`);
  };

  const handleTimeChange = (t: string) => {
    if (disabled) return;
    const normalizedTime = t.length === 5 ? `${t}:00` : t;
    if (!dateStr) {
      const now = new Date();
      const y = now.getFullYear();
      const m = String(now.getMonth() + 1).padStart(2, "0");
      const d = String(now.getDate()).padStart(2, "0");
      onChange(`${y}-${m}-${d}T${normalizedTime}`);
    } else {
      onChange(`${dateStr}T${normalizedTime}`);
    }
  };

  return (
    <Popover>
      <PopoverTrigger render={

        <Button
          id={id}
          variant="outline"
          className="w-full justify-start text-left font-normal"
          disabled={disabled}
        >
          <CalendarIcon className="mr-2 size-4 shrink-0" />
          {value
            ? format(
                new Date(dateStr + "T" + (timeStr || "00:00")),
                "MMM d, yyyy HH:mm",
                { locale: dateLocale },
              )
            : t("transactions.date")}
        </Button>
          } />
      <PopoverContent className="w-auto p-3" align="start">
        <Calendar
          mode="single"
          locale={dateLocale}
          selected={selectedDate}
          defaultMonth={selectedDate}
          onSelect={handleDateSelect}
          onMonthChange={handleMonthChange}
          captionLayout="dropdown"
          startMonth={new Date(new Date().getFullYear() - 10, 0, 1)}
          endMonth={new Date(new Date().getFullYear() + 1, 11, 31)}
          autoFocus
        />
        <div className="mt-3 flex items-center gap-2 border-t pt-3">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="text-xs"
            disabled={disabled}
            onClick={() => {
              const now = new Date();
              const y = now.getFullYear();
              const m = String(now.getMonth() + 1).padStart(2, "0");
              const d = String(now.getDate()).padStart(2, "0");
              const h = String(now.getHours()).padStart(2, "0");
              const min = String(now.getMinutes()).padStart(2, "0");
              onChange(`${y}-${m}-${d}T${h}:${min}:00`);
            }}
          >
            <CalendarIcon className="mr-1 size-3.5" />
            {t("transactions.today")}
          </Button>
          <Clock className="size-4 text-muted-foreground shrink-0" />
          <Input
            type="time"
            value={timeStr}
            onChange={(e) => handleTimeChange(e.target.value)}
            className="w-32"
            disabled={disabled}
          />
        </div>
      </PopoverContent>
    </Popover>
  );
}
