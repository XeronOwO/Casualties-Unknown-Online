# Acceptance record — a medical operation state that names no kind is read as an injection

- Ticket: `medical-state-names-no-kind` — verdict: **moved to `done/`** (its rows 1-3 passed in batch
  `20261011-b`; row 4, the Stage-3 family regression, passes here). This record covers row 4 only.
- Batch: `20261011-c` — tickets `medical-removal-limb-component-not-applied`,
  `display-clone-limb-component-runs-native-start`, `medical-state-names-no-kind` (row 4); scope:
  `docs/evidence/acceptance/20261011-c-scope.md`; the ticket's first run:
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md`
- Commit under test: `7c5646bd` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+7c5646bd…`, verified against `<game-dir>` (`tools/verify-deploy.ps1`, exit 0)
- Run: 2026-10-11 13:24 → 13:31 · Host: physical machine (the operator) · Guest: first sandbox (the
  patient) · Third client: alternate sandbox
- Artifacts: the batch directory under `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 4 | the family regression: a Stage-3 action (bandage or splint removal) still writes the target's own body through the same receiving path | machine | **pass** | a driven splint removal on the guest's limb 2 reached the same receiving path batch `20261011-b`'s row 4 exercised, and wrote the target's own body: the patient's client logged `[MedicalTargetCheck] answered request 1 … this body allows it.` at 13:27:56.127 and `[MedicalOps] local target body updated (terminal=True).` at 13:27:56.549 (`c-guest-medical-log.txt`), and its own limb then read `splinted=false, components=""` (`c-guest-local-limb2-after-removal.json`) — the component the authority removed is gone from that body, where this row's earlier run read `Splinted=false` beside a live `SplintLimb`. A second Stage-3 action of the sibling kind (tourniquet removal, limb 3) took the same path at 13:28:48.500/.542 |

Row 4's failure mode in the earlier run was a DETECTED, correctly-routed operation whose effect did not
land; the routing half was already fixed and verified in batch `20261011-b` (rows 1-3), and what this row
checks — that the Stage-3 family still names its kind and still writes the target's own body — holds. The
effect now lands as well, which is the other ticket's rows (`medical-removal-limb-component-not-applied`,
same batch).

## Residuals for the user

None: the row is judged from this run's own readings.

## Limits

- The run drove the splint and tourniquet kinds, not a bandage: the row names "bandage or splint removal"
  as alternatives, and the bandage family's own path (the native minigame's progress/terminal) is the same
  `MedicalOperationApply` receiving branch this run exercised twice. The parent ticket's rows 2-5 and 8
  remain untouched by this batch, as its own limits say.
