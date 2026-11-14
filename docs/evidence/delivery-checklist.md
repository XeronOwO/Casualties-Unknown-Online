# Delivery checklist

- [x] Design comprehension — the join path read before the change — evidence: `SessionDriver.JoinMemberAsync`
- [x] Native parity — the member joins through its own game paths — evidence: the leave-world branch
- [x] Native parity probe — the run passed with no relaunch — evidence: `member the suite joined …` (no suffix)
- [x] Entry mapping — the pre-run step no longer restarts the client — evidence: the diff
- [x] Mechanism inventory — a restart is a deploy/source-change act only — evidence: the rule
- [x] Whole-family audit — both relaunch sites are gone — evidence: the diff
- [x] Self-check table — test-only change — evidence: the diff
- [x] Verification design — a failing join now names the state and fails the case — evidence: the exception text
- [x] Plan approved by the user — never restart the guest without cause — evidence: m52221
- [x] Structure review done — one place, documented — evidence: the diff
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: suite build 0; gates 834/0