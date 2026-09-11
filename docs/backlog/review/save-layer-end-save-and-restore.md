# S2 — Layer-end save and restore

- Status: Review (code + machine verification complete; the in-game rows below need the user's
  dual-client pass — see *Verification*)
- Priority: High
- Category: Persistence / save system
- Source: Stage 2 of `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` (design frozen 2026-09-10)
- Related: `docs/architecture/save-archive-format.md`, `review/save-format-and-world-repository.md` (S1), `todo/save-mid-run-consistent-cut.md` (S3), `todo/save-multiplayer-restore-and-backups.md` (S4)

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

## Landed

**Two decisions taken with call-flow evidence (both were "either/or" in the scope above):**

- **Menu-return hook: re-pointed, not retired.** Evidence: `RunMenuReturnCoordinator.cs:58` was the
  ONLY production write to `save.sv` in `src/` (grep `SaveSystem\.`), and its purpose — "the host
  deliberately leaves a live world, persist it before the menu transition" — is a trigger the player
  asks for explicitly. Deleting the hook would delete the trigger; keeping the native call would
  leave two writers. The call now goes to `IWorldSaveControl` (`TryCaptureMenuReturnCut` in S2; since
  S3.3 the trigger ARMS a mid-run cut that the frame-end pump seam takes — see decision 167), and a
  new `SaveSystemTryLoadGamePatch` blocks the native reader, so **no production path writes or reads
  `save.sv` any more** (decision 165, enforced in both directions).
- **`KernelSaveFileStore`: retired.** Evidence: it was never constructed outside tests (no
  registration in `CuoBootstrap.BuildServiceProvider`; the only `new KernelSaveFileStore(...)` sites
  were tests), and its protobuf blob duplicated the archive's checkpoint payload in a second disk
  shape. `KernelSaveFileStore`, `KernelSaveFile` and `SaveHeader` are deleted, and the ten domain
  round-trip cases in `tests/.../GameState/*DomainKernelTests.cs` were re-pointed at the real path
  through `tests/.../Persistence/WorldArchiveRoundTrip.cs` — strictly stronger evidence: the domain
  suites now exercise the shipped encoder, the §5 transaction, the manifest gate and the decoder.

**Runtime (`src/CasualtiesUnknownOnline.Runtime/`)**

- `Persistence/WorldSnapshotEncoder` / `WorldSnapshotDecoder` / `WorldSnapshotPayload` /
  `SavedCharacter`: `GameCheckpoint` ⇄ the archive's per-domain files. Domain payloads are the same
  wire DTOs a late-joining guest receives; multi-shape tables (`world-entities`, `enemies`) write
  typed rows (`SaveWorldEntityRow`, `SaveEnemyRow`) so §6's salvage stays per entry; every file is an
  entry array and a snapshot cannot be written without a run baseline.
- `Persistence/PlayerKeyResolution` / `PlayerKeySpace` / `PlayerIdentity`: the §2 key plumbing —
  what key a peer writes under, and which present peer claims a stored key. The key space of a
  snapshot is DERIVED from its own character file names, because a host restoring from the main menu
  has no active transport to ask.
- `Session/Persistence/WorldSaveService` + `IWorldSaveControl`: the only writer (host/solo; a guest
  is refused), the cut trigger (the kernel's own `BatchCommitted` carrying `RunAdvancedEvent`, so a
  cut can never run from a half-applied batch), the deliberate menu-return cut, the continue-target
  rule (last-opened pointer, else newest) and the restore (verify checksums → decode → restore the
  kernel → bind characters → point the index at the world).
- `Networking/ITransportIdentity` (implemented by `CuoNetworkRouter`): which key space the live
  transport is in plus the local peer id/display name — available in solo play too, where no session
  exists.
- Composition root: `WorldArchiveWriter`/`Reader`, `WorldRepository` (when a `savesRoot` is given),
  `WorldSnapshotEncoder` and `WorldSaveService`/`IWorldSaveControl`; `Plugin` passes
  `<persistentDataPath>/cuo/saves` and `Application.version`.

**Game Adapter (`src/CasualtiesUnknownOnline.GameAdapter/`)**

- `RunMenuReturnCoordinator`: the native save call is replaced by the CUO cut (with the host's
  character captured from the live body at that instant).
- `WorldParamsService`: a restore pending flag makes the generation boundary replay the SAVED
  baseline (`MarkRestorePending` → `ApplyRestoredBaseline`) instead of capturing the live RNG.
- `PreRunScriptStartPatch` (new): the Continue entry's interactable state follows the CUO repository.
- `PreRunScriptLoadRunPatch`: the host's click restores the selected CUO world before the scene
  loads; a refused restore BLOCKS the original instead of falling through.
- `SaveSystemTryLoadGamePatch` (new): the native payload application is skipped while
  `SaveSystem.loadedRun` (set by the game itself) keeps the run a continuation.

**Docs**: `docs/architecture/save-archive-format.md` §3.4/§4 now state the entry-array envelope, the
typed-row rule for multi-shape tables, the character-file/claim semantics and S2's cut phases;
`docs/architecture/protocol.md` "Save / persistence" no longer points at the deleted store.

## Acceptance

| # | Scenario | Expected | Verification |
|---|---|---|---|
| 1 | Layer-end save → quit → continue | The next layer starts with the saved character and run state; identical character/run state; no duplicate or missing items | machine: capture writes the entered layer's baseline, the restore applies the checkpoint, the host character is bound back, and a container tree keeps exactly one parent per child. In-game: **user dual-client pass** |
| 2 | Save with 2 items in a container tree, restore | Same identities and exactly one parent per child; no re-materialization of generation-time items | machine: `WorldSaveContinueTests.ContainerTree_AfterRestore_HasExactlyOneParentPerChild` (ids 100/101/102, both children `Contained` in 100) |
| 3 | Save after killing an enemy / consuming a trap, restore | Terminal facts stay terminal | machine: `WorldSaveContinueTests.RemovedEnemy_StaysTerminalAfterRestore` (the tombstone survives, a re-upsert is refused); trap-consumption/opened facts round-trip in `WorldSnapshotCodecTests` |
| 4 | Restore twice | Idempotent; the world fingerprint from the pinned reference run is stable | machine: `WorldSaveContinueTests.TryContinue_Twice_KeepsTheSameFingerprintAndFacts` (item fingerprint + global revision equal across two restores) |
| 5 | Native `save.sv` present and fresh | CUO's continue path ignores it and never writes it (decision 165) | machine: the native writer call is gone and `SaveSystem.TryLoadGame` is blocked (grep: no production `SaveSystem.SaveGame()`/`TryLoadGame` call remains); the continue never opens `save.sv`. In-game: **user pass** |
| 6 | Native Load button on a machine with no native save but existing CUO worlds | The Continue entry is reachable (interactable state follows the CUO repository, not `SaveSystem.HasSave()`) | machine: `PreRunScriptStartPatch` sets it from `HasRestorableWorld()`, whose decision logic is covered by the service tests; the button state itself is **user pass** |

## Verification

Machine-verified (this cycle): checkpoint ⇄ DTO mapping and refusals (`WorldSnapshotCodecTests`),
the cut triggers and the guest/disabled paths (`WorldSaveCaptureTests`), the continue path including
idempotence, container trees, terminal facts, damaged-baseline refusal, backup fallback and the
key-space claims (`WorldSaveContinueTests`), the key plumbing (`PlayerKeyResolutionTests`), the ten
re-pointed domain round-trips, plus build/format/full-suite gates.

NOT machine-verified: the in-game save/continue flow and any two-client behaviour. Layer-end
save/restore is adapter-adjacent — the arm that runs it is the game's own Continue entry, the layer
generation and the live body capture. Those rows need the user's dual-client acceptance pass; this
ticket makes no claim that they were observed. Acceptance rows 5 and 6 are likewise NOT covered by a
behavioural test: a Harmony patch against the game assembly cannot be invoked in this test host, so
their evidence is the registration/patch wiring plus grep, and the real check is the user's run.

## Independent adversarial review (fresh subagent) and its outcome

A fresh-context reviewer audited the S2 diff against the frozen format contract and reported 5
blockers, 5 major findings and 4 minor ones. **All five blockers and the actionable major findings
are fixed with regression tests**; the rest are recorded below. Fixed:

- **Composition root (was a dead mod):** `ITransportIdentity` was never registered, and the save
  service is built by a FACTORY, which `ValidateOnBuild` does not walk — so the plugin's awake-time
  catch swallowed the resolution failure and CUO never started. Registered, plus a new
  `WorldSaveCompositionTests` (integration) that builds the production root with and without a saves
  root and resolves `IWorldSaveControl`/`ITransportIdentity`.
- **Per-entry salvage was bypassed:** the wire→kernel conversion used to run in `Finish()`, outside
  the reader's per-entry catch, so one unmaterializable row aborted the whole continue. It now runs
  per entry (`AddMapped`/`TryMaterialize`), and the rejected-row-adds-a-default-struct trap (a
  skipped item becoming "item 0") is closed and covered by
  `WorldSnapshotCodecTests.Decode_MalformedItemEntry_IsSkippedWhileTheOthersApply`.
- **Cross-mode claim:** the stored key space is now compared with the LIVE transport's; a mismatch
  leaves every stored key unclaimed (the reviewer's same-persona-name collision). The existing
  cross-mode test now uses the SAME display name in both modes.
- **Restore arming:** the restored baseline is applied at the continue click, before
  `WorldGeneration.Start` derives `unchipped`/rarity/time-limit/decay fields from
  `WorldGeneration.runSettings` (previously it was applied at the GenerateWorld prefix — too late);
  a missing baseline is a hard refusal rather than a logged fallback; a new run cancels any armed
  restore, so a refused Continue cannot replay into the next run.
- **`save.sv` was still read by the menu:** the reachability fix ran as a Postfix, i.e. AFTER the
  native body had already parsed the file for the Continue icon/label. A new
  `SaveSystemHasSavePatch` answers "no native save" while CUO is bound, so the native read is gone
  in practice as well as in intent.
- Also fixed from the same review: a cut carrying `RandomStreams` is now REFUSED (was a warning —
  the comment claimed a refusal that did not exist); the Continue target only considers worlds that
  actually carry a snapshot and the picker pointer moves on the first cut (an aborted start no
  longer hides the previous world or enables a broken entry); the archive's character set is
  authoritative on restore (the legacy table is dropped first); a revision that does not fit the
  manifest's signed field is refused; the character round-trip and the trap-terminal row now have
  real assertions.

Recorded, not fixed in this cycle:

- The native run fields that no CUO domain owns (`lootRarityMultiplier`, `trapRarityMultiplier`,
  `caloriesConsumed`, `lastHappiness`, `savedRecipeData`, `savedRunTime`, `WoundView.cInfo`) are not
  part of the v1 snapshot (decision 166: kernel checkpoint + run baseline + character data + world
  diff). A layer beyond the first therefore restores its rarity multipliers from their start values,
  not their accumulated ones — see the new `todo/save-native-run-field-parity.md`.
- `RunMenuReturnCoordinator.Flush` consumes its pending request before checking `inWorld` /
  `SessionActive` / `PlayerCamera.main` (inherited behaviour, now the only writer sits behind it): a
  failed check loses that save and the menu transition with no retry. Left alone deliberately — the
  queue semantics change needs its own test, and it is not reachable from a unit host.

## Follow-ups recorded while landing (not implemented here)

- `GameCheckpoint.RandomStreams` has no file in the snapshot set yet (nothing populates it today);
  S3's consistent cut owns the file. The encoder logs a warning instead of dropping a non-empty one
  silently.
- Two persistent copies of a member's character now exist: the archive's `characters/<key>.json`
  (durable, per-world) and the legacy `CasualtiesUnknownOnline.character-data.bin` reconnect store
  (session-scoped, fed by the same restore path). Folding the reconnect store into the archive needs
  the guest-claim machinery, so it is S4's (`todo/save-multiplayer-restore-and-backups.md`).
- A fatal continue refusal (e.g. an unreadable run baseline) currently reaches the log and the
  returned outcome only; the player-visible surface for repair/refusal reports is S4's decision, and
  S2 deliberately did not invent one.
- The Continue entry shows the native label/icon untouched when a CUO world is the only reason the
  button is reachable (the native code fills them from `save.sv`). Cosmetic, and it belongs to the
  same S4 management/UI surface as the picker.
