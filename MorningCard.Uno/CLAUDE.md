# MorningCard.Uno

School drop-off card for Uno Platform (desktop + Android). `SPEC.md` is the design source of truth; `HANDOFF.md` is the latest session state. Read both before changing behavior.

## Layout

- `MorningCard.Core`: pure .NET, no Uno references. Trip arithmetic (`TripMath`), route geometry and the time ruler (`Route/`), the simulated hub (`Services/`). Clock-injected, so every card state is reachable from a test.
- `MorningCard.Tests`: xUnit over Core.
- `MorningCard`: the Uno app. MVUX models, Material + Toolkit, Navigation. `DebugHooks.cs` and `DebugSnapshot.cs` are DEBUG-only verification hooks.
- `MorningCard.Benchmarks`: CPU benchmarks for the Core paths that run on every clock tick.

<!-- verification-kit:start -->
## Definition of done

A change is done when every applicable line below is true. The handoff has to say which lines were checked and which were not.

1. **Tests pass.** `dotnet test MorningCard.Tests -c Release` is green. New Core behavior ships with a test. A bug fix starts with a failing test that reproduces it.
2. **The desktop head builds clean.** `dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop` reports 0 warnings and 0 errors.
3. **UI changes are checked against state, not only screenshots.** For each card state the change touches, run one snapshot check and quote its PASS lines in the handoff:
   - Windows: `./scripts/Get-Snapshot.ps1 -Clock 460 -Expect 'Running','7 min late' -Hidden ErrorPanel`
   - Linux or cloud: `MC_CLOCK=460 scripts/snapshot.sh --expect "Running" --expect "7 min late" --hidden ErrorPanel`
   The snapshot's `derived` block is what the card should say; `ui.visibleText` is what it does say. When they disagree, the binding or template is wrong. Screenshots are for layout and visual polish.
4. **Performance claims come with numbers.** A change to `TripMath`, `Route/` or `RouteAsset` runs the benchmarks before and after, and the handoff includes the comparison table. Without the table, do not write "faster".
5. **Unverified work is named.** Android, dark theme, narrow widths and anything else not actually run are listed as not verified, in `HANDOFF.md` under "Last verified state".

## Rules

- Never weaken, skip or delete a test or an assertion to get to green. If a test is wrong, stop and explain why before changing it.
- A control does something real or it is not there. A button whose command binds to null is a bug, not a stub (see `DebugHooks.ProbeRetryAsync`).
- Temporary probes and scaffolding are removed before the change is done, or they move behind `#if DEBUG` and an `MC_*` variable like the existing hooks.
- One launch per check. Do not run window relaunch sweeps; the snapshot scripts exit on their own after `MC_EXIT_AFTER` seconds.
- `MorningCard.Core` never references Uno. Anything testable belongs there.
- The hub is the single source of truth for trip and car. `DriveHubState.Revision` bumps on every change.
- Asset filenames use underscores, never hyphens. Android rewrites hyphens and the asset then fails silently.

## Commands

```powershell
dotnet test MorningCard.Tests -c Release
dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop
./scripts/Get-Snapshot.ps1 -Build -State error -Expect 'Retry'
dotnet run -c Release --project MorningCard.Benchmarks -- --out artifacts/bench-before.json --label before
dotnet run -c Release --project MorningCard.Benchmarks -- --out artifacts/bench-after.json --label after
dotnet run -c Release --project MorningCard.Benchmarks -- --compare artifacts/bench-before.json artifacts/bench-after.json
```

State hooks (DEBUG builds): `MC_CLOCK` (460 = late, 453 = leave now), `MC_DELAY`, `MC_STATE` (live, offline, empty, error, loading), `MC_THEME`, `MC_SNAPSHOT`, `MC_EXIT_AFTER`, `MC_TRACE`.

Cloud sandboxes (Claude Code on the web, Codex): run `bash scripts/setup-cloud-agent.sh --desktop` first. It installs .NET 10 and the android workload packs, builds, and runs the tests. Snapshot checks there also need `xvfb`.
<!-- verification-kit:end -->
