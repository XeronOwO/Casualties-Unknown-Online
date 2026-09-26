# Self-check — the treatment family's gore presentation

- Ticket: `docs/backlog/review/treatment-gore-presentation-carried.md` (2026-09-26 follow-up cycle, HEAD started at `a17aa303`)
- Change: the `gore` / `gore{N}` clip the amputation and shrapnel minigame steps play is carried to every
  peer by a per-step capture scope on each minigame, the clips join the medical classification, and the
  suture's blocked delegate clip becomes a row of the remote-treatment table. The user ruled for
  carrying the clip (the ticket's decision), so the cycle's job was the census the ticket demanded and
  then the carriers it pointed at.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The character-sound capture chain (unchanged this cycle) | `Sound.Play` string overload → `SoundPlayPatch` maps `CallContext.Current` → `CharacterSoundPolicy.Classify` → `PatchBridge.OnCharacterSound` → `CharacterSoundSync.Report` → `CharacterSoundMsg` (star relay) → the receiver replays under `RemoteApply` and drops the owner's own echo. |
| 2 | The amputation minigame's completion | `AmputationMinigame.Update` (AmputationMinigame.cs:69) dismembers once `cutProgress >= 1f` (:73-75) → `Limb.Dismember()` (:75). |
| 3 | What `Dismember` plays | `Limb.cs:91-99`: `Sound.Play("gore", limb.transform.position, …)` then `body.DoGoreSound()` → `Sound.Play("gore{1..5}", body.transform.position, …)` (Body.cs:2443-2446). |
| 4 | Who calls `Dismember` | FOUR sites in the decompiled tree: `AmputationMinigame.cs:75`, `ConsoleScript.cs:1096`, `SpiderHandlerTBE.cs:36` (the method CUO patches in `EnemyBitePatches`) and `TraderScript.cs:587` — none of them for a kernel-applied dismemberment. The patient's client reaches dismemberment through the kernel instead: `OtherMedicalOperationApplier.CompleteAmputation` sets `limb.Dismembered = true`, and the render path applies it by field writes (`CloneLimbRenderer.ApplyLimb`: `limb.dismembered = limbData.Dismembered; limb.gameObject.SetActive(!…)`) — so its game never calls `Dismember` and never plays the clip. |
| 5 | The remote amputation's acting client | `RemoteOtherMedicalOperationHandler.TryStartRemoteAmputationUse` builds `new AmputationMinigame(display.limbs[limbIndex])` (:178) and starts it through `MinigameBase.main.StartMinigame` — the OPERATOR's client runs the native step, so the gore played there and nowhere else. |
| 6 | The shrapnel minigame's broken grasp | `ShrapnelMinigame.BreakGrasp` (:61-71) plays `limb.body.DoGoreSound()` (:71); it is called from the minigame's own `Update` (:111 / :116), and both call sites sit behind `!this.hasTweezers` (:109). |
| 7 | The suture's delegate | `Item.cs:376-386`: the `useLimbAction` delegate's FIRST statement is `limb.body.DoGoreSound()` (:378), played synchronously when the limb action runs. |
| 8 | The blocked native path | `RemoteMedicalBlockApplyWoundItemPatch` blocks `PlayerCamera.ApplyWoundItem` while the remote medical view is open, so the suture's delegate never runs on the operator's client; `RemoteMedicalOperationHandler.TryHandleLimbUse` → `TryDispatchLimbUse` routes it (the suture is `RemoteLimbToolCatalog`'s timed tool, which is NOT in `RemoteHealProfiles`, so the dispatch falls through to `SendUseRequest`) and `PlayTreatmentSound` plays the table's row after a successful dispatch. |
| 9 | The relay's dedup facts | `CharacterSoundHandler.Handle`: the host fires the event locally and `BroadcastExcept(sender, …)`; `CharacterSoundSync.OnReceived` drops any arrival whose `OwnerSteamId` is the local player ("never double-play"). One play = one message, and the source never receives its own. |
| 10 | The shrapnel session's observer half | `RemoteShrapnelOperationHandler` runs the operator's own session AND gives every other participant an observer copy of the same `ShrapnelMinigame` (`IsActiveShrapnelMinigame` covers both; `IsObserverShrapnelMinigame` names the copy), the observer's input is suppressed by `RemoteShrapnelMinigamePatch`, and the new scope excludes that copy explicitly so one removal cannot be reported twice. |
| 11 | The display copy's parked position | `RemoteMedicalCoordinator.TryCreateDisplayBody`: `body.transform.position = new Vector3(0f, -10000f, 0f)` — "keep it out of the world and out of all simulation/collision paths". The native gore call reports the LIMB's and the BODY's own transform, so on the remote path the clip would play (and be relayed) at (0, -10000). |
| 12 | The live position the window uses | `IPlayerAnchorQuery.TryGetRemoteHeadPosition` — the house read the Online UI already uses for a remote clone's visible head; `RemoteMedicalDisplayCapture` reads it through `PatchBridge.Impl` and moves the display copy there for the step. |
| 13 | The whole gore family (what is outside this cycle) | `"gore"` also plays in `Body.Disfigure` (Body.cs:1217), `PlayerCamera.cs:189`, `BearTrap.cs:50`, `CorpseScript.cs:48`, `SpiderHandler.cs:286`, `TraderScript.cs:599`; `DoGoreSound` is reached from the limb latches (`Dislocate` :203, `BreakBone` :234, `ImpactDamage` :348), trap/hazard scripts, enemies, turrets, the tutorial courses and the trader scripts; `"gore2"` is the lockpick-failure pain (LockpingMinigame.cs:155, already carried under `Origin.LockpickPain`); `"gore3"` at SpiderHandler.cs:244 and `"gore" + Random(1,6)` at TurretScript.cs:134. None of those is a treatment path — they run on the client that simulates them, and a world-damage presentation is its own decision (recorded on the ticket). |

## 2. Decisions

1. **One carrier per path, at the site the native call runs.** The amputation's clip belongs to the
   minigame's own STEP (a table row would invent a call the item never makes), so it is captured there;
   the shrapnel break is the same shape and rides its own step scope; the suture's clip IS the blocked
   limb action's own call, which is exactly what the treatment table exists for, so it is a table row.
2. **The step scope, with the local-action guard.** Each scope enters `CharacterMedicalUse` in the
   `Update` prefix and disposes in the postfix — the `BandageMinigameSoundPatches` shape — and opens
   only when `CaptureScopeGuard.IsLocalAction()`, so a remote-driven replay (which runs under
   `RemoteApply`) can never be reported back. The shrapnel scope carries a second, explicit guard —
   "not the observer copy of the shared session" — because that copy is a local action on another
   client's screen and would otherwise report the same removal twice.
3. **The clips become medical knowledge.** `CharacterSoundPolicy.IsMedicalClip` gains
   `gore`/`gore1`…`gore5`; without that classification both scopes would stay silent however they were
   bound. `gore2` keeps its separate meaning under `Origin.LockpickPain` — two distinct, never nested
   origins, and the relay drops the source's own echo, so one play can never be reported twice.
4. **The double-play question is answered, not assumed.** The ticket expected the operator + patient to
   risk a double play; the census shows the patient's client never runs `Dismember` (row 4 above), so
   the only local copy is the acting client's own and the relay never returns it. No de-duplication
   mechanism was needed for the treatment paths.
5. **The suture names the base clip.** `medicalsuture` → `"gore"`: the body's roll would otherwise pick
   one of five variants, and the table names the clip `Limb.Dismember` plays first — deterministic, and
   the variation is native flavour rather than a second fact to carry.
6. **The wire is untouched.** The carrying rides the existing one-shot character-sound relay: no new
   `NetMsg`, no protocol bump. This is a fact of the change, not a constraint on it.

## 3. Whole-family audit

| Decision | Items / mechanisms | Native source of the clip | Gate pin |
|---|---|---|---|
| Amputation step scope | the seven amputating tools' minigame; the local amputation too | `AmputationMinigame.cs:69-94` (completion at :73-75) → `Limb.cs:91-99` | `TheGoreClips_AreCapturedInsideTheMinigameStepsThatPlayThem` (anchor + guard + disposal per file) |
| Shrapnel step scope | the bare-handed removal's broken grasp | `ShrapnelMinigame.cs:61-71/109/111/116` | same |
| The clips' classification | `gore`, `gore1`…`gore5` | `Limb.cs:98`, `Body.cs:2445` | the census row `IsMedicalClip` in the same gate + `EveryCensusRow_MatchesThePolicyClassification` |
| The suture's row | `medicalsuture` | `Item.cs:378` | the treatment-table gate (`TheRemoteTreatmentTable_DecidesEveryAcceptedMedicalItem`) + `RemoteMedicalTreatmentSoundCatalogTests` |
| What stays silent | tweezers (break gated off), the non-treatment producers | `ShrapnelMinigame.cs:109`, §1 row 13 | the table's recorded silence + the ticket's census |
| The observer guard | the shrapnel session's observer copies | §1 row 10 | the observer assertion in `TheGoreClips_AreCapturedInsideTheMinigameStepsThatPlayThem` |
| The reported position | the two step scopes on a displayed body | §1 rows 11-12 | the capture-file assertions in the same pin (the reader, the write, the restore) |
| The 2D scan surface | the three new patch files join the concatenation | — | `TheTwoDimensionalCues_OpenNoCaptureScope` |
| Capability registration | both patch classes (`RemoteMedicalDisplayCapture` is a helper, not a patch class) | — | `AdapterCapabilityCatalog` (the capability gate) |

## 4. Self-check table

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | `AmputationMinigame.Update` | new per-step capture scope (`AmputationMinigameSoundPatch`) | the anchor pin + `CaptureScopeGuard.IsLocalAction()` + the disposal pin; registered in the Character capability |
| 2 | `ShrapnelMinigame.Update` | the same scope (`ShrapnelMinigameSoundPatch`) | same pins |
| 3 | `CharacterSoundPolicy.IsMedicalClip` | `gore` + `gore1`…`gore5` join the medical set | the widened census row (`EveryCensusRow_MatchesThePolicyClassification`) |
| 4 | `RemoteMedicalTreatmentSoundCatalog` | `medicalsuture` → `"gore"`, moved out of `UncarriedItems`; the amputation group's comment records the step carrier | `RemoteMedicalTreatmentSoundCatalogTests` + the table-completeness gate |
| 5 | `AdapterCapabilityCatalog` | both new patch types join the Character capability | the capability-catalog gate |
| 6 | `RemoteMedicalDisplayCapture` | the display copy is moved onto the patient's live clone for the step and restored afterwards | the capture-file assertions + `IPlayerAnchorQuery` (the house read); the operator's own audible half is the user's dual-client pass |
| 7 | `ShrapnelMinigameSoundPatch`'s observer guard | an observer copy of the shared session reports nothing | the observer assertion in the pin |
| 8 | The gate's own surface | the gore pin, the widened census floor (33/37), the three files in the 2D scan surface | the gate's own runs (§5) |

## 5. The red and the ladder

All runs are recorded under `%TEMP%` (kept, not deleted) and are quoted with the command that produced
them.

| Step | Command | Result |
|---|---|---|
| Red (FINAL gate content, `src/` at HEAD — stashed for the run) | `git stash push -u -- src/` then `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName~ItemAndBodySoundCaptureGateTests"` | **3 failed / 26 passed / 29**, exit 1 — the new gore pin (its patch files do not exist), the widened `IsMedicalClip` census row (HEAD's policy lacks the clips) and the 2D scan-surface pin (it now reads the new patch files) (`%TEMP%/cuo-red-gore-presentation.txt`) |
| Focused (gates) | same filter, after the change | **29 passed / 0 failed** (`%TEMP%/cuo-focus-gore-presentation.txt`) |
| Focused (behaviour + gates, whole solution) | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~CharacterSound\|FullyQualifiedName~TreatmentSound\|FullyQualifiedName~SoundCaptureGateTests\|FullyQualifiedName~ItemImpact\|FullyQualifiedName~RemoteMedical\|FullyQualifiedName~Shrapnel"` | **56 passed** (gates) + **144 passed** (main suite), 0 failed, exit 0 (`%TEMP%/cuo-focused-gore-presentation.txt`) |
| Gates project | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` (checklist still open) | **287 passed / 1 failed / 288** — the one failure being this cycle's own checklist gate (7 boxes unchecked, the expected red) (`%TEMP%/cuo-gates-gore-presentation.txt`) |
| `dotnet format` | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0, and `git status --short` + `git diff --shortstat` byte-identical before and after (`%TEMP%/cuo-format-gore-presentation.txt`, `cuo-status-before-format.txt`, `cuo-status-after-format.txt`) |
| Final full suite WITH build | `dotnet test CasualtiesUnknownOnline.slnx` (UNFILTERED, checklist filled) | **4132 + 288 passed / 0 failed**, exit 0 (`%TEMP%/cuo-full-final-gore-presentation.txt`), recorded on the delivery checklist's build line in the same commit |
| Review-fix re-verification | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~ItemAndBodySoundCaptureGateTests\|FullyQualifiedName~SourceShapeGateTests\|FullyQualifiedName~AdapterCapabilityCatalogTests\|FullyQualifiedName~CharacterSoundPatchTests\|FullyQualifiedName~TreatmentSound"` after the review's findings were fixed | **93 passed** (gates) + **63 passed** (main suite), 0 failed, exit 0 (`%TEMP%/cuo-focus2-gore-presentation.txt`) — this run is what caught the architecture gate's 600-line ceiling on `RemoteMedicalOperationHandler` (601 after the first position fix; the fix moved the lookup out of it, back to 581) |

Counting discipline: a `--filter`ed number is never quoted as a full-suite number — the excluded tests
are named by their filter, and the unfiltered total is stated separately.

## 6. Verification table

| What | Evidence |
|---|---|
| The pins fail before the change and pass after it | §5 red row and the focused rows |
| Behaviour: the suture's new row and the amputating tools' recorded silence | `RemoteMedicalTreatmentSoundCatalogTests` |
| Behaviour: the classification the two scopes depend on | the census row pin (`EveryCensusRow_MatchesThePolicyClassification`) |
| Contract: the new anchors resolve in the game assembly | the game-assembly patch-contract tests (walk every `[HarmonyPatch]` contract) |
| Contract: the position window's reader is the house anchor query | `IPlayerAnchorQuery` is the interface the Online UI already uses for a remote clone's live head position, so the window reads a proven source rather than a new one — pinned by the capture-file assertions |
| Class-size ceiling after the change | `RemoteMedicalOperationHandler` back to 581 lines (`SourceShapeGateTests`, 600 max) — the first position fix pushed it to 601 and the gate caught it |
| The whole suite | §5 final full-suite row |

## 7. Review disposition

Independent adversarial review, fresh context, frozen tree, no write access (the full text is
`%TEMP%/cuo-review-treatment-gore-presentation.md`: 2 majors, 5 minors, 4 nits, 1 process observation).
Every finding is dispositioned here, and every fix is in this same cycle.

| Finding | Severity | Disposition |
|---|---|---|
| M1 — the remote path reports an OFF-WORLD position: the display body is parked at (0, -10000) and nothing moves it, so the operator's own 3D play and every peer's replay were off the map | major | FIXED: `RemoteMedicalDisplayCapture` moves the displayed body onto the patient's live clone for the step and restores the parked position afterwards; the ticket's decision table, §3/§4 and the acceptance rows now carry the position half, and the gate pins the capture's own contract (the reader, the write, the restore). This is the finding that decided whether the remote half works at all — no source-reading pin could see it. |
| M2 — "`Limb.Dismember` has exactly one caller in the whole tree" is false: four callers (`AmputationMinigame.cs:75`, `ConsoleScript.cs:1096`, `SpiderHandlerTBE.cs:36`, `TraderScript.cs:587`) | major | FIXED (claim): all four are named in the ticket and §1, and they are re-read as what they are — the patient's client reaches dismemberment through the kernel, not through any of them. The conclusion survives; the completeness claim does not. |
| m3 — `AmputationMinigame.cs:69-85` is stale (`Update` spans `:69-94`) | minor | FIXED: every reference moved to `:69-94`. |
| m4 — the tweezers "second clip `gore{N}`" claim was left standing one clause away from the new "`:109` keeps the break path off the tweezers" | minor | FIXED: the suppressed-call ticket's census row and acceptance row, this ticket and the earlier self-check now record that the tweezers play NOTHING else (both break conditions sit behind `!this.hasTweezers`). |
| m5 — acceptance row 1 said peers hear "gore once", while a dismemberment carries TWO clips (`gore` from the limb + one of `gore1..5` from the body) | minor | FIXED: the row now names the clip pair and says no third play, so an auditor can tell design from double-play. |
| m6 — the "whole gore family" census was presented as exhaustive but listed only part of it | minor | FIXED: split into treatment-adjacent producers (the cycle's scope) and non-treatment producers, with the `DoGoreSound` call sites enumerated and CUO's own `AnimalDeathReplay` / `TrapStateActions` gore replays named. |
| m7 — `SpiderHandlerTBE.cs:36` (the method CUO patches in `EnemyBitePatches`) and `TraderScript.cs:587` can dismember a player's own limb outside both scopes and were unnamed | minor | RECORDED: named in the census as the same defect class, explicitly not carried here — carrying a world-damage presentation is its own cell, the way the world-item impact was. |
| n8 — §1 row 8 routed the suture to `SendHealRequest` | nit | FIXED: `RemoteHealProfiles` does not accept `medicalsuture`, so `TryDispatchLimbUse` falls through to `SendUseRequest`; the conclusion (played after a successful dispatch) is unaffected and is now stated with the right branch. |
| n9 — inconsistent self-reference path style in this file | nit | FIXED: the ticket path is written the same way everywhere. |
| n10 — the pre-existing wrong method name `LocalUseItemLimbUseItem` in a file this change edited | nit | FIXED: the base self-check now names `LocalUseItemEligibility.IsMedicalLimbUseItem`. |
| n11 — process: `docs/evidence/selfchecks/MANIFEST.md` was edited a minute after the review started, so the workspace moved during the review | nit | ACKNOWLEDGED: the row's own correctness was re-checked after the edit (`SelfcheckManifestGateTests` 7/7 green), and the review's other conclusions were drawn from the unchanged code paths. The freeze discipline slipped once and is recorded here rather than hidden. |

What the review could NOT falsify (its own words): the carrier claim itself (the relay chain end to end,
both echo guards, no wire or protocol change); the patient-side silence; no double play in any of the six
scenarios; the scope windows, their exception safety and the absence of a Harmony ordering hazard; the
classification side effects (only `Origin.Medical` can newly carry gore); the table's invariants; the
checklist reset's item-only diff; every cited `reversing/` line except the two it corrected; and it
re-measured the change's own numbers (gates 29/29, focused 56 + 144, catalog 33/33, build 0 warnings).

## 8. Limits

- **The audible result is not proven here.** No hearing: the test host cannot play Unity audio, and the
  relay's routing is what is machine-checked. Whether a peer hears the gore at the right place, once, and
  whether the patient hears their own amputation is the user's dual-client run.
- **The position window's result is a source-level claim.** The window moves the displayed body onto the
  patient's live clone and restores it; that the clip is then heard at the patient on the OPERATOR's own
  machine, and replayed next to the patient's clone by every peer, is what the user's dual-client run
  shows. The pins can see the mechanism, not the sound.
- **The observer half of the shrapnel session is argued from the mechanism** and now guarded explicitly.
  An observer client runs its own `ShrapnelMinigame` copy with its input suppressed; a break needs the
  LOCAL hand's velocity (`handVelocity` > 2.2) or a > 42px lateral pull. A dual-client shrapnel session
  is the run that confirms it.
- **The acting-client claim for the remote path rests on the minigame running on the operator's client**
  (`RemoteOtherMedicalOperationHandler` builds and starts it there) and on the patient's client not
  reaching any of `Dismember`'s four callers for a kernel-applied dismemberment. A runtime trace of a
  dual-client amputation would make it an observation rather than a source reading.
- **The non-treatment gore producers stay uncarried by construction** (§1 row 10): they run on the client
  that simulates them, and carrying a world-damage presentation is its own decision — recorded on the
  ticket rather than half-built here. Two of them can dismember a player's own limb and are named there
  as the same defect class.
- **`"gore"` is also a valid clip name outside these steps**: the classification is by clip NAME inside a
  scope, so any future scope that wraps one of those producers would start carrying them. The ticket's
  census names them so the next cycle sees what it is turning on.
- **No deployment this cycle**: a runtime behaviour change, so the deployed artifact stays at the previous
  build until the user's release cycle.
