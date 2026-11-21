# Game-update runbook (contract toolchain)

What to do the day Casualties Unknown updates. The point of the flow is that the
structural half of "what did the game change?" is answered by a command instead
of by chasing compile errors and whichever patch class fails verification first.

The tool is `tools/CasualtiesUnknownOnline.ContractTool` — **read-only and
metadata-only**: it parses a DLL with Mono.Cecil, loads nothing, executes
nothing, and never writes to the game directory. No `src/` project references it
and it references none, so it can never become a plugin runtime dependency.

## 0. Before you start

- **Keep the previous build's DLLs.** Snapshot it before `references/` is
  overwritten — the previous side cannot be reconstructed afterwards.
- Refresh `references/` with the new build's DLLs (`references/README.md`
  § How to populate).
- Build the tree once, so the adapter assembly whose contracts will run against
  the new build exists (`dotnet build CasualtiesUnknownOnline.slnx`) and lives at
  `src/CasualtiesUnknownOnline.GameAdapter/bin/<configuration>/net48/`.

## 1. Snapshot the previous build

```powershell
dotnet run --project tools\CasualtiesUnknownOnline.ContractTool -- snapshot `
  --assembly <previous-build-dir>\Assembly-CSharp.dll `
  --adapter src\CasualtiesUnknownOnline.GameAdapter\bin\Debug\net48\CasualtiesUnknownOnline.GameAdapter.dll `
  --out artifacts\contract\previous.json
```

## 2. Snapshot the new build

```powershell
dotnet run --project tools\CasualtiesUnknownOnline.ContractTool -- snapshot `
  --assembly references\Assembly-CSharp.dll `
  --adapter src\CasualtiesUnknownOnline.GameAdapter\bin\Debug\net48\CasualtiesUnknownOnline.GameAdapter.dll `
  --out artifacts\contract\current.json
```

Both snapshots must be taken with the SAME adapter build: `diff` refuses two
snapshots whose contract sets differ, because otherwise one adapter's hooks would
be judged against another adapter's targets.

The same input produces byte-identical output, so an unchanged build snapshots
to an unchanged file — a difference in the artifact is a difference in the build.

## 3. Diff, and read the compatibility report

```powershell
dotnet run --project tools\CasualtiesUnknownOnline.ContractTool -- diff `
  --previous artifacts\contract\previous.json `
  --current artifacts\contract\current.json `
  --json artifacts\contract\diff.json `
  --out artifacts\contract\report.md
```

Read the report top to bottom:

1. **Contract verdicts** — the hook work. `Removed or renamed`, `Signature
   changed` and `Harmony target ambiguous` rows each name a patch class and its
   target; a rename candidate is named when a same-shape member appeared.
   `Signature changed` also covers the shape move the runtime guard cannot see: an
   unconstrained target whose PARAMETER TYPE moved still resolves by name at
   install time (Harmony matches patch arguments by name), so nothing would fail
   there — the report is the only place it shows up.
2. **Contract-adjacent changes** — fields and enum values of the types the
   contracts target (`Field shape changed`: type, visibility, staticness or
   serialized status; `Enum value changed`: an enum member kept its name and
   changed its value).
3. **Unchanged, still needs a semantic look** — targets that are structurally
   identical. This list is the part the structural half CANNOT clear; it is what
   step 6 is for.
4. **Outside the contract lens** — real changes that touch no contract, recorded
   so nothing is hidden by omission.

`--fail-on-broken` exits `1` when a contract verdict says a target moved, which is
what a CI job should key on. The other exit codes: `0` ran, `2` usage, `3` the
input could not be read.

The snapshot covers ONE assembly, so a contract whose target type lives outside it
(the adapter hooks `SceneManager.LoadScene`, and `SceneManager` is a Unity module
type) reports as **unresolved** in the lens rather than as a verdict; step 4 is
what judges those.

## 4. Patch-contract tests (the runtime guard's own facts)

```powershell
dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~PatchContractTests"
```

These resolve every contract — including the hand-declared dynamic ones
(`PatchInventory` marks those patch classes `"(dynamic)"`), which carry no
attribute and are therefore NOT in the tool's metadata lens — against the
refreshed `references/` assemblies, before the game ever launches.

## 5. Offline replay

```powershell
dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~Replay"
```

The `.replay` archive (`tests/CasualtiesUnknownOnline.Tests/Replays/`) drives the
simulation worlds on a monotonic timeline; `tools/compare-itemtrace.ps1` compares
a real log against a replay `SimTrace`. Together they are the strongest evidence
that a fix preserved user-observable semantics without the game running.

## 6. Replay the acceptance run against the stored reading

A contract can survive a game update while the game's own behaviour moves under it.
After deploying the rebuilt mod, run the full suite — every case, one run — and compare its report with the
stored one:

```powershell
dotnet tools/acceptance/host/bin/Debug/net8.0-windows/CasualtiesUnknownOnline.AcceptanceHost.dll replay `
    --baseline ".acceptance/full-41/report/report.json" `
    --candidate ".acceptance/<the new batch>/report/report.json"
```

The baseline is the last recorded green run (`docs/acceptance/AGENTS.md`, rule 14).
The comparer prints the build stamp of both, the row set, the suite wall and every
row's wall/log deltas. It exits 1 on a regression — a baseline row missing from the
candidate, a verdict that stopped passing, a case past max(5 s, 20% of its baseline),
the suite wall past max(10 s, 5%), or an error count that rose — and 2 when the pair
is not comparable (different suites, a report that names no build, a file that is not
a report). Warnings are declared per case and printed, never counted: they move run
to run (2026-11-20: the 39b -> 39c pair, a green improvement, moved ten of them).

Measured 2026-11-20 on the 39-row batch: `full-39b -> full-39c` printed
`result   : PASS — no regression` (suite wall 285059.9 -> 262111.6 ms); the same pair
reversed printed three regressions and exited 1; an unstamped 2026-11-18 report was
refused with `names no build`. The recorded baseline moved to `full-41` on
2026-11-21: 39 of 39 green in 254.1 s on the artifact `0.1.0+a93d2c7a9…`, and
`full-39c -> full-41` replayed PASS with the wall `-7974.5 ms`.

## 7. Write the compatibility verdict, then fix

- Fix or deliberately retarget every broken hook in the SAME change that
  refreshes `references/`; when a hook is dropped on purpose, delete its contract
  with it, or the contract tests keep reporting a target nobody patches.
- The structural half stops here. "The members are all still there — do they
  still MEAN the same thing?" needs the live game and stays in
  `future/adapter-shell-verification-harness.md`; neither half substitutes for
  the other.
- Record the update in the delivery self-check, and in
  `docs/decisions/active.md` if it changed an architectural fact.

## Artifacts

`artifacts/contract/` (gitignored). Snapshots and reports carry game-assembly
content and are NEVER committed.
