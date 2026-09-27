# Acceptance record — Split IGameAdapter into capability ports

- Ticket: `adapter-capability-ports` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Behaviour-preserving refactor: no call site changes meaning, and the adapter's own tests plus the patch-contract checks stay green. | machine | pass | main suite 4482/4482 passed with 0 not executed (`trx-outcomes.txt`); `AdapterCapabilityPortShapeTests` 20 passed; the patch-contract classes stayed green — `PatchContractTests.EnemyCombatPatchSet_IsComplete` and `PatchContractTests.CrystalMimicPatchSet_IsComplete` passed |
| 2 | A shape gate fails when a new method is added back onto the aggregate interface, so the old surface cannot regrow while the ports are being introduced. | machine | pass | `AdapterCapabilityPortShapeTests.Aggregate_DeclaresNoMemberOfItsOwn` asserts the real aggregate declares nothing; `AdapterCapabilityPortShapeTests.AggregateCheck_FlagsAMemberDeclaredOnTheComposition` is the matcher's own contract with declared samples (a composition declaring a method and an event is flagged, a clean one is not) — both passed; `AdapterCapabilityPortShapeTests.Aggregate_ComposesExactlyThePinnedPorts` and `AdapterCapabilityPortShapeTests.Composition_CarriesExactlyEighteenMembers` passed |
| 3 | The InternalsVisibleTo surface is reviewed in the same change and reduced where a port can carry the read instead; anything that stays records why. | machine | pass | `src/CasualtiesUnknownOnline.Runtime/AssemblyInfo.cs` carries the grants (CasualtiesUnknownOnline.Tests, CasualtiesUnknownOnline.GameAdapter) and the plugin project carries none; the read a port could carry is port-carried — `AdapterCapabilityPortShapeTests.Port_DeclaresExactlyItsPinnedMembers` pins the capability-report port to its one member and passed, the same pin the port's single registration is asserted by in `AdapterCapabilityPortShapeTests.EveryPort_IsRegisteredExactlyOnceFromTheAdapterSingleton`; the census (22 internal names) and the reason for keeping the grant are recorded in the register's row 212 of `docs/decisions/active.md` |

## Residuals for the user
None.

## Limits
No client, no rendering and no deployed artifact were exercised; every row is a test outcome or a file fact. Row 3's "reviewed and reduced, anything that stays records why" half is decided by file inspection of `src/CasualtiesUnknownOnline.Runtime/AssemblyInfo.cs` and the decision-register row that records the census and the reason, not by a test. The red/green mutation controls the ticket describes (re-applying `ProbeRegrowth()` and deleting a registration for one run) were not re-run by this batch; what decides row 2 here is the shipped matcher contract plus its declared samples. The ticket's own test counts and file sizes are from its cycle, not from this batch.
