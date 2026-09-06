# Global adaptive report rate Stage 3 — cumulative stream coalescing self-check

Owner cycle: backlog `docs/backlog/in-progress/global-adaptive-report-rate-stage-3-cumulative-streams.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Per-stream `BaseHz` | `AdaptiveStreamProfile.BaseHz` lets medical progress streams keep a 60 Hz frame-level baseline in `Optimal` while the rate service still uses the global 20 Hz default for the existing streams. |
| 2 | Cumulative policy support | `AdaptiveRatePolicy` now adapts both `LatestWins` and `Cumulative`; only `ReliableControl` stays untouched. |
| 3 | Medical traffic sub-classification | `PacketSender.ClassifyMedicalUpdate` assigns `MedicalInjectionUpdate`, `MedicalShrapnelPositionUpdate`, or `MedicalOperationOtherUpdate` so adaptive estimates do not cross-pollinate between medical sub-streams. |
| 4 | Reliable injection coalescing | `MedicalInjectionReportBuffer` sends the first delta of a burst immediately, accumulates subsequent deltas until the adaptive interval, and flushes before End/Cancel. All sends remain reliable. |
| 5 | Shrapnel per-piece position coalescing | `ShrapnelPositionReportBuffer` keys latest position by `(operationId, pieceIndex)`, flushes due ordinary moves as unreliable, and flushes the final buffered position before release/end/cancel as reliable. Ownership transitions bypass the buffer. |
| 6 | Shared operation id source | `MedicalOperationIdAllocator` is shared by injection, shrapnel, and other-action domains so terminal broadcasts can never clear/route a different domain's same-numbered operation. |
| 7 | Existing release semantics | Host no longer overwrites a piece's authoritative position when a native release message carries default origin coordinates. |

## 2. Whole-family audit

- `AdaptiveSync`: catalog (+2 streams), wire mapper, profile, policy, rate service updated in one pass.
- `PacketSender` / `NetworkTraffic`: medical update traffic is now separated by sub-stream instead of all mapping to one `NetMsg`.
- `MedicalOperationSessionService` / `ShrapnelOperationSessionService`: coalescing extracted into dedicated single-responsibility buffers; shared allocator prevents cross-domain id collisions.
- No wire/protocol `NetMsg` / wire version change; only new `WirePayloadType` observation values for traffic accounting.

## 3. Verification

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2462 runtime tests + 16 normative gates passed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| Focused Stage 3 tests | injection burst/coalescing, cancel flush, shrapnel per-piece coalescing, release preserve, end/cancel flush, classification, catalog/policy/mapper tests |
| Independent adversarial self-check | Three fresh-context passes; findings (release reset, buffered final position loss, wire classification cross-pollution, per-operation buffer overwrite, cross-domain id collision, terminal flush reliability) addressed and re-reviewed. |
| Deployment | `tools/deploy.ps1` deployed to the real game directory; SHA-256 hashes of all six CUO DLLs match the build output. |

## 4. What was NOT changed

- No item/fluid/trader stream integration (Stage 4).
- No anti-cheat/malicious-rate policing.
- No prediction/interpolation.
- No remote-medical feature work; only adaptive rate/coalescing mechanism.
