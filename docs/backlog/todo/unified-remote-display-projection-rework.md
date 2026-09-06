# Unified remote display projection rework

- Status: Todo
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

This is a large, multi-stage architecture change. **It must not be treated as
one oversized implementation task.** When this item is picked up:

1. First perform a deep analysis and inventory of every remote display/projection
   path and every native read point it feeds.
2. Split the work into explicit phases/steps in the ticket (or a companion plan
   document) before implementation.
3. If the deep analysis produces detailed sub-tickets, the original umbrella
   ticket may be removed rather than retained; do not keep it only to preserve
   a parent/umbrella shape.
4. Implement phase by phase, with each phase producing a buildable, testable,
   verifiable result and its own handoff/summary if a new session continues the
   work.
5. Do not start another per-field point patch as the resolution path.

## Intended direction (to be confirmed/refined by the first phase)

- Create a single unified remote display projection model that describes the
  **final presentation state** needed by remote views, not a loose collection of
  source snapshot fields.
- Decide what should be computed/captured on the source side (owner) so peers
  receive presentation-finished state, versus what should be reconstructed on
  the receiving side from authoritative source facts.
- Converge existing separated projection helpers (medical display, face, body
  pose, carry pose, clone inventory/container, etc.) onto that model and delete
  the old per-field ad hoc paths.
- Keep the native UI reuse constraints: native WoundView/ECG/Moodle remains the
  UI surface; no parallel CUO medical panel.
- Add a native-read-point inventory and a regression matrix covering roles,
  directions, third-party views and edge/failure paths.

## Proposed phase sketch (must be refined by the deep analysis)

1. **Inventory & evidence**: list every remote display surface and every native
   field/component/getter it reads; classify which are source-authoritative,
   which are owner-derived live state, and which are missing on the wire.
2. **Design**: define the unified projection state shape, the source-side
   capture path, the peer-side apply path, and the seam through which native
   UI receives the state.
3. **Medical/WoundView projection migration**: move current medical display
   projection onto the unified model; verify breathing, mood, ECG, respiratory
   readout, painkiller/antidepressant-derived moodles, etc.
4. **Body/pose/face/carry projection migration**: move face, mouth, head,
   sleepiness/weakness posture, carry/riding and related pose paths onto the
   same model.
5. **Inventory/container projection migration**: move clone inventory, remote
   backpack, trash-bag and held-item projection paths onto the same model.
6. **Hardening**: complete test/acceptance matrix, independent adversarial
   checks, deploy and artifact verification.

## Acceptance criteria (high level)

- No new remote display acceptance issue is fixed by adding another
  per-field projection helper.
- Existing projection helpers are either absorbed into the unified model or
  deleted; no dual parallel projection paths remain.
- Full build, formatting, normative gates, full test suite and independent
  adversarial review pass.
- Latest DLLs deployed and artifact-verified for each runtime-behavior phase.
- Real dual-client visual behavior remains in the final unified acceptance pass.

## Non-goals

- No parallel CUO medical panel or replacement native UI.
- Host migration / strict anti-cheat / generic prediction are out of scope.
