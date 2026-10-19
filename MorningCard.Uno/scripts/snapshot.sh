#!/usr/bin/env bash
# Linux counterpart of Get-Snapshot.ps1 for cloud sandboxes and CI. Launches the Debug desktop build
# once inside a virtual display, reads the JSON snapshot, and checks it.
#
#   MC_CLOCK=460 scripts/snapshot.sh --expect "Running" --expect "7 min late"
#   MC_STATE=error MC_THEME=dark scripts/snapshot.sh --expect "Retry" --hidden OpenInMaps
#
# Pin the state with the usual MC_* variables (see MorningCard/DebugHooks.cs). Needs xvfb-run and
# python3. Exit 0 when every check passes, 1 when one fails, 2 when there is no snapshot.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN="$ROOT/MorningCard/bin/Debug/net10.0-desktop"
OUT="${MC_SNAPSHOT:-/tmp/morningcard-snapshot.json}"
SECONDS_TO_RUN="${MC_EXIT_AFTER:-10}"
EXPECT=()
HIDDEN=()

while [ $# -gt 0 ]; do
  case "$1" in
    --expect) EXPECT+=("$2"); shift 2 ;;
    --hidden) HIDDEN+=("$2"); shift 2 ;;
    --out) OUT="$2"; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

[ -f "$BIN/MorningCard.dll" ] || { echo "No Debug desktop build at $BIN" >&2; exit 2; }
command -v xvfb-run >/dev/null || { echo "xvfb-run is required (apt-get install xvfb)" >&2; exit 2; }

rm -f "$OUT"
(
  cd "$BIN"
  APP_NO_HOTDESIGN=1 MC_SNAPSHOT="$OUT" MC_EXIT_AFTER="$SECONDS_TO_RUN" \
    timeout $((SECONDS_TO_RUN + 60)) xvfb-run -a -s "-screen 0 1280x1000x24" dotnet MorningCard.dll >/dev/null 2>&1
) || true

[ -f "$OUT" ] || { echo "No snapshot at $OUT; the app did not start or is not a Debug build." >&2; exit 2; }

python3 - "$OUT" "${#EXPECT[@]}" "${EXPECT[@]}" "${HIDDEN[@]}" <<'PY'
import json, sys
path, n = sys.argv[1], int(sys.argv[2])
expect, hidden = sys.argv[3:3 + n], sys.argv[3 + n:]
snap = json.load(open(path))
print("hub:    ", json.dumps(snap["hub"]))
print("derived:", json.dumps(snap["derived"]))
print(f"theme:   {snap['ui']['theme']}  (snapshot #{snap['seq']})")
failed = 0
for text in expect:
    ok = text in snap["ui"]["visibleText"]
    failed += not ok
    print(("PASS on screen: " if ok else "FAIL not on screen: ") + repr(text))
for name in hidden:
    shown = any(e["name"] == name and e["visible"] for e in snap["ui"]["named"])
    failed += shown
    print(("FAIL visible: " if shown else "PASS hidden: ") + name)
print("Snapshot:", path)
sys.exit(1 if failed else 0)
PY
