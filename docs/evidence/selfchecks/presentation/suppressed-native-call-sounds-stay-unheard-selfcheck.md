# Self-check — the sounds whose native call is suppressed

- Ticket: `docs/backlog/review/suppressed-native-call-sounds-stay-unheard.md` (2026-09-26 cycle, HEAD started at `04557ce1`)
- Change: the two rows the item and body sound cycle recorded as out of family are carried — the
  remote limb treatment plays the clip the blocked native limb action would have played, the bandage
  minigame's own step carries `bandage` for the local and remote treatment alike, and a world-item
  impact now reaches every member as the authority's own `drop`/step/dust presentation (or a plush's
  squeak) through a dedicated host-only `ItemImpact` event.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The character-sound capture chain (unchanged this cycle) | `Sound.Play` string overload → `SoundPlayPatch` maps `CallContext.Current` → `CharacterSoundPolicy.Classify` → `PatchBridge.OnCharacterSound` → `CharacterSoundSync.Report` → `CharacterSoundMsg` (star relay) → the receiver replays under `RemoteApply` and drops the owner's own echo. |
| 2 | The blocked native limb action | `RemoteMedicalBlockApplyWoundItemPatch` blocks `PlayerCamera.ApplyWoundItem` while the remote medical view is open, so the item's `useLimbAction` (which plays the clip) never runs; `RemoteMedicalOperationHandler.TryHandleLimbUse` routes the gesture to the host-authoritative session instead. |
| 3 | The accepted medical surface | `LocalUseItemLimbUseItem` consults `RemoteHealProfiles`, `RemoteLimbToolCatalog`, `RemoteBandageMinigameCatalog`, `RemoteOtherMedicalCatalog` (amputation tools / defibrillators / wrenches), `RemoteMedicineCatalog.IsInjectableItem` and `RemoteTopicalCatalog.IsTopicalItem`, plus the handler's own `tweezers` literal — 49 item ids, the scan surface the treatment table must decide for. |
| 4 | The per-item limb-action census (decompiled tree) | Every accepted id's `useLimbAction` delegate was read: 16 play a clip themselves (syringe ×7 — six injectables plus `chestdrain` —, boneweld, goo ×2, splint ×2, tweezeruse, wrenchhit, spray ×2), 2 route through `WaterContainerItem.ApplyToLimb` where the LIQUID plays `cream` (Liquids.cs:1074/1100), 8 start the native `BandageMinigame`, 23 carry no clip this table owns. |
| 9 | What those delegates CALL (the correction the review forced) | `medicalsuture`'s delegate calls `limb.body.DoGoreSound()` (`gore{1..5}`, Item.cs:378 → Body.cs:2443-2446); the amputating tools start the native `AmputationMinigame`, whose completion calls `Limb.Dismember()` (`gore` + `gore{N}`, AmputationMinigame.cs:69-85 → Limb.cs:91-99); `ShrapnelMinigame.cs:71` adds `gore{N}` to the tweezers path. These come from the limb/minigame path, not the item's action, so they are recorded as UNCARRIED on the ticket and ticketed (`todo/treatment-gore-presentation-not-carried.md`) — the dismemberment is also applied on the patient's own client, so the carrier needs its double-play census first. |
| 5 | The bandage minigame step | `BandageMinigame.PhysicsUpdate` plays `Sound.Play("bandage", Minigame.game.currentItem.transform.position, false, true, null, 1f, 1f, false, false)` when a wrap completes (BandageMinigame.cs:112) — 3D, at the item, frames AFTER the limb action that started the minigame returned. |
| 6 | The two suppressed impact presentations | `Item.OnCollisionEnter2D` (relative velocity > 3): `drop` + the landing block's `RandomStepSound` pick + `Instantiate(Resources.Load<GameObject>("DustMini"))`, all at the item's transform (Item.cs:238-247); `PlushScript.OnCollisionEnter2D` (relative velocity > 2): the plush's own `selectedSound` clip (PlushScript.cs:17-23). Both are suppressed on a guest by `NonAuthoritativeItemImpactPolicy`. |
| 7 | The authority split | `ShouldSuppress` = live session ∧ guest ∧ standalone world item; `ShouldReport` = live session ∧ host ∧ standalone world item — the same three facts, opposite middle term, so exactly one half applies to one collision. |
| 8 | The item-domain event seam | `IItemControl.SendItemImpact` / `FireItemImpactReceived` / `ItemImpactReceived`, implemented by `ItemMessageFlowService` (host-only broadcast) and forwarded by `ItemService` — the item surface already serves both roles ("on the host … relay; on the guest … surface the events for the adapter to apply"). |

## 2. Decisions

1. **One carrier per row.** The treatment's clip is *knowledge* the blocked call took with it, so the
   medical domain holds it as a reviewed table (`RemoteMedicalTreatmentSoundCatalog`) and plays it at
   the treated limb through the existing capture scope; the impact has no acting player, so it gets a
   dedicated host → guest world-item event instead of a call-identity scope. Neither row invents a
   clip the native path could not have played: every table row is a censused native literal.
2. **The minigame-driven clips are captured per STEP, not per action.** A limb action that only
   STARTS a native minigame has already returned when the minigame plays its clip, which is why the
   bandage family was silent on every peer — locally and remotely. `BandageMinigameSoundPatches`
   enters the treatment scope around `PhysicsUpdate` and disposes it after, the shape
   `ScopedCoroutine` uses for the body one-shots.
3. **The treatment clip plays where the native call would have played it.** The clip is played by the
   ACTING side (the operator's client) at the treated limb's body of the displayed body — the same
   actor the native call had, and the only side that both holds the item and knows the limb (the
   natives differ per row — `musharm`'s `goo`, the two `spray` rows and `wrench`'s `wrenchhit` use the
   limb's or the item's own transform — and the relayed position is the treated limb, which is what a
   peer should hear). The patient's client plays nothing, so the relayed report cannot double-play.
4. **The impact event is host-only and carries the presentation facts.** A guest's copies are the
   suppressed half, so a guest never reports; the event carries the position, the presentation kind
   and — for a plush, whose receiver copy rolls its own index — the exact sound index.
5. **The receiver replays the game's OWN presentation.** The `drop` clip, the landing block's step
   sound re-picked by the receiver's own `WorldGeneration.world.RandomStepSound` (the same call the
   native body makes) and the same `DustMini` object; the replay runs under `RemoteApply`, so the
   capture can never report it back. Which parts a kind replays is a pure Runtime table
   (`ItemImpactPresentation`), not a code path.
6. **The third row is recorded, not changed.** A remote-driven item use still does not report as the
   local player's action (decision 221); the owner hears a use their peers do not, and that price is
   written on the ticket rather than paid elsewhere.
7. **The protocol bumps in the same change.** `ItemImpact = 141` joins `NetMsg`, the wire vocabulary
   index and the sync-coverage matrix (row I9, `Transient-by-design`: a one-shot presentation with no
   fallback by design), and `ProtocolVersion.Current` moves to 43 with its per-number log entry.

## 3. Whole-family audit

| Decision | Items / mechanisms | Native source of the clip | Gate pin |
|---|---|---|---|
| Remote treatment, item row | 16 accepted items | their own `useLimbAction` literals (Item.cs:616/696/1375/1483/1509/1539/1565/1589/1613/1734/1760/1926/2100/2124/4864/1700) | `TheRemoteTreatmentTable_DecidesEveryAcceptedMedicalItem` + `RemoteMedicalTreatmentSoundCatalogTests` |
| Remote treatment, liquid row | `paincream` (reliefcream), `woundglue` (woundglue) | `Liquids.cs:1074` / `:1100` through `WaterContainerItem.ApplyToLimb` | same |
| Bandage minigame step | 8 bandage-family items + `musharm`'s own `goo` | `BandageMinigame.cs:112` | `TheBandageMinigameStep_IsCapturedInsideTheLimbTreatmentScope` + `BandageMinigameClip_IsCarriedFromTheLimbTreatmentScope` |
| Uncarried, recorded | 23 accepted items in recorded groups | their delegates play nothing / a 2D minigame / a clip the limb-gore path owns | the table's `UncarriedItems` + liquid-driven rows, pinned complete by the table gate |
| The limb-gore rows (found by the review) | `medicalsuture`, the seven amputating tools, the tweezers' second clip | `Body.DoGoreSound` / `Limb.Dismember` / `ShrapnelMinigame` | recorded as UNCARRIED on the ticket + `todo/treatment-gore-presentation-not-carried.md` (this cycle's acceptance does not claim them) |
| World-item impact | `Item.OnCollisionEnter2D`, `PlushScript.OnCollisionEnter2D` | Item.cs:238-247, PlushScript.cs:17-23 | `TheSuppressedImpactFamily_IsReportedByTheAuthoritySideOnly` + `TheImpactReport_KeepsTheNativeThresholdAndTheOncePerCollisionShape` |
| Impact carrier | `NetMsg.ItemImpact = 141`, host → guest | — | `TheImpactEvent_IsAHostToGuestWorldEvent` |
| Impact replay | the game's own drop/step/dust, the plush's own clip | — | `TheImpactReplay_RunsUnderRemoteApplyAndUsesTheGamesOwnPresentation` + `ThePresentationDecision_IsAPureRuntimeTable` |
| The untouched row | remote-driven replay (decision 221) | — | the ticket's decision 3 |

## 4. Self-check table

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | `CharacterSoundPolicy.IsMedicalClip` | `"bandage"` joins the medical set (the minigame step's clip) | `CharacterSoundPolicyTests.BandageMinigameClip_IsCarriedFromTheLimbTreatmentScope` + the widened census row |
| 2 | `BandageMinigame.PhysicsUpdate` | new per-step capture scope (`BandageMinigameSoundPatches`), registered in the Character capability | the anchor pin + the game-assembly contract tests (`CharacterSoundPatchTests`) + the capability-catalog gate |
| 3 | `RemoteMedicalTreatmentSoundCatalog` | new pure table: 16 clip rows, 2 liquid rows, 2 liquid-driven items, 31 recorded silences | `RemoteMedicalTreatmentSoundCatalogTests` + `TheRemoteTreatmentTable_DecidesEveryAcceptedMedicalItem` |
| 4 | `RemoteMedicalOperationHandler.TryHandleLimbUse` | the dispatch is extracted into `TryDispatchLimbUse`; `PlayTreatmentSound` runs only after it returned true, inside `CallContext.Origin.CharacterMedicalUse` | `TheRemoteTreatmentPlaySite_PlaysOnlyAfterASuccessfulDispatch` |
| 5 | `NonAuthoritativeItemImpactPolicy` | `ShouldReport` joins `ShouldSuppress` (same three facts, opposite middle term) | `NonAuthoritativeItemImpactPolicyTests` (reflective truth table, Integration) |
| 6 | The two impact patches | a postfix on each native collision reports on the authority side, riding the native threshold | `TheImpactReport_KeepsTheNativeThresholdAndTheOncePerCollisionShape` |
| 7 | `ItemImpactMsg` / `ItemImpactKind` / `ItemImpactHandler` | the host-only event and its handler | `TheImpactEvent_IsAHostToGuestWorldEvent` + `ItemImpactPresentationTests` |
| 8 | `WorldItemImpactSync` / `WorldItemImpactReplay` | report on the authority, replay the game's own presentation under `RemoteApply` | `TheImpactReplay_RunsUnderRemoteApplyAndUsesTheGamesOwnPresentation` + the presentation-table tests |
| 9 | Wire bookkeeping | `NetMsg.ItemImpact = 141` + the vocabulary index row + matrix row I9 (18 anchors) + `ProtocolVersion.Current = 43` | `SyncCoverageGateTests` (vocabulary index, evidence quotes, verdict summary) + `ConsumeSoundCaptureGateTests.EveryProtocolBump_CarriesItsPerNumberLogEntry` |
| 10 | The reversed non-goal | `review/guest-background-ghost-item-ground-sounds.md` records the supersede (the guest-side suppression is unchanged) | the ticket's non-goal paragraph + the matrix row's loss cell |

## 5. The red and the ladder

All runs are recorded under `%TEMP%` (kept, not deleted) and are quoted with the command that
produced them.

| Step | Command | Result |
|---|---|---|
| Red (FINAL gate content, `src/` at HEAD — stashed for the run) | `git stash push -u -- src/` then `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName~ItemAndBodySoundCaptureGateTests\|FullyQualifiedName~ItemImpactPresentationGateTests"` | **10 failed / 29 passed / 39**, exit 1 — the bandage scope pin, the treatment table pin, the play-site pin, the census row, the 2D scan-surface pin (it now reads the new patch file) and the five impact pins; the reader and matcher self-tests pass before and after by design (`%TEMP%/cuo-red-suppressed-native-calls.txt`) |
| Focused | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~CharacterSound\|FullyQualifiedName~ItemImpact\|FullyQualifiedName~TreatmentSound\|FullyQualifiedName~SoundCaptureGateTests\|FullyQualifiedName~NonAuthoritativeItemImpact"` | **153 passed / 0 failed** (gates 53, main suite 100), exit 0 (`%TEMP%/cuo-focused-suppressed-native-calls.txt`) |
| Gates project, checklist filled | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` | **287 passed / 287**, exit 0 — the cycle's checklist gate turns green once its boxes are filled (`%TEMP%/cuo-gates-closed-suppressed-native-calls.txt`) |
| `dotnet format` | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0, and `git status --short` + `git diff --shortstat` byte-identical before and after (`%TEMP%/cuo-format-suppressed-native-calls.txt`, `cuo-status-before-format.txt`, `cuo-status-after-format.txt`) |
| Final full suite WITH build | `dotnet test CasualtiesUnknownOnline.slnx` (UNFILTERED, checklist filled) | **4132 + 287 passed / 0 failed**, exit 0 (`%TEMP%/cuo-full-final2-suppressed-native-calls.txt`), recorded on the delivery checklist's build line in the same commit |

Counting discipline: a `--filter`ed number is never quoted as a full-suite number — the excluded test
is named, and the unfiltered total is stated separately.

## 6. Verification table

| What | Evidence |
|---|---|
| The pins fail before the change | §5 red row (10/29/39) |
| The pins pass after it | §5 focused row + the gate project run |
| Behaviour: the treatment table's rows and its recorded silences | `RemoteMedicalTreatmentSoundCatalogTests` |
| Behaviour: the bandage clip's classification | `CharacterSoundPolicyTests.BandageMinigameClip_IsCarriedFromTheLimbTreatmentScope` |
| Behaviour: the authority/guest halves of the impact rule | `NonAuthoritativeItemImpactPolicyTests` (reflective, Integration) |
| Behaviour: which presentation a kind replays | `ItemImpactPresentationTests` |
| Contract: the new anchors resolve in the game assembly | `CharacterSoundPatchTests` (Integration, walks every `[HarmonyPatch]` contract) |
| Wire bookkeeping | `SyncCoverageGateTests`, `PatchBridgePortShapeGateTests`, `ConsumeSoundCaptureGateTests` |
| The whole suite | §5 full-suite row, plus the final unfiltered run on the checklist line |

## 7. Review disposition

Independent adversarial review, fresh context, frozen tree, no build/test access (the full text is
`%TEMP%/cuo-review-suppressed-native-call-sounds.md`: 2 majors, 5 minors, 6 nits). Every finding is
dispositioned here, and every fix is in this same cycle.

| Finding | Severity | Disposition |
|---|---|---|
| M1 — `medicalsuture` was recorded as natively silent, but its delegate calls `Body.DoGoreSound` (`gore{1..5}`, Item.cs:378 → Body.cs:2443-2446) | major | FIXED (census): the row now records that its clip belongs to the limb/gore presentation, which this table does not carry; the behavioural test says so, the ticket's census row and acceptance row 4b were corrected, and the carrier is ticketed. |
| M2 — the seven amputating tools were recorded as silent although the native `AmputationMinigame`'s completion calls `Limb.Dismember()` (`gore` + `gore{N}`, AmputationMinigame.cs:69-85 → Limb.cs:91-99); same root cause on the tweezers path (`ShrapnelMinigame.cs:71`) | major | FIXED (census): as M1 — recorded as UNCARRIED with the true mechanism, and `todo/treatment-gore-presentation-not-carried.md` carries the decision (the dismemberment is also applied on the PATIENT's own client, so the carrier needs its double-play census first). |
| m3 — the table is keyed by item only, so a clip plays in limb/item states where the native delegate's own local guard would have stayed silent (wrench on a healthy limb, splint on head/vital/already-splinted, chestdrain off limb 1, the mending tools at zero condition) | minor | RECORDED (limit): the ticket's and this file's limits state that the clip plays at dispatch, not at the delegate's own conditions, and that mirroring them is a separate decision rather than something to half-build. |
| m4 — §5 quoted "284 = 283 passed + 1 by design" against a log showing 285 total | minor | FIXED: the row now quotes the real run, and the intermediate numbers were replaced wholesale with the final records. |
| m5 — the full-suite row pointed at an empty checklist line while the only recorded full run was filtered | minor | FIXED: the ladder records the final UNFILTERED run (`4132 + 287`, exit 0) and the checklist's build line carries the same number. |
| m6 — the recorded format evidence predated the last three files of the tree | minor | FIXED: `dotnet format` was re-run on the frozen tree (exit 0, byte-identical `git status --short` + `git diff --shortstat` before and after) and the record was replaced. |
| m7 — the new pins' `Assert.Contains` calls read raw source, so a commented-out line would keep them green | minor | FIXED: the bandage-scope and play-site pins now read `WithoutComments(...)`, and `TheCommentStripper_DoesNotLetACommentedLineSatisfyAPin` pins that discipline (the red was re-recorded with the frozen gate). |
| n8 — the 2D-source pin's scan surface omitted the new patch file | nit | FIXED: `BandageMinigameSoundPatches.cs` joins the concatenation, and the red now fails that pin at HEAD because the file it must read does not exist yet. |
| n9 — the accepted-surface floor (40) tolerated one whole catalog silently unread | nit | FIXED: the floor is 45, every catalog reader must read something, and the table's decided-set floor moved to 49. |
| n10 — the position claim was broader than the code (the natives differ per row) | nit | FIXED (claim): decision 3 and the ticket's limits now state the position is the treated limb's body, name the rows whose native call used the limb's or the item's own transform, and say why the relayed position is the right one for a peer. |
| n11 — the census breakdown summed to 15 for a stated 16 | nit | FIXED: §1 row 4 now says syringe ×7 (six injectables plus `chestdrain`). |
| n12 — the replay's doc comment over-claimed the step-block match, and `WorldGeneration.world` was dereferenced unguarded | nit | FIXED: the comment names the contact-point difference and the step replay is guarded. |
| n13 — two reach comments inside the impact gate were wider than their pins | nit | FIXED: the family comment now names what the pin cannot reach (a third suppressed presentation), and the threshold pin reads the patches' POSTFIX bodies through the self-tested `BodyOf` reader instead of the whole files. |

What the review could NOT falsify (its own words): the wire half in full (uniqueness, direction,
registration, the per-number log entry, the re-pointed evidence quotes, every matrix row's anchor
count), the impact once-only semantics, the treatment's no-double-play paths, the 49-id accepted
surface and all 16 clip literals, the ≤600-line caps, the single `IItemControl` implementor, the
capability-catalog registration, and the suspected decision-221 breach on the per-step bandage scope
(unreachable — `RemoteInventoryIntentKind.ApplyToLimb` is captured but has no case in the owner's
replay switch).

## 8. Limits

- **The audible and visible result is not proven here.** No hearing, no pixels, no position: the
  treatment's clip, the impact's sound and the dust are Unity icalls and objects the test host cannot
  play, spawn or photograph. What is machine-checked is the routing every one of them depends on.
  Whether a peer hears the treatment at the treated limb, whether a landing sounds and puffs once per
  client, and whether nothing double-plays, is the user's dual-client run.
- **The treatment table is native knowledge CUO now maintains.** The census is stable only while the
  game's item table is; a game update that changes a limb action's clip would leave CUO playing the
  old one, and no gate here can see it (the decompiled tree is not in the repository). The gate
  notices an accepted item WITHOUT a decision, not a decision the game outgrew.
- **The treatment clip plays at dispatch.** A gesture the host then refuses leaves a sound without an
  effect (the local eligibility check mirrors the host's, so the window is small); a sound that waited
  for the commit would instead lag by a round trip.
- **The treatment clip plays at dispatch, not at the native delegate's own condition.** The natives
  guard themselves per limb/item state (a wrench only on a dislocated limb, splints not on head/vital/
  already-splinted limbs, a chest drain only on limb 1 and above 0.99 condition, the mending tools only
  above zero condition); CUO plays on a successful dispatch, so those states leave a clip without the
  native call's effect. Mirroring them is a separate decision, recorded here rather than half-built.
- **The gore rows are not carried** (see §1 row 9 and the ticket): the amputation/suture/shrapnel gore
  comes from the limb and minigame path and would need the double-play census of the operator's and the
  patient's own client.
- **The impact event's timing follows the authority.** A guest hears the landing when the HOST's copy
  lands, which can trail the guest's own visual copy by the item-position stream's cadence; the
  suppressed local copy is unchanged, so nothing double-plays.
- **The plush's clip needs the receiver's own plush at the impact position.** The replay looks it up
  with `Physics2D.OverlapPointAll`; if the copy is not there yet the squeak is skipped (logged), while
  the dust and the item clips are position-only and always replay.
- **A new native impact presentation is outside the gate.** The two known collision methods are
  pinned; a third suppressed presentation the game adds would need a new row and a new hook.
- **No deployment this cycle**: the change is a runtime behaviour change, so the deployed artifact
  stays at the previous build until the user's release cycle (deployment and dual-client acceptance
  are the user's actions).
