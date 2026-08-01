#!/usr/bin/env bash
#
# commit.sh — Creates a commit with a validated, concise Conventional Commit message.
#
# Usage:
#   harness/commit.sh <type> -m "message" [--scope <scope>] [--issue <n>|--pr <n>] [--breaking]
#
# Valid types: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert
#
# The message is truncated to 72 characters while keeping whole words.
# --issue/--pr appends the GitHub "(#n)" suffix to link an issue or pull request.
#
# Example:
#   harness/commit.sh feat -m "add balance validation" --scope accounts
#   harness/commit.sh fix -m "fix 500 on /health" --issue 123
#
set -euo pipefail

TYPES='^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)$'
BREAKING=0
TYPE=""
SCOPE=""
MSG=""
REF=""
MAXLEN=72

while [[ $# -gt 0 ]]; do
  case "$1" in
    --breaking) BREAKING=1; shift ;;
    --scope) SCOPE="$2"; shift 2 ;;
    --issue|--pr) REF="$2"; shift 2 ;;
    -m|--message) MSG="$2"; shift 2 ;;
    --) shift; break ;;
    -h|--help) sed -n '2,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "ERROR: unknown option '$1'" >&2; exit 2 ;;
    *)
      if [[ -z "$TYPE" ]]; then TYPE="$1"; shift
      else echo "ERROR: unexpected argument '$1'" >&2; exit 2; fi ;;
  esac
done

# Anything after "--" is joined as the message.
if [[ $# -gt 0 ]]; then
  MSG="${MSG:+$MSG }$*"
fi

if [[ -z "$TYPE" || -z "$MSG" ]]; then
  echo "ERROR: type and message are required." >&2
  exit 2
fi

if ! [[ "$TYPE" =~ $TYPES ]]; then
  echo "ERROR: invalid type '$TYPE'. Valid: ${TYPES#^}" >&2
  exit 2
fi

# Truncate the message to MAXLEN characters while keeping whole words.
truncate_msg() {
  local s="$1" max="$2"
  if [[ ${#s} -le "$max" ]]; then echo "$s"; return; fi
  local cut="${s:0:$max}"
  cut="${cut% *}"
  [[ -z "$cut" ]] && cut="${s:0:$((max - 1))}"
  echo "${cut}…"
}
MSG="$(truncate_msg "$MSG" "$MAXLEN")"

if git diff --cached --quiet; then
  echo "ERROR: no staged changes to commit." >&2
  exit 2
fi

HEADER="$TYPE"
[[ -n "$SCOPE" ]] && HEADER="$HEADER($SCOPE)"
[[ "$BREAKING" == "1" ]] && HEADER="$HEADER!"
HEADER="$HEADER: $MSG"
[[ -n "$REF" ]] && HEADER="$HEADER (#$REF)"

git commit -q -m "$HEADER"
echo "Commit created: $HEADER"
