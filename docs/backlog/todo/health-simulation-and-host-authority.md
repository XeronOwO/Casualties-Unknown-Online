# Health on every client, reconciled to the host's value

- Status: Todo — **direction ruled by the user on 2026-10-18: the HOST computes the continuous
  player state by reusing the game's own methods; the affected client judges every event and
  applies the host's rows to its own body.** This supersedes the (a)-first recommendation the
  earlier draft carried. Stage 1 (the bleeding family) is implemented and committed; its
  two-client runtime acceptance is still owed, and Stage 2 (the whole chain) is designed below.
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
- **Stage 2 additions:** the missing input flags on the 20 Hz entity stream (in-water, moving,
  exercising, current action), and the 1 Hz `CharacterData` stays as the report channel the
  reconciliation will read — the comparison itself is stage 2, not stage 1.

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

## Acceptance (the rows a run must read)

Two clients (plus a third where a row needs it), one of them with an artificial delay and packet loss:

| # | Scenario | Expected |
|---|---|---|
| 1 | A guest bleeds while its own panel is open | The panel moves continuously (no 1 Hz stepping), and the host's row agrees once the report lands |
| 2 | The same on the host's screen for the GUEST's body | The guest's state reads continuously on the host too — every client simulates every body |
| 3 | The host's value is forced away from the client's simulated value | The client aligns at once (a visible jump is accepted) and both sides read the host's value afterwards |
| 4 | An event the event chain missed (the report lost) | The next snapshot carries it, and the divergence is warned on the side that can see it — never a silent step |
| 5 | A client reports a value the host's model cannot produce | The host keeps its own value, and the disagreement is a named log line |
| 6 | A limb flag (broken, splinted, infected) changes | It is broadcast, not predicted: no peer shows it before the authoritative event |
| 7 | A member edits its own blood volume locally (console or memory) | The next row overwrites it, and the divergence line names the edit |

## Limits to write down rather than hide

- **Inputs remain the client's.** A client that lies about an input (viscosity, hunger, in-water)
  bends the model; the protection this ticket buys is that the client can no longer STATE the
  output. Closing the input side is the determinism project (`future/generic-prediction-runtime.md`).
- **A wrong model is caught only by the reconciliation**, and only because the client still
  reports the value. That report must never be removed for the sake of bandwidth.
- **Reuse pins the patch surface to private game methods**; a game update that renames one fails
  loudly at startup through the patch-contract tests, which is the intended failure mode.
- **This is not the generic prediction runtime.** One model, the body's own, no movement, no
  animation.

## Non-goals

- Movement, animation and other behaviour (the user's call, 2026-10-14).
- Anti-cheat beyond the output/input split above.
