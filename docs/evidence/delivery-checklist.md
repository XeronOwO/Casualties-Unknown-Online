# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.

- [ ] Design comprehension
      mechanism keeps its diagnostics) and the ticket `docs/backlog/in-progress/split-block-break-remote-apply.md`,
      whose chain names `OnBlockDamaged` → `FlushPendingBlockBreak` → `ApplyRemoteReport` → `TryAccept` → `AcceptBreak` →
      `OnRemoteDamageBrokeBlock` with file:line.
- [ ] Native parity
      as before; no native call changed, and the `BlockBreak` log lines are byte-identical because the new class reuses
      `ILogger<BlockBreakSync>`.
- [ ] Native parity probe
      focused `block-sync` row on the deployed build, which drives a real break through both clients.
- [ ] Entry mapping
      `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs` (`OnBlockDamaged`, `OnRemoteBlockDamaged`,
      `DrainDeferredReports`, `OnBlockAirWrite`), and the judgement/apply sites moved to
      `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakRemoteApply.cs` (`Apply`, `AcceptBreak`,
      `OnRemoteDamageBrokeBlock`); the external callers in `WorldEventSync` and `GameRestoredWorldFactSink` are unchanged.
- [ ] Mechanism inventory
      broadcast and the air-write callback each have exactly one owner after the move; the presentation gate now pins
      `BlockBreakRemoteApply.cs` for the accepted-break half.
- [ ] Whole-family audit
      touched; the debt entry for `BlockBreakSync` is deleted and `docs/design/audit/sweep.md` counts the new file (72 → 73).
- [ ] Self-check table
      origin pins (the presentation gate's per-file count and two sync-coverage quotes), both repaired to the new file
      rather than loosened.
- [ ] Verification design
      break and the same presentation), read from the report and the `BlockBreak` log lines after deployment.
- [ ] Plan approved by the user
      without asking; the ticket's acceptance (600-line gate, debt entry gone, `block-sync` green) is the check used.
- [ ] Structure review done
      captured primary-constructor parameters (CS9124), and the class summary states which half owns the judgement.
- [ ] Build + dotnet format + dotnet test normative gates pass
      "通过: 6747" and "通过: 836" with exit 0; `dotnet format --no-restore` exit 0; the build carries only the two
      pre-existing MSB3270 warnings.

- [ ] Release-cycle deployment/acceptance: deploy and read a passing pod-team-wait with zero member errors.
- [ ] FORBIDDEN — never check this box
