#!/usr/bin/env bash
#
# version.sh — Semantic versioning (SemVer 2.0.0) for Budgie.
# Reads VERSION, computes the bump and updates VERSION + a git tag.
#
# Usage:
#   harness/version.sh [major|minor|patch|auto] [--show] [--no-tag]
#
# Rules:
#   major  — commit with '!' (BREAKING CHANGE)
#   minor  — feat
#   patch  — fix, docs, style, refactor, perf, test, build, ci, chore, revert
#
# 'auto' (default) inspects the commits since the last tag.
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VERSION_FILE="VERSION"
MODE="auto"
NO_TAG=0
SHOW_ONLY=0

for arg in "$@"; do
  case "$arg" in
    major|minor|patch|auto) MODE="$arg" ;;
    --show) SHOW_ONLY=1 ;;
    --no-tag) NO_TAG=1 ;;
  esac
done

current="$(cat "$VERSION_FILE" | tr -d '[:space:]')"
if ! [[ "$current" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "ERROR: VERSION is not a valid SemVer: '$current'" >&2
  exit 2
fi

IFS='.' read -r MAJOR MINOR PATCH <<<"$current"

bump_major() { MAJOR=$((MAJOR + 1)); MINOR=0; PATCH=0; }
bump_minor() { MINOR=$((MINOR + 1)); PATCH=0; }
bump_patch() { PATCH=$((PATCH + 1)); }

if [[ "$MODE" == "auto" ]]; then
  last_tag="$(git describe --tags --abbrev=0 2>/dev/null || true)"
  if [[ -z "$last_tag" ]]; then
    mode="minor"
    if git log --oneline | grep -qE '!:|BREAKING[ -]CHANGE'; then mode="major"; fi
  else
    range="$last_tag..HEAD"
    if git log --oneline "$range" | grep -qE '!:|BREAKING[ -]CHANGE'; then
      mode="major"
    elif git log --oneline "$range" | grep -qE '^feat(\(|!)|^feat:'; then
      mode="minor"
    else
      mode="patch"
    fi
  fi
else
  mode="$MODE"
fi

case "$mode" in
  major) bump_major ;;
  minor) bump_minor ;;
  patch) bump_patch ;;
  *) echo "ERROR: invalid mode '$mode'" >&2; exit 2 ;;
esac

next="$MAJOR.$MINOR.$PATCH"

if [[ "$SHOW_ONLY" == "1" ]]; then
  echo "current=$current next=$next mode=$mode"
  exit 0
fi

if [[ "$current" == "$next" ]]; then
  echo "No version change ($current)."
  exit 0
fi

echo "$next" >"$VERSION_FILE"
git add "$VERSION_FILE"
git commit -q -m "chore(release): $next"

if [[ "$NO_TAG" == "0" ]]; then
  git tag -a "v$next" -m "Release v$next"
fi

echo "Version updated: $current -> $next (bump $mode)"
