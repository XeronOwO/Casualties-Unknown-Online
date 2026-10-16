# Acceptance record — the build names itself on the surfaces that are read

- Ticket: `docs/backlog/done/version-string-build-suffix.md` — **CLOSED** by this record; the ticket moves
  to `done/`.
- Batch: `20261017-e`, run `surfaces` — ONE client, because the question is what this build says about
  itself rather than what two clients agree on. Script, log and the UI dump are in the batch directory.
- Commit: `55063fc0` — Deployed artifact: `0.1.0+55063fc098002dce88caa6bb8215d20097de1806`, compared against
  this tree's build output by `tools/verify-deploy.ps1` (exit 0, 34 of 35 files matched).
- Run: 2026-10-17, 02:47:40 → 02:48:18 · one client on the physical machine.

| # | Surface | Verdict | Evidence |
|---|---|---|---|
| 1 | `verify-deploy.ps1`'s printed identity | **pass** | `delivered artifact: 0.1.0+55063fc098002dce88caa6bb8215d20097de1806` |
| 2 | The startup log line | **pass** | `[CasualtiesUnknownOnline.Plugin] Plugin CasualtiesUnknownOnline is loaded — build 0.1.0+55063fc098002dce88caa6bb8215d20097de1806.` — the SAME 40 characters as row 1, read from the client's own log file |
| 3 | The Online UI's Home row | **pass** | The UI dump's own node text: `"text": "构建：0.1.0+55063fc"` — the short form (`ShortRevisionLength = 7`), on the Chinese page, which is the decision this ticket asked to be stated rather than defaulted |
| 4 | A build from a dirty tree marks itself | **pass** | Read on the artifact while this cycle was uncommitted: `ProductVersion = 0.1.0+68873af11b523472423f59e6384f04f3f5ed372e-dirty`. The three surfaces above then showed NO marker, which is the correct reading for a build from a clean tree — the two readings together are what "the marker means something" rests on |

## Limits

- **The startup line was read from BepInEx's log FILE, not through the batch's log window**: the window was
  marked after the client had already started, so it legitimately held nothing. The line itself is the
  client's own output, quoted above in full.
- **The Home row was read as the UI's own node text**, not from a screenshot: the row exists, carries the
  short stamp and sits in the session section. Whether it is comfortable to read at the game's resolution is
  a judgement this record does not make — the dump's `span` field carries the geometry for whoever asks.
- **What the run does not prove**: that `-dirty` reaches the SCREEN on a dirty build. The marker is proven on
  the artifact (row 4) and the screen form is proven to KEEP a marker the stamp carries (a unit case over
  `0.1.0+<40 hex>-dirty`), so the two halves are covered separately rather than end to end — deploying a
  dirty build and reading the row would close that, and it is deliberately not done: acceptance runs against
  a commit, never against a dirty tree.
- **No behaviour changed for a player beyond the one row**: the startup line gained text, the Home page
  gained a row, and the archive's value is unchanged in meaning.
