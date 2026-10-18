# Health on every client, reconciled to the host's value

- Status: Todo — **direction ruled by the user on 2026-10-18: the HOST computes the continuous
  player state by reusing the game's own methods; the affected client judges every event and
  applies the host's rows to its own body.** This supersedes the (a)-first recommendation the
  earlier draft carried. Stage 1 (the bleeding family) landed and was superseded by Stage 2 unit 1
  (the host clone simulation plus item 4's event/quarantine/reconciliation boundary), which lands
  in this cycle's commit; the two-client runtime acceptance of both is still owed, and Stage 2's
  missing-input (item 2) and frame-cost (item 5) items are the rest.
- Priority: Medium-High
- Category: Player state / cross-player sync / authority
- Source: the user's 2026-10-14 request (health simulated on every client, reconciled to the host)
  and the 2026-10-18 authority question ("can the authoritative value move to the host, with the
  client presenting/predicting and a rollback on divergence?"), answered this cycle: yes, the host
  computes, and it reuses the game's own code rather than a reimplementation.
- Related: `done/validation-and-source-checks.md`, `future/generic-prediction-runtime.md` (the
  determinism prerequisite for validating INPUTS, not for computing outputs), `docs/decisions/active.md`
  184 (judgment ownership — still binding for events and terminal facts), 265 (data-anchored
  validation — this ticket makes the host an anchor of its own computation),
  `review/cheat-permission-and-op-commands.md`, `docs/development/agent-reference.md`,
  `docs/architecture/current.md` §7.1, `done/item-data-write-commands.md`

## The ruling (2026-10-18)

1. **The host computes a player's continuous values.** The player's own client is a presenter: it
   applies the host's rows to its own body, so a local edit between two rows cannot survive.
2. **Events and terminal facts stay with the affected client.** Injury, medical actions, limb
   latches, death, unconsciousness, infection and every random roll's OUTCOME are that client's to
   judge and report. The host never re-derives one, and its model never produces a state
   transition (decision 184 still owns this half).
3. **Reuse, do not reimplement.** The host runs the game's own vital stages on its clone of each
   player's body: `HandleVariableUpdates`, `HandleBody`, `HandleBodyTemperature`,
   `HandleRadiationSickness` and `HandlePeriodicChecks`, plus `Limb.Update`. The mechanism already
   exists for a dead/unconscious carried body (`BodyUpdatePatch.RunVitalsSubset`, resolved with
   `AccessTools.Method`); this ticket points it at the host's per-player clones.
4. **Random outcomes are events.** The host must not roll for another player's body: that body's
   own client already rolls (`HandlePeriodicChecks`'s `Random.value`, `Limb.Update`'s infection
   roll), and a second roll would disagree. Where a roll's outcome matters, the client reports it
   as an event, which is where it already belongs.
5. **The client's 1 Hz report becomes the reconciliation, not the source.** The host compares its
   computed values against the client's and logs a named divergence when they part. That
   comparison is the ONLY way a wrong host model becomes visible; without it a bad model is
   silent.

## What "reuse" still costs (the three named costs)

1. **Side effects.** Those stages are written for a local body: they call `PlayerCamera.main`
   (the host's own camera — irradiate intensity, threat music), play sounds
   (`PantSound.TryGrowl`), spawn vomit (`vomiter.Vomit`), speak (`talker.Talk`), start coroutines
   (`Cry`) and ragdoll the body. On the host's clone of someone else's body those fire on the
   host's machine. Each needs suppression on a proxy body — the same family as the carried-body
   patches, which is why they are not free.
2. **Inputs.** The host clone carries standing, crouching, sleeping and climbing (the 20 Hz entity
   stream); it does NOT carry in-water, movement/exercise intent or the current action, and its
   `moveDir` is zeroed every frame. Those must ride the stream or the temperature/stamina pass
   computes wrong numbers.
3. **Determinism inside the host.** The advance must use one dt accounting and one clamp order.
   Two hosts replaying the same events must land on the same numbers; otherwise the
   reconciliation fights its own model.

## What the client keeps (do not move these to the host)

- Every event it judges today: bites it reports, limb latches, medical actions, the effects of its
  own item uses, falls.
- Terminal state: alive, conscious, limb flags, infection, fibrillation, and anything a death or
  unconsciousness transition follows from.
- The roll itself, and therefore the outcome.

## Wire

- **New: `NetMsg.PlayerVitals` = 154** (host → client, 10 Hz, unreliable, self-healing on the next
  row). It carries ONLY the host-owned values: body blood volume, body bleeding speed, and the
  per-limb bleed amounts. Nothing terminal rides it.
- **Stage 2 unit 1 addition:** the 1 Hz `CharacterData` health block now carries
  `TotalBleedSpeed` (`[ProtoMember(81)]`) as the third host-owned value, because the stored
  snapshot is the host clone's mirror and the 10 Hz row is built from it; a report's copy is
  preserved from the host's, exactly like blood volume and per-limb bleed.
- **Stage 2 remaining additions:** the missing input flags on the 20 Hz entity stream (in-water,
  moving, exercising, current action), and the reconciliation log over the report — the comparison
  itself is stage 2 item 4.

## Stage 1 — the bleeding family (landed, runtime acceptance owed)

Owned values: per-limb `bleedAmount` and `bloodVolume` (+ derived `totalBleedSpeed`). Inputs, all
already on the existing snapshot: blood viscosity, venom, hunger, internal bleeding, and per limb
the skin health, tourniquet flag, shrapnel count and bandage amount. World settings:
`healingrate`, `bleedrate` (read by the adapter through `IPlayerVitalsTuning`, so a widened run is
honoured). No new inputs, no randomness, no terminal output.

Files:

- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/PlayerBleedingState.cs` — the model,
  one rule per native call site, with the citations in its doc comment.
- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/PlayerVitalsService.cs` — host state
  per player, 10 Hz advance and send; re-seeds when the host's own snapshot stops carrying the
  model's values (which only an event the host arbitrated can do).
- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/ContinuousVitals.cs` — keeps a plain
  report from writing the two owned fields (`CharacterDataStore.SaveReportedCharacterData`).
- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/IPlayerVitalsTuning.cs`,
  `DefaultPlayerVitalsTuning.cs` — the run-setting port and the container's unmodified-run default.
- `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/PlayerVitalsMsg.cs`,
  `PlayerLimbBleedMsg.cs`, `NetMsg.PlayerVitals = 154`.
- `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/PlayerVitalsHandler.cs` — host → client,
  with the sender check every host-addressed instruction shares.
- `src/CasualtiesUnknownOnline.GameAdapter/Character/LocalVitalsApplier.cs` — applies a row to the
  local body only (its own row; another player's is read from the snapshot).
- `src/CasualtiesUnknownOnline.GameAdapter/GamePlayerVitalsTuning.cs` — the live run settings.
- Edited: `CharacterDataStore.cs` + `ICharacterDataControl.cs` (the report/authority split and the
  cleared-table signal), `CharacterDataHandler.cs` (a report goes through the report path),
  `CharacterDataSync.cs` (the local-body hand-over), `EntityReplicationComposition.cs` and
  `GameAdapterComposition.cs` (the service and the real tuning).
- `tests/CasualtiesUnknownOnline.Tests/Character/PlayerBleedingStateTests.cs` — 18 rows.
- `tests/CasualtiesUnknownOnline.Tests/Character/PlayerVitalsServiceTests.cs` — 11 rows (cadence,
  reliability, stall cap, authority re-seed, role/world gates, session/table reset, non-finite).
- `tests/CasualtiesUnknownOnline.Tests/Character/PlayerVitalsOwnershipTests.cs` — 4 rows against
  the real store and the real revive/amputation writers.
- `tests/CasualtiesUnknownOnline.Tests/Session/PlayerVitalsForgeryTests.cs` — 1 row (a non-host
  sender is dropped by name; the host's row still lands).
- Doc/gate edits: the sync-coverage matrix P3 row + `NetMsg.PlayerVitals` vocabulary index entry +
  two evidence anchors, the protocol message table (both blocks, id 154), the protocol page's
  standalone-unreliable-message note, the member-report census's `CharacterData` row,
  `HostToGuestDirectionTests`, `CuoServiceOrderTests`, the test fake.

**Retired by Stage 2 unit 1:** `PlayerBleedingState.cs`, `IPlayerVitalsTuning.cs`,
`DefaultPlayerVitalsTuning.cs`, `GamePlayerVitalsTuning.cs` and `PlayerBleedingStateTests.cs`. The
ruling's "reuse, do not reimplement" replaced the reimplemented bleeding rules with the game's own
stages; the wire, the report/authority split, `ContinuousVitals`, the row handler, the local
applier and the service's cadence moved to the new design, and the service's own stage-1 tests were
rewritten for the snapshot-reading pump. The review findings below are kept as the record of why
the seam is shaped this way.

### Review findings fixed before the stage-1 commit

1. `PlayerBleedingState.TotalBleedSpeed` summed the raw limb bleed and missed
   `Body.bleedingSpeedMultiplier`, the factor native `Body.HandleBody` applies through
   `Limb.bleedSpeedMult` (`Body.cs:2807`): a 10-unit wound read 10 l/m instead of 0.1. Fixed and
   pinned by two exact-value rows.
2. `PlayerVitalsService` measured its step from the PREVIOUS FRAME while only advancing on every
   tenth update: the drain ran at roughly a sixth of the game's speed. The step now comes from the
   previous advance, and `PlayerVitalsServiceTests` pumps 60 fps of virtual time to pin it.
3. The row was sent RELIABLE although the design says unreliable, 10 Hz, self-healing on the next
   row (`PacketSender.Send` defaults to reliable). Now `reliable: false`, pinned by a test whose
   link drops every unreliable frame.
4. `PlayerVitalsMsg.TotalBleedSpeed` had no receiver; `LocalVitalsApplier` now writes it onto
   `Body.totalBleedSpeed` (the value the WoundView l/m line and the moodles read).
5. The service's first divergence logger compared the SAVED snapshot against the model, but
   `ContinuousVitals.Preserve` had already overwritten the report's owned values with the host's
   own from the previous tick, so it reported the model's advance as a client divergence. Removed:
   the real reconciliation needs the raw report captured before `Preserve` and a tolerance, which
   is Stage 2 item 4, not a half-correct logger in Stage 1.
6. `GamePlayerVitalsTuning` turned a deliberate `0` run setting into `1`; it now falls back only
   when the world (and therefore the settings table) does not exist yet.
7. The protocol message table had no row for 154; added in both language blocks.

Built green; the five gates that were red at hand-off (vocabulary index, P3 row budget, 600-line
aggregate, message direction, service order) are green; 34 focused model/pump/ownership/forgery
tests green; the full suite and the gates are green at the commit that carries this ticket. Not yet
verified: the two-client acceptance rows below, and the frame-cost measurement (Stage 2 item 5).

### Independent adversarial review (2026-10-18, frozen tree)

A fresh-context reviewer read the whole diff, the native call sites and the test bodies; its report
drove the fixes below, all in the same commit.

- **B1 (blocker) — the ownership filter sat on the wrong method, so every clone-and-save event was
  reverted.** `ContinuousVitals.Preserve` ran inside `CharacterDataStore.SaveCharacterData`, i.e. on
  EVERY save, while the medical/revive appliers clone the stored snapshot and save the clone:
  `OtherMedicalOperationApplier.CompleteAmputation` writes `limb.BleedAmount = 0f` / halves the
  connected limbs (lines 138/164/179) and saves; `ShrapnelSessionStateWriter.ApplyBreakGrasp` adds
  the stump bleed and saves; `TraderRecruitPolicy.PrepareRevive` builds a fresh health block with
  `BloodVolume = 100f` (line 116) and `RespawnCoordinator` (line 131) / `TraderRecruitCoordinator`
  (lines 229/246-249) save it — the recruit path mutates the very object whose health the result
  message then sends. Only the in-place appliers (bite, limb latch) survived. Red observed with the
  real store BEFORE the fix: `AReviveSave_KeepsTheRevivedBloodVolume` expected 100, got 0;
  `AnAmputationSave_KeepsTheZeroedLimbBleed` expected 0, got 4. Fix: the filter lives on the REPORT
  path only — `ICharacterDataControl.SaveReportedCharacterData`, called by `CharacterDataHandler`
  for a member's 1 Hz report, preserves the owned values; `SaveCharacterData` stays the authority
  path every event, revive and restore writes through. Pinned by `PlayerVitalsOwnershipTests`
  against the real store and the real policies.
- **M1 (major) — the row handler had no host-sender check.** `PlayerVitalsHandler` fired the event
  for any sender; the wire direction check proves only the local ROLE. A guest-addressed frame from
  a third peer could set that guest's blood. Fixed: the handler requires
  `sender == ctx.Session.HostSteamId` and logs the refusal by name;
  `PlayerVitalsForgeryTests` proves a forged frame is dropped while the host's own row still lands.
- **M2 (major) — a non-finite report could abort the pump for every player.** `Math.Sign(float.NaN)`
  throws, and every report field is unvalidated. Fixed in two layers: the model adopts a reported
  number only when finite (`FiniteOr`), and the service refuses a non-finite snapshot by name once
  per player (`FirstNonFinite`/`AcceptReport`), keeps its own values and still sends them. Two tests
  pin the layers.
- **M3 (major) — the model advanced players outside the world.** A member in the menu has no native
  `Body`/`Limb` update, so a wall-clock advance was a CUO invention. Fixed: the pump requires the
  host's own `LocalInWorld` and the member's `InWorld`; two service rows pin it.
- **M4 (major) — the modelled state outlived its session and its run.** The service declared
  `ISessionReset` but never subscribed `SessionEnded`, and `ClearSavedCharacters()` (new run / world
  restore) did not reach it, so a stale state could survive and then ADOPT the next plain report.
  Fixed: the service subscribes both (`SessionEnded`, and the store's new `SavedCharactersCleared`)
  and drops every state; two service rows and one real-store row pin it.
- **M5 (major) — the absence of reconciliation was not stated where the ticket claimed it.** The
  wire and stage-1 sections now say plainly that stage 1 computes and pushes but compares nothing;
  acceptance rows 4/5/7 need the stage-2 comparison.

Minors fixed in the same commit: the native intra-update order (limb drain before decay;
regeneration/clamp before the internal/venom drains); the venom drain runs only while positive (a
negative report must not add blood); the model doc states that inputs are sampled at 1 Hz rather
than re-running their own decay; `Limb.Dismembered` is in the input list; a limb index the previous
snapshot did not carry has its reported bleed zeroed rather than kept; the send sentinel is
`long.MinValue` (a signed `Environment.TickCount` can be negative after ~24.85 days of uptime); the
protocol and census pages state the new member's loss/repair and the input-driving consequence.
Recorded, not fixed: the family-wide signed-clock sentinel (`CharacterDataSync` has the same shape,
pre-existing); the overstatement that a row's `totalBleedSpeed` survives the body's own per-frame
recompute; the skipped advance on a re-seed tick (~100 ms); and the first-snapshot limb shape (the
snapshot is a full body list, so a partial one would cap the model's array — self-only today).

## Stage 2 — the whole chain (design)

1. **Run the game's stages on the host clones.** `RunVitalsSubset`'s five methods plus
   `Limb.Update`, per remote player, in the game's order, with one dt accounting (cap the step so
   a stalled frame cannot teleport a value).

2. **Feed the missing inputs** from the 20 Hz entity stream: in-water, moving/exercising, current
   action. Standing/crouching/sleeping/climbing already ride it.
3. **Suppress the side effects** on a proxy body: camera, sounds, vomit, talk, coroutines, ragdoll,
   and physics re-enable. Every suppression is a patch with its own contract test, the way the
   carried-body family is pinned.
4. **Report, then reconcile.** Random outcomes arrive as events from the affected client (they
   already are judged there). The host never rolls for another body. The 1 Hz snapshot is compared
   against the model each second and every disagreement is logged with player, field, model value
   and reported value; the host's value stands.
5. **Measure the cost**: N bodies × the vital pass, per frame, on the host. The ticket's original
   demand — the design is not called affordable before this number exists.
6. **Acceptance rows** (the six below, plus):
   - a local edit to health does not survive one row (overwritten within 100 ms);
   - an event the host never received shows up as a named divergence, not as a silent value;
   - a full session (a layer or more) stays inside the divergence tolerance, and the tolerance is
     recorded.

### Stage 2 unit 1 — the host clone simulation (design written before the code)

The first independently committable unit of stage 2. It settles the two questions the code cannot
answer by itself — where the simulated body comes from, and what each native side effect is
suppressed by — and it is the record the patch contract tests are derived from.

**The clone comes from the existing render clone, not a second clone.** The host already holds one
`Body` per remote player: `RemoteBodyFactory` instantiated the scene's `"Experiment"` object, froze
its physics, disabled its colliders, stripped its instance ids and disabled the owner-local
components. That clone is the only per-player body the host has, and it already carries the two
inputs a fresh clone would have to rebuild: the display inventory (whose `Item.Stats` give
`GetAllWearables` the insulation and `GetTotalEncumberance` the weights, so `HandleBodyTemperature`
and `HandleBody` read real numbers) and the stream-fed pose flags (`standing`, `crouching`,
`sleeping`, `climbing`). A second clone would duplicate all of that sanitization and would then
need the same seeding anyway, so the simulated body and the host's view of a remote player are the
same object by construction: they cannot disagree. Guests' clones keep the presentation-only path
(`RemoteBodyDriver`, no `HostVitalsDriver`); only the host attaches the simulation driver.

**Seeding and ownership.** The first snapshot that reaches a clone seeds it through
`CharacterRestoreApplier.ApplyStats` (health, skills and limbs, no limb components and no item
wipe — the render inventory already holds the display items). The seed also forces the derived
periodic fields to recompute on the first pass (`secondCheckTime`/`halfSecondCheckTime` above their
thresholds, `tempCheckTime` set to now so the first temperature refresh waits for the derived pass)
and zeroes the random-event timers (`minuteCheckTime`/`halfMinuteCheckTime`/`limbBloodUpdateTimer`)
plus the private per-limb `infectionCheckTime` (pinned far future, so the limb infection roll never
fires): the clone is an `Instantiate` of the local player's body, so the fields those passes compute
— `bleedingSpeedMultiplier`, `bleedClottingSpeed`, `clothingTemperature`, `hasScubaGear`, `immunity`,
the encumbrance cap — are not on the wire and must not start from the local player's. The seed also
resets `usingSleepingBag`/`curSleep`/`forcedSleepQuality` to their neutral values (they are not on
the health message). Each later report object is the same map with the three host-owned values
preserved: `bloodVolume`, `totalBleedSpeed` and each limb's `bleedAmount`. An authoritative write
(a revive, an amputation, a bite, a limb latch/injury event, a restore) is recognized at the saved
snapshot: when its owned values differ from the values this body last wrote back, they are copied
onto the body before the next advance. After every advance the body writes its three values back
into the saved snapshot, and `ContinuousVitals.Preserve` keeps a plain report from stating them.
The 10 Hz `PlayerVitalsService` therefore reads the host's model out of the stored snapshot and only
pushes it; it no longer reimplements the bleeding rules. Everything else the snapshot carries stays
an INPUT: the report's temperature, blood pressure, consciousness, infection, limb health and the
rest are mapped onto the clone as they arrive, so the host owns the three bleeding values in this
unit and nothing else (the ticket's wire section is the authority on that boundary; widening it is
a later stage).

**Injury event (B1 of the review).** A plain report may not state the owned bleed, so an injury the
client judged at a trap/explosion/fall would be erased. The owner's detector watches its own limb
bleeds and, on a RISE, sends the full post-injury limb+body state as the existing owner-state event
(`NetMsg.LimbStateEvent`, now documented as a limb latch OR an injury). The host takes the event's
limbs and its non-owned health fields, while keeping its own `BloodVolume`/`TotalBleedSpeed`. The
limb bleed travels in both directions, so it is clamped UPWARD: a lower event value (a member claim,
or the client's own decay) is ignored because the host holds the last written value, and the one
downward write is a dismembered limb's zero (the amputation authority) — the event cannot be used to
state the output lower. A rise is the trigger because the plain report cannot state it; the host's
own 10 Hz row can raise a limb bleed (an arbitrated bite/shrapnel), so the detector absorbs a rise
that arrives with a newly applied row and resets its baseline across a restore instead of reporting
the host's own write back as an injury.

**One dt.** A `HostVitalsDriver` on the clone's `Body` GameObject runs the pass from `FixedUpdate`,
where Unity's `Time.deltaTime` is the fixed step: every stage of a pass sees the same dt and a
variable frame delta can never teleport a value. A step above 0.25 s is skipped and named once (a
fixed-step driver should never see one; this is a belt for a future caller, while the freshness
mark on the row is the real stalled-model gate). The limbs advance first, then the five body
stages in the game's order — the relative order of `Limb.Update` and `Body.Update` is undefined
natively, so it is fixed here as a definition. The pass runs under a saved/restored `Random` state
seeded with a fixed value: the same build and fixed step replay the same numbers, and the host's
own world RNG stream is not consumed by the pass. (The same-build qualifier is deliberate: the
game's own `blackAmount` can pick a different `Time.fixedDeltaTime`; clone `HandleVisuals` still
consumes `Random` outside the pass — both recorded limits, not claims.)

**Side-effect suppression, one row per patch.** The patches are scoped by
`HostVitalsPass.IsActive` (a thread-static pass scope), so a local body never sees them. The pin
tests are in `HostVitalsSuppressionPinTests` and `HostVitalsPassTests`; the generic
`PatchContractTests` resolves every target against the game assembly before launch.

| Side effect | Native source | Suppression |
|---|---|---|
| Camera flash | `HandlePeriodicChecks` → `PlayerCamera.StartCoroutine("FlashBrain")` | `PlayerCamera.FlashBrain` prefix returns an empty routine inside the pass |
| Threat music | `HandlePeriodicChecks` writes `PlayerCamera.threatMusicTime` | `HostVitalsPass.Run` saves the field before the pass and restores it in `finally` (a field write has no patch point) |
| Sounds | `HandleCirculation` heart thump, `PantSound.TryGrowl`, `Talker.Talk`, `Vomiter.Vomit` warnings | `SoundPlayPatch` (string) and `SoundPlayAudioClipPatch` (direct AudioClip) both return false inside the pass, before the play and before the capture |
| Vomit | `HandlePeriodicChecks` → `vomiter.Vomit()` starts `DoVomit` (time scale, drop, particles, blood) | `Vomiter.Vomit` / `VomitBlood` prefixes return false inside the pass |
| Talk | `HandlePeriodicChecks` → `talker.Talk(...)` starts the speech coroutine | the existing `TalkerPatch.TalkPatch` already refuses a remote clone's speech (the string overload delegates to the list overload it patches) |
| Coroutines | `HandlePeriodicChecks` starts `Cry`, `BrainControlReverse` and `BrainDamageRagdoll` | each coroutine method's prefix returns an empty routine inside the pass |
| Ragdoll / physics re-enable | `HandleBody` calls `Ragdoll()` (enables limb rigidbodies, disables the body collider, sets `standing=false`) | `Body.Ragdoll` prefix returns false inside the pass |
| Terminal transitions | `HandleCirculation` → `TryStartFibrillation`, `HandleBody` → `TryLastStand` / `WakeUp`, `HandlePeriodicChecks` → `DropItem`, plus the model's own consciousness/brain/death/sleep/infection/fibrillation writes | the explicit entries have a prefix that returns false; the fields a random branch or terminal write can change are captured by `HostVitalsQuarantine` before the pass and restored after it — including `heartRate` (and its `inCardiacArrest` getter), `respiratoryRate`, and each limb's skin/muscle health (the client's report/event is the only writer that survives) |
| Random events | `HandlePeriodicChecks` minute/half-minute blocks, the second-check threshold draws, `Limb.Update`'s infection roll, the ECG variation draw | the minute/half-minute timers are pinned to 0 after every pass (the blocks never run); the limb infection check timer is pinned far future at seed; the remaining second-check draws execute but every field they write — the pulmonary damage to `muscleHealth`, the resulting `respiratoryRate`, and the ECG `randomFibrillationVariation` — is quarantined, so no drawn outcome persists or reaches the wire |
| Clone `Start` droppings coroutine | `Body.Start` starts `TheCoroutineThatMakesYouShitYourselfWhenUnconscious`, which creates a world item | that coroutine's prefix returns an empty routine for any remote clone (it is not a pass-scoped call — it starts at creation) |
| Water/liquid effects | `LiquidAffect` (added by `Body.Start`/`Limb.Start` after the factory's disable pass) writes wetness/temperature/limb health and calls `FluidManager.SetLiquid` | `LiquidAffect.FixedUpdate` prefix refuses a body/limb whose parent carries `RemoteBodyDriver` |
| Weight visual | `HandlePeriodicChecks`'s half-minute block writes the clone's limb/held-item `localScale` | skipped with the half-minute random block; presentation-only and not a model input |
| Injury bleed | a client-judged trap/explosion/fall raises a limb's bleed on the owner's own body | `PlayerInjuryRule` detects the rise on the local body per frame; `CharacterDataSync.ReportOwnerStateEvent` sends the full owner state on `NetMsg.LimbStateEvent`, which the host applies with its own blood values preserved and clamps a limb's bleed upward (a dismembered limb zeroes); a rise from a newly applied host row is absorbed, and a restore resets the baseline |
| RNG | `HandlePeriodicChecks` and `Limb.Update` draw from the process-global `UnityEngine.Random` | `HostVitalsPass.Run` saves `Random.state`, seeds it, and restores it in `finally`; the quarantine makes any draw harmless |

**What this unit deliberately leaves out.** The 20 Hz missing inputs (in-water, movement/exercise,
current action) are Stage 2 item 2, and the frame-cost measurement is item 5. The random branches
that remain reachable still EXECUTE, because `HandlePeriodicChecks`/`Limb.Update` cannot be split
and `UnityEngine.Random.value`/`Range(float,float)` have no IL body to patch (verified: `body=False`
on the game's UnityEngine.CoreModule); what the quarantine guarantees is that no drawn outcome
persists, changes a body transition, or leaves on the wire. The minute/half-minute random blocks
and the limb infection roll are not even reached (timers pinned). A body that cannot be cloned now
stops the 10 Hz row after the freshness window instead of freezing the owner's body to stale host
values; the existing clone-failure window names the clone failure.

**Landed in this unit.** Adapter pieces: `HostVitalsBodies` (the per-body seam and driver attach),
`HostVitalsDriver` (the fixed-step pump), `HostVitalsOwned` (the body half of the round trip),
`HostVitalsPass` (scope, fixed seed, quarantine and camera-field preservation, limb-then-body
order), `HostVitalsQuarantine` (the client-owned fields), `HostVitalsSuppressionPatches`
(camera/audio/vomit/talk/coroutines/ragdoll/terminal/drops/LiquidAffect) and the
`PlayerInjuryRule`-driven trigger in `CharacterDataSync`; edited: `RemotePlayerRenderer` (attach on
host, preserve the owned bleed in the clone limb apply), `RemoteBodyFactory` (`hostSimulated`
sanitization), `CloneLimbRenderer` (the preserve flag), `CharacterRestoreApplier` (`ApplyStats`
without the item wipe or components), `BodyUpdatePatch` (`RunVitalsSubset` is the one shared stage
list), `BodyPatches` (`RunLimbUpdates`, the pass-scope limb gate), `SoundPlayPatch` and
`SoundPlayAudioClipPatch` (the pass guards), `EnemyTerminalStateApplier` (an owner event keeps the
host's two blood values) and `CharacterDataSync` (the injury trigger and the owner-state report).
Runtime: `CharacterHealthMsg.TotalBleedSpeed`, `PlayerVitalsMsg.RefusedField`,
`CharacterDataFinite` (health/skills/limbs plus the item values that reach weight, refused whole),
`ContinuousVitals.Write`/`Differs`, `VitalsReconciliation` (the minimal item-4 comparison), the
store's finite-guarded authority path, report-refusal answer and model-advancing mark, the
snapshot-reading/gated `PlayerVitalsService` and the refusal logger in `PlayerVitalsHandler`; the
stage-1 model and tuning files were retired. Tests: `HostVitalsSuppressionPinTests`,
`HostVitalsPassTests`, `ContinuousVitalsTests`, `CharacterDataFiniteTests`, `PlayerInjuryRuleTests`,
`VitalsReconciliationTests`, the rewritten `PlayerVitalsServiceTests`, new
`PlayerVitalsOwnershipTests` rows, the new `NetPacketTests` round trips, and the updated
carried-body pin. `docs/architecture-debt.json` records the two aggregate sizes the new seams
pushed over 600 (with the planned split); not verified: the two-client acceptance rows below and
the frame cost (stage 2 item 5).

### Independent adversarial review (2026-10-18, frozen tree) — findings and dispositions

The reviewer reproduced 741/741 gates and 5905/5905 behaviour tests on the frozen tree; no tracked
source was flagged by `dotnet format --verify-no-changes` (the errors were generated
`MyPluginInfo.cs` files). It reported two blockers and six majors. The user ruled the disposition on
2026-10-18: **an injury event for B1, stage-2 item 4 completed in this unit for B2, and a minimal
reconciliation for M1**. The findings are kept here with the code they cite; the fix column is what
this unit does.

**B1 (blocker, pre-existing at HEAD) — a client-judged injury never reaches the host model, and the
10 Hz row erases the owner's wound bleed.** `World/ExplosionBodyEffect.cs` says *"The rolls
themselves are local — the results travel as character state via the 1 Hz CharacterData report"* and
writes `limb.bleedAmount += param.bleedAmount.RandomFromRange() / armorReduction;`; native traps
(`BearTrap.cs:38`, `BarbedFence.cs:30`, `GroundGlass.cs:29`) and body functions (`Body.cs:1146/1154`
`RemoveEye`, `:1220` `Disfigure`, `:2004`) add bleed on the victim's client. `ContinuousVitals.Preserve`
then overwrites every report's limb bleed with the host's previous value (`limb.BleedAmount =
old?.BleedAmount ?? 0f;`), `Limb.Update` only decays it (`Limb.cs:524`), and `LocalVitalsApplier`
writes the host's value back every 100 ms — so the wound's direct bleeding disappears and the host
never computes it. **Fix:** the owner's post-injury full limb/body state is sent as a reliable owner
event (the existing `NetMsg.LimbStateEvent` payload, now documented as the owner's post-event limb
and body state for a latch OR an injury); the host applies its limbs and non-owned health while
keeping its own `BloodVolume`/`TotalBleedSpeed`, and clamps each limb's bleed upward (a lower value
is ignored; a dismembered limb's zero is the one downward write), so the event cannot be used to
state the output lower. The local detector sends it when any limb's bleed rises; it absorbs a rise
that arrives with a newly applied host row (an arbitrated bite/shrapnel is the host's own write) and
resets its baseline across a restore. The report filter stays for plain reports. The message is
reused rather than duplicated because its payload, handler, sender check, relay and kernel sync are
exactly the event needed; the new part is the trigger and the ownership-preserving apply.

**B2 (blocker) — the host ran the random and terminal parts of the native chain, and the recorded
limit was false.** The pass ran the rolls at `Body.cs:3437/3445` (radiation infection/internal
bleeding), `:3525` (pulmonary embolism), `:3537-3539` (stroke), `:3545` (fibrillation), and
`Limb.cs:535` (infection); their effects feed `Body.cs:2799/2817/3005` → `bloodVolume`/`totalBleedSpeed`
→ the wire. `Random.InitState(Seed)` per pass made a threshold draw permanently true or false.
Terminal writes (`:2874` consciousness, `:2969` sleeping, `:2914-2917` infection, `:727-758`
fibrillation/heart rate, `:880-887/3610/2991/3006` brain health) ran unsuppressed. **Fix (stage-2
item 4 in this unit):** the pass is a deterministic model over client-owned state. The minute and
half-minute random blocks are skipped by pinning their timers; the limb infection check is disabled
by pinning `infectionCheckTime`; and the fields a roll or terminal transition can write are captured
before the pass and restored after it (body: brain health, consciousness, fibrillation,
heart rate — and therefore its `inCardiacArrest` getter — respiratory rate, the ECG variation draw,
sleeping, internal bleeding, viscosity, stroke, embolism, septic shock, shock; limb: infection,
broken/dislocated flags, the healing/infection timers and skin/muscle health). The client's
report/event is the only source of those values. The second-check's threshold draws and the ECG
variation draw still execute because the native methods cannot be split and
`UnityEngine.Random.value`/`Range(float,float)` have no IL body to patch (verified: `body=False`);
their fields are restored, so no drawn outcome persists, reaches a transition or leaves on the wire.
That residual is stated, not hidden.

**M1 (major) — no reconciliation.** `SaveReportedCharacterData` called `ContinuousVitals.Preserve`
before any comparison, so the client's own three values were discarded without a log, and ruling
item 5 calls the comparison the only way a wrong model becomes visible. **Fix:** before `Preserve`,
`VitalsReconciliation` compares the stored (host) values with the raw report and logs player, field,
model value, reported value and difference at Warning; the tolerances are recorded in this ticket
(blood 0.5, total/limb bleed 0.05). The host's value still stands, and the comparison is skipped
while the model's freshness mark is stale (a stalled clone free-runs the client; a divergence line
per report would be noise). This is the minimal form; the full comparison of every field is not part
of item 4's two owned-value scope.

**M2 (major) — an authority write could freeze a player forever.** `SaveCharacterData` accepted
anything; a non-finite value made `HostVitalsBodies.Step` stop advancing and `PlayerVitalsService`
stop sending, while `Preserve` re-copied the NaN into every later report, so no report could repair
it. **Fix:** `SaveCharacterData` refuses a non-finite physiological snapshot by name (Error, previous
snapshot kept), and the event handler refuses a non-finite event the same way; a later finite
authority write or report recovers.

**M3 (major) — suppression completeness and proof.** The `Sound.Play` AudioClip overload had no
pass guard; `LiquidAffect` (added by `Body.Start`/`Limb.Start` after the factory's disable pass)
wrote wetness/temperature/limb health and world liquid/sounds outside the pass; the half-minute
`localScale` writes were unlisted; the suppressions are text-pinned, not runtime-proven. **Fix:** the
AudioClip patch takes the same guard; `LiquidAffect.FixedUpdate` refuses a remote-clone body/limb
(marker check, since it is added after the factory); the weight `localScale` writes are listed as an
intentional host-view presentation write (not an input); runtime proof remains the two-client
acceptance row (the patch contracts and source pins are the static guard).

**M4 (major) — the census over-claimed for a fresh join.** `SaveReportedCharacterData` preserves
only when a previous snapshot exists; on a fresh join the first report's three values become the
host's baseline. **Fix:** the census row now says the first report seeds the three values (the only
source) and every later report is filtered; a real-store row pins the first-report case.

**M5 (major) — the row kept sending while the model was not advancing** (clone failure, a
non-finite stored value, or `Time.timeScale = 0` where `FixedUpdate` stops). **Fix:** the store
carries a host-local "model advancing" mark per member, written by the adapter after a successful
advance; the service sends only while that mark is fresh (250 ms). The mark is cleared with the
table/session.

**M6 (major) — the report refusal was not answered.** **Fix:** the 10 Hz `PlayerVitals` row carries
the refused field name when the store is refusing that member's report; the handler logs it by name
(once per field change for this client), so the client can see why its reports are not being used.
The handler also drops a row whose named player is not this client before the edge, so a misrouted
row cannot be answered or logged as this player's. The host's values still apply; the refusal is not
a second state channel.

Minors fixed in the same unit: the fixed-step check's tolerance is now 0.001 s (the old 0.005
accepted a 60 fps frame delta as the fixed step); the duty to name a stall is moved to the model
alive gate, since a fixed-step driver cannot receive a huge `Time.deltaTime`; the seed also resets
`usingSleepingBag`/`curSleep`/`forcedSleepQuality` and pins `infectionCheckTime`; the per-step
finiteness scan moved to the seed/new-snapshot edges; the new wire value has a round-trip test; the
service test's doc reference and the `PlayerVitalsMsg` "two values" wording are corrected. Recorded,
not fixed: `TotalBleedSpeed` is 0 in the kernel-rebuilt heal/use health blocks and in
`PrepareRevive` until the next native pass recomputes it (both self-heal); "two hosts replay the
same numbers" holds for the same build and fixed step, not across hosts whose `blackAmount` picks a
different `Time.fixedDeltaTime`; clone `HandleVisuals` still consumes `Random` and can play a burp
outside the pass (pre-existing presentation); a guest-forged injury event addressed straight at
another guest is applied there as presentation, the same residual as the limb event row; and no
runtime proof of patch installation exists short of the two-client acceptance batch.

### Delta review (same session, frozen tree) — findings and dispositions

After the item-4 expansion and the user's three rulings, the same independent reviewer re-read the
delta against the frozen tree (it reproduced 741/741 gates and 5932/5932 behaviour tests) and
reported three still-open closure defects plus three new majors; none re-scoped the rulings. The
findings and their fixes:

- **B2 (still open, narrowed) — the quarantine missed three writes and the ECG draw.** The
  pulmonary branch set the quarantined flag, then wrote `limbs[1].muscleHealth -= 0.6f` (uncovered);
  `Limb.cs` turns muscle death into `respiratoryRate = 0` and `internalBleeding += dt * 0.2f`;
  `Body.cs:757` sets `heartRate = 0f` at fibrillation 100, and `inCardiacArrest` is a getter over
  heartRate; `Body.cs:900` draws `randomFibrillationVariation`. The pin iterated the implementation
  list, so it could not discover a gap. **Fix:** `heartRate`, `respiratoryRate`, the per-limb
  `skinHealth`/`muscleHealth` and `randomFibrillationVariation` joined the quarantine; the pin is
  documented as the reviewed policy list; the ticket and class text now name the fields instead of
  claiming an unbounded set.
- **M2 (still open) — the refusal was defeated on the event path, and the report path returned
  true after an inner refusal.** `LimbStateEventHandler` applied the store's refusal and then fired
  and relayed regardless; `CloneFactTable` shares the stored object reference, so the NaN reached
  the saved snapshot, the host clone and every guest. `SaveReportedCharacterData` ended with
  `SaveCharacterData(...); return true;`, so a report the inner guard refused was still relayed.
  **Fix:** `ICharacterDataControl.ApplyLimbStateEvent` returns bool; the handler drops on false
  before the adapter event and the relay, and a guest drops a non-finite relay; `SaveCharacterData`
  returns bool and `SaveReportedCharacterData` returns that verdict; the in-place
  `ApplyEnemyBite/Lunge/Effect` now run a float finite guard and refuse by name. New tests cover
  the refused event/store and the refused enemy event; the census sentence is now true end-to-end.
- **M6 (still open) — the refusal edge did not check the named player.** The handler is a singleton
  and only checked the sender; a row naming another member would have been answered/logged as if it
  were this client's. **Fix:** a row whose `SteamId` is not `LocalSteamId` is dropped by name before
  the edge; the normal path only ever carries this client's row (the host sends each row to its
  owner), and a behavioural test sends a foreign row plus a repeated refusal and asserts one edge
  log.
- **N1 (new major) — the event could lower the host-owned limb bleed.** `ApplyLimbState` took the
  event's limbs wholesale, so a zeroed event would be adopted and Preserve would keep it low.
  **Fix:** the apply clamps each limb's bleed upward against the record's previous value; a
  dismembered limb zeroes (the amputation authority). Tests pin lower/higher/dismembered; the
  census and limits wording is corrected.
- **N2 (new major) — the detector fired on the host's own authority and across a restore.** A host
  row can raise a bleed (bite/shrapnel), and the old 100 ms comment was false; a restore skipped the
  detector without resetting its baseline. **Fix:** the detector runs per frame, absorbs a rise that
  arrives with a new row (`LocalVitalsApplier.AppliedCount`), and resets the baseline while a
  restore is pending; the comment states both.
- **N3 — the report-path return contract** is the `SaveReportedCharacterData` half of M2; fixed by
  returning the inner verdict.
- Minors fixed: the checklist evidence now cites the final unfiltered run; the service test's
  `HostVitalsOwnedTests` reference; the seed `tempCheckTime` comment; the event-health mutation
  comment. Recorded, not merged: two prefixes now target `LiquidAffect.FixedUpdate` (the kinematic
  one and the remote-clone one) — disjoint by condition and order-independent in effect, but one
  owner per target would be cleaner; noted for the next pass.

## Acceptance (the rows a run must read)

Two clients (plus a third where a row needs it), one of them with an artificial delay and packet loss:

| # | Scenario | Expected |
|---|---|---|
| 1 | A guest bleeds while its own panel is open | The panel moves continuously (no 1 Hz stepping), and the host's row agrees once the report lands |
| 2 | The same on the host's screen for the GUEST's body | The guest's state reads continuously on the host too: the host's clone is the model on the host only, while the guest's own client renders/updates its own body |
| 8 | A guest is wounded by a trap/explosion (bleed rises locally, no limb latch) | The owner's injury event reaches the host immediately and the host's model/row carries the wound; the 10 Hz row does not erase it |
| 3 | The host's value is forced away from the client's simulated value | The client aligns at once (a visible jump is accepted) and both sides read the host's value afterwards |
| 4 | An event the event chain missed (the report lost) | The next snapshot carries it, and the divergence is warned on the side that can see it — never a silent step |
| 5 | A client reports a value the host's model cannot produce | The host keeps its own value, and the disagreement is a named log line |
| 6 | A limb flag (broken, splinted, infected) changes | It is broadcast, not predicted: no peer shows it before the authoritative event |
| 7 | A member edits its own blood volume locally (console or memory) | The next row overwrites it, and the divergence line names the edit |

## Limits to write down rather than hide

- **Inputs remain the client's.** A client that lies about an input (viscosity, hunger, in-water)
  bends the model; the protection this ticket buys is that the plain report can no longer STATE the
  output. The one event channel that can raise a limb's bleed is the owner's injury event, and the
  host clamps it upward (a lower value is ignored; a dismembered limb zeroes). Closing the input
  side is the determinism project (`future/generic-prediction-runtime.md`).
- **A wrong model is caught only by the minimal reconciliation**, and only because the client still
  reports the value. That report must never be removed for the sake of bandwidth. The comparison's
  tolerances are sampling-window values (blood 0.5, total/limb bleed 0.05), recorded so a run can
  judge the noise floor; a full-field comparison is not part of this item. It is skipped while the
  model mark is stale (the client is then free-running), and an authority write that lands between
  two clone passes can still be compared against the client's pre-event value once.
- **The injury event is the owner's own body state.** It has no independent anchor for the wound
  amount (the alternative is re-running the client's local damage, which decision 265 rules out),
  so a lying member can report a worse wound on its own body; the event keeps the host's two
  continuous blood values, its limb bleed is accepted upward only (a dismembered limb zeroes), and
  it is refused whole when non-finite. The census row names this.
- **The host's random branches still draw.** The two methods cannot be split and the RNG entry
  points have no IL body to patch, so the second-check threshold draws and the ECG variation draw
  execute; the quarantine restores every field they write, and the minute/half-minute blocks and the
  limb infection roll are never reached. No drawn outcome persists or rides the wire — the literal
  "must not draw" would require reimplementing the stages, which the ruling's reuse clause forbids.
- **A first report seeds the three owned values on a fresh join.** There is no trusted prior state
  to anchor them; every later report is filtered, and the census row says so.
- **The 10 Hz row is gated on a fresh model mark** (250 ms). A clone that cannot be built, a
  non-finite stored snapshot or a stopped FixedUpdate (start-gate `timeScale = 0`) stops the row
  instead of freezing the owner's body; the owner's own body then keeps simulating locally until
  the host model advances again.
- **Reuse pins the patch surface to private game methods**; a game update that renames one fails
  loudly at startup through the patch-contract tests, which is the intended failure mode. The
  suppressions themselves are static pins + resolvable targets; their runtime effect is the
  two-client acceptance batch, not a unit test.
- **`TotalBleedSpeed` is 0 in the kernel-rebuilt heal/item-use health blocks and in
  `TraderRecruitPolicy.PrepareRevive`** until the next native pass recomputes it; both self-heal
  within a pass / 100 ms and are recorded rather than hidden.
- **Same-build determinism only.** The game's own `blackAmount` can pick a different
  `Time.fixedDeltaTime`; clone `HandleVisuals` still consumes `Random` and can play a burp outside
  the pass (pre-existing presentation). The pass's own RNG stream is isolated and seeded.
- **Two prefixes target `LiquidAffect.FixedUpdate`** (the existing kinematic-item guard and the
  remote-clone guard). They are disjoint by condition and each returns false only for its own
  case, so the effect is order-independent, but one owner per target would be cleaner; recorded
  for the next patch-surface pass rather than merged under this unit's freeze.
- **This is not the generic prediction runtime.** One model, the body's own, no movement, no
  animation.

## Non-goals

- Movement, animation and other behaviour (the user's call, 2026-10-14).
- Anti-cheat beyond the output/input split above.
