# Acceptance record — Content registration carries its type: a typed definition instead of an opaque payload

- Ticket: `mod-content-typed-registration` — verdict: **moved to `done/`**
- Batch: 20261012-c — offline batch; scope and plan: docs/evidence/acceptance/20261012-c-scope.md
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: CasualtiesUnknownOnline.dll 0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91 — deployed and hash-verified in this run (tools/verify-deploy.ps1 exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (exit 0; its own last line reads `RESULT: OK - a full two-client run is possible`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `swap-needed=false`), both captured into the batch directory before the build; no client capability (steam, sandboxie, sandbox-alt, hotrepl, capture, input, logs) is consumed by this run
- Artifacts: none — the run's logs and TRX results are under the directory named by acceptance-artifacts-dir

This ticket has no acceptance table: its landed surface is the six-item frozen shape under `What landed`. The
six rows below are written from those six items, in their order, before judging them; each row text is the
item's own claim.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | `IModContentDefinition` carries `Id`, `Kind` and `SchemaVersion` and nothing else, and the nine content DTOs implement it while keeping their own typed members | machine | pass | `file:src/CasualtiesUnknownOnline.Abstractions/IModContentDefinition.cs` — "the address, the kind and the schema version, and nothing else". The nine definitions' own identity cases are `Passed` in this run — `CasualtiesUnknownOnline.Tests.Mods.ModItemDefinitionTests.Identity_IsFixedByTheType` and the same case in the recipe, liquid, liquid-tile, tile, building, structure, status and moodle definition suites — and the real registration stack exposes the same identity through `CasualtiesUnknownOnline.Tests.Mods.ModContentTests.BindRegistersContent_ContextExposesIt`, `Passed` |
| 2 | `TryRegister` takes the definition, replacing both byte[] overloads, so the kind travels with the object | machine | pass | `file:src/CasualtiesUnknownOnline.Abstractions/IModContent.cs` — `bool TryRegister(IModContentDefinition definition);` is the only registration entry in this run's read of the file. Both removed overloads are recorded in the contract baseline: its tombstones for `IModContent.TryRegister(string id, string kind, byte[] data)` and for `…, int schemaVersion)` read "replaced by TryRegister(IModContentDefinition definition): the kind travels with the object, so no caller can file a definition under another kind" |
| 3 | Providers claim by kind and cast, and a definition of another type filed under a claimed kind is refused the same named way a failed decode was | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.ModContentNullCollectionBindingTests.EveryProvider_RefusesADefinitionOfAnotherTypeFiledUnderItsKind` — `Passed` in this run (nine real providers, one stub definition per claimed kind); a provider suite drives the same refusal on its own kind: `CasualtiesUnknownOnline.Tests.Patching.StatusMoodleContentProviderTests.StatusProvider_RefusesADefinitionOfAnotherTypeFiledUnderItsKind`, `Passed` |
| 4 | The registry, the catalog, the ownership query, the console resource-id completion and the binder keep working on the base interface, and the registry keeps the instance the mod handed over | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModContentTests.ContentCatalog_ReadsRealModStack` — `Passed` in this run; the other readers are `CasualtiesUnknownOnline.Tests.Mods.ModContentTests.ContentOwnerQuery_ReadsRealModStack`, `CasualtiesUnknownOnline.Tests.Content.ModContentResourceLocationSourceTests.Entries_UseCanonicalIdAndTypedDisplayName` and `CasualtiesUnknownOnline.Tests.Mods.ModContentBinderTests.Binder_RoutesRealModContentThroughDi`, all `Passed`, and the stored instance is pinned by `CasualtiesUnknownOnline.Tests.Mods.ModContentTests.RegisteredDefinition_IsStoredAsTheInstanceTheModHandedOver`, `Passed` |
| 5 | Serialization stays where it crosses a boundary: the content DTOs stopped being serialization contracts, the content payload cap is gone and the deleted wrapper type stays deleted | machine | pass | `file:docs/contracts/abstractions-api-baseline.txt` — its tombstone for `CasualtiesUnknownOnline.Abstractions.ModItemDefinition.ToPayload()` reads "the content definition is registered as a typed object and is never serialized", with the same pair tombstoned for all nine DTOs and the wrapper type `CasualtiesUnknownOnline.Abstractions.ModContentDefinition` also tombstoned. This run's read of `src/` finds no `ToPayload`, `FromPayload`, `DataContract`, `MaxDefinitionBytes` or `IsValidData` left, and no `ModPayloadCodec` |
| 6 | Decision 244's member half is kept: every collection member still coalesces a null write, and a declaration answered by the mod is read through the same rule | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModNullCollectionRuleTests.NullCollectionWrite_IsNone` — `Passed` in this run as 26 parameterised cases, with `...Census_CoversEveryModAuthoredDeclarationTheAssemblyBuilds` and `...ADeclarationThatCarriesNoCollection_ReadsAsNoneAtTheReadSeam` also `Passed`; that suite's own header states it is the member half's census, which is the half this row claims |

## Residuals for the user

None. Every row is decided by this run's own test outcomes or by inspection of the committed source, contract
baseline and tests; no row asks for a person's judgement.

## Limits

- **No client was started**, so this run produced no rendering, no frame, no live log and no in-process probe;
  no row depends on the module example's live-provider proof or on any other runtime path.
- **One suite the ticket names has a different name in this tree.** The row-6 text names
  `ModPayloadNullCollectionTests`; no class of that name appears in this run's outcome index, and the census
  that carries the member half is `ModNullCollectionRuleTests` (its own header states that the rule "used to
  need two halves", that neither half has a payload to answer for now, and that "this suite is its census").
  The row is judged on its substance through that suite, and the two groups the ticket describes are read as
  the member half plus the retired decode half rather than as two live groups.
- **One sentence of the ticket is stale in this tree**: "`ModPayloadCodec` now serves `ModStatusUpdate` and the
  two status projections only". No `ModPayloadCodec` exists anywhere under `src/` in this run — the typed
  value model replaced the decode seam, as the sibling null-collection ticket records. Row 5 is therefore
  judged on its substance (a content definition is not a serialization contract), which the committed tree
  grounds; the codec's own end state is not re-measured against the ticket's wording.
- **The cycle's numbers were not re-measured**: the 27 mod-built members, the content payload's 64 KiB cap and
  the "nine DTOs" census are that cycle's own measurements. This run re-read the census that exists now — 26
  parameterised member cases, all `Passed` — and the file that states why the count moved from 27 to 26.
- **The bullets' record pointer is not claimed**: the phrase "Decision 244's member half" names an entry under
  `docs/decisions/`; the row anchors the census suite instead, and no decision entry is claimed as verified
  here.
- **The ticket's self-check table is not a second row list.** Its mechanism → evidence rows are the writing
  cycle's own map of the change; the rulings they carry appear above as rows 1–5, and this record does not
  re-judge the self-check's rows as if they were acceptance rows of the ticket.
- The ticket's nine-provider coverage is anchored by the family-wide refusal case plus one provider suite, not
  by listing all nine suites; the run's index carries the provider suites it carries, and none of them failed.
