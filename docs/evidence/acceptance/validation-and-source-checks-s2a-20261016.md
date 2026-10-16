# Acceptance record — Source, permission and data checks, stage S2a (a member's break drops are bounded)

- Ticket: `validation-and-source-checks` — **S2a's acceptance rows are read and pass for the BLOCK family**
  (rows 1 and 2 of the ticket's table, plus row 6's new half). The ticket does NOT move: S2b (the entity
  death drops) and S3 (the state preconditions) are still undeveloped code, so it stays in `todo/` with the
  stage marked landed.
- Batch: `20261016-a`, runs 6, 7 and 8 — three client sessions. Run 6 (on the previous build) read the
  one-arm/two-drop shape and left two readings void; run 7 read the id and count rules on the final build;
  run 8 read the CONDITION WINDOW on both sides of an arm's own window. The table below is judged from runs 7
  and 8, on one deployed artifact. All three runs' files are in the batch directory (`run6.ps1`/`run6.log`,
  `run7.ps1`/`run7.log`, `run8.ps1`/`run8.log`).
- Commit: `8605a34c` (HEAD at runs 7 and 8; its own delta over the feature commit `774a89a3` is the refusal
  wording and the `block-drop-forge` recipe it added; that recipe gained its absolute-cell form and a
  per-cell drop id between runs 7 and 8, both of which the acceptance commit carries) · Deployed artifact:
  CasualtiesUnknownOnline.dll, ProductVersion `0.1.0+8605a34c0b8fd3c4a2fd311a9755e5549c8a417d`, verified
  against this tree by `tools/verify-deploy.ps1` (exit 0; `g1-verify-deploy.txt` and `h1-verify-deploy.txt`).
  Run 6 ran the same tree minus that delta (`774a89a3`, named by `f1-verify-deploy.txt`).
- Run: 2026-10-16, 19:02 → 19:10 · Host: physical machine · Member: `Steam1` sandbox, reading the physical
  install through and shadowing its own log.
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (host and member endpoints),
  `capture`, `input` (the in-process driver and the scenario recipes), `logs`, `artifacts` — the preflight
  reported 11 present, `RESULT: OK - a full two-client run is possible`.
- Artifacts (batch `20261016-a/`): runs 7 and 8 (`g1`–`g18`, `h1`–`h16`) and run 6 (`f1`–`f19`) — the
  deployed-artifact checks, the client states, the cell passes, the recipes' own answers, the two clients'
  log windows (`g15`/`g16`, `h13`/`h14`, `f16`/`f17`) and the run logs.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A block break whose drops are outside the block's own table: refused by name on the host, the client rolls its drops back, both sides agree | machine | **pass** | Run 7, the member's forged report at (515,997): `g15-host-break-lines.txt` — `[WRN] [BlockDropBoundJudge] [BlockBreak] <member>'s break at (515,997) claimed 1 drop(s) block 9 cannot produce — refused by name (scrapmetal@0.50), 0 drop(s) accepted and relayed.` followed by `[INF] [BlockBreakSync] … accepted — 0 block drop(s) + 0 building drop(s) registered + relayed.`: the drop was refused WITH the block named, and NOTHING was registered or relayed from it, so no side materialized the item. The answer reached the breaker: `g16-guest-reject-lines.txt` — `[WRN] [KernelProtocolService] Kernel command rejected by host for item 900000000000: ItemSpawn refused (BlockAlreadyBroken).`, and run 8 read the breaker's own half of it literally — `h14-guest-reject-lines.txt`: `[INF] [ItemApplication] [ItemReject] block drop 900000000000 (scrapmetal) refused — destroying the local copy.` Run 6's `f16`/`f17` read the same shape on the previous build, where the table's own ROW COUNT was judged too: a report claiming TWO drops of one arm (`claimed 1 drop(s) block 9 cannot produce — refused by name (plasticchunk@1.00), 1 drop(s) accepted and relayed`) — one row is one roll, so exactly one of the two was accounted for |
| 2 | A break whose drops are inside the block's own table: accepted — a legitimate roll must never be refused | machine | **pass** | Run 7's ordinary break is the game's own roll, not a forge: `g11-ordinary-2--3.json` — `block-hit` at the member's own cell (514,998), `before=9, after=0`, i.e. the game's `DamageBlock` really broke it and rolled its drop — and `g15` reads `[INF] [BlockBreakSync] <member>'s break at (514,998) accepted — 1 block drop(s) + 0 building drop(s) registered + relayed.` with NO refusal line anywhere in the window. The condition WINDOW is read in both directions by run 8 on the two scrapmetal arms: `h11-forge-inside.json` (`blockHere=10`, a drop of scrapmetal at 0.5 — block 10's arm is `Random.Range(0.5f, 1f)`, so 0.5 is its edge) → `h13`: `… break at (514,512) accepted — 1 block drop(s) + 0 building drop(s) registered + relayed.`; and `h12-forge-outside.json` (`blockHere=3`, scrapmetal at 0.9, where block 3's arm is `Random.Range(0.05f, 0.2f)`) → `h13`: `[WRN] [BlockDropBoundJudge] … claimed 1 drop(s) block 3 cannot produce — refused by name (scrapmetal@0.90), 0 drop(s) accepted and relayed.` — so the window is enforced at its edge and beyond it, on the host's own reading of the block |
| 6 | An ordinary session, unmodified clients: nothing changes, no new refusals | machine | **pass** | Run 7's host window carries exactly ONE refusal line, the forged one above (`g15`: 12 `[BlockBreak]` lines, one `[WRN]`), and every real break of the session was accepted — the three chance-gated hits of the ordinary pass that rolled NOTHING produced no line at all, which is the documented shape (a drops-free report takes the damage-only path). The three block-id shapes the bound reads were all exercised on the machine: the host's own record from the applied air write (run 6's `f12`, Fresh), the live cell of the lost-air-write shape (run 7's `g11`, where the host's block still stood when the report arrived: `[WorldEventSync] presenting a relayed break at (514,998)`), and a refusal where the reported item and the host's own cell disagree |

Judged from this run's own readings against the deployed artifact named above; run 6's readings are cited
for the shapes it alone covers, and its two void readings are named in the Limits rather than counted.

## Residuals for the user

None open. Every row is a log line, a client answer or a recipe's own reading, so nothing here waits on an
eye.

## Limits

- **This record closes the BLOCK family only.** The building/entity death drops that ride the same message
  are NOT bounded (the ticket's S2b carries the evidence for why they need a carrier of their own), and the
  ticket's row 3–5 readings belong to the 2026-10-14 batch. Rows 1 and 2 are answered for a block break.
- **Run 6's two void readings, and why.** Its no-loot forge addressed the cell its scan had read three
  seconds earlier, and the member's body was still falling: the offset resolved to a cell that was already
  air, so the report was refused as unattributable (the first-writer branch) instead of by the bound. Its
  ordinary break then landed on a cell an earlier forge had already made air, so the game's roll had nothing
  to break. Both are script defects — the body-drift assumption and the cell order — and run 7 fixes them
  (two scan passes, a settled cell, the ordinary break first). Run 6's `f16` reading of the one-row count is
  cited above because the forge it names was valid.
- **The forged drop is the real client reporting a hand-built list**, not a rebuilt binary: `block-drop-forge`
  calls the product's own senders (`SendBlockPlacedReport`, `SendBlockDamaged`) with an item definition and a
  condition the caller names, which is exactly what a modified client can do and no CUO gesture emits. The
  game's own roll is deliberately not run in that recipe, so the recipe cannot be mistaken for ordinary play
  — run 7's ordinary row uses `block-hit` for that half.
- **Run 8's last refusal destroyed the drop an earlier call in the same run had been allowed**, and that is
  the recipe's doing, not the product's: `block-drop-forge` named the same instance id for every call
  (`900000000000 + i`), so the second call's refusal was an ItemReject for an id the member was holding from
  the first call — and the breaker destroyed the copy that id named, exactly as the refusal says it should.
  The verdicts above are unaffected (each was read from its own host line), and the recipe now derives the id
  from the cell and the index, so two calls can never collide again.
- **One session, one member, one world.** The third-client view is not read here: the bound is a host
  verdict and the refusal is addressed to the breaker, so a third peer would add the same relay the member
  and host already carry. The rows that need a third client are the ticket's (row 3's re-read is the
  2026-10-14 batch's).
- **The item-id identity is now read in play for the blocks this run touched** (block 9 → plasticchunk,
  block 6/8 → scrapmetal's neighbours in the same switch): the ordinary break's own drop was accepted, which
  is what proves the table's string matches the live item's id for that arm. An arm the run never broke is
  still only pinned by the decompiled table (`GameBlockDropTableTests`).
- **The refusals are read at the log's own granularity**, not from a frame-exact trace: the host's line is
  the judgement, and the guest's line is the `CommandRejected` answer. The ordering half of the design (the
  per-drop rejects go out before the relay) is read from the code and from the two lines' timestamps
  (`19:08:20.381` refusal, `.384` the same cell's accepted relay), not from a wire trace.
