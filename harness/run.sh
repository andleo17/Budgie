#!/usr/bin/env bash
#
# run.sh — Budgie headless harness: dispatches a task to `opencode run`
# (DeepSeek model by default) and verifies the result with real .NET/Bun
# gates before reporting success. Uses Conventional Commits and SemVer.
#
# Usage:
#   harness/run.sh "task description" [-m model] [-a agent]
#       [--no-verify] [--keep-changes] [--commit <type>] [--bump [auto|major|minor|patch]]
#       [--title "Concise commit title"] [--issue <n>|--pr <n>]
#
# Options:
#   -m, --model <m>         opencode model (default: opencode/deepseek-v4-flash-free)
#   -a, --agent <a>         opencode agent (default: build)
#   --no-verify             Skip the verification gate (fast iteration)
#   --keep-changes          Do not revert changes after failed verification
#   --commit <type>         Conventional commit after successful verification. Default: "auto", which lets
#                           the agent generate both the type and the concise English message from the diff
#                           (falls back to keyword inference if the agent fails). Pass "none" to skip the
#                           commit, or use one explicitly:
#                           (feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)
#   --bump [level]          SemVer bump after the commit (default: auto; Conventional Commits rules)
#   --title <text>          Concise commit title (English, truncated to 72 chars). Defaults to the task.
#   --issue <n>             Append GitHub "(#n)" to the commit header (links an issue)
#   --pr <n>                Append GitHub "(#n)" to the commit header (links a pull request)
#   -h, --help              Show help
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MODEL="opencode/deepseek-v4-flash-free"
AGENT="build"
VERIFY=1
KEEP_CHANGES=0
COMMIT_TYPE="auto"
BUMP=""
TITLE=""
REF=""
TASK=""

usage() {
  sed -n '2,24p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -m|--model) MODEL="$2"; shift 2 ;;
    -a|--agent) AGENT="$2"; shift 2 ;;
    --no-verify) VERIFY=0; shift ;;
    --keep-changes) KEEP_CHANGES=1; shift ;;
    --commit)
      if [[ "$2" == "auto" || "$2" == "none" || "$2" =~ ^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)$ ]]; then
        COMMIT_TYPE="$2"; shift 2
      else
        echo "ERROR: invalid commit type: $2 (use auto, none or a Conventional Commit type)" >&2; exit 2
      fi ;;
    --bump)
      if [[ "${2:-}" =~ ^(auto|major|minor|patch)$ ]]; then BUMP="$2"; shift 2; else BUMP="auto"; shift; fi ;;
    --title) TITLE="$2"; shift 2 ;;
    --issue|--pr) REF="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) TASK="${TASK:+$TASK }$1"; shift ;;
  esac
done

if [[ -z "$TASK" ]]; then
  echo "ERROR: missing task description." >&2
  usage >&2
  exit 2
fi

command -v opencode >/dev/null 2>&1 || { echo "ERROR: opencode is not on PATH." >&2; exit 2; }

# Infer the Conventional Commit type from the task description.
infer_commit_type() {
  local t="${TASK,,}"
  if   [[ "$t" =~ (fix|bug|bugfix|error|corrige|corregir|arregla|arreglar) ]]; then echo "fix"
  elif [[ "$t" =~ (docs|readme|documenta|documentar|documentaci[oó]n) ]]; then echo "docs"
  elif [[ "$t" =~ (feat|feature|adds?|new|implement|implementa|crea|crear|agrega|añade|anade) ]]; then echo "feat"
  elif [[ "$t" =~ (refactor|clean|simplif|refactoriza|limpia) ]]; then echo "refactor"
  elif [[ "$t" =~ (test|tests?|prueba|pruebas) ]]; then echo "test"
  elif [[ "$t" =~ (style|format|estilo|formatea) ]]; then echo "style"
  elif [[ "$t" =~ (perf|performance|optimiz|rendimiento) ]]; then echo "perf"
  else echo "chore"
  fi
}

# Ask the agent to generate a Conventional Commit header ("type: message") from
# the staged diff. Sets AGENT_TYPE, AGENT_SCOPE, AGENT_MSG. Returns 1 on failure
# so the caller can fall back to the heuristic inference.
generate_commit_meta() {
  local stat full prompt line regex out
  stat="$(git diff --cached --stat | grep -vE '^ files? changed|^$' | head -20)"
  full="$(git diff --cached | grep -E '^[+-]' | grep -vE '^(\+\+\+|---)' | head -120)"

  prompt="You are a Conventional Commits formatter for the Budgie repo.
TASK: $TASK

CHANGES:
$stat

DIFF (excerpt):
$full

Write exactly ONE line in this format:
<type>[<scope>]: <concise English imperative summary (max ~60 chars)>
type must be one of: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert
Output ONLY that single line, nothing else."

  out="$(opencode run --model "$MODEL" "$prompt" 2>/dev/null || true)"
  line="$(printf '%s\n' "$out" | grep -E '^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([^)]*\))?!?: ' | tail -1 || true)"
  regex='^([a-z]+)(\(([^)]*)\))?!?: (.+)$'
  if [[ -n "$line" && "$line" =~ $regex ]]; then
    AGENT_TYPE="${BASH_REMATCH[1]}"
    AGENT_SCOPE="${BASH_REMATCH[3]}"
    AGENT_MSG="${BASH_REMATCH[4]}"
    return 0
  fi
  return 1
}

LOGS="$ROOT/harness/logs"
mkdir -p "$LOGS"
TS="$(date +%Y%m%d-%H%M%S)"
RUN_LOG="$LOGS/session-$TS.json"
VERIFY_LOG="$LOGS/verify-$TS.log"
REPORT="$LOGS/report-$TS.txt"

echo "═══ Budgie Harness ═══"
echo " Task  : $TASK"
echo " Model : $MODEL"
echo " Agent : $AGENT"
echo " Log   : $RUN_LOG"
echo ""

# 1) Baseline: clean state before the task
if [[ -z "$(git status --porcelain)" ]]; then
  echo "[1/3] Working tree clean — no pending changes."
  BASELINE_COMMIT="$(git rev-parse HEAD)"
else
  echo "[1/3] Pending changes found; creating a backup commit…"
  git add -A >/dev/null 2>&1
  git commit -q -m "chore(harness): backup before task ($TS)" >/dev/null 2>&1
  BASELINE_COMMIT="$(git rev-parse HEAD)"
fi

# 2) Run the agent
echo "[2/3] Running opencode run…"
if ! opencode run --model "$MODEL" --agent "$AGENT" --format json \
     "$TASK" >"$RUN_LOG" 2>&1; then
  echo "WARN: opencode run exited with error (code $?). Check $RUN_LOG" >&2
fi

# 3) Verification
STATUS=1
if [[ "$VERIFY" == "1" ]]; then
  echo "[3/3] Running the verification gate…"
  if "$ROOT/harness/verify.sh" >"$VERIFY_LOG" 2>&1; then
    STATUS=0
    echo "Verification: PASS"
  else
    STATUS=1
    echo "Verification: FAIL — summary:"
    grep -E '^(PASS|FAIL|VERIFY SUMMARY| === )' "$VERIFY_LOG" || true
  fi
else
  STATUS=0
  echo "[3/3] Verification skipped (--no-verify)."
fi

# Report
{
  echo "Task        : $TASK"
  echo "Model       : $MODEL"
  echo "Agent       : $AGENT"
  echo "Timestamp   : $TS"
  echo "Baseline    : $BASELINE_COMMIT"
  echo "Verification: $([[ $STATUS == 0 ]] && echo PASS || echo FAIL)"
  echo ""
  echo "Changed files:"
  git status --porcelain | sed 's/^/  /'
  echo ""
  echo "Logs:"
  echo "  Session : $RUN_LOG"
  echo "  Verify  : $VERIFY_LOG"
} >"$REPORT"
echo "Report  : $REPORT"

# 4) Conventional commit + SemVer bump (only if verification PASSED)
if [[ $STATUS == 0 ]]; then
  if [[ -n "$COMMIT_TYPE" ]]; then
    if [[ -z "$(git status --porcelain)" ]]; then
      echo "No changes to commit — skipping commit."
    elif [[ "$COMMIT_TYPE" == "none" ]]; then
      echo "Commit skipped (--commit none)."
    else
      git add -A
      type="$COMMIT_TYPE"
      msg="${TITLE:-$TASK}"
      scope=""
      if [[ "$type" == "auto" ]]; then
        echo "Asking the agent for a commit header…"
        if generate_commit_meta; then
          type="$AGENT_TYPE"
          scope="$AGENT_SCOPE"
          msg="$AGENT_MSG"
          echo "Agent commit: ${type}(${scope:-no-scope}): ${msg:0:60}"
        else
          type="$(infer_commit_type)"
          echo "Agent unavailable; inferred type: $type"
        fi
      fi
      commit_args=("$type" -m "$msg")
      [[ -n "$scope" ]] && commit_args+=(--scope "$scope")
      [[ -n "$REF" ]] && commit_args+=(--issue "$REF")
      "$ROOT/harness/commit.sh" "${commit_args[@]}" || STATUS=1
      if [[ $STATUS == 0 && -n "$BUMP" ]]; then
        "$ROOT/harness/version.sh" "$BUMP"
      fi
    fi
  elif [[ -n "$BUMP" ]]; then
    echo "WARNING: --bump requires --commit <type> to create a release." >&2
  fi
fi

# Revert changes if verification failed (unless --keep-changes)
if [[ $STATUS != 0 && $KEEP_CHANGES == 0 ]]; then
  echo "Reverting changes to $BASELINE_COMMIT…"
  git checkout -q -- .
  git clean -fdq . --exclude=harness/logs/ 2>/dev/null || true
fi

echo ""
echo "Exit code: $STATUS"
exit "$STATUS"
