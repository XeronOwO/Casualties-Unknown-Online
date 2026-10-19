# Acceptance record — random rolls in a cross-player item use (batch 20261023-a)

- Ticket: `cross-player-item-use-random-determinism` — verdict: **stays in `review/`**: rows 2, 4 and 5 now
  pass; row 3 is not reachable as written (the code read below names why); row 8 is a residual for the user.
- Batch: `20261023-a` — tickets: this one and `limb-tool-family-protection-gaps`; one three-client session
  served both (`docs/evidence/acceptance/limb-tool-family-protection-gaps-20261023-a.md`)
- Commit: `44e263cc` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  `ProductVersion 0.1.0+44e263cc681d2f8f2ccd8fee6dd9272f4365b999` — `tools/deploy.ps1` exit 0 and
  `tools/verify-deploy.ps1` exit 0 before any client started
- Run: 2026-10-19 21:18 → 21:26 by the session's clock · Host: physical machine (the operator) · Guest:
  sandbox (the patient) · Third client: alternate sandbox (the late joiner)
- Dependencies: all eleven present; `active=cuo`, `game-running=false`
- Artifacts: the `r-*` JSON files and `z-*.log` copies in the directory named by `acceptance-artifacts-dir`;
  Steam ids are written `<host>` / `<guest>` / `<third>`

Row 1 passed in batch `20261011-b`; rows 6 and 7 passed in batch `20261011-a`. This run re-read the rows
those two left open.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 2 | the same pull replayed for a late joiner lands on the committed values, not on a fresh roll | machine | **pass** | The guest seeded 2 shrapnel pieces on limb 1 (`r1-guest-seed-shrapnel.json`), the operator started the shared session through the medical view (`r4-host-start-session.json`) and broke the grip on piece 0 (`r5-host-break-grasp.json`, `activeOpId:1`, `tryBegin:"slip"`, display deltas skin 5.394 / bleed 0.516). The authority derived and committed one slip: `session 1 grip slip 2 on piece 0 by <host> (head=False, seed 363833036): derived skin 5.393552 bleed 0.5164161 pain 15.44704 brain 0.`, `both sides derived the same values.`, `break-grasp applied to <guest> limb 1: skin -5.393552 bleed +0.5164161 pain +15.44704 brain -0.` at 21:21:20.524/.525 (`z-host-latest.log`). The patient's own limb 1 reads `94.606 / 0.516 / shrapnel 2` (`r6-guest-limb1-after-slip.json`), the authority's stored copy the same skin/bleed (`r17-host-saved-guest-limb1.json`), and the LATE JOINER — launched after the slip, its own `PlayerJoin` in its log — reads `skinHealth 94.606, bleedAmount 0.516, shrapnel 2` on the same limb through its own medical view (`r8-alt-display-limb1.json`). That is the committed value, not a fresh roll. |
| 3 | an injection whose liquid carries a per-call roll: the patient's own values and the operator's display agree once the report lands | — | **unjudged — the scenario the row names has no ordinary-world instance** | The row's premise was re-checked against the decompiled `Liquids.cs` this run: the only `onHealthUse` bodies that read `Random` are `midgradestimulant` (inside its `DoTimedOp` tick, not per call — `Liquids.cs:513-568`) and `epinephrine` (a 5 % branch reachable only while `limb.body.inCardiacArrest` — `Liquids.cs:1347-1363`). Every per-call `Random` in the liquid bodies this ticket's §2 lists (`Random.value < ml / 80f`) is in `onDrink` (`reliefcream` at `:1059`, `woundglue` at `:1090`), i.e. the DRINK chain that row 4 covers. No ordinary injectable liquid rolls per call, so an injection test would measure a draw that does not exist. |
| 4 | a drink whose liquid carries a per-call roll: the patient's values and the operator's display agree once the report lands | machine | **pass** | The host's water bottle was emptied and filled with 500 ml `groundwater` through the game's own `DrainAll`/`AddLiquid` (`r12-host-fill-bottle.json`; declared fixture). One character report later the host sent the production drink request (`SendUseRequest(<guest>, 1086947706819, -1, 100)`; `r13-host-drink-request.json`), the authority answered `[ItemUse] <host> used waterbottle (id 1086947706819) on <guest>; destroyed=False.` at 21:22:51.645 and set the bottle's condition to 0.80 (`z-host-latest.log`), and the patient's client ran the liquid's own body: `[ItemUse] local body drank 1 stack(s) from <host>.` (`z-guest-logoutput.log`). The rolled sickness (groundwater's `onDrink` rolls 50 % then 7–15) is `11.446` on the patient's own client (`r14-guest-health-after-drink.json`), `11.429` in the authority's stored copy (`r15-host-saved-guest-health.json`) and `11.429` on the operator's displayed body (`r16-host-display-guest-health.json`) — one roller, and the display agrees with the authority to the report's own sampling. The 0.017 gap to the patient's live read is the patient's continuous decay between its report and the probe; the guest ran the `onDrink` body and the operator only measured/drained. |
| 5 | an unrelated local roll (a footstep or gore clip) on either client is not perturbed by any of the above | machine | **pass** (the operator's engine stream, as a controlled draw) | A batch probe seeded the engine stream, ran one native `ShrapnelMinigame.BreakGrasp` inside the patch's derived window, and compared the NEXT draw with a fresh stream at the same seed: `controlEqual:true` (the control pair), `drawEqual:true`, `afterDraw 0.869553` = `referenceDraw 0.869553`, and the same call moved the displayed limb (`skinDelta 5.371033`, `bleedDelta 0.971646`, `calls:"none"` — `r22-host-window-isolation.json`). A native break that drew from the global stream would have advanced it past the first value, so equality is exactly the window's own restore; the guest opens no window at all, so its stream is untouched by construction. |
| 8 | the same two actions never read as a stall, and the immediate, roll-free feedback still plays locally | feel | **residual for the user** | The machine half is in row 2's own drive: the operator's displayed limb moved by `skinDelta 5.394 / bleedDelta 0.516` in the SAME invocation as the break (`r5-host-break-grasp.json`), with no round trip before the local feedback. Whether it READS as a stall while watching the screen is the user's judgement; the drive cannot measure a perceived pause. |

## What the run did, in order

```text
21:19:0x  host + guest in world; member presence true; the host creates two ice packs (limb-tool ticket)
21:20:0x  limb-tool rows run (their own record)
21:20:5x  guest seeds shrapnel 2 on limb 1; host opens the medical view and reads shrapnel 2 on the display
21:21:17  host starts the shared shrapnel session (session 1, 2 pieces)
21:21:20  host breaks the grip on piece 0; the authority derives, compares and commits
21:21:3x  the patient's own limb and the authority's stored copy are read
21:22:1x  the third client is launched, joins the run in progress and enters the world (its own PlayerJoin)
21:22:3x  the third client reads limb 1 through its own medical view: the committed value
21:22:5x  the groundwater drink: host request, patient's onDrink, the three sickness readings
21:24:4x  the engine-stream isolation control on the operator (row 5)
21:25:0x  all three clients quit through the game
```

## Findings this run records

- **Row 3's premise is empty in ordinary play.** The per-call rolls in this family's liquids are drink bodies,
  and the injections' random reads are either inside `midgradestimulant`'s timed op (one roller — the patient —
  and not a per-call draw) or gated on cardiac arrest. The ticket's row should be re-scoped rather than
  re-run as written; this record is the input for that.
- **A second start on the same displayed minigame returned `started:true` without an authority session.** The
  row-5 control needed a native break; it re-opened the medical view and called the start recipe again, which
  answered `started:true, shrapnel:2` but produced no `[Shrapnel] session 2 created` line in the authority's
  log, and the break's slip report left no authority line either. The window still opened and restored (row 5's
  equality), so the isolation reading is valid; the start path itself is noted here as a batch observation, not
  as a row verdict — it was driven through the recipe's seam rather than the game's own UI.

## Residuals for the user

- **Row 8, the perceived smoothness.** Watch one bare-hand grip slip while the operator's screen is up: the
  limb should take the damage immediately with no pause before the local feedback. Artifact
  `r5-host-break-grasp.json` carries the machine half (the same-call display delta).

## Limits

- **Pain is read, not compared as a constant.** The native limb drives pain every frame, so the patient's
  `pain 12.844` at read time is a decayed value against the committed `+15.44704`; skin and bleed are the
  stable fields while the limb still carries fragments, and row 2 is judged on those.
- **One slip and one drink.** A rare race is not excluded by one run; the authority's `both sides derived the
  same values` line and the draw-order gate are the standing checks, not this record.
- **The row-5 control is on the operator's client.** The mechanism opens a window only there; the guest runs
  no window, so "either client" is answered by the operator's measured restore plus the guest's structural
  absence of a window.
- **The third client joined after the slip, not during it.** Row 2 asks for a late joiner's landing, which is
  what its entry snapshot and displayed limb show; the shrapnel session itself may already have been past its
  idle terminal (session 1 timed out at 21:21:35), and the row is about the committed limb state, not a live
  minigame.
