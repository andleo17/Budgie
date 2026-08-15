import { useState } from "react";
import { useTranslation } from "react-i18next";
import GoalCard from "../components/GoalCard";
import GoalFormDialog from "../components/GoalFormDialog";
import { GoalsStatsBar } from "../components/GoalsStatsBar";
import { GoalsEmptyState } from "../components/GoalsEmptyState";
import { toGoalAccount, type GoalAccount } from "../components/goal-utils";
import { Plus } from "lucide-react";
import { useGoals } from "@/lib/hooks/useGoals";
import { PageSkeleton } from "@/components/page-skeleton";

export default function Goals() {
  const { t, i18n } = useTranslation();
  const [createOpen, setCreateOpen] = useState(false);
  const [editGoal, setEditGoal] = useState<GoalAccount | null>(null);

  const { data: goalsData, isLoading } = useGoals();
  const goals: GoalAccount[] = (goalsData ?? []).map(toGoalAccount);

  return (
    <>
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight md:text-3xl">
            {t("goals.title")}
          </h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {t("goals.description")}
          </p>
        </div>
        <button
          id="create-goal-btn"
          onClick={() => {
            setEditGoal(null);
            setCreateOpen(true);
          }}
          className="inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-lg border border-transparent bg-primary px-2.5 text-sm font-medium text-primary-foreground transition-all hover:bg-primary/80 active:translate-y-px disabled:pointer-events-none disabled:opacity-50 select-none whitespace-nowrap outline-none"
        >
          <Plus className="h-4 w-4" />
          {t("goals.newGoal")}
        </button>
      </div>

      {goals.length > 0 && <GoalsStatsBar goals={goals} />}

      {isLoading ? (
        <PageSkeleton header={false} cards={3} rows={0} />
      ) : goals.length === 0 ? (
        <GoalsEmptyState onCreate={() => { setEditGoal(null); setCreateOpen(true); }} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {goals.map((goal) => (
            <GoalCard
              key={goal.id}
              goal={goal}
              t={t}
              i18n={i18n}
              onEdit={(g) => {
                setEditGoal(g);
                setCreateOpen(true);
              }}
            />
          ))}
        </div>
      )}

      <GoalFormDialog
        open={createOpen}
        onOpenChange={(v) => {
          setCreateOpen(v);
          if (!v) setEditGoal(null);
        }}
        editGoal={editGoal}
        t={t}
      />
    </>
  );
}
