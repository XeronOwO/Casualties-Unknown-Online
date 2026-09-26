# The treatment family's gore presentation is carried

- Status: Review
- Priority: Low
- Category: Audio sync / report coverage
- Source: the independent adversarial review of the `review/suppressed-native-call-sounds-stay-unheard.md` cycle (2026-09-26) — its census read the delegates of the accepted medical items but not what those delegates CALL, so two groups were recorded as "natively silent" although they play a 3D gore clip.
- Related: `review/suppressed-native-call-sounds-stay-unheard.md` (the cycle that censused the accepted surface and recorded these rows as uncarried), `review/unhooked-item-and-body-sound-families.md`, `docs/evidence/selfchecks/presentation/treatment-gore-presentation-carried-selfcheck.md`

## The gap

Two groups of the accepted remote-limb-treatment surface play a 3D clip that does NOT come from the
item's own limb action, so the treatment table (which mirrors those actions) carried nothing for them:

| Group | What plays | Where it is played |
|---|---|---|
| `medicalsuture` | `gore{1..5}` | the delegate's first statement calls `limb.body.DoGoreSound()` (Item.cs:378 → Body.cs:2443-2446: `Sound.Play(string.Format("gore{0}", Random.Range(1, 6)), base.transform.position, false, true, …)`) |
| the seven amputating tools (`machete`, `titaniummachete`, `crudecleaver`, `sickle`, `claws`, `titaniummultitool`, `flimsyknife`) | `gore` + `gore{N}` | the delegate starts the native `AmputationMinigame`; its completion calls `Limb.Dismember()` (AmputationMinigame.cs:69-94), which plays `Sound.Play("gore", …)` and then `body.DoGoreSound()` (Limb.cs:91-99) |
| (adjacent, same shape) `tweezers` | `tweezeruse` only | the carried `tweezeruse` row is correct, and the tweezers play NOTHING else: the minigame's gore roll sits behind `!this.hasTweezers` (ShrapnelMinigame.cs:109) on both break conditions (:111/:116) |

## Decision (2026-09-26 follow-up cycle, user ruling)

**Carried.** The user chose "carry it" over "record it as deliberately silent": a player whose limb is
taken off should hear it, and a peer standing next to the operation should too. One carrier per row,
each at the site the native call actually runs:

| Row | Carrier | Why that carrier |
|---|---|---|
| the amputation's `gore` + `gore{N}` | `AmputationMinigameSoundPatch` — a per-step capture scope around `AmputationMinigame.Update`, plus the gore clips in `CharacterSoundPolicy.IsMedicalClip` | the clip is played by the minigame's own STEP (`Dismember` from `Update`), frames after the limb action returned. A table row would invent a call the item never makes. The step runs on the ACTING client on both paths (the local one, where the operator is the patient, and the remote one, where the operator's minigame drives the displayed body), so one scope carries both. |
| the shrapnel removal's `gore{N}` | `ShrapnelMinigameSoundPatch` — the same step shape around `ShrapnelMinigame.Update`, with an extra observer guard | `BreakGrasp` is called from that step (`:111`/`:116`) and plays the body's roll (`:61-71`). A shared remote session gives every OBSERVER a copy of the same minigame, so the scope carries an explicit "not the observer copy" guard on top of the local-action guard — the operator already reported the removal. Only the bare-handed path can break a grasp (`!this.hasTweezers`, `:109`), so the tweezers stay silent. |
| `medicalsuture`'s `gore` | a row of `RemoteMedicalTreatmentSoundCatalog` (`medicalsuture` → `"gore"`) | its delegate plays the clip SYNCHRONOUSLY (`DoGoreSound` is the delegate's first statement), so the blocked native call is exactly the silenced site the treatment table exists for. The body's roll picks one of five variants; the table names the base clip `Limb.Dismember` plays first (`Limb.cs:98`). |
| the POSITION both step scopes report | `RemoteMedicalDisplayCapture` — the window moves the displayed body onto the patient's live render clone for the step and restores the parked position | the display copy is parked out of the world (`RemoteMedicalCoordinator.TryCreateDisplayBody`: `body.transform.position = new Vector3(0f, -10000f, 0f)`), and the native gore call reports the LIMB's and the BODY's own transform — without this the operator's own 3D play would be off the map and every peer would replay it at (0, -10000). It is the re-point `PlayTreatmentSound` already makes for the treatment table, applied to the step scopes. |

## Census (decompiled tree; `reversing/` line numbers are stable)

**Who plays what, on which client — the double-play question, answered with the mechanism.**

| Path | The clip plays on | The peers | Double-play? |
|---|---|---|---|
| LOCAL amputation (the operating player's own limb) | the acting client (`AmputationMinigame.Update` → `limb.Dismember()`) | were silent; now hear the stepping scope's report once | no — the report goes to the star relay, which never returns a message to its own source (`CharacterSoundSync.OnReceived`: an arrival carrying the local `OwnerSteamId` is dropped) |
| REMOTE amputation (the operator's client drives the displayed body) | the OPERATOR's client only (`AmputationMinigame.Update` → `Dismember()` on the displayed body's limb) | were silent; now hear it once, at the patient's live position | no — and the PATIENT's client never plays it: `OtherMedicalOperationApplier.CompleteAmputation` writes `limb.Dismembered = true` in the kernel and the projection/render path applies it by field writes (`CloneLimbRenderer.ApplyLimb`, `limb.gameObject.SetActive(false)`), so the game's `Dismember` is not called there. `Limb.Dismember` has four callers in the tree — `AmputationMinigame.cs:75`, `ConsoleScript.cs:1096`, `SpiderHandlerTBE.cs:36`, `TraderScript.cs:587` — and none of the four runs for a kernel-applied dismemberment. |
| REMOTE suture | no client (the delegate is blocked by `RemoteMedicalBlockApplyWoundItemPatch`) | were silent; the operator's dispatch now plays the table's row and the relay carries it | no — one report, no local copy |
| LOCAL suture | the acting client (the delegate's own `DoGoreSound` inside `PlayerCamera.ApplyWoundItem`, which the medical scope already wraps) | were silent before this cycle; the clip now classifies as medical, so the same scope reports it | no — one report per play |
| shrapnel removal, bare-handed break | the acting client (`ShrapnelMinigame.Update` → `BreakGrasp` → `DoGoreSound`) | were silent; now the step scope reports it | no — an observer's copy of the same minigame is excluded by its own guard, and a break needs the LOCAL hand's velocity/misalignment (`handVelocity` > 2.2 or a > 42px lateral pull) |
| tweezers removal | nobody (`!this.hasTweezers` gates both break conditions, ShrapnelMinigame.cs:109) | nothing to hear | n/a — recorded as deliberately silent |

**The gore producers, in two groups (what this cycle binds, and what stays outside it).**

*Treatment-adjacent (the cycle's scope).* `Limb.Dismember` (Limb.cs:91-99), reached by the amputation
minigame's completion, and `Body.DoGoreSound` (Body.cs:2443-2446) reached by `Limb.Dismember`, by the
shrapnel minigame's broken grasp and by the suture item's own delegate (Item.cs:378).

*Non-treatment producers (outside this cycle, listed so the family is not claimed to be closed).* The
`gore` clip also plays at `Body.Disfigure` (Body.cs:1217), `PlayerCamera.cs:189`, `BearTrap.cs:50`,
`CorpseScript.cs:48`, `SpiderHandler.cs:286` and `TraderScript.cs:599`; `DoGoreSound` has 22 call sites
in the tree (the limb latches `Dislocate` Limb.cs:203 / `BreakBone` :234 / `ImpactDamage` :348; the trap
and hazard scripts `CactusScript.cs:19`, `GroundGlass.cs:36`, `SawbladeScript.cs:50`,
`RecipeResult.cs:20`, `SpikeStabberScript.cs`, `CrystalEnemy.cs:153`, `TraderScript.cs:255`,
`TurretScript.cs:165`; the tutorial courses `BasicCourse.cs:254` / `EscapeCourse.cs:38` /
`FirstAidCourse.cs`); `"gore2"` is the lockpick-failure pain (LockpingMinigame.cs:155, already carried
under `Origin.LockpickPain`); `"gore3"` at `SpiderHandler.cs:244` and `"gore" + Random(1,6)` at
`TurretScript.cs:134`. Every one of those runs on the client that simulates it. Two of the dismemberment
callers above — `SpiderHandlerTBE.cs:36` (the method CUO patches in `EnemyBitePatches`) and
`TraderScript.cs:587` — can dismember a PLAYER's own limb outside both new scopes; that is the same
defect class as this ticket, named here rather than silently claimed closed. Carrying a world-damage
presentation is its own cell, the way the world-item impact was.

## What done looks like

1. The gore rows are carried by a decided mechanism — done: two step scopes (with the position window)
   and one table row (above).
2. The double-play question (operator + patient) is answered with the census, not assumed — done: the
   table above, anchored on `OtherMedicalOperationApplier.CompleteAmputation`, the four
   `Limb.Dismember` callers and `CharacterSoundHandler.BroadcastExcept` / `CharacterSoundSync`'s echo drop.
3. The treatment table's `UncarriedItems` comments and the self-check's census cite this ticket's
   outcome instead of "carrying it is its own decision" — done in this same change.

## Acceptance criteria

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | A remote amputation finishes on another player's limb | the operator, the patient and every third peer hear the dismemberment's clip pair — the limb's `gore` AND one of the body's `gore1..5` — once each, at the patient's live position (the limb for `gore`, the body for the roll); no third play on any client | the step-scope pin + the display-capture pin + the medical-set pin + the echo-drop path; the audible half is the user's dual-client pass |
| 2 | A remote bare-handed shrapnel removal breaks its grasp | every peer hears the body's `gore{N}` roll once, at the patient's live position; an observer's own copy reports nothing | the shrapnel step pin + the observer guard; the audible half is the user's dual-client pass |
| 3 | A remote suture | every peer hears the suture's `gore` clip once, at the treated limb | the table row's pins (`RemoteMedicalTreatmentSoundCatalogTests`, the census gate) |
| 4 | A LOCAL amputation or suture | the peers now hear the clip too (they never did) | the same two pins — the step and the scope are the acting client's, not the view's |
| 5 | Tweezers removal, and any removal whose grasp never breaks | nothing is carried (the native path plays nothing) | the `!hasTweezers` gate + the table's recorded silence |
| 6 | A replayed gore sound on the receiving client | never reported back (the replay runs under `RemoteApply`) | `CaptureScopeGuard.IsLocalAction()` in both scopes + the existing replay tests |
| 7 | Solo / no active session | nothing is reported, local presentation unchanged | the session-active guard of the relay (`CharacterSoundSync.Report`) |
| 8 | The patient's render clone does not exist yet when the step runs | the window leaves the display body parked; the report carries the copy's own position, which is the best fact available | `RemoteMedicalDisplayCapture.Enter`'s clone check |

## What landed (2026-09-26 follow-up cycle)

- **The amputation step is captured** (`AmputationMinigameSoundPatch`): `AmputationMinigame.Update`
  enters the medical capture scope per step, so `Limb.Dismember`'s `gore` + `gore{N}` reach every peer
  for the local AND the remote amputation — the remote one is where it mattered, since only the operator
  heard it before.
- **The shrapnel step is captured** (`ShrapnelMinigameSoundPatch`): a broken grasp's `gore{N}` now
  reaches every peer; the tweezers path is recorded as reaching no clip at all, and an observer copy of
  the shared session is explicitly excluded from reporting.
- **The reported position is the patient's live one** (`RemoteMedicalDisplayCapture`): the two step
  scopes move the displayed body off its parked off-world position onto the patient's render clone for
  the step and restore it afterwards. Without this the remote half would be inaudible on every client.
- **The gore clips join the medical set** (`CharacterSoundPolicy.IsMedicalClip`): without the
  classification both scopes would stay silent however they are bound. `gore2` keeps its separate
  meaning under `Origin.LockpickPain`; the two origins are distinct, non-nested scopes.
- **The suture's blocked clip is a table row** (`RemoteMedicalTreatmentSoundCatalog`: `medicalsuture`
  → `"gore"`), which moves the item out of `UncarriedItems` — it was only there because its clip came
  from the limb path rather than the item's action.
- **The wire is untouched**: this is the character-sound relay's existing one-shot carrier, so no new
  `NetMsg` and no protocol bump (a fact of the change, not a constraint).

## Evidence

- Self-check: `docs/evidence/selfchecks/presentation/treatment-gore-presentation-carried-selfcheck.md`
  (census, decisions, the ladder, the review disposition and the limits)
- Gate: `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ItemAndBodySoundCaptureGateTests.cs` — the
  `TheGoreClips_AreCapturedInsideTheMinigameStepsThatPlayThem` pin (anchors, guards, the display-capture
  re-point, the observer guard), the widened `IsMedicalClip` census row, the new files in the 2D-source
  scan surface; red 3/26/29 against the pre-change `src/`, green after it
- Behaviour: `RemoteMedicalTreatmentSoundCatalogTests` (the suture's new row, the amputating tools'
  unchanged recorded silence)
- Native: `Limb.cs:91-99/98/203/234/348`, `Body.cs:1217/2443-2446`, `AmputationMinigame.cs:69-94`,
  `Limb.cs:75`'s callers (`AmputationMinigame.cs:75`, `ConsoleScript.cs:1096`, `SpiderHandlerTBE.cs:36`,
  `TraderScript.cs:587`), `ShrapnelMinigame.cs:61-71/109/111/116`, `Item.cs:378`,
  `LockpingMinigame.cs:155`, `TurretScript.cs:134/165`, `BearTrap.cs:50`, `CorpseScript.cs:48`,
  `SpiderHandler.cs:244/286`, `TraderScript.cs:599`, `CactusScript.cs:19`, `GroundGlass.cs:36`,
  `SawbladeScript.cs:50`, `RecipeResult.cs:20`, `CrystalEnemy.cs:153`, `BasicCourse.cs:254`,
  `EscapeCourse.cs:38`
- Review: the independent adversarial review of this cycle (2 majors, 5 minors, 4 nits — every finding
  dispositioned in the self-check's §7)

## Limits

- The audible half is the user's dual-client run: whether a peer hears the gore at the right place, once,
  and whether the patient hears their own amputation.
- The observer half of the shrapnel session is argued from the mechanism (their input is suppressed; a
  break needs the local hand's velocity) and is now also guarded explicitly — a dual-client shrapnel
  session is the run that confirms it.
- The position window is a display-copy presentation move: it runs only for a body named
  `MedicalDisplay_*` whose owner's clone exists, and it restores the parked position when the step
  returns.
- The gore a non-treatment producer plays (traps, hazards, enemies, corpses, the tutorial) is outside
  both scopes by construction and stays uncarried, except where the producer calls `Limb.Dismember` on a
  player's limb — named in the census as the same defect class, not carried here.
- The treatment table names the base `gore` for the suture, not one of the body's five rolled variants;
  the variants are classified (so any scope that plays one is carried) but the table's own row stays
  deterministic.

## Non-goals

- Not re-deriving the accepted surface or the item-action rows: the cycle that opened this ticket
  censused them item by item and the review re-derived that census independently.
- Not a general "every gore becomes a message" sweep: the non-treatment producers are listed above as
  out of family, deliberately.
