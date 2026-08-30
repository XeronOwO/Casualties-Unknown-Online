# Cross-player component-bearing limb-tool use self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/evidence/selfchecks/MANIFEST.md` and
> `docs/architecture/protocol.md` before citing.

Owner cycle: backlog "Cross-player item use" component-bearing tools slice.
Decision: add `splint`, `carcasssplint`, `tourniquet` and `icepack` to the
existing `PlayerItemUseRequest`/`PlayerItemUseResult` operation, and synchronize
the limb `[Saveable]` component state that those tools install. No new wire
message and no protocol bump (additive `CharacterLimbMsg` fields only).

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Tool `useLimbAction` tables | `Item.cs:392-408` (tourniquet), `1471-1490` (splint), `1497-1516` (carcasssplint), `1621-1638` (icepack) |
| 2 | Limb components | `SplintLimb.cs` (`condition`/`conditionLossMinute`/`item`, `Start` sets `limb.splinted`), `TourniquetScript.cs` (`condition`/`timeApplied`, `Start` sets lower-limb `blockedBleeding`), `ChilledLimb.cs` (`timeLeft`/`maxTime`) |
| 3 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged |
| 4 | Character snapshot surfaces | `CharacterLimbMsg` now carries `Components` + `IsHead`/`IsVital` (vital/head are needed for the native eligibility checks) |
| 5 | Local body apply | `CharacterDataSync.ApplyHealState` + `LimbComponentStateCodec.Apply` — adds/updates the real game component on the target's own body |
| 6 | Reconnect restore | `CharacterDataSync.ApplyRestoredStatsAndWipe` + `LimbComponentStateCodec.Apply` — limb components survive the save/restore path |
| 7 | Guest transfer table | `IItemControl.UpdateTransferredItem` / `RemoveTransferredItem` — same consume path as other slices |

## 2. Design

- `CharacterLimbMsg` gains `Components` (same `ComponentStateMsg` shape as item
  component state), `IsHead` and `IsVital`. The last two are not part of the
  vanilla save set but are required by the host to mirror the native splint and
  tourniquet eligibility checks.
- `RemoteLimbToolProfile` gains a neutral `RemoteLimbComponentKind` plus the
  component constants (`conditionLossMinute`, `timeLeft`, `maxTime`) and a
  `DestroyAtZero` flag (icepack does not destroy when its condition hits zero).
- `RemoteLimbToolApplication` now writes the neutral component state onto the
  selected limb and honors the native eligibility rules: splint/tourniquet are
  refused on head/vital limbs, tourniquet refuses the body's central limb
  (`limbs[2]`), and splint/tourniquet refuse an already-installed component.
  Icepack refreshes an existing `ChilledLimb`.
- New `LimbComponentStateCodec` (Game Adapter) captures the three known limb
  component types from the owner body into `ComponentStateMsg` and applies
  `ComponentStateMsg` back by adding/updating the real Unity component on the
  local body. CharacterDataSync calls it on every captured limb and on every
  heal-result/restore limb apply.
- `PlayerItemUseService` passes the original item condition into the tool
  application (splint/tourniquet components carry the item's remaining
  condition), and the item-destroy decision respects `DestroyAtZero`.
- **Scope limits** — supported component-bearing tools: `splint`,
  `carcasssplint`, `tourniquet`, `icepack`. Minigame-random tools (tweezers),
  timed tools (medicalsuture), wear, and timed/random medicine remain future
  slices.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Limb wire state | `CharacterLimbMsg.Components` round-trips through the character-data file | `CharacterDataFileStoreTests.Save_Load_RoundTripsEveryFieldFamily` now asserts a `SplintLimb` state |
| Splint | adds `SplintLimb` state + marks `Splinted`, refuses head/vital/existing | `RemoteLimbToolApplicationTests.ApplySplint_...` (3 cases) |
| Tourniquet | adds `TourniquetScript` state + marks `BlockedBleeding`, refuses head/vital/central/existing | `RemoteLimbToolApplicationTests.ApplyTourniquet_...` |
| Icepack | adds `ChilledLimb` state, lowers temperature, keeps the item at zero condition | `RemoteLimbToolApplicationTests.ApplyIcepack_...` + service test |
| Host operation | guest uses splint/tourniquet on host — item destroyed, component lands in host snapshot | `PlayerInteractionServiceTests.Guest_UsesSplintOnHost_...`, `...UsesTourniquetOnHost_...` |
| Host operation | guest uses icepack on host — item remains with reduced condition | `PlayerInteractionServiceTests.Guest_UsesIcepackOnHost_...` |
| Adapter codec surface | `LimbComponentStateCodec` has static Capture/Apply with the expected signatures | `LimbComponentStateCodecTests` |
| UI eligibility | known component tools appear in the local use-item list | `PlayerInteractionApply.IsLocalUseItem` uses the shared `RemoteLimbToolCatalog` (no projection change) |

## 4. Verification

- **L0 unit**: `RemoteLimbToolApplicationTests` (12),
  `PlayerInteractionServiceTests` +3, `CharacterDataFileStoreTests` +1 limb
  component assertion, `LimbComponentStateCodecTests` (2).
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` 1454 green,
  `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
