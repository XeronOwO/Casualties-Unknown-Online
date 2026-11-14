# Delivery checklist

- [x] Design comprehension — the pair setup read before the change — evidence: `SessionDriver.EnsurePairAsync`
- [x] Native parity — a down client is started, a live-but-stuck one is a finding — evidence: the `RelaunchAsync` body
- [x] Native parity probe — the case passes with no restart — evidence: `block-idle-drift PASS`
- [x] Entry mapping — the leave wait is one driver call — evidence: `wait-state notInWorld`
- [x] Mechanism inventory — the driver blocks in-process, so no cross-process poll — evidence: `InProcessDriver` `wait-state`
- [x] Whole-family audit — the 750 ms loop is gone — evidence: the diff
- [x] Self-check table — test-only change — evidence: the diff
- [x] Verification design — the next run must still pass and take less time — evidence: the timer
- [x] Plan approved by the user — no dead waits — evidence: m52266
- [x] Structure review done — one place, documented — evidence: the diff
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0; gates 834/0