# Global adaptive report-rate flow control — Stage 4: remaining high-frequency domain streams

- Status: Review
- Priority: Medium
- Category: Network / adaptive sync / flow control
- Parent: `../todo/global-adaptive-report-rate-flow-control.md`

## Objective

Extend the global adaptive flow-control framework from player/enemy/tutorial
and medical streams to the remaining frequent, loss-tolerant domain streams:

1. **World item movement stream** (host → guests, 10 Hz absolute overwrite).
2. **World item periodic full-table snapshot** (host → guests, 5 s absolute
   overwrite keyframe).
3. **Fluid region diff stream** (host → guests, 10 Hz absolute RLE overwrite).
4. **Fluid region full-viewport reconciliation stream** (host → guests, 1 Hz
   absolute RLE overwrite).
5. **Trader-state periodic fallback** (host → guests, 5 s full-state
   overwrite; the immediate action broadcast remains reliable).

No wire-protocol version change: the existing NetMsg/KernelEnvelope payloads
stay the same; only local send cadences change.

## Design decisions

- `AdaptiveStreamProfile` gains an interval-based mode (`BaseIntervalMs`,
  optional `MaxIntervalMs`) in addition to the Hz-based mode. Low-frequency
  reconciliation/fallback streams (5 s item snapshot, 1 s fluid full, 5 s
  trader fallback) cannot be expressed as whole Hz; the rate policy applies the
  same pressure/priority factor to the base interval so pressure lengthens the
  interval instead of being clamped by a 1 Hz floor.
- `AdaptiveStreamRateService` exposes `GetEffectiveIntervalMs` and delegates
  `GetSendIntervalMs` through it. Broadcast interval queries pick the most
  constrained (longest) peer interval; empty peer sets return the optimal base.
- Fluid diff and full-viewport frames are sent as the same `NetMsg.FluidRegion`
  payload, so `FluidRegionMsg.FullViewport` is a sender-only, non-wire hint
  (not a `ProtoMember`) and `PacketSender` classifies each frame into separate
  observation-only `WirePayloadType` values (`FluidRegionDiff`,
  `FluidRegionFull`); this keeps the two adaptive cadences from cross-polluting
  each other's bandwidth estimates, including full-size diff edge cases.
- Stream catalog additions:

| Stream | Mode | Direction | Base | Max interval | Transport |
|---|---|---|---|---|---|
| `WorldItemMoveStream` | `LatestWins` | host → guests | 10 Hz | — | unreliable |
| `WorldItemSnapshotStream` | `LatestWins` | host → guests | 5000 ms | 30000 ms | unreliable |
| `FluidRegionDiffStream` | `LatestWins` | host → guests | 10 Hz | — | unreliable |
| `FluidRegionFullStream` | `LatestWins` | host → guests | 1000 ms | 10000 ms | unreliable |
| `TraderStateStream` | `LatestWins` | host → guests | 5000 ms | 30000 ms | reliable (cadence-adapted only) |

- `TradeChannel.SendTraderState` (the periodic fallback path) stays reliable.
  The fallback and the immediate action broadcast share the same `TraderState`
  message with no sequence/version, so reliable FIFO order is required to
  prevent an older fallback from overwriting a newer interaction result; the
  adaptive governor only changes how often the fallback is sent, never drops or
  reorders it. `BroadcastTraderState` (immediate action broadcast) is likewise
  not adapted.
- The Game Adapter domain owners (`ItemPositionAuthority`,
  `FluidSimulationAuthority`, `TradeStateSync`) now query the shared
  `AdaptiveStreamRateService` for their next send cadence instead of hard-coded
  constants.

## Non-goals

- No anti-cheat/malicious-rate policing.
- No prediction/interpolation.
- No wire/protocol version change or new NetMsg.
- No host-migration or save changes.
- No new medical/other future stream integration.

## Verification

- Full solution build: 0 warnings / 0 errors.
- Full test suite: 2481 runtime tests + 16 normative gates passed.
- `dotnet format`: clean.
- Focused tests: interval policy/rate-service, catalog/wire mappings, fluid
  diff/full classification (including full-size diff edge), and Stage 4
  adapter contract/reset tests.
- Independent adversarial self-check: two fresh-context passes; findings
  (trader reliability/ordering, session reset leaks, fluid observation
  cross-pollution, contract-test weakness, byte-budget/MaxInterval conflict,
  docs) addressed and re-reviewed.
- Deployment: `tools/deploy.ps1` deployed to the real game directory; SHA-256
  hashes of all six CUO DLLs match the build output.
- Self-check: `docs/evidence/selfchecks/protocol/global-adaptive-report-rate-stage-4-high-frequency-domains-selfcheck.md`.

## Remaining before final unified acceptance

- Real dual-client acceptance remains the user's final unified acceptance pass.
