# Acceptance record — S3.4b native character fields

- Ticket: `save-native-character-field-parity` — verdict: **stays in `review/`** (rows 2–4 unproven: the death
  panel did not appear, the pause tooltip is the ticket's named surface gap, and no snapshot without the
  native fields was staged)
- Batch: `20261001-m` (Run A) — tickets `save-layer-end-save-and-restore`, `save-mid-run-consistent-cut`,
  `save-native-run-field-parity`, `save-native-character-field-parity`, `save-layer-time-not-carried`,
  `world-determinism-world-fingerprint`
- Commit: `e86241a5` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+e86241a5`
- Run: 2026-10-01 10:34 → 10:47 · Host: physical machine (Steam) · Guest: sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The wound window's version text | frame + machine | pass | the game's own wound view, opened through `PlayerCamera.ToggleWoundView`, reads `versionText = "UNI-HEALTH v2.05"` before and after the restore (`run-a/wound-before.json`, `run-a/wound-after.json`), and the captured host window shows the panel and that text (`run-a/wound-view-host.png`, read by the agent) |
| 2 | The death-stats calorie counter | machine + named gap | **unproven** | `game-console kill` applied and the following Continue restored the character, but the death panel never became reachable: the captured frame shows the dead character's vignette/name tag only, no stat panel (`run-a/death-panel-host2.png`; the framed Online UI variant is `run-a/death-panel-host.png`). The ticket's named gap for the exact panel child stands |
| 3 | The happiness history | unproven — named surface gap | **unproven** | the pause tooltip is the row's own surface and has no drive path; the wound view's own `happyText` was read as the nearest live value and is recorded as a substitution: `-0.9` before the cut, `-0.7` after the restore (`run-a/wound-before.json`, `run-a/wound-after.json`), `-11.4` once dead (`run-a/wound-after-death.json`) |
| 4 | A missing field set is named | machine | **unproven** | no snapshot without the native fields was staged in this run, so the restore account's "names what it did not carry" behaviour was not exercised live; the run only observed the normal "native run fields present" report (`run-a/host-restore.txt`) |
| 5 | A malformed row is refused by name | machine | pass | this run's own suite is the evidence: normative gates 300/300 and the main suite 4 545/4 545 green against the deployed commit (`acceptance-artifacts-dir` key, batch `20261001-m` build/suite step recorded with the batch) |

## Residuals for the user

- Row 1: whether the panel's layout and numbers look right to a player (`run-a/wound-view-host.png`).

## Limits

- Row 2's death panel was not reached; the kill was applied, the frame was captured, and the panel is named
  as unreachable in this state rather than judged.
- Row 3 is judged as a substitution, exactly as the batch scope declared; it cannot close the row by itself.
