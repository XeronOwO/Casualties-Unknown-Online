# Delivery checklist

- [x] Design comprehension — the pair setup read; every relaunch site removed — evidence: `SessionDriver`
- [x] Native parity — the case passes with no restart path — evidence: `block-idle-drift PASS`
- [x] Native parity probe — the run names the client state when a join fails — evidence: the exception text
- [x] Entry mapping — `EnsurePairAsync`, `JoinMemberAsync`, `RelaunchAsync`, `RecoverPairAsync` — evidence: the diff
- [x] Mechanism inventory — a restart is a deploy/source-change act — evidence: the rule
- [x] Whole-family audit — no `RelaunchAsync` caller remains — evidence: the grep
- [x] Self-check table — test-only change — evidence: the diff
- [x] Verification design — a broken pair now fails the case with its state — evidence: the exception text
- [x] Plan approved by the user — never restart the guest without cause — evidence: m52221/m52262
- [x] Structure review done — one place, documented — evidence: the diff
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: suite build 0; gates 834/0