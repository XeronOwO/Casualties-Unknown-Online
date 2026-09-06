# Global adaptive report rate Stage 4 — remaining high-frequency domain streams self-check

Owner cycle: backlog `docs/backlog/review/global-adaptive-report-rate-stage-4-high-frequency-domains.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Interval-based adaptive streams | `AdaptiveStreamProfile.BaseIntervalMs` / `MaxIntervalMs` plus `AdaptiveRatePolicy.GetEffectiveIntervalMs` let low-frequency full/fallback streams (1 s or 5 s) adapt without an integer-Hz floor. |
| 2 | Rate-service interval queries | `AdaptiveStreamRateService.GetEffectiveIntervalMs` returns the most constrained peer interval for broadcasts and delegates `GetSendIntervalMs` through it; `Reset()` clears both Hz and interval caches. |
| 3 | Catalog additions | `WorldItemMoveStream` (10 Hz), `WorldItemSnapshotStream` (5 s), `FluidRegionDiffStream` (10 Hz), `FluidRegionFullStream` (1 s), `TraderStateStream` (5 s) added with `LatestWins` semantics and declarative byte budgets. |
| 4 | Fluid observation separation | `FluidRegionMsg.FullViewport` is a sender-only non-wire hint; `PacketSender` classifies diff/full into `WirePayloadType.FluidRegionDiff` / `FluidRegionFull` so the two adaptive cadences do not cross-pollute, including full-size diff edge cases. |
| 5 | Item stream integration | `ItemPositionAuthority` queries the shared service for both the 10 Hz movement stream and the 5 s periodic snapshot; the adaptive peer set matches the real send fan-out (Handshaken guests). |
| 6 | Fluid stream integration | `FluidSimulationAuthority` queries the shared service for the 10 Hz diff and 1 s full cadences; `FluidWorldSync.ResetSessionState` resets both authority and kernel-summary timers. |
| 7 | Trader fallback integration | `TradeStateSync` queries the shared service for the 5 s fallback cadence; `TradeChannel.SendTraderState` stays reliable so the fallback and immediate action broadcasts preserve FIFO order on the shared TraderState message. |
| 8 | Session reset | `ItemPositionAuthority`, `FluidSimulationAuthority`, `FluidRegionKernelSync`, and `TradeStateSync` clear their session timers/views/pending state from `GameAdapterSessionBinding.OnSessionEnded`, preventing stale adaptive deadlines from delaying a new session. |

## 2. Whole-family audit

- `AdaptiveSync`: catalog (+5 streams), wire mapper, profile, policy, rate service updated in one pass; interval and Hz modes share the same pressure/priority factors.
- `PacketSender` / `NetworkTraffic`: fluid direct-message traffic is now sub-classified; medical sub-classification unchanged.
- Item, fluid, and trader senders now all consult the same `AdaptiveStreamRateService` rather than hard-coded constants.
- No wire/protocol `NetMsg` or wire version change; the `FullViewport` property is intentionally not a `ProtoMember` and does not alter the wire shape.

## 3. Verification

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2481 runtime tests + 16 normative gates passed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| Focused Stage 4 tests | interval policy/rate-service tests, catalog/wire mapper tests, fluid classification (including full-size diff edge), contract tests for adaptive injection + reset surfaces |
| Independent adversarial self-check | Two fresh-context passes; findings (trader reliability/ordering, session reset leaks, fluid observation cross-pollution, contract-test weakness, byte-budget/MaxInterval conflict, docs) addressed and re-reviewed. |
| Deployment | `tools/deploy.ps1` deployed to the real game directory; SHA-256 hashes of all six CUO DLLs match the build output. |

## 4. What was NOT changed

- No anti-cheat/malicious-rate policing.
- No prediction/interpolation.
- No new medical/other future stream integration.
- No host-migration or save changes.
- No wire/protocol version change.
