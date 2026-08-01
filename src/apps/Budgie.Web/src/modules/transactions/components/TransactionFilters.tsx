import { useMemo, useState, useCallback, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { format } from "date-fns";
import { getDateLocale } from "@/lib/date-locale";
import client from "@/lib/api-client";
import { buildCategoryTree } from "@/modules/categories/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";

import { Calendar } from "@/components/ui/calendar";
import {
  Search,
  CalendarIcon,
  RotateCcw,
  ChevronsUpDown,
  SlidersHorizontal,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";

import { VIEW_MODES, parseViewMode } from "@/lib/constants";
import {
  getMonthRange,
  navigateMonth,
  findCurrentBillingPeriod,
  getBillingPeriodRange,
  navigateBillingPeriod,
  type PeriodRange,
} from "../period-utils";

const TRANSACTION_TYPE_KEYS = ["income", "expense", "transfer", "loan", "repayment"] as const;

function parseLocalDate(dateStr: string): Date {
  const [y, m, d] = dateStr.split("-").map(Number);
  return new Date(y, m - 1, d);
}

const MONTHS = [
  "january", "february", "march", "april", "may", "june",
  "july", "august", "september", "october", "november", "december",
] as const;

export function TransactionFilters() {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  const [searchParams, setSearchParams] = useSearchParams();

  const search = searchParams.get("search") ?? "";
  const accountsParam = searchParams.get("accounts") ?? "";
  const categoriesParam = searchParams.get("categories") ?? "";
  const typesParam = searchParams.get("types") ?? "";
  const personIdsParam = searchParams.get("personIds") ?? "";
  const tagIdsParam = searchParams.get("tagIds") ?? "";
  const rawFrom = searchParams.get("from");
  const rawTo = searchParams.get("to");
  const view = parseViewMode(searchParams.get("view"));

  const now = useMemo(() => new Date(), []);
  const from = rawFrom ?? "";
  const to = rawTo ?? "";

  const isCustomMode = !!rawFrom || !!rawTo;

  const selectedAccountIds = useMemo(
    () => accountsParam.split(",").filter(Boolean),
    [accountsParam],
  );

  const { data: accounts } = useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data;
    },
  });

  const creditAccount = useMemo(() => {
    if (selectedAccountIds.length !== 1) return null;
    const a = accounts?.find((ac) => ac.id === selectedAccountIds[0]);
    return a?.type === 2 ? a : null;
  }, [accounts, selectedAccountIds]);

  const cd = creditAccount?.billingCycleDay != null ? Number(creditAccount.billingCycleDay) : null;
  const cdi = creditAccount?.billingCycleDayInclusive ?? true;

  // Current period (in period mode only — read from URL `period` param)
  const periodKey = searchParams.get("period");
  const currentPeriod: PeriodRange | null = useMemo(() => {
    if (!periodKey) {
      if (cd != null) {
        return findCurrentBillingPeriod(now, cd, cdi, dateLocale);
      }
      return getMonthRange(now.getFullYear(), now.getMonth(), dateLocale);
    }
    // billing ref key format: YYYY-MM-DD (10 chars)
    if (cd != null && periodKey.length === 10) {
      const d = parseLocalDate(periodKey);
      if (!isNaN(d.getTime())) {
        return getBillingPeriodRange(d, cd, cdi, dateLocale);
      }
    }
    // month key format: YYYY-MM (7 chars)
    if (periodKey.length === 7) {
      const parts = periodKey.split("-");
      const y = parseInt(parts[0], 10);
      const m = parseInt(parts[1], 10) - 1;
      if (!isNaN(y) && !isNaN(m)) {
        return getMonthRange(y, m, dateLocale);
      }
    }
    // fallback to current
    if (cd != null) {
      return findCurrentBillingPeriod(now, cd, cdi, dateLocale);
    }
    return getMonthRange(now.getFullYear(), now.getMonth(), dateLocale);
  }, [periodKey, cd, cdi, now, dateLocale]);

  const syncPeriod = useCallback(
    (period: PeriodRange) => {
      const next = new URLSearchParams(searchParams);
      next.delete("from");
      next.delete("to");
      next.set("period", period.key);
      setSearchParams(next, { replace: true });
    },
    [searchParams, setSearchParams],
  );

  // Initialize period on mount if none set
  const initialized = useRef(false);
  useEffect(() => {
    if (initialized.current) return;
    initialized.current = true;
    if (!searchParams.get("period") && !isCustomMode && currentPeriod) {
      syncPeriod(currentPeriod);
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const goNext = useCallback(() => {
    if (cd != null && periodKey?.length === 10) {
      const ref = parseLocalDate(periodKey);
      const nextRef = navigateBillingPeriod(ref, cd, 1);
      syncPeriod(getBillingPeriodRange(nextRef, cd, cdi, dateLocale));
    } else {
      const cur = currentPeriod
        ? navigateMonth(
            parseLocalDate(currentPeriod.from).getFullYear(),
            parseLocalDate(currentPeriod.from).getMonth(),
            1,
          )
        : { year: now.getFullYear(), month: now.getMonth() };
      syncPeriod(getMonthRange(cur.year, cur.month, dateLocale));
    }
  }, [cd, periodKey, currentPeriod, now, cdi, dateLocale, syncPeriod]);

  const goPrev = useCallback(() => {
    if (cd != null && periodKey?.length === 10) {
      const ref = parseLocalDate(periodKey);
      const prevRef = navigateBillingPeriod(ref, cd, -1);
      syncPeriod(getBillingPeriodRange(prevRef, cd, cdi, dateLocale));
    } else {
      const cur = currentPeriod
        ? navigateMonth(
            parseLocalDate(currentPeriod.from).getFullYear(),
            parseLocalDate(currentPeriod.from).getMonth(),
            -1,
          )
        : { year: now.getFullYear(), month: now.getMonth() };
      syncPeriod(getMonthRange(cur.year, cur.month, dateLocale));
    }
  }, [cd, periodKey, currentPeriod, now, cdi, dateLocale, syncPeriod]);

  const switchToCustomMode = useCallback(() => {
    const next = new URLSearchParams(searchParams);
    next.set("from", currentPeriod!.from);
    next.set("to", currentPeriod!.to);
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams, currentPeriod]);

  const switchToPeriodMode = useCallback(() => {
    const next = new URLSearchParams(searchParams);
    if (currentPeriod) {
      next.delete("from");
      next.delete("to");
      next.set("period", currentPeriod.key);
    }
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams, currentPeriod]);

  // Year/month picker state
  const [pickerYear, setPickerYear] = useState(now.getFullYear());
  const [, setPickerMonth] = useState(now.getMonth());
  const [pickerOpen, setPickerOpen] = useState(false);

  const handlePickerOpen = useCallback((open: boolean) => {
    setPickerOpen(open);
    if (open && currentPeriod) {
      if (cd != null && periodKey?.length === 10) {
        const ref = parseLocalDate(periodKey);
        setPickerYear(ref.getFullYear());
        setPickerMonth(ref.getMonth());
      } else if (periodKey?.length === 7) {
        const parts = periodKey.split("-");
        setPickerYear(parseInt(parts[0], 10));
        setPickerMonth(parseInt(parts[1], 10) - 1);
      } else {
        setPickerYear(now.getFullYear());
        setPickerMonth(now.getMonth());
      }
    }
  }, [currentPeriod, cd, periodKey, now]);

  const goToPickerMonth = useCallback((month: number) => {
    if (cd != null) {
      const ref = new Date(pickerYear, month, Math.min(cd, 28));
      syncPeriod(getBillingPeriodRange(ref, cd, cdi, dateLocale));
    } else {
      syncPeriod(getMonthRange(pickerYear, month, dateLocale));
    }
    setPickerOpen(false);
  }, [pickerYear, cd, cdi, dateLocale, syncPeriod]);

  const selectedCategoryIds = useMemo(
    () => categoriesParam.split(",").filter(Boolean),
    [categoriesParam],
  );
  const selectedTypeIds = useMemo(
    () => typesParam.split(",").filter(Boolean),
    [typesParam],
  );
  const selectedPersonIds = useMemo(
    () => personIdsParam.split(",").filter(Boolean),
    [personIdsParam],
  );
  const selectedTagIds = useMemo(
    () => tagIdsParam.split(",").filter(Boolean),
    [tagIdsParam],
  );

  const { data: rawCategories } = useQuery({
    queryKey: ["categories", "includeSystem"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/categories", {
        params: { query: { includeSystem: true } },
      });
      if (error) throw error;
      return data;
    },
  });

  const { data: persons } = useQuery({
    queryKey: ["persons"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/persons");
      if (error) throw error;
      return data;
    },
  });

  const { data: tags } = useQuery({
    queryKey: ["tags"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/tags");
      if (error) throw error;
      return data;
    },
  });

  const categoryTree = useMemo(
    () => buildCategoryTree(rawCategories ?? [], { includeSystem: true }),
    [rawCategories],
  );

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(searchParams);
    if (value) {
      next.set(key, value);
    } else {
      next.delete(key);
    }
    setSearchParams(next, { replace: true });
  };

  const toggleAccount = (id: string) => {
    const next = selectedAccountIds.includes(id)
      ? selectedAccountIds.filter((a) => a !== id)
      : [...selectedAccountIds, id];
    setParam("accounts", next.join(","));
  };

  const toggleCategory = (id: string) => {
    const next = selectedCategoryIds.includes(id)
      ? selectedCategoryIds.filter((c) => c !== id)
      : [...selectedCategoryIds, id];
    setParam("categories", next.join(","));
  };

  const toggleType = (id: string) => {
    const next = selectedTypeIds.includes(id)
      ? selectedTypeIds.filter((t) => t !== id)
      : [...selectedTypeIds, id];
    setParam("types", next.join(","));
  };

  const togglePerson = (id: string) => {
    const next = selectedPersonIds.includes(id)
      ? selectedPersonIds.filter((p) => p !== id)
      : [...selectedPersonIds, id];
    setParam("personIds", next.join(","));
  };

  const toggleTag = (tagId: string) => {
    const next = selectedTagIds.includes(tagId)
      ? selectedTagIds.filter((t) => t !== tagId)
      : [...selectedTagIds, tagId];
    setParam("tagIds", next.join(","));
  };

  const clearFilters = () => {
    setSearchParams({}, { replace: true });
  };

  const hasActiveFilters = search || accountsParam || categoriesParam || typesParam || isCustomMode || !!personIdsParam || !!tagIdsParam;

  const [filtersOpen, setFiltersOpen] = useState(hasActiveFilters);

  const [accountSearch, setAccountSearch] = useState("");
  const [categorySearch, setCategorySearch] = useState("");
  const [personSearch, setPersonSearch] = useState("");
  const [tagSearch, setTagSearch] = useState("");

  const accountLabel = selectedAccountIds.length > 0
    ? t("transactions.nAccounts", { count: selectedAccountIds.length })
    : t("transactions.filterAccounts");

  const categoryLabel = selectedCategoryIds.length > 0
    ? t("transactions.nCategories", { count: selectedCategoryIds.length })
    : t("transactions.filterCategories");

  const typeLabel = selectedTypeIds.length > 0
    ? t("transactions.nTypes", { count: selectedTypeIds.length })
    : t("transactions.filterTypes");

  const personLabel = selectedPersonIds.length > 0
    ? t("transactions.nPersons", { count: selectedPersonIds.length })
    : t("transactions.filterPerson");

  const filteredAccounts = useMemo(
    () => (accounts ?? []).filter((a) => a.name.toLowerCase().includes(accountSearch.toLowerCase())),
    [accounts, accountSearch],
  );

  const filteredPersons = useMemo(
    () => (persons ?? []).filter((p) => p.name.toLowerCase().includes(personSearch.toLowerCase())),
    [persons, personSearch],
  );

  const filteredTags = useMemo(
    () => (tags ?? []).filter((t) => t.name.toLowerCase().includes(tagSearch.toLowerCase())),
    [tags, tagSearch],
  );

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <div className="relative min-w-48">
          <Search className="absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground pointer-events-none" />
          <Input
            placeholder={t("transactions.searchPlaceholder")}
            value={search}
            onChange={(e) => setParam("search", e.target.value)}
            className="pl-8"
          />
        </div>
        <Button
          variant={filtersOpen ? "default" : "outline"}
          size="sm"
          onClick={() => setFiltersOpen(!filtersOpen)}
        >
          <SlidersHorizontal className="mr-1.5 size-3.5" />
          {t("transactions.filters")}
        </Button>
        {hasActiveFilters && (
          <Button variant="ghost" size="sm" onClick={clearFilters}>
            <RotateCcw className="mr-1 size-3.5" />
            {t("transactions.clearFilters")}
          </Button>
        )}
      </div>

      {filtersOpen && (
      <div className="flex flex-wrap items-end gap-3">

      {/* Account filter */}
      <Popover onOpenChange={(open) => { if (!open) setAccountSearch(""); }}>
        <PopoverTrigger render={<Button variant="outline" className="min-w-44 justify-between">
          {accountLabel}
          <ChevronsUpDown className="ml-2 size-4 shrink-0 opacity-50" />
        </Button>} />
        <PopoverContent className="w-56 p-2" align="start">
          <div className="relative mb-1">
            <Search className="absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground pointer-events-none" />
            <Input
              value={accountSearch}
              onChange={(e) => setAccountSearch(e.target.value)}
              placeholder={t("transactions.searchAccounts")}
              className="h-7 pl-7 text-xs"
            />
          </div>
          <div className="max-h-60 overflow-y-auto space-y-1">
            {filteredAccounts.length === 0 ? (
              <p className="text-xs text-muted-foreground px-2 py-1">{t("transactions.noResults")}</p>
            ) : (
              filteredAccounts.map((account) => (
                <label
                  key={account.id}
                  className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent"
                >
                  <Checkbox
                    checked={selectedAccountIds.includes(account.id)}
                    onCheckedChange={() => toggleAccount(account.id)}
                  />
                  {account.name}
                </label>
              ))
            )}
          </div>
        </PopoverContent>
      </Popover>

      {/* Category filter */}
      <Popover onOpenChange={(open) => { if (!open) setCategorySearch(""); }}>
        <PopoverTrigger render={<Button variant="outline" className="min-w-44 justify-between">
          {categoryLabel}
          <ChevronsUpDown className="ml-2 size-4 shrink-0 opacity-50" />
        </Button>} />
        <PopoverContent className="w-56 p-2" align="start">
          {selectedCategoryIds.length > 0 && (
            <button
              type="button"
              onClick={() => setParam("categories", "")}
              className="w-full text-left px-2 py-1 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
            >
              {t("transactions.clearFilter")}
            </button>
          )}
          <div className="relative mb-1">
            <Search className="absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground pointer-events-none" />
            <Input
              value={categorySearch}
              onChange={(e) => setCategorySearch(e.target.value)}
              placeholder={t("transactions.searchCategories")}
              className="h-7 pl-7 text-xs"
            />
          </div>
          <div className="max-h-60 overflow-y-auto space-y-1">
            {categoryTree.length === 0 ? (
              <p className="text-xs text-muted-foreground px-2 py-1">{t("transactions.noResults")}</p>
            ) : (
              categoryTree
                .filter((cat) => cat.name.toLowerCase().includes(categorySearch.toLowerCase()))
                .map((cat) => (
                <label
                  key={cat.id}
                  className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent"
                >
                  <Checkbox
                    checked={selectedCategoryIds.includes(cat.id)}
                    onCheckedChange={() => toggleCategory(cat.id)}
                  />
                  <span style={{ paddingLeft: `${cat.depth * 16}px` }}>
                    {cat.name}
                    {cat.isSystem && (
                      <span className="text-xs text-muted-foreground ml-1">
                        ({t("categories.system")})
                      </span>
                    )}
                  </span>
                </label>
              ))
            )}
          </div>
        </PopoverContent>
      </Popover>

      {/* Type filter */}
      <Popover>
        <PopoverTrigger render={<Button variant="outline" className="min-w-36 justify-between">
          {typeLabel}
          <ChevronsUpDown className="ml-2 size-4 shrink-0 opacity-50" />
        </Button>} />
        <PopoverContent className="w-48 p-2" align="start">
          <div className="space-y-1">
            {[0, 1, 2, 3, 4].map((i) => (
              <label
                key={i}
                className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent"
              >
                <Checkbox
                  checked={selectedTypeIds.includes(String(i))}
                  onCheckedChange={() => toggleType(String(i))}
                />
                {t(`transactions.${TRANSACTION_TYPE_KEYS[i]}`)}
              </label>
            ))}
          </div>
        </PopoverContent>
      </Popover>

      {/* Person filter */}
      <Popover onOpenChange={(open) => { if (!open) setPersonSearch(""); }}>
        <PopoverTrigger render={<Button variant="outline" className="min-w-40 justify-between">
          {personLabel}
          <ChevronsUpDown className="ml-2 size-4 shrink-0 opacity-50" />
        </Button>} />
        <PopoverContent className="w-56 p-2" align="start">
          {selectedPersonIds.length > 0 && (
            <button
              type="button"
              onClick={() => setParam("personIds", "")}
              className="w-full text-left px-2 py-1 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
            >
              {t("transactions.clearFilter")}
            </button>
          )}
          <div className="relative mb-1">
            <Search className="absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground pointer-events-none" />
            <Input
              value={personSearch}
              onChange={(e) => setPersonSearch(e.target.value)}
              placeholder={t("transactions.searchPersons")}
              className="h-7 pl-7 text-xs"
            />
          </div>
          <div className="max-h-60 overflow-y-auto space-y-1">
            {filteredPersons.length === 0 ? (
              <p className="text-xs text-muted-foreground px-2 py-1">{t("transactions.noResults")}</p>
            ) : (
              filteredPersons.map((p) => (
                <label
                  key={p.id}
                  className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent"
                >
                  <Checkbox
                    checked={selectedPersonIds.includes(p.id)}
                    onCheckedChange={() => togglePerson(p.id)}
                  />
                  {p.name}
                </label>
              ))
            )}
          </div>
        </PopoverContent>
      </Popover>

      {/* Tag filter */}
      <Popover onOpenChange={(open) => { if (!open) setTagSearch(""); }}>
        <PopoverTrigger render={<Button variant="outline" className="min-w-36 justify-between">
          {selectedTagIds.length > 0
            ? t("transactions.nTags", { count: selectedTagIds.length })
            : t("tags.title")}
          <ChevronsUpDown className="ml-2 size-4 shrink-0 opacity-50" />
        </Button>} />
        <PopoverContent className="w-56 p-2" align="start">
          {selectedTagIds.length > 0 && (
            <button
              type="button"
              onClick={() => setParam("tagIds", "")}
              className="w-full text-left px-2 py-1 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
            >
              {t("transactions.clearFilter")}
            </button>
          )}
          <div className="relative mb-1">
            <Search className="absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground pointer-events-none" />
            <Input
              value={tagSearch}
              onChange={(e) => setTagSearch(e.target.value)}
              placeholder={t("tags.placeholder")}
              className="h-7 pl-7 text-xs"
            />
          </div>
          <div className="max-h-60 overflow-y-auto space-y-1">
            {filteredTags.length === 0 ? (
              <p className="text-xs text-muted-foreground px-2 py-1">{t("tags.noResults")}</p>
            ) : (
              filteredTags.map((tag) => (
                <label
                  key={tag.id}
                  className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent"
                >
                  <Checkbox
                    checked={selectedTagIds.includes(tag.id)}
                    onCheckedChange={() => toggleTag(tag.id)}
                  />
                  {tag.name}
                </label>
              ))
            )}
          </div>
        </PopoverContent>
      </Popover>

      {/* Period navigator / Date range picker */}
      {isCustomMode ? (
        <div className="flex items-center gap-1">
          <Popover>
            <PopoverTrigger render={<Button variant="outline" className="min-w-56 justify-start text-left font-normal">
              <CalendarIcon className="mr-2 size-4" />
              {from && to
                ? `${format(parseLocalDate(from), "MMM d, yyyy", { locale: dateLocale })} – ${format(parseLocalDate(to), "MMM d, yyyy", { locale: dateLocale })}`
                : t("transactions.customDate")}
            </Button>} />
            <PopoverContent className="w-auto p-0" align="start">
              <Calendar
                mode="range"
                locale={dateLocale}
                selected={{
                  from: from ? parseLocalDate(from) : undefined,
                  to: to ? parseLocalDate(to) : undefined,
                }}
                onSelect={(range) => {
                  const next = new URLSearchParams(searchParams);
                  if (range?.from) {
                    next.set("from", format(range.from, "yyyy-MM-dd"));
                  } else {
                    next.delete("from");
                  }
                  if (range?.to) {
                    next.set("to", format(range.to, "yyyy-MM-dd"));
                  } else {
                    next.delete("to");
                  }
                  setSearchParams(next, { replace: true });
                }}
                numberOfMonths={2}
              />
            </PopoverContent>
          </Popover>
          <Button variant="ghost" size="sm" onClick={switchToPeriodMode} className="text-xs text-muted-foreground">
            {t("transactions.backToPeriods") ?? "Periods"}
          </Button>
        </div>
      ) : (
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={goPrev}
            className="rounded-md p-1.5 text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
            aria-label={t("transactions.prevPeriod")}
          >
            <ChevronLeft className="size-4" />
          </button>
          <Popover open={pickerOpen} onOpenChange={handlePickerOpen}>
            <PopoverTrigger render={<Button variant="outline" className="min-w-44 justify-center text-sm font-normal">
              {currentPeriod?.label ?? ""}
            </Button>} />
            <PopoverContent className="w-64 p-3" align="start">
              <div className="flex items-center justify-between mb-3">
                <button
                  type="button"
                  onClick={() => setPickerYear((y) => y - 1)}
                  className="rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
                >
                  <ChevronLeft className="size-4" />
                </button>
                <span className="text-sm font-semibold">{pickerYear}</span>
                <button
                  type="button"
                  onClick={() => setPickerYear((y) => y + 1)}
                  className="rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
                >
                  <ChevronRight className="size-4" />
                </button>
              </div>
              <div className="grid grid-cols-3 gap-1">
                {MONTHS.map((monthKey, i) => (
                  <button
                    key={monthKey}
                    type="button"
                    onClick={() => goToPickerMonth(i)}
                    className={`rounded-md px-2 py-1.5 text-xs font-medium transition-colors ${
                      currentPeriod && !cd
                        ? periodKey === `${pickerYear}-${String(i + 1).padStart(2, "0")}`
                          ? "bg-primary text-primary-foreground"
                          : "text-muted-foreground hover:text-foreground hover:bg-accent"
                        : "text-muted-foreground hover:text-foreground hover:bg-accent"
                    }`}
                  >
                    {t(`transactions.${monthKey}`)}
                  </button>
                ))}
              </div>
            </PopoverContent>
          </Popover>
          <button
            type="button"
            onClick={goNext}
            className="rounded-md p-1.5 text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
            aria-label={t("transactions.nextPeriod")}
          >
            <ChevronRight className="size-4" />
          </button>
          <Button variant="ghost" size="sm" onClick={switchToCustomMode} className="text-xs text-muted-foreground">
            {t("transactions.specificDates")}
          </Button>
        </div>
      )}

      {/* View mode toggle */}
      <div className="flex items-center gap-1 h-9">
        <span className="text-xs text-muted-foreground mr-1">{t("transactions.view")}:</span>
        {VIEW_MODES.map((mode) => (
          <button
            key={mode}
            type="button"
            onClick={() => setParam("view", mode === "daily" ? "" : mode)}
            className={`rounded-md px-2.5 py-1.5 text-xs font-medium transition-colors ${
              view === mode
                ? "bg-primary text-primary-foreground"
                : "text-muted-foreground hover:text-foreground hover:bg-accent"
            }`}
          >
            {t(`transactions.${mode}`)}
          </button>
        ))}
      </div>

      </div>
      )}
    </div>
  );
}
