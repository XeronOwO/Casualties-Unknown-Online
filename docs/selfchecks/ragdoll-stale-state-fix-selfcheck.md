# Ragdoll stale-state / clone-creation race fix self-check

Owner cycle: backlog "Host ragdoll-key collapse not visible on guest (guest sees
host standing)". The original `CharacterRagdoll` one-shot (NetMsg 120 / PV50)
was not sufficient in two race windows that left the guest's clone standing
while the host was locally ragdolled.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Reliable ragdoll one-shot | `CharacterRagdollMsg` (NetMsg 120) — sent from `PlayerCameraHandleInputPatch`'s standing → collapsed transition. |
| 2 | Unreliable 20 Hz entity state | `EntityStateMsg.Standing` bit — the fallback continuous pose flag; delivered on an unreliable channel with a sequence gate. |
| 3 | Remote clone pose application | `SessionStatePump.Apply` previously wrote `body.standing = entity.Standing` and drove the lying clip from `LyingPose`. |
| 4 | Clone-creation race | `CharacterRagdollSync.OnReceived` previously dropped the one-shot when `RemotePlayerRenderer` had not created the owner's clone yet. |
| 5 | Stale snapshot overwrite race | A reliable one-shot can arrive while an older `Standing=true` 20 Hz snapshot is still in flight; the next pump immediately stands the clone back up. |

## 2. Root cause

The one-shot and the entity stream are independent channels. A collapse event
can arrive before the state stream's `Standing=false` snapshot, and the next
`SessionStatePump` application of the older `Standing=true` snapshot overwrites
the replay. The clone-creation path made this worse: if the event arrived
before the clone existed, it was dropped with only the unreliable stream as a
fallback.

## 3. Fix

- `RagdollPoseGate` (Runtime): a pure 500 ms suppression gate. While a ragdoll
  collapse is pending and not yet confirmed by the stream, a `Standing=true`
  snapshot is ignored for render-pose purposes.
- `RemoteBodyDriver`: added `RagdollCollapsePending`, `RagdollCollapseConfirmed`
  and `RagdollCollapseMs` latch fields.
- `CharacterRagdollSync`: queues a collapse event when the owner's clone is not
  ready, flushes after the renderer creates clones, clears the queue on session
  end, and drops any queued event when the owner leaves the world before the
  clone can appear.
- `SessionStatePump`: uses `RagdollPoseGate` before writing `body.standing`, so
  the reliable one-shot is not overwritten by a stale stream snapshot.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Stale true suppression | `RagdollPoseGate.ShouldSuppressStanding` | `RagdollPoseGateTests` stale-true/confirmation/timeout/expiry cases |
| Driver latch | new `RemoteBodyDriver` fields | `RagdollPresentationStateTests.RemoteBodyDriver_HasRagdollCollapseLatchFields` |
| Clone-creation queue | `CharacterRagdollSync.Update` + `Reset` | `RagdollPresentationStateTests.RagdollSync_HasCloneCreationFlush_AndStillReportsTheOneShot`; static wiring in `GameAdapter.Update` |
| Session-end hygiene | `CharacterRagdollSync.Reset` | wired from `GameAdapterSessionBinding.OnSessionEnded`; static evidence |
| World-exit hygiene | `CharacterRagdollSync.OnRemoteSceneChanged` | subscribed in `BindToSession`; static evidence (a queued event cannot bleed into a later re-entry clone) |
| Existing wire/star relay | unchanged | `CharacterRagdollSyncTests` (roundtrip, guest→host relay, host broadcast, guest relay) |

## 5. Verification

- L0: `RagdollPoseGateTests` +5, `RagdollPresentationStateTests` +3, plus all
  existing ragdoll/wire tests; full suite **1552 green**.
- `dotnet build`: 0 warnings / 0 errors.
- `dotnet format`; `check-architecture`; `check-event-replay`;
  `check-entity-event-dispatch` all pass.
- Development-period rule: L0 + static evidence, `no manual acceptance`.
