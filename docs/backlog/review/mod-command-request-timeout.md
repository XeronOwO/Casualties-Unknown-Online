# Dropped mod command requests settle on a deadline instead of waiting for the session to end

- Status: Review — landed 2026-09-19 (decision 197). Awaiting the final unified acceptance pass.
- Priority: Low
- Category: Network / sync coverage / mod API
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row N8)
- Related: `docs/decisions/active.md` 197, `docs/api/mod-api.md` §4a/§4b,
  `docs/evidence/selfchecks/mod-api/mod-service-split-selfcheck.md`,
  `src/CasualtiesUnknownOnline.Abstractions/IModCommands.cs`,
  `src/CasualtiesUnknownOnline.Abstractions/IModCommandResult.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModCommandService.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModCommandPolicy.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModLifecycle.cs`

## The gap (as it was)

The mod domain drops frames on purpose: the per-sender token bucket refuses over-burst frames with
a log and never queues them (`ModRateLimitPolicy`), a command frame that fails the shape caps or
arrives from a non-handshaken sender is dropped the same way, and neither the request nor the result
is ever re-sent. For `ModMessage` that is an accepted loss. For `ModCommandRequest` it had a side
effect on the requester: `ModCommandAdapter.TryExecute` registered a pending callback keyed by
request id, and only the result frame or the session-end fail-all resolved it — so a request the
host dropped, or whose result was lost, left the caller's callback unresolved until the session
ended, and nothing bounded the map those entries accumulated in.

The recorded red (pre-implementation tree, with the two policy values declared but not yet read by
any code): `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~ModCommandTests"`
reported 3 failed / 15 passed — the burst case accepted 35 pending requests instead of a cap, and
the dropped and lost cases invoked no callback at all after the clock passed the deadline. The same
filter reports 18 passed / 0 failed with the implementation in place.

## What landed

- **A requester-side deadline** (`ModCommandPolicy.CommandRequestTimeoutMs`, 10 s). Every guest
  request stores its command name, its deadline and its callback; the per-frame pump
  (`ModCommandService.PumpPendingTimeouts`, called from `ModLifecycle.Update`, the clock read ONCE
  per frame) settles each entry at or past its deadline with `Success=false`, the reason
  `command request timed out after 10000 ms`, and one WARN naming the mod, the command and the
  request id. The expired entries are collected before any removal and each entry is removed BEFORE
  its callback runs, so a result that arrives later can no longer settle a request twice.
- **A bounded pending map** (`ModCommandPolicy.MaxPendingRequests`, 32, per mod). A call over the
  cap is refused at the sender (`false` + WARN, like every other sender-side refusal: no request is
  sent and no callback is invoked), so a mod that fires without waiting cannot grow the map without
  bound. The sweep drains it, so the cap is a bound on the outstanding set, not a queue.
- **No per-frame NACK for a host-side drop.** The host still answers nothing for an over-burst, an
  over-cap or a non-member frame (unchanged), because answering every dropped frame would let a
  flooding peer drive the host's OUTBOUND traffic past the token bucket that exists to bound one
  member's consumption. The deadline is the requester's own answer, so a drop and a lost result
  reach the caller as the same observable reason.
- **Both failure paths name the request they settle.** The timeout result carries the real request id
  and command name, and the session-end `FailPending` now reports them too instead of `0` / `""`, so
  a callback can tell which request failed.
- **The success path is untouched**: a delivered result removes its entry, and a later sweep never
  touches a settled request (the entry is gone, so there is nothing to settle). Host-local calls stay
  synchronous — the host invokes its callback in place and never registers a pending entry.
- **The mod-facing contract moved with the behaviour.** `IModCommands.TryExecute` and
  `IModCommandResult` (the `Abstractions` assembly, the only one a mod references) named only the
  session-end failure and still called the request/result channel reliable; both now state that
  delivery is not guaranteed, that the request deadline settles a dropped or lost request with
  `Success=false` and a timeout reason, and that an over-cap call returns `false` without invoking the
  callback. This gap was found by the cycle's independent adversarial pass — the binding document had
  been updated while the interface it mirrors had not.
- **No wire member changed** (protocol stays 34) — a fact about the mechanism, never a design input
  (decisions 137/188).

## Acceptance

| # | Scenario | Evidence |
|---|---|---|
| 1 | The host rate-limits a command request: the requester's callback fails with a reason, no silent wait | `ModCommandTests.RateLimitedRequest_FailsOnTheDeadline_WithAnObservableReason` spends the host's per-guest burst on requests that resolve, sends one more (the host drops it, nothing comes back), then asserts the deadline settles it with `Success=false` and a `timed out` reason |
| 2 | A result frame is lost: the callback fails on the deadline, not at session end | `ModCommandTests.RequestWithNoResult_FailsOnTheDeadline_NotAtSessionEnd` delays the result far past the deadline, asserts the entry is still pending one millisecond before it, that the sweep at the deadline fails the callback, and that the late result is dropped instead of settling a second time |
| 3 | A normal request/result is unchanged | `ModCommandTests.SettledRequest_IsNeverReplacedByALaterDeadlineSweep` (a delivered success is never replaced by a later sweep) plus the untouched happy-path cases `ModCommandTests.HostLocalCommand_RunsSynchronously_WithHostRequester` and `ModCommandTests.GuestRequest_ExecutesOnlyOnHostCopy_AndReturnsDirectedResult` |
| 4 | A session ending with pending requests still fails them all | `ModCommandTests.SessionEnded_SettlesPendingRequestWithFailure` (pre-existing, still green: the reason is `session ended` and the late result cannot overwrite it) |
| 5 | A burst of dropped requests leaves the pending map bounded | `ModCommandTests.PendingBurstOverTheCap_IsRefusedAtTheSender_AndTheMapDrainsOnTheDeadline` (35 calls with no result coming back: exactly the cap accepted, the surplus refused without a callback, every accepted entry failed by the sweep, and a fresh request accepted afterwards because the map drained) |
| 6 | `ModStatusTransport` (and `ModMessage`) are documented as loss-tolerant, not silently reliable | `docs/api/mod-api.md` §4a states the drop-and-no-backfill semantics for both; the matrix row N8's accepted-loss entry records the reason |

## Family sweep

Every path in the mod domain where a frame can be dropped, and its verdict after this cycle:

| site | verdict |
|---|---|
| `ModCommandRequest` (guest → host) | fixed here: the requester's deadline settles it, and the pending map is capped |
| `ModCommandResult` (host → guest) | covered by the same deadline; a result for an unknown request id is dropped with a log (unchanged) |
| `ModMessage` (guest → host and host → peer/all) | accepted loss, now documented in `docs/api/mod-api.md` §4a: opaque payloads, no backfill, the next message is the recovery |
| `ModStatusTransport` (host → guests) | accepted loss, documented the same way: a projection with no per-message settlement to hang, so a dropped update costs one update |
| A host-local command call | synchronous: the callback runs in place before the call returns, so there is no pending entry and no deadline |
| Session end / framework shutdown | unchanged fail-all; it is now the last resort rather than the only one, and the deadline covers a session that stays open |

## Limits

- The 10 s deadline is a judgement, not a measurement. A host frame that stalls longer than it (world
  load, a heavy handler) makes the requester report a failure for a command the host did execute; the
  entry is gone by then, so the late result is dropped with a log. "Far above an observed round trip
  and far below the session-end fallback" is the reasoning, not an experiment.
- Drop and loss are indistinguishable to the requester by design: the host answers nothing for an
  over-burst frame, so a rate-limited request fails with the same reason as a lost result. A mod that
  needs to know whether a command ran cannot learn it from this failure.
- The cap refuses the surplus instead of queueing it: a mod that fires more than 32 requests without
  waiting gets `false` synchronously for the rest and no callback for them (the existing sender-side
  refusal contract). The cap bounds memory; it does not preserve delivery.
- The deadline is an absolute tick comparison (`deadline = now + 10 s`, then `now >= deadline`), so it
  inherits the tree's clock: `SystemTimeSource.NowMs` mirrors `Environment.TickCount`, a 32-bit counter
  that wraps after ~24.9 days of uptime. A request created in the ~10 s window before the wrap would
  not expire until the counter came back around — bounded in practice by the cap and by the session-end
  fail-all. Recorded rather than fixed here: every absolute deadline in the tree shares the property
  (`DropProtectionGuard` arms the same way), and moving `NowMs` to a 64-bit source is a tree-wide clock
  change — the GameAdapter's buffers stamp against `Environment.TickCount` — not this ticket's blast
  radius.
- Real dual-client behaviour is unverified. The deadline's real-world value, and whether a dropped
  request ever masks a visible failure, belong to the unified acceptance pass — a test harness with a
  virtual clock cannot prove either.
- `ModCommandTests` is an Integration class driving the production composition root, so the sweep is
  exercised through `ModLifecycle.Update` as it is wired in production; the timeout was not observed
  on a real client.

## Evidence

| claim | how it is proven | where |
|---|---|---|
| the fix has a recorded red | the three regression cases failed on the pre-fix tree (3 failed / 15 passed for the class; the burst accepted 35 instead of 32) | `ModCommandTests` |
| a dropped request fails the caller with a reason | the rate-limit case above, end to end over the real session stack | `ModCommandTests.RateLimitedRequest_FailsOnTheDeadline_WithAnObservableReason` |
| a lost result fails the caller on the deadline, and never twice | the delayed-result case above (one millisecond before the deadline pending, at the deadline failed, the late frame dropped) | `ModCommandTests.RequestWithNoResult_FailsOnTheDeadline_NotAtSessionEnd` |
| the pending map is bounded and drains | the burst case above (cap accepted, surplus refused, sweep settles every accepted entry, a fresh call accepted afterwards) | `ModCommandTests.PendingBurstOverTheCap_IsRefusedAtTheSender_AndTheMapDrainsOnTheDeadline` |
| a settled request is never replaced | the success case above | `ModCommandTests.SettledRequest_IsNeverReplacedByALaterDeadlineSweep` |
| the session-end path still fires | the pre-existing session-end case, unchanged | `ModCommandTests.SessionEnded_SettlesPendingRequestWithFailure` |
| a request the host ran whose answer was lost is exactly the case tested | the same case asserts the host's execution log (the command ran once) before proving the callback failed on the deadline | `ModCommandTests.RequestWithNoResult_FailsOnTheDeadline_NotAtSessionEnd` |
| the mod-facing interface contract matches the binding document | read-only comparison of the two texts: the reliability claim is gone from both, and both name the deadline settlement and the over-cap `false` | `IModCommands.cs`, `IModCommandResult.cs`, `docs/api/mod-api.md` §4b |
| the sweep runs once per frame from the lifecycle pump, on one clock reading | read-only review of the wiring (`ModLifecycle.Update` → `ModCommandService.PumpPendingTimeouts` → `ModContext.PumpPendingCommands` → `ModCommandAdapter.PumpPending`), driven by the harness pump the tests already use | `ModLifecycle.cs`, `ModCommandService.cs`, `ModContext.cs` |
| the change is green end to end | the full suite with build reports 3514 passed / 0 failed (the pre-change baseline was 3510; this cycle adds the four cases above), and the normative gate project reports 69 / 69 | `dotnet test CasualtiesUnknownOnline.slnx` |
