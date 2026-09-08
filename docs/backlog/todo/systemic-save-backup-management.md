# Systemic save and backup management

- Status: Todo
- Priority: Medium
- Category: Persistence / tooling
- Source: Promoted from `future/` by user request (2026-09-07, user-promoted) alongside the new save-system requirement
- Depends on: `todo/save-system-mid-run-and-layer-end.md` — **reuse its package format** (manifest + per-domain JSON files + directory-entry archive). Do not define a second archive format.

Goal: provide a user-facing, inspectable backup/restore layer on top of CUO's
persistence without breaking the runtime/wire contracts.

Current state (updated 2026-09-07):

- `CharacterDataFileStore` / `CharacterDataFile`: protobuf host-side per-SteamID
  character snapshots (atomic write, survives a host restart, cleared on a new run).
- `ModStateFileStore` / `HostBanService`: mod-state and host-ban persistence.
- `KernelSaveFileStore` + `SaveHeader` + `GameCheckpoint`: protobuf authoritative
  checkpoint save exists and is unit-tested, but has **no production caller**
  (not registered in `CuoBootstrap`; `Plugin.cs:82-96` passes only the character,
  mod-state, and ban paths). The save-system ticket owns wiring it up.
- No scheduled/manual backup, archive, or restore workflow exists yet.

Scope owned by this ticket (the package format itself is owned by the save-system ticket):

1. Manual backup: host command/UI to create a backup package at any time.
2. Scheduled backup: configurable interval plus lifecycle hooks (session start,
   new run, layer end, host shutdown) with retention/pruning.
3. Restore/import: validated package import with checksum/version/build gating,
   host-only, requires no active live session, explicit degradation semantics.
4. Native game-layer backup: GameAdapter-only slice to capture/restore game save
   data (`SaveSystem` / `save.sv` and related files); only the Game Adapter
   may reference game assemblies.
5. Retention/rotation and a "restore previous" surface.
6. Migration/versioning: define coexistence/migration between old protobuf saves
   and new packages; never guess silently.

Open questions before implementation:

- Backup trigger defaults and retention policy.
- Whether backups live under the save root or a sibling directory.
- Which domains are in v1 scope.
- How much native game-layer backup is wanted in v1.
- Which existing native UI surface (if any) the manual backup/restore entry point
  should reuse; if none, record the blocker and get user direction before adding UI.
