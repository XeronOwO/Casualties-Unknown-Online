# Delivery checklist

- [x] Design comprehension — the launcher read before the change — evidence: `launch-clients.ps1` lines 248-261, 400-419
- [x] Native parity — the box Steam rule comes from the user, not from a guess — evidence: m52114/m52115
- [x] Native parity probe — the box never starts Steam — evidence: `Ensure-BoxSteam` is a no-op now
- [x] Entry mapping — the preflight that demanded Steam is gone — evidence: the diff
- [x] Mechanism inventory — the suite calls the launcher without the flag — evidence: `SessionDriver.RelaunchAsync`
- [x] Whole-family audit — the restart path calls the same function — evidence: `Restart-BoxGame`
- [x] Self-check table — tooling only, no product code — evidence: the diff
- [x] Verification design — a pair launch must print `up (endpoints answered)` and no Steam line — evidence: the next run
- [x] Plan approved by the user — never start Steam in a box — evidence: m52114
- [x] Structure review done — one function, one place — evidence: the diff
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: unit 6665/0, gates 834/0