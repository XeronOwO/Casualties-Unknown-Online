# Acceptance record — the placement failure report's applied arm (row 1)

- Ticket: `placement-failure-report-anchor` — verdict: **stays in `todo/`**. Row 1 now has its machine reading;
  the ticket's own header keeps it open for the NOT-APPLIED half, which is undeveloped work rather than an
  acceptance row, so the folder is decided by that half and not by this reading (workflow §1).
- Batch: `20261023-f` — tickets `placement-failure-report-anchor` (row 1) and
  `give-gesture-own-ui-and-center-use` (its ten rows).
- Commit: `9226c020` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+9226c020abb665adda8bf676d63aac973511d39f` — `tools/verify-deploy.ps1` exit 0 before the clients
  started, and both clients were launched after the deploy.
- Run: 2026-10-23 22:33 → 23:0x, this session's clock · Host: physical machine (the GIVER) · Guest: first
  sandbox (the RECEIVER); one world, one session.
- Dependencies: `preflight.ps1` (`RESULT: OK - a full two-client run is possible`, 11 present),
  `session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `launch=ok`), the in-process
  evaluator on both endpoints, and the physical pointer and keyboard under the standing 2026-10-22
  authorisation.
- Artifacts: `placement-host.txt`, `placement-guest.txt`, `row1-positions.json`, `marks-row2.txt` and
  `host-ring-list.json` in the directory named by `acceptance-artifacts-dir`; players are written
  `<host>`/`<member>`.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A give applied at the answered destination reports the same place; the host sees a match and writes nothing | machine | **pass** | `<host>`: `[Placement] asked <member> where item bandage (id 1134192347075) can go (query 1, slot 1, container 0).` at 22:39:52.090. `<member>`: `[Placement] answered query 1 for item bandage (id 1134192347075): slot 1` at 22:39:52.115, then `[PlayerInteraction] placed bandage (id 1134192347075) in local slot 1.` at 22:39:52.199, then **the receiver's own report**: `[Placement] item 1134192347075: placed it at slot 1 — reporting it to the host.` at 22:39:52.200. `<host>` then writes **the match**: `[Placement] item 1134192347075 is where this host wrote it (slot 1): the placement outcome matches, nothing moved.` at 22:39:52.224, and no return follows. Both lines the ticket names as owed are on both clients, against the deployed `9226c020` artifact. |
| 2 | A receiving client that changed its body reports a different place, the host refuses it by name, returns the row and the giver sees the return | machine | not re-read | Not staged in this batch; the ticket's own verdict for this row is PASS by test, and the batch was scoped to the owed row-1 reading. |
| 3 | A report naming a place the host never wrote is refused by name and moves nothing | machine | not re-read | As row 2: test-covered, not re-read here. |

## What the run did, in order

```text
22:33:xx  preflight: 11 present, RESULT: OK; session-environment status: active=cuo, game-running=false
22:34:37  build 0 warnings / 0 errors; deploy + verify-deploy -> 0.1.0+9226c020… (HEAD)
22:35:12  host launched through Steam (physical machine), evaluator ready in 8 s
22:35:25  guest launched in the first sandbox (box Steam was started by the bring-up, then 30 s settle)
22:36:xx  host create-lobby; guest join applied (first attempt did not settle, the retry entered the lobby)
22:36:xx  host start-run; both clients in world at (0, 491.615)
22:38:2x  the give gesture, all through the real pointer: ring opened with the game's own Tab, right-press on
          slot 0 (the created bandage), Tab pressed once with the button held, the receiver's backpack opened
          by its detection range, release on its displayed slot 1
22:39:52  the four [Placement] lines above
```

Setup substitutions (declared): the bandage was created with `item-provide mode=create` (instance
`1134192347075`, its own answer: slot 0); the receiver was moved to (3.0, 491.615) with `body-place` before
the gesture, so the pointer could sit inside its 1.5-unit range while off the giver's own collider.

## Residuals for the user

None.

## Limits

- One give, one destination shape (a free slot). The container half of the match arm was read by test only in
  the ticket's own commit and is not re-run here.
- The receiver's report and the host's match are read from the two clients' rolling logs; the committed row's
  own snapshot was not censused afterwards, because the match arm moves nothing by design.
- The batch that found the carrier defect (`20261023-e`) performed the same gesture on `0.1.0+aaf89655…`; this
  record is the re-read on the fixed artifact, and the give's own gesture was repeated rather than replayed
  from a probe.
