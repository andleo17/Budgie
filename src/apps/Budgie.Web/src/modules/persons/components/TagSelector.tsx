import { useState, useRef, useCallback, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { X, Plus } from "lucide-react";
import type { components } from "@/lib/api-types";

type TagResponse = components["schemas"]["TagResponse"];

const TAG_COLORS: Record<string, string> = {
  red: "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
  orange: "bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400",
  yellow: "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400",
  green: "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400",
  teal: "bg-teal-100 text-teal-800 dark:bg-teal-900/30 dark:text-teal-400",
  blue: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  purple: "bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400",
  pink: "bg-pink-100 text-pink-800 dark:bg-pink-900/30 dark:text-pink-400",
  gray: "bg-gray-100 text-gray-800 dark:bg-gray-800/30 dark:text-gray-400",
};

function getTagClasses(color: string | null | undefined): string {
  if (!color) return TAG_COLORS.gray;
  if (color.startsWith("#")) return "";
  return TAG_COLORS[color] || TAG_COLORS.gray;
}

interface TagSelectorProps {
  value: TagResponse[];
  onChange: (tags: TagResponse[]) => void;
}

export function TagSelector({ value, onChange }: TagSelectorProps) {
  const { t } = useTranslation();
  const [input, setInput] = useState("");
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const blurTimerRef = useRef<ReturnType<typeof setTimeout>>(null);
  const isClickingDropdown = useRef(false);

  useEffect(() => {
    return () => {
      if (blurTimerRef.current) clearTimeout(blurTimerRef.current);
    };
  }, []);

  const { data: allTags } = useQuery({
    queryKey: ["tags"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/tags");
      if (error) throw error;
      return data;
    },
  });

  const filtered = (allTags ?? []).filter(
    (tag) =>
      !value.some((v) => v.id === tag.id) &&
      tag.name.toLowerCase().includes(input.toLowerCase()),
  );

  const selectTag = useCallback(
    (tag: TagResponse) => {
      onChange([...value, tag]);
      setInput("");
      setOpen(false);
    },
    [value, onChange],
  );

  const createTag = useCallback(
    (name: string) => {
      const trimmed = name.trim();
      if (!trimmed) return;
      if (value.some((v) => v.name.toLowerCase() === trimmed.toLowerCase())) return;
      onChange([...value, { id: crypto.randomUUID(), name: trimmed, color: "gray" }]);
      setInput("");
      setOpen(false);
    },
    [value, onChange],
  );

  const removeTag = useCallback(
    (tag: TagResponse) => {
      onChange(value.filter((v) => v.id !== tag.id));
    },
    [value, onChange],
  );

  const trimmedInput = input.trim();
  const canCreateNew =
    trimmedInput.length > 0 &&
    !(allTags ?? []).some((t) => t.name.toLowerCase() === trimmedInput.toLowerCase());

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter" && trimmedInput) {
      e.preventDefault();
      const existing = (allTags ?? []).find(
        (t) => t.name.toLowerCase() === trimmedInput.toLowerCase(),
      );
      if (existing) {
        selectTag(existing);
      } else {
        createTag(trimmedInput);
      }
      setInput("");
    }
    if (e.key === "Backspace" && !input && value.length > 0) {
      removeTag(value[value.length - 1]);
    }
    if (e.key === "Escape") {
      setOpen(false);
    }
  }

  return (
    <div className="relative" ref={ref}>
      <div className="flex flex-wrap items-center gap-1.5 rounded-md border border-input px-3 py-1.5 focus-within:ring-1 focus-within:ring-ring">
        {value.map((tag) => (
          <Badge
            key={tag.id}
            variant="secondary"
            className={`gap-1 pr-1 ${getTagClasses(tag.color)}`}
            style={tag.color?.startsWith("#") ? { backgroundColor: tag.color + "20", color: tag.color } : undefined}
          >
            {tag.name}
            <button
              type="button"
              onClick={() => removeTag(tag)}
              className="ml-0.5 rounded-full hover:bg-muted-foreground/20 p-0.5"
            >
              <X className="size-3" />
            </button>
          </Badge>
        ))}
        <Input
          value={input}
          onChange={(e) => { setInput(e.target.value); setOpen(true); }}
          onFocus={() => setOpen(true)}
          onKeyDown={handleKeyDown}
          onBlur={() => {
            if (isClickingDropdown.current) {
              isClickingDropdown.current = false;
              return;
            }
            blurTimerRef.current = setTimeout(() => setOpen(false), 200);
          }}
          placeholder={value.length === 0 ? t("persons.tagsPlaceholder") : undefined}
          className="min-w-[120px] flex-1 border-0 p-0 shadow-none focus-visible:ring-0"
        />
      </div>

      {open && (filtered.length > 0 || canCreateNew) && (
        <div
          className="absolute z-50 mt-1 w-full rounded-md border bg-popover p-1 shadow-md"
          role="listbox"
          onMouseDown={() => { isClickingDropdown.current = true; }}
        >
          {filtered.map((tag) => (
            <button
              key={tag.id}
              type="button"
              role="option"
              tabIndex={0}
              onClick={() => selectTag(tag)}
              onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); selectTag(tag); } }}
              className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <span
                className="inline-block size-3 rounded-full"
                style={
                  tag.color?.startsWith("#")
                    ? { backgroundColor: tag.color }
                    : { backgroundColor: tag.color ? `var(--${tag.color}-500)` : undefined }
                }
              />
              {tag.name}
            </button>
          ))}
          {canCreateNew && (
            <button
              type="button"
              role="option"
              tabIndex={0}
              onClick={() => createTag(trimmedInput)}
              onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); createTag(trimmedInput); } }}
              className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <Plus className="size-3.5 text-muted-foreground" />
              {t("persons.createTag", { name: trimmedInput })}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
