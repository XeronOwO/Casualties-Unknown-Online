# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the cut's own transient policy is the seam (`WorldTransientPolicy`), so the fix adds a class to that table with a verdict and a reason instead of a wait inside the binder.
- [x] Native parity — evidence: no game behaviour changes; the fix only decides when CUO takes its own snapshot, and the row's judgement still comes from the game's archive and the member's own tree.
- [x] Native parity probe — evidence: `MemberSnapshotFreshnessTests` four facts on the age rule (fresh, stale, silent, written, forgotten) with an injected clock, and the row's own red/green pair measured 2026-11-24 (`.acceptance/savecont-2` red, `.acceptance/savecont-diag` green with a 1.5 s wait).
- [x] Entry mapping — evidence: the wait enters through `WorldCutTransientProbe.Capture` → `WorldCutTrigger.TryCollectTransients`, the seam every other in-flight class already uses, so the cut defers by its own deadline and names what it waited for.
- [x] Mechanism inventory — evidence: `MemberSnapshotFreshness` (new owner of the age), `ICharacterDataControl.StaleMemberSnapshots`, `CharacterDataStore` marking reports and writes, the policy row `member-character-report`, and the probe that counts it.
- [x] Whole-family audit — evidence: every member row a cut writes goes through the same snapshot (host rows use the host's own character data), so the class is placed at the table rather than at one row's call site.
- [x] Self-check table — evidence: the three questions are answered — the row's failure is a stale write (measured both directions), the count only includes members that reported within six seconds (so a stalled client cannot hold the cut), and the wait is bounded by the trigger's existing deferral deadline.
- [x] Verification design — evidence: failures name the class (`member character report(s) due`) in the cut's own deferral log and in the report's waiting list; the acceptance is the `save-continue` row's archive read.
- [x] Plan approved by the user — evidence: the user's standing order is to fix the product at its owner when a rebuilt row is honestly red, and the ticket carries the three candidate fixes with the recommended one implemented.
- [x] Structure review done — evidence: the store stays the owner of its snapshot age, the policy owns the verdict, and the deferral deadline stays with `WorldCutTrigger`; no new message, no second copy of the snapshot.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6760/6760, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: the change needs a deployment; `deploy.ps1` and the `save-continue` row follow this commit in the same work item, and the ticket records that reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
