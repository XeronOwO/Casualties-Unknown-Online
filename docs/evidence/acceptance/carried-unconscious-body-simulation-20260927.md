# Acceptance record — A dead or unconscious carried body's own simulation stops

- Ticket: `carried-unconscious-body-simulation` — verdict: **moved to `done/`**
- Batch: `20260927-d` (carry/pose family: `carried-rider-own-body-stops-simulating`,
  `carry-piggyback-rider-position-smoothing`, `carrier-sit-while-carrying`,
  `carried-player-idle-sit-suppression`, `carry-piggyback-vertical-placement-asymmetry`,
  `host-severe-sleepiness-posture-desync`, `host-fall-injury-mouth-expression-desync`; the two
  item-domain tickets are split out, see the batch scope)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine through Steam · Guest:
  Sandboxie sandbox · dependencies: preflight `10 present`, exit 0
- Artifacts: `.acceptance/batch-d/` under the directory named by `acceptance-artifacts-dir`
  (`z1-z11`, `logs/host-LogOutput.log`, `logs/guest-LogOutput.log`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A bleeding unconscious carried player's vitals keep advancing on their own client | machine | **pass** | `z2` → `z5` → `z7`: `brainHealth`/`consciousness` `1.004 → 1.011 → 1.033` while carried; `standing=false` |
| 2 | A carried corpse keeps the limp ragdoll presentation, no standing up | machine | **pass** | `z4`: clone `ragdollPoseActive=true`, `standing=false`, `pin=24`, `mounted=true`; `clip=ExperimentIdle` |
| 3 | The carrier moves/turns/crouches with a limp body; it stays attached and does not come apart | machine | **pass** | `z10/z11`: after the carrier relocated, the limp rider sat at carrier + `(0.35, 0.9)`, `pin=78`, `drift=0`, `limbSep=0`, `mounted=true` |
| 4 | Release restores exactly the pre-carry state, immediately | machine | **pass** | `z8/z9`: driver cleared, `standing=false`, ragdoll kept, released at the carrier's position; guest log `[Carry] rider simulation released … standing=False rbSimulated=True` |

## Residuals for the user
None required: every row has this run's machine evidence.

## Limits
- One session; the vitals advance was observed across roughly 30 s of carried time, not a long soak.
- The game renders at a 22.5-unit orthographic half-height, so world frames do not resolve a body
  pose; row 2's presentation is judged from the state reads (`ragdollPoseActive`, `standing`), not from
  a picture.
