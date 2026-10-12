# Acceptance record — Crafting-quality labels: the item surface and the load-time reference check

- Ticket: `mod-crafting-quality-labels` — verdict: **moved to `done/`**
- Batch: 20261012-c — offline batch; scope and plan: docs/evidence/acceptance/20261012-c-scope.md
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: CasualtiesUnknownOnline.dll 0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91 — deployed and hash-verified in this run (tools/verify-deploy.ps1 exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (exit 0; its own last line reads `RESULT: OK - a full two-client run is possible`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `swap-needed=false`), both captured into the batch directory before the build; no client capability (steam, sandboxie, sandbox-alt, hotrepl, capture, input, logs) is consumed by this run
- Artifacts: none — the run's logs and TRX results are under the directory named by acceptance-artifacts-dir

The rows below are the ticket's own acceptance table, in its order. Every row is load-time and client-local, so
each is judged by the suite this run re-ran, whose per-case outcomes are the run's own outcome index.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A mod item satisfies a quality recipe | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.CraftingQualityLabelTests.ItemQualities_SatisfyTheVanillaRecipeMatcher` — `Passed` in this run; the case drives the game's own `Item.GetQualityThatMeetsCriteria` over the built `ItemInfo.qualities` rather than re-reading the DTO |
| 2 | A recipe that references a quality nobody provides is refused at load time | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.CraftingQualityLabelTests.Recipe_IsRefusedWhenNoProviderInItsOwnDirectionCarriesTheLabel` — `Passed` in this run; the row's other two halves are the same suite's `Recipe_IsRefusedWhenTheLabelComesFromTheOtherDirection` (both directions) and `Recipe_IsRefusedWhenNoProviderReachesItsAmount` (mod and vanilla amounts), both `Passed` |
| 3 | A recipe that can be satisfied is still injected | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.CraftingQualityLabelTests.Recipe_IsInjectedWhenAVanillaItemProvidesItsQuality` — `Passed` in this run; the row's other three halves are the same suite's `Recipe_IsInjectedWhenAVanillaLiquidProvidesItsQuality`, `Recipe_IsInjectedFromAModDeclarationBeforeTheItemIsMaterialized` and `Recipe_IsInjectedFromAModLiquidDeclarationBeforeTheLiquidIsMaterialized`, all `Passed` |
| 4 | A declared label that is neither a vanilla-style token nor a canonical namespaced id is refused on BOTH surfaces | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.CraftingQualityLabelTests.TryBind_RefusesAQualityLabelThatIsNotCanonical` — `Passed` in this run (the item surface); the liquid half is the same suite's `LiquidQualities_AreValidatedAndMaterialized`, also `Passed` |
| 5 | A definition whose lists are explicit nulls binds as "no qualities" / is refused as "no ingredients" | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.CraftingQualityLabelTests.TryBind_TreatsAnExplicitNullQualityListAsNoQualities` — `Passed` in this run; the rule is pinned by `CasualtiesUnknownOnline.Tests.Mods.ModItemDefinitionTests.NullCollectionMembers_MeanNone`, also `Passed` |
| 6 | The liquid surface keeps its materialization behaviour after the rename | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.CraftingQualityLabelTests.LiquidQualities_AreValidatedAndMaterialized` — `Passed` in this run (the liquid is injected and its labels reach `LiquidType.qualities`); the renamed type's own suite is green too — `CasualtiesUnknownOnline.Tests.Mods.ModLiquidDefinitionTests.Identity_IsFixedByTheType`, `...DeclaredDefaults_HoldOnAFreshDefinition` and `...NullCollectionMembers_MeanNone`, all `Passed` |

Supplementary reads of this run, each from a file the rows name: `ModCraftingQuality.cs` is the single label
type on the item and liquid surfaces, and the renamed-away type is recorded as a tombstone in
`docs/contracts/abstractions-api-baseline.txt` (the tombstone for the removed type `CasualtiesUnknownOnline.Abstractions.ModLiquidQuality`
— "renamed to ModCraftingQuality: one crafting-quality label type for the item and liquid surfaces"). The
full suite this run measured is 5072 passed / 0 failed / 0 skipped and the gate suite 626 passed / 0 failed /
0 skipped.

## Residuals for the user

None. Every row is a test outcome this run produced against this tree; no row asks for a person's judgement.

## Limits

- **No client was started**, so this run produced no rendering, no frame, no live log and no in-process
  probe. Every row above is decided by test output from this run (the behavioural and gate suites) or by
  inspection of the committed tree — there is no runtime observation in this record.
- **The ticket's own table records these rows as `Verified 2026-10-07`.** That date is a state of the cycle
  that wrote the ticket; this run judged the durable outcome instead — the suites the rows name still exist
  and their cases are `Passed` in this run's outcome index. The historical verification date itself was not
  re-measured, and neither was the earlier cycle's count of 169 vanilla ingredient entries.
- **`RecipeItem.GetMatchingItem` itself is still not driven end to end** (it needs live Unity `Item` /
  `WaterContainerItem` instances), and row 1 drives the amount-aware matcher it calls; live injection into
  `Item.GlobalItems`, the recipe panel drawing `cq<label>` and the craft-deny sound stay reasoned from
  decompiled source, exactly as the ticket's own `Limits` state.
- The ticket's `Limits` also carry behaviours this batch cannot reach at all (a liquid label's amount
  scaling with container volume, the colliding-id case, the label rendering without a locale entry); none of
  them is claimed here.
