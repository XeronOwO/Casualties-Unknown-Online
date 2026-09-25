# World-time acceleration is gated on being asleep

- Status: Review
- Priority: Medium
- Category: World / session (world-time initiation)
- Source: User ruling 2026-09-18 (design alignment session): the shared clock stays shared, but the OPERATION must be immediate and local — a player who presses accelerate accelerates at once, the host arbitrates, and the broadcast brings everyone else in with an acceptable delay. Sleep keeps its "everyone unconscious" gate.
- Related: `resolved/sleep-behavior-policy.md` (the 2026-09-05 decision this supersedes in part), `review/enemy-hit-determination-local.md`, `review/remote-interaction-local-gating.md`, `review/item-creation-registration-first.md` (the same ruling's other families), `review/sync-cadence-review.md`

## Reopened (2026-09-21 acceptance pass)

The user reports that the acceleration ended as soon as the host moved, and rules that movement
must no longer change the world speed: an acceleration ends only when the player ends it, through
the sleep gate, or through the death/pause transitions. That supersedes §4 ("the only movement
rule left is the native one") and acceptance row 7 ("a player presses a movement key during a
manual fast-forward"), which describe the shipped behaviour.

This ticket is moved back to `todo/`; the requirement, the native evidence and the replacement
acceptance matrix are recorded in `review/world-acceleration-survives-movement.md`. The local-first
and accept-first half of this delivery stands.

**Back to `review/` (2026-09-25):** the replacement requirement shipped in `b045b974`
(`fix(world-time): only an announced speed change owns the shared clock`, decision 223) with its
own ticket in `review/`, so nothing on this ticket is open; the superseded movement rows stay as
the record of what the earlier delivery shipped.

## Problem (evidence)

The manual acceleration key was effectively disabled in a session:

- `WorldTimePolicy.Decide` ended by discarding a manual request: when nothing else applied,
  `IsManualAccelerationSpeed(requested)` returned `Normal` for both the speed and the kept
  request, and the type's own doc said manual requests "never move the shared clock while any
  in-world player is awake".
- Movement was stricter still: any player whose body velocity crossed
  `MovingSpeedSquaredThreshold` forced `Normal` first — a CUO-side velocity veto, not the
  native rule.
- `WorldTimeSync` turned a guest's manual speed into a request and REFUSED the local apply
  (`OnTimeScaleSetRequested` returned false), so the key produced no local effect either; a
  guest's own sleep fast-forward was suppressed outright ("the host's all-unconscious policy
  owns it").

## Goal

Pressing accelerate is a LOCAL, immediate operation that the host arbitrates and the
session follows: the initiator's world accelerates at once, the host accepts and
broadcasts the shared clock, and the other players enter the accelerated state after the
round trip. Sleep keeps its all-unconscious gate. Nothing about the shared clock's
authority changes — only who may initiate and when the initiator feels it.

## What landed

**1. A manual speed is LOCAL FIRST on the initiator's own client.**
`WorldTimeSync.OnTimeScaleSetRequested` no longer swallows a guest's
`Normal`/`Fast`/`SuperFast` call: the native `SetTimeScale` runs (that client's clock moves at
once, HUD and sound included) and the intent is reported as a `WorldTimeRequest`. The start
gate still keeps the clock for itself (no local body, or `WaitingForReady`, swallows the call —
the path the acceptance matrix calls "the start gate owns the clock"), and
`UnconsciousFast`/`DyingFast` stay host-owned. The vanilla per-side unconscious fast-forward
stays suppressed by its own scope.

**2. The initiator reconciles through a pure state machine.** `WorldTimeLocalInitiation`
(Runtime, no Unity, no clock) owns the pending intent: an authoritative value equal to the
intent CONFIRMS it (no clock write, no sound replay), an ordinary change on an idle client is
`Adopt`ed exactly as before, and a different value — a refused request, or the sleep gate
answering something else — starts a `Ramp` back to the host's value over
`RampSeconds = 0.4 s` of UNSCALED time, so the clock being corrected cannot drive its own
correction; the ramp ends exactly on the host's value through the normal `SetTimeScale` path
(HUD + sound). Enforcement of the host's last value (`EnforceAppliedSpeed`) is suspended while
an intent is pending or ramping, so a local acceleration is never fought by its own
enforcement. There is no timer and no latency parameter anywhere: the host answers every
request (§3), the 5 s resend settles anything else (an unmatched authoritative value settles a
pending intent too), and the session end resets the state.

**3. Host arbitration is accept-first, and every request is answered.**
`OnRequestReceived` refuses only what the host cannot represent — not an in-world member, not a
guest-requestable speed (`WorldTimePolicy.IsGuestRequestSpeed`), or the start gate owning the
clock — and each refusal is ANSWERED with the host's authoritative speed, which is how the
initiator learns to return to Normal (the sync model's "a rejection must be visible"). An
accepted request that does not change the speed is answered too (`Answer()`), because the
initiator is waiting on the verdict and must not sit ahead of the shared clock until the next
resend. `WorldTimePolicy.Decide` therefore returns the standing request instead of discarding
it; the all-unconscious sleep branch keeps the clock while it applies and still clears the
request.

**4. The host-side movement veto is DELETED.** `WorldTimePolicy.IsMoving`,
`MovingSpeedSquaredThreshold` and the velocity inputs of `WorldTimePlayerState` are gone, and
`CapturePlayerStates` reads no velocity, so a body that was only pushed, carried or drifting changes
nothing. **SUPERSEDED (2026-09-25, decision 223):** the half of this paragraph that read "the only
movement rule left is the native one … travels the same local-initiation path as any other speed
change" is no longer the behaviour and WAS the reported defect — the native movement reset is a
SILENT automatic reset, not a speed intent, so it never reaches the shared clock at all
(`review/world-acceleration-survives-movement.md` carries the replacement requirement and the
replacement acceptance matrix). `StateKnown` (the remote body proxy plus the host's character-data
store) survives for the SLEEP gate alone: an unobserved player still blocks automatic acceleration.

**5. The speed ↔ timeScale mapping has one owner.** `WorldTimeSpeedScale` (Runtime, pure) holds
the multipliers the adapter used to duplicate (1 / 5 / 20 / 25 / 3.5), so
`AdoptDirectTimeScaleWrite`, `EnforceAppliedSpeed` and the ramp endpoints cannot drift apart;
reading a live clock back still refuses Paused, Slowmo and mid-ramp values.

**6. Protocol 27 → 28** (`ProtocolVersion.Current`, `docs/decisions/active.md` #137,
`docs/api/mod-api.md`): a manual speed's judging behaviour changed on both sides and the
host-side veto is gone, so a mixed session would have one peer swallowing its own local speeds
and expecting a host that discards manual requests. Decision #159 (manual acceleration is
cooperative) is recorded as superseded in part; the sleep half of
`resolved/sleep-behavior-policy.md` stands unchanged. The stamp is also the save archive's
protocol field (`WorldCutWriter` writes it, `SaveArchiveReader` refuses a mismatch), so a
mid-run snapshot cut by the previous build no longer opens — consistent with the pre-release
policy (no released compatibility surface exists), and stated here so the consequence is not
discovered later.

## Verification

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | A player presses Fast while a teammate is awake | The initiator accelerates at once, the host accepts, the session follows | `WorldTimePolicyTests.ManualAcceleration_StandsWhileTeammatesAreAwake` (the policy half), `WorldTimeLocalInitiationTests.BeginLocalInitiation_AheadOfTheHost_SuspendsEnforcement` (the lead window is not fought by enforcement), `WorldTimeSync.OnTimeScaleSetRequested` (the native call is allowed through); the on-screen feel is the user's release-cycle check |
| 2 | Everyone falls asleep | Unchanged: the all-unconscious gate accelerates and clears the request | `WorldTimePolicyTests.AllUnconscious_AcceleratesTo25AndClearsTheRequest`, `WorldTimePolicyTests.SleepOwnsTheClockOverAStandingManualRequest`, `WorldTimePolicyTests.AnyDyingUnconscious_Uses35DyingFast`, `WorldTimePolicyTests.UnknownPlayerState_BlocksSleepAcceleration` |
| 3 | A request the host refuses | The initiator returns to the host's value quickly and observably | `WorldTimeLocalInitiationTests.AuthoritativeDifferingFromTheIntent_RampsBackInsteadOfSnapping` and `WorldTimeLocalInitiationTests.Ramp_InterpolatesOverTheRampLength_AndEndsExactlyOnTheHostsValue` (0.4 s of unscaled time, the exact target at the end); the host answers a refusal inside `WorldTimeSync.OnRequestReceived` |
| 4 | Invalid request (not in world / bad speed / start gate) | Refused, as today — and now answered | `WorldTimePolicyTests.GuestRequests_OnlyManualSpeeds`; the three guards in `WorldTimeSync.OnRequestReceived` are unchanged and every refusal path broadcasts the authoritative speed |
| 5 | Late joiner / reconnect | Unchanged: the world-entry fan-out and the 5 s resend carry the shared speed | `WorldTimeSync.OnRemoteSceneChanged` and the resend pump are untouched, and an unmatched value settles a pending intent; `WorldTimeFlowTests.RequestAndAnswer_RoundTripThroughTheRealStack` pins the wire path through the real handlers |
| 6 | Two players press accelerate at once | Idempotent: one shared accelerated state | `WorldTimeLocalInitiationTests.ATwoPressBurst_SettlesOnTheNewestIntent` (the newest intent wins; the older answer's dip is retargeted, never stuck) plus the host's last-writer `_requestedSpeed` |
| 7 | ~~A player presses a movement key during a manual fast-forward~~ **SUPERSEDED (2026-09-25, decision 223)**: movement no longer changes the world speed on any client — the native reset is a silent automatic reset and never reaches the shared clock. The replacement rows are 1-3 of `review/world-acceleration-survives-movement.md`; this row stays as the record of what this delivery shipped and why it was wrong | — | — |
| 8 | A teammate walks while the session is accelerated | No change: the veto is gone | `WorldTimePolicyTests.ManualAcceleration_StandsWhileTeammatesAreAwake` — a teammate's motion is not an input at all; `WorldTimePlayerState` has no velocity field and `CapturePlayerStates` reads none. The neighbouring `WorldTimePolicyTests.ManualAcceleration_IsNotCancelledByAJustJoinedPlayer` covers the unobserved-player case, where only the SLEEP gate is blocked |

Cycle measurements (this tree): focused world-time filter 57/57, main suite 3339 green,
normative gates 56 with the delivery-checklist case (55 without), `dotnet build` 0 warnings /
0 errors, `dotnet format` exit 0.

## Known coverage gaps (declared, not silently carried)

- The Unity-facing wiring itself — the native `SetTimeScale` call being allowed through the
  patch prefix, the `Time.timeScale` writes during the ramp, the prefix/postfix pair — is NOT
  unit-testable in the simulation harness (the test host cannot touch `PlayerCamera`). The
  decisions are pure-tested (`WorldTimeLocalInitiationTests`, `WorldTimePolicyTests`,
  `WorldTimeSpeedScaleTests`), the seam is static evidence (`PatchContractTests` resolves both
  patched methods against the game assembly), and what a player actually sees is the user's
  release-cycle acceptance.
- The lead window's real feel under two-machine latency, the ramp's look on the HUD (the
  `curTimeScale` indicator and the speed sound switch at the ramp's END, not during it), and
  the second operator's screen are outside the simulation harness.
- A two-press burst can dip the clock briefly before the newest verdict lands (the ramp is
  retargeted, never stuck). Accepted as the ruling's "rare rollback", not a defect.
- Settlement is bounded by the ANSWER, not by a timer: every host path answers a request (the
  start gate's branch now answers too — closed in this cycle after the adversarial review
  found it silent), the transport is reliable, and the initiator never re-sends. The pending
  state therefore has exactly two backstops — the next 5 s resend and the session end — and
  the second covers a host that has gone away. No test can reach an unanswered request (the
  adapter is not constructible in the test host), so this invariant is static evidence plus
  the log line every answer writes.
- Physical deployment and dual-client acceptance remain the user's release-cycle action;
  development-period verification is simulation/static by rule.

## Non-goals

- Per-player time scales or a local-only clock.
- Changing the sleep gate or the start gate.
- Prediction of world events during the initiator's lead window (the lead is the accepted
  cost, bounded by one round trip).
