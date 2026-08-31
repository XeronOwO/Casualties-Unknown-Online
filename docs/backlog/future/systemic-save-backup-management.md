# Systemic save and backup management

- Status: Future
- Priority: Low
- Category: Persistence / tooling

Goal: provide a user-facing, inspectable backup/restore layer on top of CUO’s
current internal persistence without breaking the runtime/wire contracts.

Current state:
- `CharacterDataFileStore` / `CharacterDataFile`: protobuf host-side per-SteamID
  character snapshots.
- `KernelSaveFileStore` + `SaveHeader` + `GameCheckpoint`: protobuf authoritative
  checkpoint save.
- `ModStateFileStore` / `HostBanService`: mod-state and host-ban persistence.
- No scheduled/manual backup, archive, or restore workflow exists yet.

Proposed slices:
1. Archive format: ZIP backup package containing `manifest.json` + one readable
   state file per domain (character data, checkpoint, mod state, bans), with
   schema version, game/CUO version, run id, timestamps, checksums. Keep
   internal hot stores protobuf if that remains the right runtime format; the
   backup/export layer is the human-readable surface.
2. Manual backup: host command/UI to create an archive at any time.
3. Scheduled backup: configurable interval and lifecycle hooks (session start,
   new run, host shutdown) with retention/pruning.
4. Native game-layer save backup: GameAdapter-only slice to capture/restore
   game save data (`SaveSystem` / `WorldSaveData`) or related files; only the
   Game Adapter may reference game assemblies.
5. Load/restore: validated archive import with checksum/version gating,
   host-only, requires no active live session, explicit degradation semantics.
6. Migration/versioning: define coexistence/migration between old protobuf
   saves and new text archives; never guess silently.

Open questions before implementation:
- Should the user-facing archive replace live protobuf stores, or first wrap
  them as an export/import layer?
- Which domains are in v1 scope: characters only vs full kernel checkpoint?
- Backup frequency / retention defaults.
- How much of the native game-layer save backup is wanted in v1.
