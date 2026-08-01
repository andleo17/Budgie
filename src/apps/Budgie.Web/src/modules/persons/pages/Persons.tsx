import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import client from "@/lib/api-client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PersonFormDialog } from "../components/PersonFormDialog";
import { Loader2, MoreHorizontal, Plus, Pencil, Trash2, Search, Users, Tags } from "lucide-react";
import { Link } from "react-router-dom";
import { formatCurrency } from "@/lib/format";
import type { components } from "@/lib/api-types";
import { usePersons, useTags, usePersonSummary } from "@/lib/hooks/usePersons";

type PersonResponse = components["schemas"]["PersonResponse"];
type TagResponse = components["schemas"]["TagResponse"];

function getInitials(name: string): string {
  return name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

const TAG_COLOR_MAP: Record<string, string> = {
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

function TagBadge({ tag }: { tag: TagResponse }) {
  const classes = tag.color?.startsWith("#")
    ? ""
    : TAG_COLOR_MAP[tag.color ?? "gray"] ?? TAG_COLOR_MAP.gray;

  return (
    <Badge
      variant="secondary"
      className={`text-[11px] px-1.5 py-0 ${classes}`}
      style={
        tag.color?.startsWith("#")
          ? { backgroundColor: tag.color + "20", color: tag.color }
          : undefined
      }
    >
      {tag.name}
    </Badge>
  );
}

function PersonCard({
  person,
  onEdit,
  onDelete,
}: {
  person: PersonResponse;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const { t } = useTranslation();
  const { data: summary } = usePersonSummary(person.id);

  return (
    <div className="group relative rounded-lg border bg-card p-3 transition-all hover:shadow-sm hover:border-accent-foreground/20 min-h-[120px]">
      <div className="flex items-start gap-3">
        <Avatar className="size-12 shrink-0">
          <AvatarImage src={person.photoUrl ?? undefined} alt={person.name} />
          <AvatarFallback className="text-sm">{getInitials(person.name)}</AvatarFallback>
        </Avatar>

        <div className="flex min-w-0 flex-1 flex-col gap-1.5">
          <div className="flex items-start justify-between gap-2">
            <span className="truncate text-sm font-medium leading-tight">{person.name}</span>
            <DropdownMenu>
              <DropdownMenuTrigger
                render={<Button
                  variant="ghost"
                  size="icon-xs"
                  className="shrink-0 opacity-0 group-hover:opacity-100 -mr-1 -mt-1"
                />}
              >
                <MoreHorizontal className="size-4" />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={onEdit}>
                  <Pencil className="mr-2 size-4" />
                  {t("persons.editPerson")}
                </DropdownMenuItem>
                <DropdownMenuItem onClick={onDelete} className="text-destructive">
                  <Trash2 className="mr-2 size-4" />
                  {t("persons.deletePerson")}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          <div className="flex min-h-[20px] flex-wrap items-center gap-1">
            {person.tags && person.tags.length > 0 ? (
              person.tags.map((tag) => (
                <TagBadge key={tag.id} tag={tag} />
              ))
            ) : (
              <span className="text-[11px] text-muted-foreground italic">{t("persons.noTags")}</span>
            )}
          </div>
        </div>
      </div>

      {summary && Number(summary.totalTransactions) > 0 && (
        <div className="mt-2.5 grid grid-cols-3 gap-px overflow-hidden rounded-md border bg-border text-center text-xs leading-none">
          <div className="bg-card py-1.5">
            <div className="font-semibold text-green-600 dark:text-green-400">
              {formatCurrency(summary.totalContributed, summary.currency)}
            </div>
            <div className="mt-0.5 text-muted-foreground">{t("persons.contributed")}</div>
          </div>
          <div className="bg-card py-1.5">
            <div className="font-semibold text-blue-600 dark:text-blue-400">
              {formatCurrency(summary.totalRepaid, summary.currency)}
            </div>
            <div className="mt-0.5 text-muted-foreground">{t("persons.repaid")}</div>
          </div>
          <div className="bg-card py-1.5">
            <div className="font-semibold text-orange-600 dark:text-orange-400">
              {formatCurrency(summary.totalPending, summary.currency)}
            </div>
            <div className="mt-0.5 text-muted-foreground">{t("persons.pending")}</div>
          </div>
        </div>
      )}
    </div>
  );
}

function Persons() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [tagFilter, setTagFilter] = useState<string>("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editPerson, setEditPerson] = useState<PersonResponse | null>(null);
  const [deletePerson, setDeletePerson] = useState<PersonResponse | null>(null);

  const { data: persons, isLoading } = usePersons();
  const { data: tags } = useTags();

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/api/relationships/persons/{id}", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["persons"] });
      setDeletePerson(null);
    },
    onError: (err) => {
      toast.error("Something went wrong", {
        description: err instanceof Error ? err.message : undefined,
      });
    },
  });

  const filtered = (persons ?? []).filter((p) => {
    const matchesSearch = p.name.toLowerCase().includes(search.toLowerCase());
    const matchesTag = !tagFilter || (p.tags ?? []).some((t) => t.id === tagFilter);
    return matchesSearch && matchesTag;
  });

  return (
    <div className="w-full">
      <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <h1 className="text-2xl font-bold tracking-tight">{t("persons.title")}</h1>
          <p className="text-muted-foreground mt-1">{t("persons.description")}</p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Button variant="outline" size="sm" render={<Link to="/persons/tags" />}>
            <Tags className="mr-1.5 size-3.5" />
            {t("persons.manageTags")}
          </Button>
          <Button size="sm" onClick={() => { setEditPerson(null); setDialogOpen(true); }}>
            <Plus className="mr-1.5 size-3.5" />
            {t("persons.addPerson")}
          </Button>
        </div>
      </div>

      <div className="mb-6 flex items-center gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("persons.namePlaceholder")}
            className="pl-9"
          />
        </div>
        <Select value={tagFilter} onValueChange={(v) => setTagFilter(v ?? "")}>
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder={t("persons.filterByTag")}>
              {tagFilter ? (tags ?? []).find((t) => t.id === tagFilter)?.name : t("persons.allTags")}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">{t("persons.allTags")}</SelectItem>
            {(tags ?? []).map((tag) => (
              <SelectItem key={tag.id} value={tag.id}>
                {tag.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="size-8 animate-spin text-muted-foreground" />
        </div>
      ) : !filtered || filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Users className="mb-4 size-12 text-muted-foreground/60" />
          <p className="text-lg font-medium">{t("persons.noPersons")}</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {filtered.map((person) => (
            <PersonCard
              key={person.id}
              person={person}
              onEdit={() => { setEditPerson(person); setDialogOpen(true); }}
              onDelete={() => setDeletePerson(person)}
            />
          ))}
        </div>
      )}

      <PersonFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        person={editPerson}
      />

      <AlertDialog
        open={deletePerson !== null}
        onOpenChange={(v) => { if (!v) setDeletePerson(null); }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("persons.deletePerson")}</AlertDialogTitle>
            <AlertDialogDescription>{t("persons.deleteConfirm")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("persons.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => deletePerson && deleteMutation.mutate(deletePerson.id)}
            >
              {deleteMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
              {t("persons.deletePerson")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

export default Persons;
