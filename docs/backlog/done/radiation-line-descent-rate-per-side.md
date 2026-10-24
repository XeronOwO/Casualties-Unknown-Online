# The radiation line descends at each client's own rate between the host's resends

- Status: Done — FIXED and read on the machine (2026-10-24, deployed `0.1.0+8b5baee6`): with the member's body forced
  unconscious and the host's conscious, both lines advanced at the host's rate (delta 6.142 vs 6.174 over the same
  window) instead of the member's own `0.3` units/s. Record
  `docs/evidence/acceptance/radiation-rate-and-panel-20261024-h.md`. **User finding (2026-10-24)**: watching a member's
  screen during a run, the radiation line's descent rate looked wrong. The reading below says it was not the rate the
  host decided, but the rate the member's OWN body gave the native formula between the host's 5 Hz resends.
- Priority: Medium
- Category: World state sync / radiation line
- Source: the user's observation on the deployed artifact (`0.1.0+d0b38521`), attributed in the source below.

## The mechanism, read from the code

The line's descent is native and per-side: `RadiationLine.Update` advances a private `timeGone` by
`Time.deltaTime * (conscious ? 1f : 0.2f) * 1.5f` and derives the position from it
(`reversing/Assembly-CSharp/Assembly-CSharp/RadiationLine.cs`). The multiplier is the LOCAL body's state.

CUO syncs the line as world state: `RadiationLineSync` has the host broadcast `(Active, TimeGone)` at 5 Hz
(`BroadcastIntervalSeconds = 0.2f`), and a member applies that absolute value (`RadiationLineTable.Apply`). Between two
resends the member keeps running its own `RadiationLine.Update`, i.e. with **the member's own body's multiplier**:

- the member is conscious while the host's body is not → the member's line advances 5× too fast for up to 200 ms, then
  snaps back to the host's value;
- the member is unconscious (or dead, which is `conscious == false` too) while the host is conscious → the line crawls
  at 0.3 units/s and then jumps forward at the next resend.

That is a visible sawtooth in the descent rate, and it is what the user saw. It is NOT an intentional design: the 5 Hz
resend was chosen to keep "the guest's local-continued state within a small fraction of a unit"
(`RadiationLineSync`'s own comment), which only holds when both bodies share the multiplier.

## The fix the reading points at

The wire carries `(Active, TimeGone)` but not the RATE the host is using, so the member cannot continue the host's motion
correctly; it continues its own. One value fixes it: the host sends its own multiplier (or the derived rate) in
`RadiationLineStateMsg`, and a member's local pass advances `timeGone` from the last received value at the **host's**
rate rather than its own body's. The local body's radiation/eye effects stay per-side (they are the member's own
physiology), which is why the native `Update` keeps running locally.

Cheaper alternative if the wire change is unwanted: on a member in an active session, skip the local advance entirely and
extrapolate from the last received value at a fixed 1.5 units/s (the conscious rate). That removes the sawtooth too and
is wrong only while the host's body is unconscious, a shorter-lived window than a member's.

## Acceptance

A two-client run: place both bodies at the same depth under an active line, make ONE side unconscious (`body-force
consciousness=0`), and read the line's `timeGone` on both clients over a few seconds (a `radiation-line-state` recipe
reading `RadiationLine.line`'s `active`/`timeGone`). Before the fix the two diverge by the multiplier and snap every
200 ms; after it they stay within the resend delay's worth of motion.
