#!/usr/bin/env bash
#
# verify.sh — Budgie harness verification gate.
# Runs build, tests and frontend checks. Exit code != 0 if anything fails.
#
# Usage:
#   harness/verify.sh [--skip-frontend] [--skip-tests]
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

SKIP_FRONTEND=0
SKIP_TESTS=0
for arg in "$@"; do
  case "$arg" in
    --skip-frontend) SKIP_FRONTEND=1 ;;
    --skip-tests) SKIP_TESTS=1 ;;
  esac
done

pass=0
fail=0
results=()

record() {
  local name="$1" status="$2"
  if [[ "$status" == "0" ]]; then
    results+=("PASS  $name")
    pass=$((pass + 1))
  else
    results+=("FAIL  $name")
    fail=$((fail + 1))
  fi
}

run_gate() {
  local name="$1" log="$2"
  shift 2
  echo ""
  echo "=== GATE: $name ==="
  if "$@" >"$log" 2>&1; then
    echo "OK"
    record "$name" 0
  else
    echo "FAILED (see $log)"
    tail -n 40 "$log" || true
    record "$name" 1
  fi
}

mkdir -p harness/logs

run_gate "dotnet build (Budgie.slnx)" harness/logs/verify-dotnet-build.log \
  dotnet build Budgie.slnx --nologo

if [[ "$SKIP_TESTS" == "0" ]]; then
  run_gate "dotnet test (Budgie.slnx)" harness/logs/verify-dotnet-test.log \
    dotnet test Budgie.slnx --no-build --nologo
fi

if [[ "$SKIP_FRONTEND" == "0" ]]; then
  run_gate "tsc typecheck (Budgie.Web)" harness/logs/verify-bun-typecheck.log \
    bash -c "cd src/apps/Budgie.Web && bunx tsc --noEmit -p tsconfig.json"
  run_gate "bun lint (Budgie.Web)" harness/logs/verify-bun-lint.log \
    bash -c "cd src/apps/Budgie.Web && bun run lint"
  run_gate "bun build (Budgie.Web)" harness/logs/verify-bun-build.log \
    bash -c "cd src/apps/Budgie.Web && bun run build"
fi

echo ""
echo "═══════════════════════════════════════════"
echo " VERIFY SUMMARY — pass: $pass, fail: $fail"
echo "═══════════════════════════════════════════"
printf '%s\n' "${results[@]}"

[[ "$fail" == "0" ]]
