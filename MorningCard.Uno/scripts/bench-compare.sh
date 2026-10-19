#!/usr/bin/env bash
# Benchmarks the working tree against a baseline commit with the same benchmark code, and prints a
# comparison table. CI runs it on every push and pull request; it runs the same way locally.
#
#   scripts/bench-compare.sh <baseline-ref> [summary.md]
#
# The baseline is checked out into a temporary git worktree, and this tree's MorningCard.Benchmarks
# is copied into it, so both sides run identical measurement code against their own Core. Both use
# this tree's route asset, so the input is held constant. If the baseline's Core cannot compile the
# current benchmarks (an API changed), the comparison is skipped and the summary says so.
#
# Report only: shared CI runners are too noisy to fail a build on timing. Pass --fail-over <percent>
# to the compare step yourself when you want a gate.
set -euo pipefail

BASE_REF="${1:?usage: bench-compare.sh <baseline-ref> [summary.md]}"
SUMMARY="${2:-}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO="$(git -C "$ROOT" rev-parse --show-toplevel)"
REL="$(realpath --relative-to="$REPO" "$ROOT")"
OUT="$ROOT/artifacts/bench"
ROUTE="$ROOT/MorningCard/Assets/route.json"
BENCH="$ROOT/MorningCard.Benchmarks"
mkdir -p "$OUT"

# A result left over from an earlier run would be compared as if it were this baseline.
rm -f "$OUT"/*.json "$OUT"/*.log

BASE_SHA="$(git -C "$REPO" rev-parse --verify "$BASE_REF^{commit}")"
HEAD_LABEL="$(git -C "$REPO" rev-parse --short HEAD)$(git -C "$REPO" diff --quiet HEAD -- "$REL" || echo '+changes')"
BASE_LABEL="$(git -C "$REPO" rev-parse --short "$BASE_SHA")"

WT="$(mktemp -d)/baseline"
cleanup() { git -C "$REPO" worktree remove --force "$WT" >/dev/null 2>&1 || true; }
trap cleanup EXIT

git -C "$REPO" worktree add --detach "$WT" "$BASE_SHA" >/dev/null
rm -rf "$WT/$REL/MorningCard.Benchmarks"
mkdir -p "$WT/$REL/MorningCard.Benchmarks"
cp "$BENCH"/*.cs "$BENCH"/*.csproj "$WT/$REL/MorningCard.Benchmarks/"

note() { echo "$1"; if [ -n "$SUMMARY" ]; then echo "$1" >> "$SUMMARY"; fi; }

# Baseline first, then current, back to back on the same machine.
if dotnet build "$WT/$REL/MorningCard.Benchmarks" -c Release >"$OUT/baseline-build.log" 2>&1; then
  dotnet run -c Release --no-build --project "$WT/$REL/MorningCard.Benchmarks" -- \
    --route "$ROUTE" --out "$OUT/baseline.json" --label "$BASE_LABEL"
else
  note "Baseline $BASE_LABEL could not compile the current benchmarks (a Core API changed). Comparison skipped; see artifacts/bench/baseline-build.log."
fi

dotnet build "$BENCH" -c Release >/dev/null
dotnet run -c Release --no-build --project "$BENCH" -- --route "$ROUTE" --out "$OUT/current.json" --label "$HEAD_LABEL"

if [ -f "$OUT/baseline.json" ]; then
  args=(--compare "$OUT/baseline.json" "$OUT/current.json")
  if [ -n "$SUMMARY" ]; then args+=(--summary "$SUMMARY"); fi
  dotnet run -c Release --no-build --project "$BENCH" -- "${args[@]}"
fi
