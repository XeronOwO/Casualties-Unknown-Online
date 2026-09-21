# Adapter capability catalog and probe aggregation — Self-Check (2026-09-20)

Delivery fact sheet for `docs/backlog/review/adapter-capability-catalog.md` (High, the second ticket
of the 2026-09-20 review batch), STAGE 1 only. A broken hook used to surface as a list of patch class
names, and nothing in the tree recorded which gameplay system a patch, type or member belongs to —
so "which system broke, and why" had no answer that did not require reading the patch list. This
cycle lands the catalog and the probe that answers it, with installation behaviour untouched.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Current behaviour | This cycle's change | Evidence |
|---|---|---|---|---|
| 1 | Patch → gameplay system mapping | — (nothing recorded it; a failure surfaced as patch class names) | one capability table: 14 stable ids with a Required/Optional class, type-bound patch owners, declared dynamic rows and declared game types/members | `src/CasualtiesUnknownOnline.GameAdapter/Capabilities/AdapterCapabilityCatalog.cs`; `AdapterCapabilityDefinition.cs` |
| 2 | Install verdict | `PatchInventory.VerifyMissing` returned `List<string>`; `Install()` refused on `missing.Count > 0` | the same verdict over the same violation texts, now decided by `AdapterCapabilityReport.RefusesInstall` over structured failure facts; a THROW in the install path is recorded as a blocking fact too, so a refused install can never print an all-OK capability list | `src/CasualtiesUnknownOnline.GameAdapter/Patches/PatchInstallLifecycle.cs`; `Capabilities/AdapterCapabilityReporter.cs`; `src/CasualtiesUnknownOnline.Runtime/Patching/PatchVerificationFailure.cs` |
| 3 | Dynamic patch targets | the installer logged a missing target and the install continued; its targets were ALSO written out by hand as the dynamic contract rows | verdict unchanged (still log-only); the targets are now ONE declared table (`DynamicPatchInstaller.Targets`) that the installer binds and `PatchInventory.BuildContracts` derives its dynamic rows from, so a guarded row cannot describe an unbound target; a miss returns non-blocking facts, including an explicit "the remaining dynamic targets were not attempted" row when the original flow aborts | `DynamicPatchInstaller.cs`, `DynamicPatchTarget.cs`, `Patches/PatchInventory.cs` |
| 4 | Game probe | `ProbeGame()`'s four `typeof` reads produced a three-value `CapabilityReport` string nothing else consumed | the four types are declared as the session capability's game types and the probe line rides the report; the verdict and the exact text are unchanged (pinned) | `GameAdapter.cs` (`ProbeGame`, `Initialize`); `Patches/PatchInstallLifecycle.cs` (`ProbeGame`); `Capabilities/AdapterCapabilityReporter.cs` |
| 5 | Contract identity | `PatchContract` carried the patch class's SIMPLE name (205 rows share 201 names) | it also carries `PatchClassType` (full CLR spelling), and the contract-tool parity gate compares it against the snapshot's own fact | `src/CasualtiesUnknownOnline.Runtime/Patching/PatchContract.cs`; `Patches/PatchInventory.cs`; `tests/.../ContractTool/PatchContractRowParityTests.cs` |
| 6 | Per-capability status (new) | — | one report line per capability (class, id, contract count, verdict, one indented reason per failure) plus a session verdict line; printed once from `Initialize`, at Error level when the session is refused | `src/CasualtiesUnknownOnline.Runtime/GameAdapter/AdapterCapabilityStatus.cs`, `AdapterCapabilityReport.cs`; `Capabilities/AdapterCapabilityProbe.cs` |
| 7 | Declared game members (new) | decision 199's pinyin residual (`PlayerCamera.recipeItemFilter`, read inside a compiler-generated lambda) lived in prose only | declared as a member probe and resolved at probe time; a miss becomes a reported reason instead of an assumption | `Capabilities/AdapterMemberProbe.cs`; the `pinyin-search` catalog entry (that row left with the pinyin mod on 2026-09-21 — decision 209 — and the mod asserts the field in its own contract test; the probe type stays for the next such row) |
| 8 | Adapter coordinator size | `GameAdapter.cs` sat at 599 lines — the 600-line gate's edge — and this cycle's wiring would have crossed it | the patch-install life cycle moved to its own collaborator (harmony ownership, PatchAll, dynamic install, verification, refusal/rollback, report publishing); `GameAdapter.cs` is 565 lines | `Patches/PatchInstallLifecycle.cs`; `GameAdapter.cs` |
| 9 | Wire / protocol / save | — | untouched: no wire shape, no save shape, no `ProtocolVersion` change | `git status` (no `Protocol/`, `GameState/` or wire file touched) |

## Design

- **One auditable table, not 205 attributes.** The classification is a policy a reviewer has to be
  able to read in one place — the Required/Optional yardstick applied to every gameplay system at
  once — so the catalog is a single declaration whose owners are `typeof(...)` references (compile
  bound, rename safe, and a container type brings its nested patch classes with it). The guarantee an
  attribute would have given is kept by the gate instead: a patch class the catalog does not claim
  fails the build with its own name in the message.
- **Identity is the full CLR name.** 205 contracts share only 201 simple names, so the catalog joins on
  `PatchClassType`; the hand-declared dynamic rows have no type at all and keep `PatchInventory`'s
  `"(dynamic)"` pseudo name — and they are no longer declared twice: the installer's target table is
  the declaration, and the contract rows are derived from it.
- **Behaviour preservation is structural, not promised.** The install gate and the report read the SAME
  failure rows — `VerifyMissing` returns facts whose `Detail` is the text it always returned, in the
  same order — and the decision itself sits in one place (`AdapterCapabilityReport.RefusesInstall`),
  which the report's own verdict calls too, so a printed "session available" cannot sit next to a
  refused install. Everything the probe adds beyond the gate (dynamic target misses, declared members)
  is marked non-blocking and rendered as *reported only*.
- **Required/Optional is the user's yardstick, not a difficulty ranking.** A feature the vanilla game
  has is Required (items, medical, crafting, world generation, the session itself, and the multiplayer
  wiring of those systems); CUO's own additions are Optional (the mod content surface, the diagnostic
  hooks — the `pinyin-search` row was one of these until it left with the pinyin mod, decision 209). A
  class that carries BOTH — `PlayerCameraDragUsePatch` implements the
  remote-backpack take (a game feature) and the KrokMP-style cross-player use-by-drag seam (ours) — is
  classified by the vanilla path and says so in the catalog; splitting it is stage 3's install-unit work.
- **Recorded boundaries.** Stage 1 prints, it does not degrade: a Required failure still refuses the
  whole session (stage 2 adds the player-visible refusal) and an Optional failure does not yet switch
  anything off. The gate proves totality and well-formedness, NOT the curation: a class moved between
  capabilities keeps the gate green, because membership is a reviewed declaration printed in the report
  (`AdapterCapabilityCatalogTests` pins only that every class has exactly one home). `ProbeGame`'s
  `MISSING` branch is unreachable for a compile-time reference, so its pin can only fail if the TEXT
  moves — the ticket's own subject is what will make that branch mean something. The member probe
  covers only members the catalog declares; the structural half of "what did the game change" stays
  with the contract toolchain and the semantic half with
  `future/adapter-shell-verification-harness.md`. The install path itself (`PatchInstallLifecycle.Install`)
  cannot run outside the game process — it is a Harmony `PatchAll` — so its all-or-nothing rule is pinned
  at the seam (`RefusesInstall` ⇔ the report's verdict) plus by the message texts, not by driving it.

## Verification design

- The totality gate enumerates the adapter assembly itself — it does not reuse the production
  expansion — and demands exactly one claim per `[HarmonyPatch]` class, exactly one claim per dynamic
  row, no barren owner, and unique well-formed ids; it also pins the row census (205 attributed + 9
  dynamic = 214) and that every contract row joins to a capability. It caught
  `WorldGen/LayerModifierApplyPatch`, a patch class living OUTSIDE the `Patches/` namespace.
- The declared dynamic targets are resolved against the real game assembly (they live on `internal`
  types no compile-time reference can check), and their four miss-message shapes are pinned against the
  wording the installer logged before the table existed.
- The report's own contract (classes, counts, reasons, the session verdict and its agreement with the
  install rule) is covered by fast unit tests over the Runtime types, which need neither the game
  assembly nor the adapter.
- What the machine cannot prove: that a degraded Optional capability leaves a playable session (stage 2),
  that a still-present member still MEANS the same thing (the semantic half), and that the curation
  itself is right (reviewed, not gated).

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Catalog totality | every patch class and dynamic row claimed exactly once, ids stable and classified | `AdapterCapabilityCatalogTests.EveryPatchClass_IsClaimedByExactlyOneCapability`, `...EveryDeclaredOwner_ContributesPatchClasses`, `...CapabilityIds_AreUniqueStableAndClassified`, `...DynamicRows_AreDeclaredExactlyOnce` |
| Row accounting | every contract row joins to a capability; the census is pinned | `AdapterCapabilityCatalogTests.ContractRows_AllJoinToACapability`, `...Probe_AccountsForEveryContractRow` |
| Dynamic targets | the declared table resolves against the shipped game build, and its miss wording is unchanged | `AdapterCapabilityCatalogTests.EveryDeclaredDynamicTarget_ResolvesAgainstTheGameAssembly`, `...DynamicMissMessages_KeepTheirOriginalWording` (7 shapes) |
| Install verdict unchanged | all-or-nothing over the blocking facts, whatever the capability class says, and the report's verdict is the same call | `AdapterCapabilityReportTests.RefusesInstall_IsAllOrNothingForBlockingFailures`, `...RefusesSession_ReportsAProbeOnlyFailureWithoutRefusing`, `...RefusesSession_UsesTheSameRuleAsTheInstallGate` |
| Report content | class, id, contract count, reason and session verdict per capability; unclaimed failures printed, never dropped | `AdapterCapabilityReportTests.Render_ListsEveryCapabilityWithItsClassAndContractCount`, `...Render_PrintsTheFailureReasonUnderItsCapability`, `...Render_PrintsAProbeOnlyFailureWithItsReason`, `...Render_NamesAnUnmappedFailureInsteadOfDroppingIt` |
| Probe aggregation | the report names the broken capability from real failure facts; a dynamic miss is reported without refusing | `AdapterCapabilityCatalogTests.Probe_NamesTheBrokenCapabilityAndItsReason`, `...Probe_DynamicFailure_IsReportedWithoutRefusingInStageOne` |
| Probe types | `ProbeGame`'s four types are the session capability's declared game types, and its verdict/text are pinned | `AdapterCapabilityCatalogTests.SessionCapability_CarriesTheGameProbeTypes`, `...ProbeGame_KeepsItsVerdictAndReportText` |
| Contract identity | the tool's namespaced row identity equals the adapter's | `PatchContractRowParityTests.ToolRows_EqualTheAdaptersOwnContractRows` (now compares `PatchClassType`) |

## Delivery evidence (2026-09-20)

- Capability family: **25/25 passing**; the touched contract/patching/capability family: **388/388**.
- Full suite with build: **3 703 passed / 0 failed** (56 s) — 25 more than the 3 678 baseline recorded
  before this cycle, i.e. exactly this cycle's cases; normative gates **69/69**; `dotnet format` exit 0.
- Structure: `AdapterCapabilityCatalog.cs` 282 lines, `DynamicPatchInstaller.cs` 137,
  `PatchInstallLifecycle.cs` 102, `PatchInventory.cs` 223, `GameAdapter.cs` 565 (limit 600); no new
  boolean state field; no dead mechanism left behind.
- The delivery checklist was reset for this cycle (the documented deliberate multi-line exception: item
  lines only, prose verified intact) and then checked one Edit per box, each with its evidence suffix.
- Independent adversarial review (fresh context, frozen tree, 2026-09-20, full report
  `%TEMP%\cuo-review-adapter-capability-catalog.md`): **PASS WITH FINDINGS — no blocker** (3 majors,
  6 minors, 3 nits). Fixed in this commit: (1) a THROW in the install path recorded no failure fact, so
  a refused install printed an all-OK capability list — `PatchInstallLifecycle` now records the throw as
  a blocking fact; (2) the dynamic installer's two abort paths left the remaining targets unattempted AND
  unrecorded while the report listed the declared rows — the abort now records an explicit
  "remaining targets were not attempted" failure; (3) the nine dynamic rows were pinned to nothing
  because the installer declared its targets a second time — the target table is now the single
  declaration the contract rows are derived from, with a resolution test against the real build;
  plus the report's verdict now calls the install rule itself, the owner maps keep the first claim
  deterministically instead of a silent last-wins, and three stale numbers/citations in this file were
  corrected. Accepted as recorded boundaries (not defects): the curation is not gated (see Design), and
  `ProbeGame`'s unreachable `MISSING` branch is the ticket's own stage-2 subject.
