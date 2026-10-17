# Acceptance record — a member's native-use claim is bounded by the item's own data

- Ticket: `docs/backlog/review/validation-and-source-checks.md` — **row 7 (S3b) is READ and PASSES**; the
  ticket stays in `review/` for row 5 only (see *Rows 5 and the S2b loop read* below).
- Batch: `20261017-o`, run 1 — two clients in one world (host on the physical machine, the member in the
  `<sandbox-guest>` sandbox). Two clients, because the row is about what the HOST does with one member's
  report and how that member's own object is settled.
- Commit: `f76644c3` — Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f76644c3f096c11a7d702baaafe1c5166b4fcc6b`, verified against this tree by
  `tools/verify-deploy.ps1` (exit 0; `b2-verify-deploy.txt`). The two probes the run drives
  (`tools/acceptance/recipes/item-use-forge.cs`, `tools/acceptance/recipes/building-support-cells.cs`) are
  read from the tree by the driver, so they are not part of the deployed artifact.
- Run: 2026-10-17, 17:34:05 → 17:47 (readings) · Host: physical machine · Member: `<member>` (the forger)
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input` (the
  in-process driver and the two scenario recipes), `logs`, `artifacts` — the preflight reported eleven
  present before the run (`b0-preflight.txt`), and the install's active BepInEx tree was CUO's
  (`b1-session-environment.txt`, `action=already-cuo`).
- Artifacts (batch `20261017-o/`): `prelude.ps1`/`prelude.log` (the launch), `log.ps1` (the byte-mark log
  reader), `b0`-`b2` (preflight, environment, deployed-artifact check), `b3`-`b10` (endpoints, lobby, both
  clients in world), `r1`-`r35` (the readings, in the order the run took them), `driver.log` (every driver
  call's own trace).

## The forged member, and what the run substituted

The row needs a member SENDING a native-use report no gesture of this client can produce: an item whose own
data says no native use exists (`ItemInfo.usable` clear). The run reaches that state through the product's
own calls and rules, not through a patch:

- the item is created with the game's own grant (`Utils.Create` + `Body.PickUpItem(force)`) inside CUO's
  own inventory-internal scope, so the pickup report is suppressed by the product's own call
  classification, and its instance id is stamped by the product's own allocator
  (`ItemIdAllocator.EnsureId`) — the same staging `item-hold-unreported` uses, which is what makes the item
  a fact the host learns from the carried-set statement rather than from a creation report;
- the carried-set statement then lands it on the host (`r4`-`r6`), so the host holds a row for the id and
  judges the USE claim rather than the creation (an unjudged id would be refused by the
  creation-before-operation rule, which proves nothing about this row);
- the "use that already ran" is the forged member's own `condition` write on its own object (`mode=apply`,
  `r10`), read back in its own step (`r11`) so the value the settlement has to repair is a reading and not
  a claim;
- the report itself is ONE hand-built `WireCommand` of kind `ItemUse` sent through
  `IKernelProtocolControl.SendCommand` (`mode=forge`), carrying exactly the state a native use would have
  reported. The real `Body.UseItem` cannot produce this report: `UseItemPatches` mirrors the game's own
  guard, so an item with `usable` clear reaches no report at all.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A member reports a native use of an item whose own data says it cannot be used (`ItemInfo.usable` clear) | machine | **pass** | the host refuses it BY NAME, naming the item, its definition and the fact: `r23-host-session-use-not-permitted.txt` `Command admission refused (answered): UpdateItemStateCommand actor <member> sender <member> — UseNotPermitted: item <id> is 'bandage', whose own data says no native use exists (ItemInfo.usable is clear) — both of the game's use call sites would have run something else.` The member's own registry answers the same `usable:false` for that definition (`r3-guest-hold-bandage.json`), and the verdict is the HOST's read of its own registry (`GameItemUseFacts`), not the report's identity |
| 2 | The refusal is ANSWERED | machine | **pass** | the member receives it: `r24-guest-session-use-refusals.txt` `Kernel command rejected by host for item <id>: ItemUse refused (UseNotPermitted).` — the answer names the kind it judged (`RefusedKind`), and the same call's traffic line counts the pair (`r7`/`r12` `Send ItemUseCommand=64B/1f`, `Receive CommandRejected=43B/1f`) |
| 3 | The sender's already-applied state is put back to the recorded one | machine | **pass** | the pair is read on both ends of the settlement: `r10` applies `condition 0.4` and `r11-guest-read-applied.json` reads the local object back at `localCondition: 0.4` while the record says `recordedCondition: 1`; the forged report (`r12`) is then refused and `r13-guest-read-settled.json` reads `localCondition: 1` again — with `r2` `[ItemState] the host refused the use to #<id> (bandage, UseNotPermitted) — the recorded state is back on the local object (condition 1).` and `[ItemWrite] #<id> (bandage) written: condition 1` beside it |
| 4 | The host's own row is unchanged | machine | **pass** | `r6-host-read.json` (before: `recordedKind: Carried`, `recordedOwner: <member>`, `recordedCondition: 1`, `recordedRevision: 1`) and `r9-host-read-after.json` (after: identical) — the refusal wrote nothing, twice (`r7`, `r12`) |
| 5 | An ordinary session meets no such refusal | machine | **pass** | the WHOLE session carries exactly TWO `UseNotPermitted` lines, both the forged reports this run sent (`r23`), and ZERO protocol-violation or command-rule refusals (`r35-host-session-admission-refusals.txt`: `Command admission refused` = 2, `Kernel command from … refused` = 0, `Protocol violation` = 0). The ordinary stretch before the first forged report (world entry → the first `mark`, ~52 s of play, registration and item traffic) carries none |

| # | Control the bound is read against | Class | Verdict | Evidence |
|---|---|---|---|---|
| 6 | The SAME forged shape, with an item whose own data DOES mark it usable, is admitted | machine | **pass** | `r20` forges a `painkillers` use (`usable:true`) at `condition 0.5`; the host answers nothing, records the claim (`r21` `recordedCondition: 0.5`, `recordedRevision: 3`, up from 2) and the member's own object keeps it (`r22` `localCondition: 0.5`, no settlement line in `r4`/`g4`) — so the refusal in rows 1-4 is the ITEM's data, not the forged shape |
| 7 | An ordinary native use still passes, and lands as the holder's fact (row 6's S3b half) | machine | **pass** | a real `Body.UseItem` of the same `painkillers` on the member (`r17`) reports through the ordinary path (`g3` `[ItemUsed] painkillers (id <id>) reported (use digest).`), the host refuses nothing and applies it (`r3` `[CarriedSync] applied painkillers … to <member>'s snapshot`), and both sides read the post-use state — `liquid 90`, `condition 0.9`, `revision 2` (`r18`, `r19`) |

Judged from this run's own readings against the deployed artifact named above.

## Rows 5 and the S2b loop read — what this batch could not settle

- **Row 5 (a report arriving before its creation).** Its REFUSAL half — the creation-before-operation
  invariant, which is what the ticket's own gloss names — was read on the machine in batch `20261017-c`
  (`docs/evidence/acceptance/validation-and-source-checks-s3a-20261017-c.md`, row 1: a use report for an id
  the host never judged, refused by name and answered; the same invariant covers the use kind via
  `IsItemOperation`). Its ACCEPTED half — the pre-statement window, where such a report is materialized
  instead — was NOT staged: the window is now spent by the carried-set statement at world entry, so staging
  it is a race against the member's first statement, and this run did not attempt it. Verdict: **unproven**,
  and the ticket stays in `review/` for it.
- **The S2b owed machine read (the host's support-loss capture loop).** Read, but only its population half:
  `building-support-cells` found 40 cells carrying more than one `requireGround` building (one with three;
  host and member agree on 1902 entities / 1357 `requireGround` / 40 cells, `r28`, `r29`), and breaking the
  support block of the three-building cell `503,327` from the member made the host capture `15 drop row(s)
  from 3 building(s) … 3 were still unmarked` and then accept the breaker's own 10 building drops against
  them (`r5` `[BuildingSupport] captured …` and `[BlockBreak] … accepted — 0 block drop(s) + 10 building
  drop(s) registered + relayed`). What that does NOT prove is the reordered half — an ALREADY-MARKED
  entity's table contributing rows: in this staging the air-write relay and the break report land ~100 ms
  apart (two host frames), so by the second capture the entities are gone, and a marked building with
  health left is a one-frame state in production (a mark is applied together with the health that kills it
  on its next `Update`). Verdict for that half: **unproven**, with the candidate cells named in `r28`.
- **The gun chain's call site** (S3b's own "Not covered" line) was not read: this game build's item
  registry — the 123 definition ids in the reversed `Item.SetupItems` — carries no gun, and `GunStateSync`
  only reports a `GunScript` that has an `Item` sibling, so no carried gun can be staged on this map. The
  ticket's existing wording stands unchanged.

## Residuals for the user

None open. Every row is a host log line, a member log line or a probe's own answer, so nothing here waits
on an eye.

## Limits

- **What the run substituted, and what that leaves unproven.** The forged client is the run's recipe acting
  inside this build, not a hand-edited assembly: it writes the post-use state itself and sends the frame the
  product's own sender would have sent. The row therefore proves the HOST's bound, the ANSWER and the
  member's own settlement on a report that really crossed the wire; it does not prove that a binary-level
  patched client cannot produce some other frame this run never sent.
- **The apply/read pair is the run's own staging step, not a game call.** A real native use cannot run on an
  item whose own data forbids one (`UseItemPatches` mirrors the guard), which is exactly why the value is
  written by the recipe. The settlement's own write is the product's (`[ItemWrite]` from `LocalItemAccess`).
- **The control's claim left the two sides' liquid disagreeing.** The admitted forged use (row 6) carried no
  liquids, so the host's record dropped the item's 90 units while the member's local object kept them
  (`r21` `recordedLiquid` empty, `r22` `localLiquid: painkillers=90`). That is the documented pre-release
  trust model — an item's numbers are its holder's claim — and the run read it rather than smoothed over
  it; the value is not covered by this row.
- **The refusal's window is one report per forged send, not a rate**: this run sent two forged bandage
  reports. The re-report budget a refused report consumes (`GuestCommandReconciliation`) is a different
  family and is not exercised here.
- **One building family only.** The S2b half above read `requireGround` plants on a surface cell; it says
  nothing about a marked entity's rows, a building that does not depend on its ground, or a cell whose
  population changes between the census and the break.
- **No third client was in this world**, so "no other member is written to" is not read from a third view;
  what the run reads instead is that the refused report wrote nothing on the host at all (row 4).

The commit that carries this record is the one below it in the log; the deployed artifact the run drove is
`f76644c3`, and the delta above it is tools-and-docs only (the two recipes and this record), so the DLL's
own source is unchanged between them.
