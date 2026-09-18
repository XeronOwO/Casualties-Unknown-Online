# World-time acceleration is gated on being asleep

- Status: Todo
- Priority: Medium
- Category: World / session (world-time initiation)
- Source: User ruling 2026-09-18 (design alignment session): the shared clock stays shared, but the OPERATION must be immediate and local — a player who presses accelerate accelerates at once, the host arbitrates, and the broadcast brings everyone else in with an acceptable delay. Sleep keeps its "everyone unconscious" gate.
- Related: `resolved/sleep-behavior-policy.md` (the 2026-09-05 decision this supersedes in part), `todo/sync-cadence-review.md`

## Problem (evidence)

The manual acceleration key is effectively disabled in a session:

- `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldTimePolicy.cs` `Decide` ends by
  discarding a manual request: when nothing else applies, `IsManualAccelerationSpeed(requested)`
  returns `Normal` for both the speed and the kept request, and the type's own doc says
  manual requests "never move the shared clock while any in-world player is awake … a
  manual request adds nothing outside that window". Movement is stricter still: any moving
  player forces `Normal` first — a CUO-side velocity veto, not the native rule (see design
  direction 5).
- `src/CasualtiesUnknownOnline.GameAdapter/World/WorldTimeSync.cs` turns a guest's manual
  speed into a request and REFUSES the local apply (`OnTimeScaleSetRequested`), so the key
  produces no local effect either; a guest's own sleep fast-forward is suppressed outright
  ("the host's all-unconscious policy owns it").
- Sleep acceleration stays as decided: `DecideSleepSpeed` accelerates only when every
  in-world alive player is unconscious (dead players ignored; an unobserved player blocks).

## Goal

Pressing accelerate is a LOCAL, immediate operation that the host arbitrates and the
session follows: the initiator's world accelerates at once, the host accepts and
broadcasts the shared clock, and the other players enter the accelerated state after the
round trip. Sleep keeps its all-unconscious gate. Nothing about the shared clock's
authority changes — only who may initiate and when the initiator feels it.

## Design direction (decide at implementation)

1. Local initiation: the initiator's client applies the manual speed immediately and
   reports the intent; the current "request only, refuse the local apply" path in
   `WorldTimeSync` is replaced by "apply locally, report, reconcile".
2. Host arbitration is accept-first: only invalid requests are refused (not an in-world
   member, an invalid speed, the start gate owning the clock) — a teammate being awake is
   no longer grounds for refusal, otherwise the immediate local effect would be rolled back
   every time and the experience would be worse than today.
3. Reconciliation is honest, not predictive-cheating: on acceptance the initiator's clock
   IS the shared clock and others catch up; on refusal the initiator returns to the host's
   value quickly (a fast ramp down, never a snap). The brief window where the initiator is
   ahead is accepted (user ruling), as is the rare rollback.
4. Sleep acceleration is untouched: the all-unconscious gate, the native black-screen
   acceleration and the request-clearing semantics stay.
5. Movement is an ACTION of the player who performs it, never a world state the host polls
   — settled by the user 2026-09-18 after the first draft proposed a session-wide veto. The
   native rule is the mover's own key check:
   `PlayerCamera.Update` binds `speed1`/`speed2`/`speed3` to Normal/Fast/SuperFast
   (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:885-896`), and the lines just
   below return the clock to Normal for the player who pressed a movement key —
   `Input.GetKeyDown(KeyBinds.GetBind("right")) || Input.GetKeyDown(KeyBinds.GetBind("left"))`
   calls `SetTimeScale(Normal)` (`PlayerCamera.cs:921-924`); up/down never did it. CUO must
   therefore run that rule on the mover's OWN client and report it through the same
   local-initiation path as any other speed change, and DELETE the host-side velocity veto
   (`WorldTimePolicy.IsMoving` / `MovingSpeedSquaredThreshold`): it forced `Normal` for every
   player's movement — a body that was only pushed, carried or drifting included — which is
   a judgement about a player's own input taken on somebody else's client (decision 184).
   Consequences: a teammate walking no longer cancels an accelerated session, and the
   feedback names the player who changed the speed instead of guessing it from velocity.
6. `resolved/sleep-behavior-policy.md` is rewritten to record the supersession (its
   "manual requests never move the clock while anyone is awake" and "any awake player
   blocks" clauses), with the reason.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | A player presses Fast while a teammate is awake | The initiator accelerates at once; the host accepts; the session follows |
| 2 | Everyone falls asleep | Unchanged: the all-unconscious gate accelerates the session |
| 3 | A request the host refuses | The initiator returns to the host's value quickly and observably |
| 4 | Invalid request (not in world / bad speed / start gate) | Refused, as today |
| 5 | Late joiner / reconnect | The existing world-entry fan-out and 5 s resend carry the shared speed |
| 6 | Two players press accelerate at once | Idempotent: one shared accelerated state |
| 7 | A player presses a movement key during a fast-forward | That player's own client returns to Normal at once (the native rule) and the session follows through the same local-initiation path; a body that was only pushed, carried or drifting changes nothing |
| 8 | A teammate walks while the session is accelerated | No change: the session stays accelerated (the movement veto is gone); a player who wants Normal presses their own key |

## Non-goals

- Per-player time scales or a local-only clock.
- Changing the sleep gate or the start gate.
- Prediction of world events during the initiator's lead window (the lead is the accepted
  cost, bounded by one round trip).
