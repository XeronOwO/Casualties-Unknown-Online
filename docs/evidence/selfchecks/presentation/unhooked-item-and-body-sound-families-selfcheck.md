# Self-check — the item and body one-shot sounds outside the ingest family

- Ticket: `docs/backlog/review/unhooked-item-and-body-sound-families.md` (2026-09-26 cycle, HEAD started at `c76c3078`)
- Change: five new call identities for the character-sound capture (the limb treatment, the world
  drink, the transfer gesture, the per-step coroutine scope) plus the classification of two origins
  that already existed, five new wire kinds with the protocol bump, and a census gate that pins which
  clips each decision carries AND which scopes consult each set.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The capture chain | `Sound.Play` string overload → `SoundPlayPatch` maps `CallContext.Current` to a `CharacterSoundPolicy.Origin` → `CharacterSoundPolicy.Classify(origin, clip)` → `PatchBridge.OnCharacterSound` → `CharacterSoundSync.Report` → `CharacterSoundMsg` (star relay) → the receiving side replays under `RemoteApply` (`CharacterSoundSync.OnReceived` drops the owner's own echo, so no double play). The AudioClip overload has its own patch and captures none of this cycle's clips. |
| 2 | The limb-treatment choke point | `PlayerCamera.ApplyWoundItem` (PlayerCamera.cs:739-763): both branches run inside it — `item.Stats.useLimbAction(this.selectedLimb, item)` (:754) and `waterContainerItem.ApplyToLimb(this.selectedLimb, 100f)` (:760) → `WaterContainerItem.ApplyToLimb` (WaterContainerItem.cs:218-256) → `LiquidType.onHealthUse`. A whole-assembly census confirms the choke point: `useLimbAction` has exactly ONE invocation (PlayerCamera.cs:754) and `onHealthUse` exactly two (WaterContainerItem.cs:230/256), both reached only from `ApplyToLimb`, whose only non-delegate caller is PlayerCamera.cs:760. |
| 3 | The four medical sites that do NOT go through it | `Item.cs:515` `"splint"` (`itemInfo9.useAction`, the rag — its `useLimbAction` is the separate `BandageMinigame` at :517), `:1443` `"goo"` (`itemInfo17.useAction`, the rosepod), `:1658` `"drainuse"` (`itemInfo24.useAction`, the drainer; the clip occurs exactly once in the assembly) and `:7123` `"syringe"` inside `static Item.DrawBlood`, whose only callers (:1764, :1930) are liquid-container `useAction`s. All four run under `Body.UseItem` (Body.cs:2479) → `CharacterItemUse`, so the medical set is consulted from BOTH scopes. The independent review found this: the first cut listed the set under the limb scope alone and those four sites stayed silent. |
| 4 | The world-drink path | `FluidManager.DrinkLiquid` (FluidManager.cs:286-329): the water branch plays `"drink"` itself (:314); the groundwater / lumalgae / oil / sap branches call `Liquids.Registry[...].onDrink` (:293-308), where the remaining clips play (Liquids.cs:1133/1175/1190/1286/1298 `pills`, :1501 `drink`). `Body.HandlePhysics` is the caller. `onDrink` has two other entries (`WaterContainerItem.cs:210`, `NonDescriptCan.cs:78`) — container uses, i.e. `CharacterItemUse`, where the same clips already report as the ingest family. |
| 5 | The item-use family | The item's own `useAction` runs inside `Body.UseItem` / `Body.UseItemInHand`, which already open `CharacterItemUse` (`BodyItemPatches.OpenUseSoundScope`). `flashlighttoggle` (Item.cs:3998/4022), `error` (5672), `centrifuge` (5688), `combine` (4297/6700) and `drop` (2614) are all inside those delegates — so only the classification was missing. |
| 6 | The inventory gestures | `Body.SwitchHands` (Body.cs:1131) and `Body.SwapSlots` (:1427) already run inside `InternalReorder` (`BodyItemPatches.SwapSlotsPatch` / `SwitchHandsPatch`); `Body.CombineItems` (:1284) runs inside `Craft` (`CraftingPatches.BodyCombinePatch` → `CraftingSync.OnCombineBegin`); `Body.CombineLiquids` (:1250) is reached from `LiquidTransfer.Finish` (LiquidTransfer.cs:38), which IS patched but opens NO scope. |
| 7 | The coroutine family | `Vomiter.DoVomit` / `DoBloodVomit` (Vomiter.cs:45/98, started by the public `Vomit` / `VomitBlood`), `Body.NapCoroutine` (Body.cs:2502, started at :2497 — `TakeANap` picks `AltNapCoroutine`, which plays nothing, when sickness/happiness/temperature are out of band, :2492-2496) and `Body.WaterShake` (:2550, started by name at :3334). Each clip plays inside the coroutine body, i.e. inside the generated state machine's `MoveNext` — after the patched method already returned. The water shake passes `base.transform` as its follow target; the vomit and the nap pass null. |
| 8 | What the 2D calls are | `Sound.Play("vomitwarning", Vector2.zero, true, …)` / `bloodvomitwarning` (Vomiter.cs:144/155, outside the wrapped routines), the climb clips (`Sound.Play(climbable.climbSounds.PickRandom<AudioClip>(), Vector2.zero, true, …)`, Body.cs:477) and the syringe minigame's own cues (`bullethit` SyringeMinigame.cs:79, `syringe` :86, both at `Vector2.zero` with the 2D flag, from the minigame's Update). |
| 9 | Who suppresses a native call | `RemoteMedicalPatches.RemoteMedicalBlockApplyWoundItemPatch` blocks `ApplyWoundItem` while the remote medical view is open; `RemoteDragMutationPatches.RemoteDragApplyWoundItemPatch` captures the drag-release intent instead of applying it; `ItemCollisionEnter2DPatch` + `NonAuthoritativeItemImpactPolicy` suppress a guest's world-item collision effects; `RemoteIntentApplier` replays a remote-driven use under `RemoteApply`. |
| 10 | The scope readers a new origin can disturb | Every `CallContext.Current` reader in `src/` was read (30 sites). The drop/pickup hooks (`BodyItemPatches`) report unless the origin is `InternalReorder`/`Craft`; the item hooks (`ItemPatches`, `ContainerItemPatches`, `BodyPatches`) gate on `Craft`/`BuildingDeathDrop`; the write paths gate on `RemoteApply`; the time-scale patch (`PlayerCameraSetTimeScalePatch`) special-cases only `WorldTimeApply`/`WorldTimeSleepLocal` — and `Vomiter` calls `SetTimeScale` INSIDE the newly wrapped coroutine, now under `CharacterBodySound` instead of `LocalAction`, which that patch does not distinguish. `BodyItemPatches.IsEligibleLocalUse` is the only `== LocalAction` reader, and no path inside any new scope reaches `Body.UseItem`. |

## 2. Decisions

1. **One scope per native choke point, not one per clip.** The limb-treatment family needed a single
   anchor: `PlayerCamera.ApplyWoundItem` is entered by both branches the census found. The four sites
   that bypass it are NOT given a second limb scope — they already run inside the item-use scope, so
   the medical SET is consulted from both origins instead (one classification, two entry paths).
2. **The position carries the subject.** Every medical call passes `follow: null` and a world
   position, so the previous cut's "the carrier needs a subject identity" was wrong about the
   mechanism: the report is position-based and the receiver plays it where the call happened. A
   follow-based report would have been wrong — it re-parents to the REPORTER's clone.
3. **The coroutine family is scoped per step.** A Prefix/Postfix pair around an iterator method wraps
   the state machine's CREATION, not its body, so the scope would be disposed before the first
   statement; `ScopedCoroutine` enters around each `MoveNext` and never keeps a scope open across a
   `yield`. Precedent in tree: `WorldGenRandomIsolation` drives a wrapped enumerator.
4. **Two existing origins are classified instead of nested.** `InternalReorder` (SwitchHands /
   SwapSlots) and `Craft` (CombineItems) already wrap the gesture sounds. Opening a second, nested
   capture origin inside them would hide the outer origin from the guards that read it. The
   classification is clip-keyed, so the wider scopes capture only the three gesture clips.
5. **The capture stays local-action only.** A remote-driven mutation still never reports as the local
   player's action (`CaptureScopeGuard.IsLocalAction()`), which is also what stops a scope being
   opened INSIDE a `RemoteApply` scope — the two `Sound.Play` patches' only echo guard is that outer
   scope. The consequence is recorded on `todo/suppressed-native-call-sounds-stay-unheard.md`.
6. **The 2D calls stay local (user decision).** The user chose "every 3D world sound is carried; 2D
   screen feedback stays the acting player's own". The vomit prompts, the climb clips and the syringe
   minigame's cues carry no world position at all, so carrying them would have meant inventing one.
7. **The wire change bumps the protocol in the same change.** Five kinds join `CharacterSoundKind`;
   `ProtocolVersion.Current` moves with its per-number log entry, and the kind census pin in
   `ConsumeSoundCaptureGateTests` was widened in the same change (a kind added to the enum is a red
   there until it is reviewed).

## 3. Whole-family audit

| Decision | Clips | Native sites | Gate pin |
|---|---|---|---|
| `IsMedicalClip` (carried from `Origin.Medical` AND `Origin.ItemUse`) | syringe, splint, goo, boneweld, drainuse, tweezeruse, spray, laser, wrenchhit, cream | 28 sites in Item.cs (10 via `useLimbAction`, 4 via `useAction`, the rest via `ApplyToLimb` delegates) + Liquids.cs:1074/1100/1494 | `EveryCensusRow_MatchesThePolicyClassification` + `TheItemUseRow_ConsultsTheIngestMedicalAndFeedbackSets` |
| `IsIngestClip` (`Origin.ItemUse`) | eatCrunch, eatFlesh, glass, crystalenemylaugh, drink, pills | the edible use actions + the container drink | same + `ConsumeSoundCaptureGateTests` (the ingest census) |
| `IsItemUseFeedbackClip` (`Origin.ItemUse`) | flashlighttoggle, error, centrifuge, combine, drop | Item.cs:3998/4022/5672/5688/4297/6700/2614 | same |
| `Origin.WorldDrink` | drink, pills | FluidManager.cs:314 + Liquids.cs:1133/1175/1190/1286/1298/1501 | same |
| `Origin.InventoryGesture` | switch, waterpour, combine | Body.cs:1131/1427/1250/1284 | same |
| `Origin.BodySound` | vomit1, vomit2, stretch, dogshake | Vomiter.cs:86/125, Body.cs:2510/2553 | same |
| Deliberately local (2D) | vomitwarning, bloodvomitwarning, the climb clips, SyringeMinigame's bullethit/syringe | Vomiter.cs:144/155, Body.cs:477, SyringeMinigame.cs:79/86 | `TheTwoDimensionalCues_OpenNoCaptureScope` (no scope on a known 2D source; the two prompt clips absent from the policy) |
| Out of family (ticketed) | the world-item impact `drop` + block step sound, the remote-driven replay, the blocked remote-medical treatment | Item.cs:238-247, `RemoteIntentApplier.ApplyUseItem`, `RemoteMedicalBlockApplyWoundItemPatch` | the ticket's census rows + §8 |

## 4. Self-check table

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | `PlayerCamera.ApplyWoundItem` choke point | new `CharacterMedicalUse` scope, local camera + local action only | `MedicalSoundPatches` + `ItemAndBodySoundCaptureGateTests.TheMedicalFamily_IsAnchoredOnTheLocalCamerasLimbAction` + `CharacterSoundPatchTests.PatchInventory_DeclaresEveryCharacterSoundTarget` |
| 2 | The four world-use medical sites | the medical set is consulted from `Origin.ItemUse` as well | `CharacterSoundPolicyTests.MedicalClips_AreCarriedFromBOTHTheLimbActionAndTheItemUseScope` + `...TheItemUseRow_ConsultsTheIngestMedicalAndFeedbackSets` |
| 3 | `FluidManager.DrinkLiquid` | new `CharacterWorldDrink` scope, local body only | `WorldDrinkSoundPatches` + `...TheWorldDrinkFamily_IsAnchoredOnDrinkLiquid` |
| 4 | `Body.CombineLiquids` | new `CharacterInventoryGesture` scope, local body + local action only | `InventoryGestureSoundPatches` + `...TheInventoryGestureFamily_SplitsBetweenItsOwnScopeAndTheExistingOnes` |
| 5 | The four coroutines | per-step scope through `ScopedCoroutine` | `BodySoundPatches` + `ScopedCoroutine` + `...TheBodyOneShotFamily_IsCapturedThroughTheCoroutineWrapper` |
| 6 | `CharacterSoundPolicy` | five new origins; the item-use row consults three sets | `CharacterSoundPolicyTests` (per-family positives and negatives) + the two reach/census gate pins |
| 7 | `CharacterSoundKind` / `ProtocolVersion` | five kinds + the bump with its log entry | `CharacterSoundSyncTests.ItemAndBodyFamilies_RoundTripTheirKindsClipsAndSpatialFacts` + `ConsumeSoundCaptureGateTests.EveryProtocolBump_CarriesItsPerNumberLogEntry` |
| 8 | The receiver | unchanged — position/follow/2D facts as the native call had them; a `BodySound` clip may be follow-based (dogshake) | `CharacterSoundSyncTests.BodySoundFamily_CarriesTheFollowFactTheNativeCallHad` |
| 9 | No self-echo | the source never hears its own sound back | `CharacterSoundSyncTests.HostLimbTreatmentSound_BroadcastsToBothGuests_AndNeverReturnsToTheHost` (the host's own store raises nothing for its own send) |
| 10 | Capability registration | four new patch classes registered | `AdapterCapabilityCatalog` (Character capability) + the full suite's catalog gate |

## 5. The red and the ladder

All runs are recorded under `%TEMP%` (kept, not deleted) and are quoted with the command that produced
them. The red was re-recorded with the FINAL gate content after the review found the first recording
was made with a matcher that still had two self-test bugs (that first run said 7 failed / 8 passed /
15 with the matcher's own faults among the failures).

| Step | Command | Result |
|---|---|---|
| Red (frozen matcher) | `git stash push -u -- src/ <the two test files that name the new types>` then `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~ItemAndBodySoundCaptureGateTests"` | **7 failed / 9 passed / 16**, exit 1 — the four "patch file is missing" facts, the census row, the item-use reach pin and the 2D-cues pin; the nine matcher self-tests pass before and after by design (`%TEMP%/cuo-red-itembody-final.txt`) |
| Focused | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~CharacterSound\|FullyQualifiedName~SoundCaptureGateTests"` | **82 passed / 0 failed** (gates project 32, main project 50), exit 0 (`%TEMP%/cuo-focused3.txt`) |
| Gates project | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` | **263 = 262 passed + 1 by design**: `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` is red until the cycle's checklist is filled, which happens at the end of the cycle (`%TEMP%/cuo-gates-itembody2.txt`) |
| `dotnet format` | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0, and `git status --short` + `git diff --shortstat` byte-identical before and after (`%TEMP%/cuo-format-itembody.txt`, `cuo-status-before-format.txt`, `cuo-status-after-format.txt`) |
| Full suite WITH build, checklist gate excluded | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` | **4083 + 262 passed / 0 failed**, exit 0 (`%TEMP%/cuo-full-itembody.txt`) |
| Final unfiltered run, checklist complete | `dotnet test CasualtiesUnknownOnline.slnx` | recorded on the delivery checklist's build line in the same commit |

Counting discipline: a `--filter`ed number is never quoted as a full-suite number — the excluded test
is named, and the unfiltered total is stated separately.

## 6. Verification table

| What | Evidence |
|---|---|
| The pins fail before the change | §5 red row (7/9/16) |
| The pins pass after it | §5 focused row + the gate class alone: 16/16 |
| Behaviour: classification per family and the two-scope medical reach | `CharacterSoundPolicyTests` |
| Behaviour: the wire round-trip, the follow fact, the host broadcast with no self-echo | `CharacterSoundSyncTests` |
| Contract: the seven new anchors resolve in the game assembly | `CharacterSoundPatchTests` (Integration) |
| The whole suite | §5 full-suite row, plus the final unfiltered run on the checklist line |

## 7. Review disposition

Independent adversarial review, fresh context, frozen tree, no build/test access (the full text is
`%TEMP%/cuo-review-unhooked-item-and-body-sound-families.md`). Every finding is dispositioned here;
all fixes are in this same cycle.

| Finding | Severity | Disposition |
|---|---|---|
| M1 — four censused medical sites (`splint`/`goo`/`drainuse`/`DrawBlood`'s `syringe`) run from the item's world `useAction`, so the limb-scope classification carried nothing for them | major | FIXED: the medical set is now consulted from `Origin.ItemUse` too; the policy test that pinned the (wrong) silence was replaced by `MedicalClips_AreCarriedFromBOTHTheLimbActionAndTheItemUseScope`, and the gate gained `TheItemUseRow_ConsultsTheIngestMedicalAndFeedbackSets`. The census row now names both entry paths. |
| M2 — `SyringeMinigame.cs:79/86` plays 2D cues that no census row mentioned | major | FIXED: recorded as a deliberate-local row (ticket + §3), and `TheTwoDimensionalCues_OpenNoCaptureScope` now pins that no patch anchors the minigame's Update or `Vomiter.Vomit`/`VomitBlood`. |
| M3 — the recorded red (7/8/15) described a gate whose matcher still had two self-test failures | major | FIXED: the red was re-recorded with the frozen gate — 7 failed / 9 passed / 16 (§5) — and the earlier run is named as superseded. |
| M4 — §5 was an empty template and §6 deferred to it | major | FIXED: §5/§6 carry the commands, the numbers and the artifact names. |
| M5 — the "final unfiltered" run was a filtered one, and the unfiltered sibling was red on the checklist | major | FIXED: the filter and the excluded test are named, the checklist is filled before the final unfiltered run, and the final run is recorded on the checklist's build line. |
| m6 — `onDrink` attributed to `DrinkLiquid` alone | minor | FIXED: the census row names the other two entries (`WaterContainerItem.cs:210`, `NonDescriptCan.cs:78`) and why no clip is lost. |
| m7 — `CombineLiquids` scope had no body-identity guard | minor | FIXED: the prefix now requires `IsLocalPlayerBody(__instance)` as well as `IsLocalAction()`. |
| m8/m9 — the architecture page over-claimed (`batteryinsert` named as carried) and its §3.4 paragraph contradicted the new one | minor | FIXED: both paragraphs corrected in the same change. |
| m10 — §4 cited a test that does not pin the self-echo drop | minor | FIXED: the drop now has its own assertion (`..._AndNeverReturnsToTheHost`) and the row cites it. |
| m11 — the round-trip test asserted "position-based" across `BodySound`, but `dogshake` is follow-based | minor | FIXED: the loop covers the four position-based families and `BodySoundFamily_CarriesTheFollowFactTheNativeCallHad` pins both values. |
| m12 — the 2D pin was negative-only and could not fail for the carried families | minor | FIXED: replaced by a pin with real teeth (no scope on a known 2D source) and its reach stated in the gate's own doc comment. |
| n13 — "28 clips"/"28 call sites" vs the 31 sites the row lists | nit | FIXED: the ticket says 31 sites (28 in Item.cs). |
| n14 — the nap acceptance row ignored `AltNapCoroutine` | nit | FIXED: the row and §1 now state the conditional. |
| n15 — "the treated limb may be another player's" is not exercised by any reachable path | nit | FIXED: the ticket's Limits state why (the only `selectedLimb` writer is the local wound view, and the remote view blocks the native call), while the position-based shape stays. |
| n16 — "stacked attributes are out of reach" was wrong (the matcher's own sample reads two attributes) | nit | FIXED: the limits now name what is genuinely out of reach — a SPLIT anchor, the `nameof(...)` form, a manual `PatchProcessor` call. |
| n17 — the ingest gate's origin floor (12) tolerated four origins vanishing | nit | FIXED: floor raised to 14 of 16, with the reason in its doc comment. |
| n18 — the ingest ticket/self-check kept the refuted "needs a subject decision" rationale | nit | FIXED: both sentences now state the corrected mechanism. |

What the review could NOT falsify (recorded as its own words): the blast radius on the 30
`CallContext.Current` readers, the coroutine wrapper's scope discipline, the nesting rationale, the
absence of a double-play path, the wire numbering and census pin, and reference integrity.

## 8. Limits

- **The audible result is not proven here.** The sounds are Unity icalls; the test host cannot play
  them, and the production capture reads `CallContext` from the real call. What is machine-checked is
  the routing the user's expectation depends on. Whether a peer actually HEARS the clip, at the right
  place, once, is the user's dual-client run.
- **The 2D boundary is a decision, not a defect.** The climb clips, the two vomit prompts and the
  syringe minigame's cues stay local because the user chose "3D world sounds only"; they have no world
  position to replay at.
- **The gate's reach is our own source.** The decompiled tree is not in the repository, so a NEW native
  clip cannot fail a pin; a SPLIT anchor, the `nameof(...)` form and a manual `PatchProcessor` call are
  outside the matcher (the game-assembly contract tests own the half that needs the assembly). The 2D
  pins are shape-level and say so.
- **Three paths have no native clip to carry at all**: the remote-medical treatment (blocked in that
  view), the world-item impact (suppressed on guest copies) and the remote-driven replay (deliberately
  not reported as the local player's action). All are recorded and ticketed as
  `todo/suppressed-native-call-sounds-stay-unheard.md`.
- **The coroutine wrapper is exercised by contract, not by a running Unity coroutine.** The gate pins
  its shape and its use; that a real coroutine plays its clip exactly once inside the stepped scope,
  and that `StartCoroutine("WaterShake")` (the string form, Body.cs:3334) routes through the Harmony
  postfix, are the user's session.
- **The medical position path is not exercised for another player's limb** (see the ticket's Limits):
  every reachable treatment today plays at this client's own body, and the remote-view path plays no
  native clip at all.
- **No deployment this cycle**: the change is a runtime behaviour change, so the deployed artifact
  stays at the previous build until the user's release cycle (deployment and dual-client acceptance are
  the user's actions).
