# S2 — Layer-end save and restore

- Status: Todo (blocked on S1: `in-progress/save-format-and-world-repository.md`)
- Priority: High
- Category: Persistence / save system
- Source: Stage 2 of `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` (design frozen 2026-09-10)
- Related: `docs/architecture/save-archive-format.md`, `in-progress/save-format-and-world-repository.md` (S1), `todo/save-mid-run-consistent-cut.md` (S3), `review/world-determinism-world-fingerprint.md`

## Scope

Wire the layer-boundary save/restore onto the S1 format. Layer-end is the cheapest cut: the world is
regenerable from the run baseline there, so the payload is the kernel checkpoint + character data +
run baseline, with no world diff yet.

1. **Capture at the layer boundary** — the boundary event that already exists in CUO (layer advance)
   plus the host's deliberate "save and return to menu" path. The current hook
   `RunMenuReturnCoordinator.Flush` calls the native `SaveSystem.SaveGame()`
   (`src/CasualtiesUnknownOnline.GameAdapter/Run/RunMenuReturnCoordinator.cs:42-68`); decision 165 says
   CUO is independent of `save.sv`, so that hook is retired or re-pointed at the CUO save — decide
   which with evidence from the actual call flow, and do not leave both writers active.
2. **Payload** — `GameCheckpoint` mapped to the S1 domain files (`run.json`, `players.json`,
   `items.json`, `world-entities.json`, `enemies.json`, `fluids.json`), plus
   `characters/<playerKey>.json` for every member present, plus `world-blocks.json` /
   `world-transients.json` written as their layer-end form (a layer-end cut has no in-layer
   deviations; the files exist so S3 can fill them without a schema change).
3. **The checkpoint store comes alive** — `KernelSaveFileStore` is currently unregistered
   (`CuoBootstrap.BuildServiceProvider` never registers it). Either promote it into the S1 writer or
   retire it, but the repository must not end up with two competing save paths. `GameCheckpoint`
   stays the in-memory shape; the S1 DTOs are the disk shape.
4. **Restore into a freshly generated layer** — the host continues from the native Continue entry
   (`PreRunScriptLoadRunPatch` already intercepts `PreRunScript.LoadRun`), which must resolve to
   "restore the selected CUO world" instead of the native path, and must not silently fall back to
   the native regenerate-the-layer semantics.
5. **Player key plumbing** — the transport-scoped key from the format doc §2 (`steam-<steamId64>` /
   `name-<displayName>`), including which transport mode a run is in and how the key is resolved at
   both save and load time.

Out of scope: mid-run world diff (S3), guest reconnect claims (S4), mod state, host bans, legacy
protobuf save migration.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Layer-end save → quit → continue | The next layer starts with the saved character and run state; identical character/run state; no duplicate or missing items |
| 2 | Save with 2 items in a container tree, restore | Same identities and exactly one parent per child; no re-materialization of generation-time items |
| 3 | Save after killing an enemy / consuming a trap, restore | Terminal facts stay terminal |
| 4 | Restore twice | Idempotent; the world fingerprint from the pinned reference run is stable |
| 5 | Native `save.sv` present and fresh | CUO's continue path ignores it and never writes it (decision 165) |
| 6 | Native Load button on a machine with no native save but existing CUO worlds | The Continue entry is reachable (interactable state follows the CUO repository, not `SaveSystem.HasSave()`) |

## Verification limits

Layer-end save/restore is adapter-adjacent: the checkpoint/format paths are machine-verifiable, but
the in-game continue flow and the two-client behaviour can only be proven by the user's dual-client
acceptance pass. This ticket must state exactly which rows are machine-verified and which need the
user's run; no claim of dual-client verification is allowed.
