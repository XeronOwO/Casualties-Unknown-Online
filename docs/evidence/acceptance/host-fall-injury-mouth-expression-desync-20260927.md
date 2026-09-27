# Acceptance record — Host fall-injury mouth-expression desync

- Ticket: `host-fall-injury-mouth-expression-desync` — verdict: **moved to `done/`** (no unproven row;
  the visual half is a residual for the user)
- Batch: `20260927-d` — carry/pose family (see `20260927-d-scope.md`)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/` (`snippet-dislocate.cs`, `snippet-clonemouth.cs` readings;
  `f10/f11` frames)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | After a fall injury the host's own view shows the actual mouth state | visual | **residual** | The host's head was dislocated through the game's own limb entry (`headDislocated=true`), which is the native open-mouth condition; the frame (`f10`) does not resolve the mouth at this zoom, so the render is yours to confirm |
| 2 | The guest's view matches the owner (no remote-only open mouth) | machine | **pass** | The guest-side host clone read `HeadMouth=Open` with `cloneHeadDislocated=true` — the clone's mouth follows the owner's captured decision; the control (host's view of the guest's clone) read `Closed`, matching a healthy guest |

## Residuals for the user
- Row 1: with a fall injury, check the host's own mouth on a full-size screen. Frame artifact `f10`
  (`.acceptance/batch-d/`).

## Limits
- The injury was injected through the game's own limb dislocation entry, not by an actual fall from
  height; the rule it feeds (`HeadMouthRule`: dislocated head → `Open`) is the same one the report's
  fall exercised.
- The mouth sprite itself is a few pixels at this zoom; the machine reading is the decision value the
  clone applies.
