# Acceptance record — A null collection in a content payload must mean "none"

- Ticket: `mod-payload-null-collection-tolerance` — verdict: **moved to `done/`**
- Batch: 20261012-c — offline batch; scope and plan: docs/evidence/acceptance/20261012-c-scope.md
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: CasualtiesUnknownOnline.dll 0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91 — deployed and hash-verified in this run (tools/verify-deploy.ps1 exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (exit 0; its own last line reads `RESULT: OK - a full two-client run is possible`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `swap-needed=false`), both captured into the batch directory before the build; no client capability (steam, sandboxie, sandbox-alt, hotrepl, capture, input, logs) is consumed by this run
- Artifacts: none — the run's logs and TRX results are under the directory named by acceptance-artifacts-dir

The rows below are the ticket's own acceptance table, in its order, including the table's retired line, which
is kept and judged rather than dropped. Every row is load-time and client-local and is judged by the suites
this run re-ran.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Every collection member of every mod-built declaration means "none" when a mod assigns null in C# | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModNullCollectionRuleTests.NullCollectionWrite_IsNone` — `Passed` in this run as 26 parameterised cases (one per member), read from the run's outcome index |
| 2 | The census reaches every collection member of the assembly, so a new member cannot arrive without the rule | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModNullCollectionRuleTests.Census_CoversEveryModAuthoredDeclarationTheAssemblyBuilds` — `Passed` in this run; its pinned count is written down where the census lives (`tests/CasualtiesUnknownOnline.Tests/Mods/ModNullCollectionRuleTests.cs`: `private const int ExpectedConstructedMemberCount = 26;`), and this run's index carries exactly 26 `NullCollectionWrite_IsNone` rows |
| 3 | The declarations a mod can only build through a constructor follow the same rule | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModNullCollectionRuleTests.ParameterObjectDeclarations_TreatNullAsNone` — `Passed` in this run |
| 4 | A definition with every collection member null still BINDS | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.ModContentNullCollectionBindingTests.Item_EveryCollectionNull_Binds` — `Passed` in this run; the row's other providers are the same suite's `Liquid_EveryCollectionNull_Binds`, `LiquidTile_EveryCollectionNull_Binds`, `Building_EveryCollectionNull_Binds`, `Tile_EveryCollectionNull_Binds`, `Status_EveryCollectionNull_Binds`, `Moodle_NullCollections_AreRefusedOrBoundByTheirOwnRule` and `Structure_NullCollections_AreRefusedOrBoundByTheirOwnRule`, all `Passed` |
| 5 | A member that is genuinely required is refused with the message that says why, never with the binder's logged exception | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.ModContentNullCollectionBindingTests.Item_AnimationWithoutFrames_IsRefusedByItsOwnRule` — `Passed` in this run; the row's other three halves are the same suite's `Recipe_WithoutIngredients_IsRefusedByItsOwnRule`, `Structure_NullCollections_AreRefusedOrBoundByTheirOwnRule` and `Moodle_NullCollections_AreRefusedOrBoundByTheirOwnRule`, all `Passed` |
| 6 | No provider normalises a collection any more | machine | pass | A read of every provider under `src/CasualtiesUnknownOnline.GameAdapter/Content/` in this run finds no `?? []` / `??= []` normalisation left in them; the rule's own census is green — `file:tests/CasualtiesUnknownOnline.Tests/Mods/ModNullCollectionRuleTests.cs` — "and this suite is its census". That census plus the binder suite is what the ticket names as failing if the rule is lost, and both are green in this run (rows 1–4 above) |
| 7 | *(retired)* rows 1-5 of the previous revision: an explicit nil in a payload, an omitted element, a null write, a payload we build staying free of nils, and the payload-member census | machine | pass | `file:docs/contracts/abstractions-api-baseline.txt` — its tombstone for `CasualtiesUnknownOnline.Abstractions.ModStatusUpdate.ToPayload()` reads "CUO's status frame is a model value now". The retired row's own claim is that the contracts it measured are gone, and this run reads that in the committed tree: no `ToPayload`, `FromPayload`, `DataContract` or `ModPayloadCodec` remains anywhere under `src/`, so there is no decoded object graph and no nil to normalise |

## Residuals for the user

None. Every row is decided by this run's own test outcomes or by inspection of the committed tree; no row
asks for a person's judgement.

## Limits

- **No client was started**, so this run produced no rendering, no frame, no live log and no in-process
  probe. Every row is decided by test output or by inspection of the committed tree.
- **The ticket records counts and dates from the cycle that wrote it** (27 members, "26 rows", "one fewer
  than the 27 below"). Those historical numbers were not re-measured; the durable outcome was — this run's
  own index carries 26 `NullCollectionWrite_IsNone` cases, and the census's own file states why the count
  moved from 27 to 26 (the moodle request's `Payload` is a value, and a value is not a collection).
- **The retired line (row 7) is judged on the state the tree is in now, not on the old payload shapes.** The
  ticket's own narrowing notes already retire the decode half; this record only confirms that the contracts
  that half measured are absent from the committed tree. Nothing is re-measured about the old shapes.
- **The provider paths that need Unity objects** (runtime template construction, `CustomComponentAttach.Attach`,
  `Resources.Load` of animation frames) are not executed by any suite, exactly as the ticket's `Limits` state;
  the rule is proven at the boundary those paths share.
- `ModContentDefinition.Data`'s accounted-for exception, the interface members outside the census, and the
  framework's own file formats stay outside this rule, as the ticket records.
