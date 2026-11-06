# Batch 20261106-h — shrapnel row 7: all pieces removed, the session ends itself

Artifact: `0.1.0+39300598` (acceptance build, deployed 2026-11-06 20:0x). Clients: host `ws://127.0.0.1:18590`,
guest `ws://127.0.0.1:18591`, alt `ws://127.0.0.1:18592` (batch `20261106-h`; the alt was quit after launch).
Ticket: `docs/backlog/done/remote-medical-stage-2-shrapnel-multiplayer.md`, row 7.

## What was driven

1. Patient (guest): `seed-shrapnel limb=5 count=5` → `{"shrapnel":5,"hasShrapnel":true,"skinHealth":47.336,"bleedAmount":7.191,"pain":7.926}`.
2. Operator (host): `remote-wound-special mode=open target=76561198863287957 limb=5` → `opened:true, viewOpen:true,
   displayBodyName:"MedicalDisplay_110000135D30695"`.
3. Operator: `remote-wound-special mode=special target=76561198863287957 limb=5` → `started:true, shrapnel:5`.
4. Operator: `shrapnel-break6 count=5` → `started:"started", pieces:"5"`.

## Reading — the host's own log

- `20:31:03.343 [Shrapnel] session 3 created for 76561198863287957 limb 5 (5 pieces).` and `operator 76561198281246659 joined session 3.`
- `20:31:07.887` … `20:31:08.399`: five `grip slip N on piece N-1` and five `break-grasp applied` lines — skin
  −4.667 / −5.911 / −5.615 / −4.500 / −5.507, bleed +0.974 / +0.695 / +0.706 / +0.662 / +0.894, pain
  +14.09 / +14.44 / +9.79 / +9.91 / +12.37 — then five `piece N removed by 76561198281246659` lines, and
  `20:31:08.399 [Shrapnel] session 3 terminal Completed.`
- Afterwards the display limb reads `shrapnel 0` and `minigame-end` on the operator answers `before:"none"` —
  the display minigame had already closed itself; no screen was left behind.

## Verdict

**Row 7 PASS.** All five pieces leave the limb, the shared session ends by itself (`terminal Completed`) carrying the
limb's final state, and the operator's screen closes. Rows 1-6/9 inherit this window but are not read by this batch
(single operator; they need two operators acting on the same limb at once).

## Instrument

- `tools/acceptance/driver/eval-declarations/shrapnel-break6.cs` (`AcceptanceShrapnelBreak6`) and
  `tools/acceptance/recipes/shrapnel-break6.cs`: a coroutine that, per piece, posts the ownership report, waits two
  frames, calls the native `BreakGrasp()`, lifts the piece above the game's y=35 line, and then posts one ordinary
  position report with `Y = 60` — the exact reading the host uses.
- Why the position report is the missing piece: `ShrapnelOperationSessionService` marks `piece.Removed` only when a
  report carries `msg.Y >= 35` (`RemoveThresholdY`), and `AllPiecesRemoved` is what terminates the session
  `Completed`. Earlier cuts (break3/4/5) posted only grabs; the session idled out at `TimedOut` instead
  (run 1 in this batch: `session 1 timed out after 15016 ms idle`).

## Limits and observations

- One operator (host) and one patient (guest). Rows 1-6 and 9 — two operators on one limb, leases, force-ungrab,
  cancel, disconnect — remain unread; they need a second operator client.
- Observed, not judged: each slip also logs `[Vitals] divergence: limb 5 bleed model … reported …` (−0.66…−0.97) —
  the display model and the reported limb differ by the slip's bleed increment. Left for a later read.
- Recovery, recorded per the batch rules: the host was restarted at 20:24:50 because a failed declaration load
  (`CS1626`: `yield` inside `try/catch`) wedged that client's evaluator and every later eval returned the same stale
  compile error. The guest kept running; the lobby and the world were rebuilt. The restart's cause is the evaluator,
  not the fixture.
