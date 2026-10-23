# Acceptance — remote medical Stage 1: the disconnect rows, read on the deployed artifact

- Ticket: `docs/backlog/review/remote-medical-stage-1-injection-session.md` — verdict: **stays in `review/`**
  (row 20, the mixed-version handshake, remains `unproven`; rows 8, 10 and 22 now pass)
- Batch: `.acceptance/20261024-c/` (local, gitignored; runs `md3` and `md4`)
- Artifact: `0.1.0+9c350df3...` deployed and verified before launch
- Date: 2026-10-24
- Result: **rows 8, 10 and 22 pass on the machine**; row 20 stays blocked on an artifact this machine cannot
  produce.

## What was run

Three clients (host physical, guest and alt in sandboxes), a Steam lobby and a world. The stages reused the
earlier batch's probes: `medical-send` drives the production `IMedicalOperationControl` send surface
(`start` / `update`), `medical-session-read` reads the host's registry through the DI service. A fresh
`fentanyl` container was provided with the committed `item-provide` recipe.

## Rows

| Row | Evidence |
|---|---|
| 8 — operator disconnects mid-injection | op 1 (guest operator → host target, limb 2): one committed increment `[MedicalOps] operation 1 committed 5.00 ml (total 5.00, sequence 1)` and the native apply; the guest was then quit → `operation 1 terminated because <guest> left the session.` and `operation 1 terminal Disconnected: committed 5.00 ml, item 0.95, target health <host>`. The registry read back `count:0, sessions:[], itemClaims:[], operatorClaims:[]` |
| 10 — target disconnects mid-injection | op 2 (host operator → alt target, limb 2): `committed 3.00 ml (total 3.00, sequence 1)`; the alt was quit → `operation 2 terminated because <alt> left the session.` and `terminal Disconnected: committed 3.00 ml, item 0.97, target health <alt>`; registry cleared and no further dose apply line followed the terminal |
| 22 — the host leaves with a session in flight | a fresh run: op 1 host → alt with `committed 2.00 ml`; `open-window` then `click home.leave` answered `applied:true`, the log says `Session ended (role Host kept)`, the registry read `count:0, sessions:[], itemClaims:[], operatorClaims:[]`, and the host's state went `role:None, active:false, inWorld:false` |
| 20 — mixed CUO versions | **unproven**: no old-version artifact exists on this machine to launch; the comparison is `protocol != ProtocolVersion.Current` in the handshake handlers (the earlier record's own reason), not re-run live |

## Limits and substitutions

- The dose was delivered through the production send surface at controlled ml increments instead of dragging
  the hand with the pointer (the OS pointer is out of bounds); every dose still travelled the session commit and
  the patient's native apply.
- The disconnects are the products' own `quit` path, not a network cut; the session service's own disconnect
  handling is what the rows measure.
- The acceptance driver needed a fix in this batch: after the plugin entry split, the Online UI is reached
  through the `PluginHost` component on the entry's GameObject, and `FindObjectsOfType` does NOT return the
  plugin's `DontDestroyOnLoad` object on this runtime (measured: both the entry and its host were absent from
  its answer while `GetComponents` on the entry's object listed them). The driver now walks the entry's own
  GameObject.
