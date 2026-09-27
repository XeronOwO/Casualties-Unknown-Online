# Acceptance record — Two censuses that drifted from reality: the catalogue's unreferenced keys, and the selfcheck MANIFEST

- Ticket: `catalogue-and-manifest-census-drift` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Declare a catalogue key nothing reads → the gate fails and names the key. | machine | pass | `LocalizationCatalogueGateTests.EveryDeclaredKey_IsReadByProductSource` passed on the tree and its orphan sample `LocalizationCatalogueGateTests.TheGate_FlagsADeclaredKeyWithNoReader` passed in the gate suite 288/288; the census floor is held by `LocalizationCatalogueGateTests.TheCensus_MeetsItsFloor` |
| 2 | Read a key the catalogue does not declare → the gate fails and names the key. | machine | pass | `LocalizationCatalogueGateTests.EveryKeyTheSourceReads_IsDeclared` passed on the tree and its sample `LocalizationCatalogueGateTests.TheGate_FlagsAReadKeyThatIsNotDeclared` passed; both language tables stay in step through `LocalizationCatalogueGateTests.BothLanguageTables_DeclareTheSameKeySet` |
| 3 | Read a key through a helper or an interpolated key space → it counts as a read, so a live key is never swept; the 73-key deletion left every live key behind. | machine | pass | the three shapes are asserted by `LocalizationCatalogueGateTests.TheCensus_ReadsAKeyHandedToAKeyCarryingHelper`, `LocalizationCatalogueGateTests.TheCensus_ReadsAnInterpolatedKeySpace` and `LocalizationCatalogueGateTests.TheCensus_ReadsAKeyReturnedByADeclaredHelper`, all passed; the "no live key was swept" half is the read-side gate of row 2 — `LocalizationCatalogueGateTests.EveryKeyTheSourceReads_IsDeclared` passed over `src/`, so a live key the deletion took would have failed there |
| 4 | Add a self-check file without a MANIFEST row → the gate fails and names the file. | machine | pass | `SelfcheckManifestGateTests.EverySelfcheckFile_HasExactlyOneManifestRow` passed on the tree, with its samples `SelfcheckManifestGateTests.TheIndex_ReportsAFileWithoutARow`, `SelfcheckManifestGateTests.TheIndex_ReportsARowWithoutAFile`, `SelfcheckManifestGateTests.EveryRow_NamesASelfcheckFileThatExists` and `SelfcheckManifestGateTests.TheManifest_NamesTheDirectoryItIndexes` passed; the index floor is `SelfcheckManifestGateTests.TheIndex_MeetsItsFloor` |
| 5 | Build, tests, gates, format → green. | machine | pass | the run's build log records 0 warnings / 0 errors; its gate-suite log records 288/288 with 0 not executed; its main-suite log records 4482/4482 with 0 not executed; the run's format step exited `0` (`evidence-run.log`; the tree-unchanged half is the before/after comparison in `format-evidence.txt`) |

## Residuals for the user
None.

## Limits
No client and no rendering; the two censuses are text scans of the repository and are decided entirely by the gate suite and the run results above. Every row is a test outcome except row 5's build/format half, which is the run's own exit codes. The ticket's selfcheck sheet and its cycle numbers are not used as evidence here. The gates' own stated blind spots (an accessor reached through an unresolvable receiver; the MANIFEST pass being a presence/uniqueness triage rather than a re-audit of sheet contents) are the ticket's recorded Limits and are not re-measured by this batch.
