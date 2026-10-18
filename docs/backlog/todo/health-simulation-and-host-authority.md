# Health on every client, reconciled to the host's value

- Status: Todo — **direction ruled by the user on 2026-10-18: the HOST computes the continuous
  player state by reusing the game's own methods; the affected client judges every event and
  applies the host's rows to its own body.** This supersedes the (a)-first recommendation the
  earlier draft carried. Stage 1 (the bleeding family) is written and staged; Stage 2 (the whole
  chain) is designed below. Not started on the verification side.
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
  exercising, current action), and the 1 Hz `CharacterData` stays as the reconciliation channel.

## Stage 1 — the bleeding family (written, staged in git, NOT verified)

Owned values: per-limb `bleedAmount` and `bloodVolume` (+ derived `totalBleedSpeed`). Inputs, all
already on the existing snapshot: blood viscosity, venom, hunger, internal bleeding, and per limb
the skin health, tourniquet flag, shrapnel count and bandage amount. World settings:
`healingrate`, `bleedrate` (read by the adapter through `IPlayerVitalsTuning`, so a widened run is
honoured). No new inputs, no randomness, no terminal output.

Files (all staged as `stash@{0}` at hand-off):

- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/PlayerBleedingState.cs` — the model,
  one rule per native call site, with the citations in its doc comment.
- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/PlayerVitalsService.cs` — host state
  per player, 10 Hz advance and send; re-seeds when the host's own snapshot stops carrying the
  model's values (which only an event the host arbitrated can do).
- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/ContinuousVitals.cs` — keeps a plain
  report from writing the two owned fields (`CharacterDataStore.SaveCharacterData`).
- `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/PlayerVitalsMsg.cs`,
  `PlayerLimbBleedMsg.cs`, `NetMsg.PlayerVitals = 154`.
- `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/PlayerVitalsHandler.cs` — host → client.
- `src/CasualtiesUnknownOnline.GameAdapter/Character/LocalVitalsApplier.cs` — applies a row to the
  local body only (its own row; another player's is read from the snapshot).
- `src/CasualtiesUnknownOnline.GameAdapter/GamePlayerVitalsTuning.cs` — the live run settings.
- `tests/CasualtiesUnknownOnline.Tests/Character/PlayerBleedingStateTests.cs` — 9 rows, green.
- Doc/gate edits: the sync-coverage matrix P3 row + `NetMsg.PlayerVitals` vocabulary index entry +
  two evidence anchors, `HostToGuestDirectionTests`, `CuoServiceOrderTests`, the test fake.

Where it stands: build green and the 9 model tests green at the last full run. Five gates went red
and were being fixed when the cycle handed off — the vocabulary index entry, the P3 row length
(1200-char bound), the `CharacterDataSync` 611-line aggregate (split into `LocalVitalsApplier`),
and the message-direction/order registries. **The last three edits were not built or gated**, so
the next round's first action is `git stash pop` followed by a build and those five gates.

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
