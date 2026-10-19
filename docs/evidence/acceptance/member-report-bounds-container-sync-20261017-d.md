# Acceptance record — a member's container report may not create what the member never carried

- Ticket: `docs/backlog/done/member-report-bounds.md` — **the container-sync machine row is READ and PASSES**; the
  ticket does NOT move, because sections B and C still hold rows with no code at all (the ticket's own list).
- Batch: `20261017-d`, run 1 — two clients in one world (host on the physical machine, the member in the
  `<sandbox-guest>` sandbox). Two clients, because the row is about what the HOST does with one member's report.
- Commit: `e054d332` — Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+e054d33262c65d537e1e956da6b5d5f58eea1ef9`, verified against this tree by `tools/verify-deploy.ps1`
  (exit 0; `b1-verify-deploy.txt`). The recipes the run drives
  (`tools/acceptance/recipes/container-sync-unreported.cs`, plus `item-hold-unreported` and `container-fill` for the
  second arm) are read from the tree by the driver, so they are not part of the deployed artifact.
- Run: 2026-10-17, 15:53:33 → 15:56:46 · Host: physical machine · Member: `<member>` (the forger)
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input` (the in-process
  driver and the scenario recipes), `logs`, `artifacts` — the preflight reported eleven present before the run
  (`b0-preflight.txt`), and the environment gate reported `active=cuo` with no game process (`b0-session-environment.txt`).
- Artifacts (batch `20261017-d/`): `prelude.ps1`/`prelude.log` and `log.ps1` (the scripts and their own traces),
  `b0`–`b1` (preflight, environment, deployed-artifact check), `b2`–`b9` (endpoints coming up, lobby, both clients in
  world), `b10`–`b35` (the readings), `b36`–`b37` (teardown).

## The forged member, and what the run substituted

The row needs a member reporting a container fact that names a container and a content item the host never judged.
The run reaches that state through the product's own calls and rules, not through a patch: both items are created with
the game's own grant (`Utils.Create` + `Body.PickUpItem(force)`) inside CUO's own inventory-internal scope, so the
pickup report is suppressed by the product's own call classification, and `Item.Start` allocates an id only for a
STANDALONE WORLD item, so an item already in a body when `Start` runs is never reported as a spawn. Both ids are then
stamped by the product's own allocator (`ItemIdAllocator.EnsureId`), the one id source a client owns. From there every
step is the game's and CUO's ordinary path: the content goes into the container through `Container.LoadItem` (the
drag-UI entry the CUO container patches hook), so the report is the client's own `ItemContainerSync`, and the way back
is the carried-set statement the refusal makes due.

The member entered the world carrying one item (`emergencylight`, judged by the host as a set of 1 at 15:54:10.648 —
`b10-host-registration.txt`), which is the precondition the statement arm needs: `HasJudgedCarriedSet(member)` holds,
so a container report is no longer the creation claim.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A container report naming a parent the host never judged is refused BY NAME on the host | machine | **pass** | the staged container `trashbag` `#9492956821` holds `waterbottle` `#13787924117` (`b12-member-stage.json`: `canHold:true, distance:1.337, loaded:true, held:true, contentsCount:1, childLiquid:"water=500", depth:0`); the member reports it — `b14-member-settlement.txt` `[ItemTrace] op=104 item=13787924117 origin=OnItemLoadedIntoContainer result=Committed events=[ContainerContent]` + `[ContainerLoad] waterbottle (id 13787924117) moved inside body container trashbag — root content event up to trashbag (id 9492956821).` — and the host refuses it: `b13-host-refusal.txt` / `b19-host-session-container-refusals.txt` `[WRN] Container sync from <member> refused: container 9492956821 was never judged, and that member's carried-set statement already was — a container report is not the creation claim for what a member carries, so the member re-states the set instead.` |
| 2 | The refusal is ANSWERED, and the member's own settlement is the re-stated carried set — never the pickup rollback | machine | **pass** | the answer names the kind: `b14` `[WRN] Kernel command rejected by host for item 9492956821: ItemContainerSync refused (UnclaimedItem).`; the member then does what the ticket says it does — `[INF] Kernel command for container 9492956821 rejected (UnclaimedItem) — container reports settle by re-stating the carried set, never by rolling the container back.`, `[DBG] [CarriedInventory] the next carried-set report is due now: the host refused a container report.`, and 10 ms later `[DBG] [CarriedInventory] re-reported 2 carried item(s) with self-assigned ids (6 dense report(s) left in this window).` The same window's own traffic counter reads one report and one answer: `Send ItemContainerSyncCommand=114B/1f … Receive CommandRejected=43B/1f` |
| 3 | The re-stated set LANDS the container AND its content, with the state they carried | machine | **pass** | the host judges the statement 20 ms after the re-report: `b13` `Registered 1 content item(s) inside carried container 9492956821 of <member>.` + `Registered 1/2 carried items of <member> in the transfer table (1 already registered, 0 refused by the kernel).` The host's own tables then hold the tree — `b15-host-tables.json`: `table[1] = {id:9492956821, type:trashbag, isTransferred:true, contents:["13787924117"]}`, `carried = [5197989525, 9492956821]` both `owner:<member>`, `contained = [{13787924117, parent:9492956821}]`, `terminal: []`. The kernel row of each half carries the data: `b16-host-container-state.json` `source:kernel, kind:Carried, owner:<member>, revision:1, condition:1`; `b17-host-content-state.json` `source:kernel, kind:Contained, owner:<member>, revision:1, liquid:"water=500", condition:1` — the 500 ml is the content's own state as the statement captured it |
| 4 | The refusal does not destroy or detach the member's own copy | machine | **pass** | after the settlement the reporting client still holds the tree it reported — `b18-member-local-tree-after.json`: `trashbag #9492956821 slot 0` with `children [{waterbottle, 13787924117}]`, plus the child's own row reading `parentType:"trashbag"`, and the emergency light untouched in slot 3 |
| 5 | The refused report itself wrote nothing: no row was created before the settlement | machine | **pass** | the staged window carries ZERO accepted-sync lines (`b25-host-not-executed.txt`: no `Container sync for … : N child fact(s)`, no spawn/creation line), and no registration line precedes the refusal: `b13`'s first line IS the refusal at 15:54:30.570 and the first registration line is the statement's at 15:54:30.658. The kernel's terminal half stayed empty throughout (`b15` `terminal: []`, and the one contained row is the child the statement landed) |
| 6 | The CHILD arm reads its own refusal: a known parent with an unjudged child | machine | **pass** | a second unreported item is held (`b26-member-hold-child.json`: `dogfood #18082891413, condition 1, depth 0`) and loaded into the ALREADY-JUDGED bag through the game's own path (`b27-member-fill-child.json`: `containerId:9492956821, contentsBefore:1, contentsAfter:2, children [waterbottle 13787924117, dogfood 18082891413]`); the host answers with the child's own branch: `b28-host-child-arm.txt` `[WRN] Container sync from <member> refused: content item 18082891413 of container 9492956821 was never judged, and that member's carried-set statement already was — the statement carries a carried container's contents, so the member re-states the set instead.` |
| 7 | The child arm is ANSWERED, and its re-statement lands the child under its container | machine | **pass** | `b29-member-child-arm.txt`: `[ContainerLoad] dogfood (id 18082891413) moved inside body container trashbag — root content event up to trashbag (id 9492956821).` → `[WRN] Kernel command rejected by host for item 9492956821: ItemContainerSync refused (UnclaimedItem).` → the same two settlement lines, then `re-reported 2 carried item(s) with self-assigned ids (0 dense report(s) left in this window)` — the dense window was EMPTY, so this re-statement is the refusal's own, not the cadence's; the host judges it 10 ms later (`b28` `Registered 1 content item(s) inside carried container 9492956821 of <member>.`). The kernel then holds both children — `b30-host-tables-after-child.json`: `contained = [{13787924117 → 9492956821}, {18082891413 → 9492956821}]`, `carriedCount:2, containedCount:2, terminalCount:0` — and the second child's own row reads `b34-host-child2-state.json` `source:kernel, kind:Contained, owner:<member>, revision:1, condition:1`. The member's local tree still shows both under the bag (`b35-member-local-tree-final.json`) |
| 8 | The ordinary session meets no such refusal | machine | **pass** | the window from world entry to the stage carries ZERO of these lines (`b22-host-baseline-refusals.txt`: pre-stage 0); the whole session carries exactly TWO, both provoked by this run and both named above (`b31-host-session-container-refusals.txt`); zero protocol-violation refusals across the whole session (`b20`/`b32`); and the host's stretch after each settlement carries zero matching lines (`b23-host-quiet-stretch.txt`, `b33-host-quiet-stretch.txt`), while the member's stretch after the settlements carries no rejection at all (`b24-member-quiet-stretch.txt`, whose single match is a crafting line reading `0 refused`) |

Judged from this run's own readings against the deployed artifact named above.

The commit that carries this record is `44525454`, and it is rebuilt and redeployed as
`0.1.0+44525454d1fc4eccdaee3c4f92037cae0ae4c948` (`tools/verify-deploy.ps1`, exit 0) — a tools-and-docs delta above
the artifact the run drove, named here rather than left implicit: the DLL's own source is unchanged between the two
commits, and the run's verdicts belong to `e054d332`.

This is an acceptance run, not a development cycle: it does not reset and does not fill
`docs/evidence/delivery-checklist.md` (workflow §1), whose boxes belong to the `e054d332` development commit it is
reading. The only tracked source-adjacent change this run commits is the scenario recipe above, under
`tools/acceptance/`, which is not part of the deployed artifact; the gates were run before the commit.

## Residuals for the user

None open. Every row is a host log line, a member log line or a probe's own answer, so nothing here waits on an eye.

## Limits

- **What the run substituted, and what that leaves unproven.** The forged client is the run's recipes acting inside
  this build, not a hand-edited assembly: they reach "a container and a content item the host never judged" through the
  product's own suppression rules rather than by disabling them. The row therefore proves the HOST's half and the
  member's own settlement on reports that really crossed the wire; it does not prove that a binary-level patched client
  cannot produce some other frame this run never sent.
- **The container's id is this member's own space**, stamped by the product's allocator, so the host could in principle
  have recognised the space (it did not — that is the row). A report naming ANOTHER member's rows is a different
  refusal, the admission seam's, read in batch `20261014-a`.
- **The transfer table's entry keeps its registration-time capture.** `b30` shows the bag's entry `contents` still
  listing only the waterbottle after the dogfood landed: `RegisterCarried` never rewrites an entry it already holds
  ("the host's entry is the arbitrated record"), so that field is a capture, not a second authority. The kernel's
  `contained` rows are the record and carry both children; this run reads the landing from them, not from the table.
- **One field of the staging recipe's own answer was not a reading.** `b12` reports `contents[0].id` as `0` because the
  recipe read the marker's `Id` as a FIELD while the marker exposes a PROPERTY; the ids in this record come from the
  allocator's `itemId` and from the host's own tables, never from that field. The recipe is fixed in the commit that
  carries this record, and the fixed branch is not re-driven here (a second stage would add a third provoked refusal to
  the session without changing any verdict).
- **The session carries two provoked reports, not one.** Both are named in `b31`, and both were this run's own. The
  ordinary-session claim therefore rests on the pre-stage window (`b22`) and the post-settlement stretches
  (`b23`/`b33`), never on the whole-session count being one.
- **The mirrored shape is not driven on the machine**: the child arm used a KNOWN parent with an UNKNOWN child; "a known
  child moved under an unknown parent" and "another member's row" are the unit cases' and the seam's own readings.
- **The state that landed is the state that TRAVELLED**, not a change that converged across the round trip: the
  waterbottle's 500 ml and condition 1 are its capture's own values. Whether a post-refusal change to that state
  converges is S3a's row (`docs/evidence/acceptance/validation-and-source-checks-s3a-20261017-c.md`), not this one.
- **No third client was in this world**, so "no other member is written to" is not read from a third view: what the run
  reads instead is that nothing durable was created before the settlement and that the kernel's terminal half stayed
  empty throughout.
