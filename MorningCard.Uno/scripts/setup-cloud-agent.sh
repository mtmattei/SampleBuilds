#!/usr/bin/env bash
# Prepares a fresh Linux sandbox (Claude Code on the web, Codex cloud, GitHub Actions) to build and
# test MorningCard. Paste `bash MorningCard.Uno/scripts/setup-cloud-agent.sh` into the environment's
# setup-script field, or run it by hand at the start of a session.
#
# What it does, in order:
#   1. Fails fast if NuGet is unreachable, because every later step needs it.
#   2. Installs the .NET 10 SDK into ~/.dotnet unless a .NET 10 SDK is already on PATH.
#   3. Persists DOTNET_ROOT and PATH for later shells (~/.bashrc, and GITHUB_ENV/GITHUB_PATH in CI).
#   4. Restores and builds the portable projects (Core, Tests, Benchmarks) and runs the tests once,
#      so the sandbox starts from a known-green state.
#   5. With --desktop, installs the android workload packs (restore needs them) and builds the
#      net10.0-desktop head.
#
# Building the Android head is out of scope: it also needs a JDK and the Android SDK, a
# multi-gigabyte install that cloud sandboxes rarely allow. Build Android on your own machine.
set -euo pipefail

DESKTOP=0
for arg in "$@"; do
  case "$arg" in
    --desktop) DESKTOP=1 ;;
    -h|--help) sed -n '2,18p' "$0"; exit 0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

step() { printf '\n== %s\n' "$*"; }

step "Checking NuGet reachability"
if ! curl -fsS --max-time 20 -o /dev/null https://api.nuget.org/v3/index.json; then
  echo "api.nuget.org is unreachable. Allow it in the sandbox's network settings; nothing below can work without it." >&2
  exit 1
fi

step "Locating a .NET 10 SDK"
has_net10() { command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; }
if has_net10; then
  echo "Found: $(dotnet --version)"
else
  export DOTNET_ROOT="$HOME/.dotnet"
  curl -fsSL --max-time 60 https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_ROOT" --no-path
  export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
  echo "Installed: $(dotnet --version)"

  # Later shells in the same sandbox, and later steps in a CI job, need the same PATH.
  line='export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"'
  grep -qxF "$line" "$HOME/.bashrc" 2>/dev/null || echo "$line" >> "$HOME/.bashrc"
  if [ -n "${GITHUB_ENV:-}" ]; then echo "DOTNET_ROOT=$DOTNET_ROOT" >> "$GITHUB_ENV"; fi
  if [ -n "${GITHUB_PATH:-}" ]; then echo "$DOTNET_ROOT" >> "$GITHUB_PATH"; fi
fi

cd "$ROOT"

step "Restoring and building the portable projects"
dotnet build MorningCard.Tests/MorningCard.Tests.csproj -c Release
if [ -f MorningCard.Benchmarks/MorningCard.Benchmarks.csproj ]; then
  dotnet build MorningCard.Benchmarks/MorningCard.Benchmarks.csproj -c Release
fi

step "Running the tests once, so the session starts green"
dotnet test MorningCard.Tests/MorningCard.Tests.csproj -c Release --no-build

if [ "$DESKTOP" = 1 ]; then
  # Restore evaluates every TFM in the csproj, net10.0-android included, even with -f. The workload
  # packs are enough for restore (about 20 s); the Android SDK and JDK are only needed to build
  # the Android head itself, which this script never does.
  step "Installing the android workload packs (restore needs them; no Android SDK required)"
  dotnet workload install android --skip-manifest-update

  step "Restoring and building the desktop head (net10.0-desktop)"
  dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop -c Debug
fi

step "Ready"
echo "dotnet $(dotnet --version) at $(command -v dotnet)"
echo "Open a new shell or run: source ~/.bashrc"
