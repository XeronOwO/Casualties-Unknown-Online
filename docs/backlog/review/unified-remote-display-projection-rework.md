# Unified remote display projection rework

- Status: Review
- Priority: High
- Category: Remote presentation / architecture / projection
- Source: User direction (2026-09-07) after repeated acceptance failures across remote medical, pose/face/carry and backpack projection. The current per-field projection patch path has been judged not acceptable; this item is the architecture replacement, not another point patch.

## Why this item exists

Remote presentation has repeatedly failed in acceptance because each remote
view tries to reconstruct a live native object from a snapshot:
remote WoundView/medical, remote body pose/head/mouth, carried/carry poses,
remote backpack/container content, and related UI readouts. Each fix has been
a per-field projection patch, and each acceptance round has found another
derived field that the native UI reads but the projection did not carry.

The root cause is structural:

- Native UI and renderer paths are hard-wired to a live local `Body` /
  `MoodleManager` / `ECGVisualizer` / native inventory objects.
- Those live objects produce many readouts from ongoing simulation
  (`Body.Update`, `Painkillers.Update`, `Antidepressants.Update`, pose
  renderers, container load/apply rules).
- The wire carries authoritative source facts, but not all derived
  presentation-finished state.
- Current projection adapters therefore guess/replicate formulas field by
  field. This is unmaintainable and has already produced a long family of
  acceptance regressions.

## Handling this item

This item was picked up as a multi-stage architecture change. The deep-analysis
inventory confirmed the structural cause: three separate per-field projection
helpers (`CloneFacePresentation`, `CloneBodyPosePresentation`,
`RemoteMedicalDisplayProjection`) plus inline derived-field code in
`RemoteMedicalCoordinator` and item-source-value copies inside
`CloneInventoryRenderer`. The work was split into concrete phases and the
umbrella is now code-complete at the projection architecture level; the
remaining remote-backpack *interaction* issues are tracked in their own ticket
(`docs/backlog/todo/remote-backpack-item-projection-acceptance-issues.md`),
not hidden under this umbrella.

## Phases completed

1. **Inventory & evidence** — inventoried every remote display surface:
   remote render clone face/body pose, remote WoundView display body, remote
   clone inventory source values, carry/ride presentation, and the limbs/health
   wire fields they read.
2. **Design** — introduced one unified adapter-side projection seam,
   `RemoteCharacterDisplayProjection`, and one item source-value seam,
   `RemoteItemPresentation.ApplySourceValues` / `SourceValues`.
3. **Medical/WoundView projection migration** — moved the inactive display
   body's breathing, respiratory readout, heart progress, opiate ramp,
   antidepressant happiness, mindwipe and component sync into the unified
   projection; `RemoteMedicalCoordinator` no longer owns derived-field formulas.
4. **Body/pose/face/carry projection migration** — face latches, face vitals,
   facial-expression component fields, head/mouth state and leg-speed pose
   now flow through the same unified capture/apply. Carry/ride presentation was
   already on the shared `CarriedBodyPlacement` / `CarriedBodyPose` path and is
   unchanged.
5. **Inventory/container projection migration (source values)** — all
   clone-inventory renderer branches (new, matched, container update, container
   create) now apply `Condition`, `Favourited` and `Liquids` through one seam;
   the top-level durability mismatch is fixed.
6. **Hardening** — full build, format, 2506 + 17 tests, normative gates,
   independent adversarial subagent review, and latest DLL deployment/hash
   verification are complete for this development cycle.

## Remaining boundary

This ticket is the remote-character-display slice of the projection work, not
the complete macro projection system. The global framework is tracked in
`docs/backlog/review/global-projection-framework.md`: typed `IProjectionDomain` /
`ProjectionDomain` core, the typed health-tracked domains, and the additional
`run` / `players-carry` / `remote-character-presentation` / `mod-status`
domains have landed; enemy/player continuous projection and remote-presentation
read-source boundaries were audited and are documented in the architecture doc.

The user-visible interactive remote-backpack issues (trash-bag selective
insertion, double-Tab transfer, pour/edge-drop, main-hand placement,
held-remote-item + R medical-use chain) remain in the separate High priority
todo ticket. This umbrella ticket does not claim those as done. Real dual-client
visual acceptance remains part of the final unified acceptance pass for all
review-stage tickets.

## Acceptance criteria (high level)

- No new remote display acceptance issue is fixed by adding another
  per-field projection helper. — **Met**: the new path is a unified seam.
- Existing projection helpers are either absorbed into the unified model or
  deleted; no dual parallel projection paths remain. — **Met**: the three old
  helpers are deleted; the item source-value copies are consolidated.
- Full build, formatting, normative gates, full test suite and independent
  adversarial review pass. — **Met**: 0 warnings/errors, 2506 + 17 tests,
  independent subagent review passed.
- Latest DLLs deployed and artifact-verified for each runtime-behavior phase.
  — **Met for this cycle** (deployment hash verification recorded below).
- Real dual-client visual behavior remains in the final unified acceptance pass.
  — **Pending**: this remains the user's final unified acceptance.

## Deployment verification

Deployed with:
`powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"`.

SHA256 of deployed CUO assemblies matches the build output for all six CUO
assemblies (`CasualtiesUnknownOnline.dll`,
`CasualtiesUnknownOnline.Abstractions.dll`,
`CasualtiesUnknownOnline.GameAdapter.dll`,
`CasualtiesUnknownOnline.GameState.dll`,
`CasualtiesUnknownOnline.Protocol.dll`,
`CasualtiesUnknownOnline.Runtime.dll`).

## Non-goals

- No parallel CUO medical panel or replacement native UI.
- Host migration / strict anti-cheat / generic prediction are out of scope.
