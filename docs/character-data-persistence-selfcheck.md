# Character-data disk persistence — delivery fact sheet

Status: delivered — build + format + architecture/event-replay/entity-dispatch gates green, 877 tests green (L0), deployed to the real game dir via deploy.ps1, runtime verification = L0 simulation + static evidence (no manual acceptance), structure review done.

Cycle: character-data disk persistence (backlog `Persistence` — "Character data
disk persistence (currently in-memory, lost on host exit)").

## 0. Mandate

User 2026-08-16: "由你来自主挑选一个并完成" — autonomous item selection and
execution. This cycle is the selected backlog item. Development-period rule
(user 2026-08-16): no manual acceptance; verification is L0 simulation + static
evidence, marked `no manual acceptance`.

## 1. Mechanism inventory (every touched mechanism)

### 1.1 CharacterDataStore lifecycle (Runtime/Session/CharacterData/CharacterDataStore.cs)

- **Guest report save** — `SaveCharacterData` stores the latest snapshot in the
  in-memory `_savedCharacters` table (CharacterDataStore.cs:34,77-82). This is
  the host's single write point for the 1 Hz guest report
  (CharacterDataHandler.cs). Change: after the in-memory write the full table
  is persisted to disk.
- **Terminal-state merge mutations** — `ApplyEnemyBite` / `ApplyEnemyLunge` /
  `ApplyEnemyEffect` mutate a saved `CharacterDataMsg` in place
  (CharacterDataStore.cs:89-119). These writes bypass `SaveCharacterData`, so a
  disconnect/restart after a bite/lunge/effect but before the next 1 Hz report
  would lose the event. Change: persist after every successful merge.
- **New-run clear** — `ClearSavedCharacters` clears only memory
  (CharacterDataStore.cs:127-141); the new-run semantics ("previous run's saves
  are void") must also delete the disk copy, otherwise a process restart could
  resurrect the old run's supplies.
- **Session-end reset** — `ResetForSessionEnd` clears memory
  (CharacterDataStore.cs:147). Change: memory keeps its session-scoped
  semantics; the disk copy intentionally SURVIVES the session (that is the
  persistence item). A later `ClearSavedCharacters` (new run) deletes it.
- **Restore reads** — `SendSavedCharacter` / `GetSavedCharacter` keep reading
  only memory (CharacterDataStore.cs:154-166,236-237). The persisted table is
  loaded exactly once, at host construction, so a process restart (the
  continue-run path) restores from disk through the unchanged handshake flow
  (HandshakeHandler.cs / SceneStateHandler.cs call `SendSavedCharacter`).
  There is deliberately NO lazy reload after a same-process session end: the
  next run's identity is unknown, so reloading then could leak the old run's
  saves into a brand-new lobby before `ClearSavedCharacters` runs.
- **Merge is transient** — `MergeTransferredItems` merges the item arbitration's
  transfer table into the snapshot before sending
  (CharacterDataStore.cs:187-215). Change: intentionally NOT persisted as part
  of this cycle — the transfer table is still session-scoped (ItemArbitration),
  so a disk restore after a host restart merges only the CURRENT session's
  transfer table, exactly like today's in-memory restore.

### 1.2 Disk file store (new: Runtime/Session/CharacterData/CharacterDataFileStore.cs)

- **Format** — protobuf-net (same serializer as every wire message,
  NetPacket.cs:13-28) wrapping a schema-versioned list of `(SteamId,
  CharacterDataMsg)` entries. A version number is included from day one:
  unknown/older/newer files must be recognized and degraded explicitly, never
  guessed.
- **Atomic write** — serialize to `<file>.tmp` in the same directory, flush,
  then `File.Replace` (destination exists) or `File.Move` (first write). A
  crashed write never leaves a half-file as the current file.
- **Read degradation** — missing file → empty table. Corrupt/unknown-version
  file → log a warning and start empty (safe degradation, no startup crash).
- **Delete** — `File.Delete` for `ClearSavedCharacters`.
- **Failure policy** — save/delete failures log a warning and keep the
  in-memory session working. A new-run clear writes an EMPTY-table tombstone
  before deleting: if the delete fails, the file still reads as empty for a
  later restart; if both writes fail, the degradation is logged explicitly.
- **Path** — `null` disables persistence (all tests by default stay
  in-memory-only); production passes a path computed by the Plugin. Committed
  code contains no machine-specific path literal.

### 1.3 DI composition (Runtime/CuoBootstrap.cs, Plugin/Plugin.cs)

- `CuoBootstrap.BuildServiceProvider` currently registers `CharacterDataStore`
  directly (CuoBootstrap.cs:142-148). Change: add an optional
  `characterDataFile` parameter, register `CharacterDataFileStore` before
  `CharacterDataStore`, and inject it.
- The Plugin passes `Path.Combine(Paths.ConfigPath,
  "CasualtiesUnknownOnline.character-data.bin")` (Plugin.cs:76-84 call site).
  BepInEx creates its config directory; no hard-coded machine path is committed.

### 1.4 Test composition (tests/.../Fakes/TestNode.cs)

- `TestNode.Create` builds the production composition root
  (TestNode.cs:48-59). Change: optional `characterDataFile` parameter passed
  through; default `null` keeps every existing test in-memory-only.

### 1.5 Explicit non-touched mechanisms

- Wire protocol: no new message and no changed message — **ProtocolVersion
  stays 15**. The file is a local disk artifact, never sent.
- Game Adapter: no patch, no game reflection, no Harmony change.
- Item transfer table / world / entity domains: untouched.

## 2. Design

1. `CharacterDataFile` (protobuf wrapper, version + entry list) owns the disk
   shape; `CharacterDataFileStore` owns path + atomic read/write/delete.
2. `CharacterDataStore` loads the file exactly once at construction and
   persists after every verified in-memory mutation (`SaveCharacterData`,
   enemy terminal-state merges). No lazy reloads: the construction load is the
   restart/continue-run path.
3. Session-end reset keeps clearing memory (existing test stays true) but does
   NOT delete the file; a new run writes the empty tombstone and deletes it.
4. Production file lives under BepInEx `config` and is generated at runtime,
   never committed.

## 3. Verification design (development period — no manual acceptance)

- **L0 file-store unit tests**: round-trip every `CharacterDataMsg` field
  family; missing file; corrupt file; wrong version; delete; no `.tmp` residue.
- **L0 restart simulations over the production DI stack**: save on host A,
  dispose the whole stack, build host B on the same file path → saved character
  reloads and reaches the guest on `SendSavedCharacter`; new-run clear deletes
  the disk copy (host B sees nothing); session end keeps the disk copy while
  memory clears.
- **Mutation-family tests**: enemy bite/effect/lunge merges persist across a
  restart (the whole mutation family is covered, not only `SaveCharacterData`).
- **No same-process cross-run leak**: after a session-end memory reset, the
  disk copy is reloaded only by a new process/continue-run start, never on
  demand inside the old process.
- **Static evidence**: `ProtocolVersion.Current` unchanged; no Harmony/game
  assembly references added to Runtime; `dotnet build` (warnings-as-errors) +
  `dotnet format` + the architecture/event-replay/entity-dispatch/delivery
  gates.
- **Deployment**: `tools/deploy.ps1` to the real game directory only. Runtime
  verification box = L0 simulation + static evidence, `no manual acceptance`
  per the development-period rule.
- **Post-delivery structure review**: touched classes under 600 lines, one
  top-level type per file, no new expression-state bools beyond the gate.

## 4. Self-check table (mechanism × change × evidence)

| # | Mechanism | Change | Evidence |
|---|-----------|--------|----------|
| 1 | Guest report save (`SaveCharacterData`) | Persist full table after memory write | `CharacterDataFileStoreTests` round-trip + restart simulation |
| 2 | Bite/lunge/effect terminal-state merges | Persist after each merge | Restart tests for all three merge kinds |
| 3 | New-run clear (`ClearSavedCharacters`) | Clear memory, write empty-table tombstone, then delete | Restart-after-clear test + failed-clear no-leak test |
| 4 | Session-end reset (`ResetForSessionEnd`) | Memory clears, disk survives | Existing `SavedCharacters_ClearOnSessionEnd` stays green + new disk-survives test |
| 5 | Restore reads (`SendSavedCharacter`/`GetSavedCharacter`) | Construction-only disk load; no lazy reload after session end | Restart + session-end-reset tests |
| 6 | DI / plugin path | New optional bootstrap parameter + BepInEx config-path default | Build + `TestNode` path tests + Plugin compile |
| 7 | Protocol / Game Adapter | No change | `ProtocolVersion.cs` unchanged in the diff; Runtime csproj has no game references |
| 8 | Degraded disk (corrupt/unknown version/IO failure) | Warning + continue in-memory, never crash startup | Corrupt-file test + wrong-version test + save-failure policy test |
## 5. Post-delivery evidence (runtime verification = L0 + static)

- `dotnet test CasualtiesUnknownOnline.slnx` — **877/877 green** after the
  final build (development-period rule: no manual acceptance).
- `dotnet build` — 0 warnings, 0 errors (warnings-as-errors);
  `dotnet format` clean; `check-architecture`, `check-event-replay` and
  `check-entity-event-dispatch` all passed.
- `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"`
  completed: all 25 build-output DLLs + Steamworks.NET/steam_api64 deployed to
  the real game directory; the script's process/lock gates prove no game was
  running during deployment.
- Static diff: `ProtocolVersion.cs` unchanged (15); no Harmony patch, no game
  assembly reference, no GameAdapter source change; the new file format is
  host-local and never travels the wire.
- Structure review: `CharacterDataStore` 296 lines / `CharacterDataFileStore`
  182 / `CharacterDataFile` 35 (all under the 600-line gate); zero new
  expression-state bools; one top-level type per file (gate passed). The
  interim lazy-reload approach was removed in the same round — no dead
  mechanism was left behind.

