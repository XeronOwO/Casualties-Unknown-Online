# Acceptance record — a report before its creation claim, and the marked building's rows

- Batch: `20261017-p`, run 1 — two clients in one world (host on the physical machine, the member in the
  `<member>` sandbox).
- Tickets: `docs/backlog/done/validation-and-source-checks.md` — **row 5's accepted half is READ and
  PASSES** (its refusal half was read in batch `20261017-c`), and **S2b's owed reordered half — an
  ALREADY-MARKED building's table contributing rows — is READ and PASSES**. With both read, every
  acceptance row of the ticket is read; the ticket moves to `done/` with this record.
- Commit: `cddbe954` — Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+cddbe95484b12efae0dc379d3b0ddc6b1dfc1cfb`, verified against this tree by
  `tools/verify-deploy.ps1` (exit 0; `b2-verify-deploy.txt`). The product's source is unchanged since
  `f76644c3` (the S3b commit): `d64ae236` and `cddbe954` are a docs delta and a tools delta. The probes the
  run drives (`carried-registration`, `use-before-carried-set`, `building-support-cells`,
  `block-drop-forge`, `item-use-forge`, `net-receive-blackout`) are read from the tree by the driver, so
  they are not part of the deployed artifact.
- Run: 2026-10-17, 18:09:13 → 18:14:05 (readings) · Host: physical machine · Member: `<member>` (the forger).
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input` (the
  in-process driver and its recipes), `logs`, `artifacts` — the preflight reported eleven present before
  the run (`b0-preflight.txt`), and the install's active BepInEx tree was CUO's
  (`b1-session-environment.txt`).
- Artifacts (batch `20261017-p/`): `prelude.ps1`/`prelude.log` (the launch — it deliberately stops after
  the lobby is joined), `wait-entry.ps1`/`entry-wait.log` (the entry wait), `log.ps1` (the byte-mark log
  reader), `b0`-`b2` (the gates), `s1`-`s25` (the readings in the order the run took them), `driver.log`.

## The window row 5 lives in, and what the run substituted

The host records whether it has judged a member's carried-set statement, and that statement is the
CREATION CLAIM for what the member carries: before it is judged, a use report for an id the host holds no
row for is materialized accepted-first (`KernelProtocolCommandHandler.Handle` →
`HandleMissingCarriedUpdate`: "the heal IS the claim"); after it is judged, the same report is refused by
name (S3a). At a fresh world entry the member's reporter sends that statement one frame after the
generation-finished edge — batch `20261017-o` read that this is before any in-process probe can run — and
the ticket named the join/reconnect edge as the avenue left.

This run took the other production route into the same window:

- the host's INBOUND BLACKOUT (the committed `net-receive-blackout` recipe, the declared substitution for
  the documented lazy-P2P swallow the registration window exists for) was armed before `start-run`, so the
  member's entry statement and its repeats never reached the host;
- the member's registration cadence was then FROZEN through the schedule's own `Reset`
  (`carried-registration mode=freeze`, a named substitution), so the report could be put AHEAD of the
  re-statement instead of racing it;
- `carried-registration mode=arm` re-opened the same window through the public item control afterwards,
  and that statement went out through the product's own reporter.

What the staging substitutes is the LOSS of the statement frame (dropped at the host rather than lost by
the network) and the one-step hold on the member's cadence. What the host does with the report, and with
the statement that follows, is the product's own path.

## Row 5 — a report arriving before its creation claim

The member created a `painkillers` through the game's own grant, inside the product's inventory-internal
scope and with the product's own allocator stamping the id (`item-use-forge mode=hold`: condition 1,
`usable:true`), wrote the post-use state on its own object with nothing sent (`use-before-carried-set
mode=apply`: condition 0.62), read it back, and only then sent ONE hand-built `ItemUse` frame carrying
exactly that state (the same frame the product's own sender emits). The recipe refuses that frame if the
local object does not hold the reported value, so the report can never carry a state the object does not
have.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The report arrives while the host has NOT judged the member's carried-set statement | machine | **pass** | `s7-host-status-before.json`: `memberHandshaken:true`, `judged:false`; the member had in fact stated its set at the entry — its log reads `reported 1 carried item(s) with self-assigned ids` at 18:10:10.870 and re-reported it every 5 s — and the host had seen none of it, because the statement frames were dropped at the inbound blackout |
| 2 | The host holds no row of that id, and the report is the member's already-applied state | machine | **pass** | `s12-host-read-before.json`: `recorded:false`, `recordedKind:"none"`; `s11-guest-read-applied.json`: `held:true`, `localCondition:0.62`, `recorded:false` — the member's own mirror has no row either, so the not-yet-recorded state is a reading on both sides |
| 3 | The report is ACCEPTED and materialized, not refused | machine | **pass** | `s14-host-read-after.json`: `recorded:true`, `recordedKind:"Carried"`, `recordedOwner:"<member>"`, `recordedCondition:0.62`, `recordedRevision:1`; the host's own line: `Accepted-first missing carried update for item <item> from <member>: spawned carried fact.` (18:10:52.315); and its clone fact table applied the same row to the member's snapshot 2 ms earlier. NO answer was sent for it: the member's session log carries no `Kernel command rejected` for this item, and the whole session's rejections are the two `ItemDestroy` refusals of the restore edge (named below) |
| 4 | The creation claim arrives LATER and is judged — the documented order | machine | **pass** | after `carried-registration mode=arm` (18:11:26.276) the member's own reporter sent the statement (`reported 2 carried item(s) with self-assigned ids`, 18:11:26.290), and the host judged it: `s18-host-status-after-statement.json` `judged:true` (it was `false` in `s15`, taken after the materialization), and the host's line `Registered 1/2 carried items of <member> in the transfer table (1 already registered, 0 refused by the kernel)` (18:11:26.314) |
| 5 | Both sides agree after the statement; the member keeps its item | machine | **pass** | `s19-host-read-after-statement.json`: `Carried` / owner `<member>` / condition `0.62` / revision `1` — unchanged by the statement; `s20-guest-read-after-statement.json`: `held:true`, `localCondition:0.62`, and the member's mirror now records the same row (`recorded:true`, `0.62`, revision 1) |

One fact the run does not smooth over: the item's id had already reached the host's RENDER through the
member's 1 Hz character data — the host's own line names it (`[CharSync] divergence for <member>'s
painkillers (id <item>): a new carried item the fact table never saw — a pickup without an event sync (the
1 Hz snapshot carried it)`), and `s16-host-transfer-before-statement.json` shows the transfer table holding
exactly that one entry (rebuilt from the kernel after the materialization). The KERNEL had no row and the
creation claim was unjudged, which is what the row is about; the record says so instead of calling the
report the host's first knowledge of the id.

## The reordered capture S2b owed — an already-marked building's table contributes rows

The capture (`WorldBuildingEntitySync.MarkSupportLossRemote`) reads every `requireGround` building standing
on the cell, marked or not, and the marker only decides whether THAT call marks the death. Batch
`20261017-o` could not read the marked case: the relayed air write and the break report landed in different
host frames, and by the second capture the entities were gone. This run read it on the production path with
the LOST-AIR-WRITE verdict, where ONE handler call runs the capture twice — before the block write and
after it — with the entity alive across both because its death is its own next `Update`:

- census (`building-support-cells min=2 limit=8`): 1916 entities, 1354 `requireGround`, 46 cells carrying
  more than one, 8 listed — `s22-host-support-cells.json`. The chosen cell `(215,14)` carried two
  `leadbush` entities, each with three `blobflesh` rows at the condition window `1..1`; the rows are the
  product's own reading (`GameBuildingDropTable.ForEntity`), not the probe's arithmetic.
- the forged report (`block-drop-forge`, `airwrite=0`, `building=1`, absolute cell): a member's break of
  that cell with NO air write and ONE building drop, `blobflesh@1` — a drop the cell's own rows can
  account for. The cell's block stood on both sides (`blockHere:15` on the member), so the host judged the
  shape as this generation's lost air write.

| # | Reading | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The FIRST capture finds the cell's buildings unmarked and takes their rows | machine | **pass** | host log 18:13:29.049: `[BuildingSupport] captured 6 drop row(s) from 2 building(s) losing support at cell (215,14) — 2 were still unmarked and were marked here`, with one `marked remote death` line per building |
| 2 | The break is accepted and its LEGAL building drop registered and relayed | machine | **pass** | host log 18:13:29.066: `[BlockBreak] <member>'s break at (215,14) accepted as this generation's lost air write — 0 block drop(s) + 1 building drop(s) registered + relayed`; the member materialized the relayed drop (`[ItemSpawn] materializing blobflesh (id <drop>) at (215.0,14.0)`) |
| 3 | The SECOND capture, in the SAME call, finds the SAME buildings ALREADY MARKED and still captures all their rows | machine | **pass** | host log 18:13:29.080, 31 ms after the first: `[BuildingSupport] captured 6 drop row(s) from 2 building(s) losing support at cell (215,14) — 0 were still unmarked and were marked here` — same row count, same building count, the marker now present, and the entity still alive because its death is applied on its own next `Update` |
| 4 | Nothing was refused | machine | **pass** | the `r2` host window holds no `refused`, `cannot produce` or `Protocol violation` line; the member side mirrors both captures and the relayed block (`Block placed at (215,14) type 0`), and the drop it was told about is the one it materialized |

## What else the session recorded, named rather than buried

- **The two protocol violations of the entry edge.** At 18:10:36 the host refused two `ItemDestroy`
  reports (items `<item>` and `<supply>`) as operations on items whose creation it had never judged — the
  two and only refusals of the whole session on each side. Their cause is the staging, not row 5: with the
  entry-group completion marker swallowed by the blackout, the member's readiness window re-asserted its
  scene state five times; when the fifth landed (18:10:35.627) the host ran the entry path, sent the saved
  character data (`Sent saved character data to <member> (2 items)`), and the member applied that restore
  (`Applying character restore (2 items)`), which replaced its two local objects and reported their
  destroys. The host answered each with the creation-before-operation refusal — row 5's REFUSAL half, in
  the same session — and the member settled them. The rows above are unaffected; the two lines are quoted
  here so a later reader does not find them unexplained.
- **The member's normal traffic is unaffected by the freeze.** After `mode=arm` the reporter re-asserted
  the set every 5 s exactly as its cadence documents (`10 dense report(s) left in this window` …), and the
  host kept judging each statement.

## Residuals for the user

None open. Every row is a host log line, a member log line or a probe's own answer.

## Limits

- **The window was entered through dropped frames, not a network loss.** The host's handling of both the
  report and the later statement is the production path; the absence of the statement frames is the run's
  doing, and the record names it as such.
- **The freeze/arm pair steers the member's cadence.** `mode=freeze` calls the schedule's own `Reset`;
  `mode=arm` is the public item-control entry. Neither is a path a player can take, and neither decides
  anything the host does.
- **One member, no third view.** The row is about the host's handling of one member's report, so a third
  client would add nothing to it; it also means "no other member sees the item" is not read from a third
  side.
- **The building rows are one cell's rows.** The two `leadbush` entities' six rows are the product's own
  table for those entities; the record does not claim anything about a cell whose population changes
  between the census and the break, about a building that does not depend on its ground, or about the
  gun chain (this build's item registry carries no gun; S3b's own limit stands unchanged).
- **The member's starting supply was not registered by the statement in this session.** It was carried and
  its id was destroyed with the restore edge; the statement that was finally judged carried the restored
  copy plus the report's item, and the host registered the one it did not already hold (`1/2`, the other
  already known). The row reads the report's item, not the starting supply.

The commit that carries this record is the one that moves the ticket to `done/`; the artifact it was read
against is named at the top.
