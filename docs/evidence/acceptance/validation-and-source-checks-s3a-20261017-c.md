# Acceptance record — a member's use report may not create an item the host never judged

- Ticket: `docs/backlog/todo/validation-and-source-checks.md` — **S3a's acceptance row is READ and PASSES**;
  the ticket does NOT move, because S3's second clause ("in a state that permits that use") is still
  undeveloped code and the ticket says so in its own limits.
- Batch: `20261017-c`, run 1 — two clients in one world (host on the physical machine, the member in the
  `<sandbox-guest>` sandbox). Two clients, because the row is about what the HOST does with one member's report.
- Commit: `feed95c0` — Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+feed95c08a505523cb04b7211a8ae94aa3454367`, verified against this tree by
  `tools/verify-deploy.ps1` (exit 0; `b1-verify-deploy.txt`). The probe the run drives
  (`tools/acceptance/recipes/item-hold-unreported.cs`) is read from the tree by the driver, so it is not
  part of the deployed artifact.
- Run: 2026-10-17, 14:56:00 → 15:04:07 · Host: physical machine · Member: `<member>` (the forger)
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input` (the
  in-process driver and the scenario recipe), `logs`, `artifacts` — the preflight reported eleven present
  before the run (`b0-preflight.txt`).
- Artifacts (batch `20261017-c/`): `prelude.ps1`/`prelude.log` and `readings2.ps1`/`readings.log` (the
  scripts and their own traces), `b0`–`b1` (preflight, environment, deployed-artifact check), `b2`–`b9`
  (endpoints coming up, lobby, both clients in world), `b10`–`b19` (the reading), `b20`–`b21` (teardown).

## The forged member, and what the run substituted

The row needs a member HOLDING an item whose creation it never reported. The run reaches that state through
the product's own calls and rules, not through a patch: the item is created with the game's own grant
(`Utils.Create` + `Body.PickUpItem(force)`) inside CUO's own inventory-internal scope, so the pickup report is
suppressed by the product's own call classification; and because `Item.Start` allocates an instance id only
for a STANDALONE WORLD item, an item already in a body when `Start` runs is never reported as a spawn. The
instance id it holds is then stamped by the product's own allocator (`ItemIdAllocator.EnsureId`), which is the
one id source the client itself owns. From there every step is the game's and CUO's ordinary path: the use is
`Body.UseItem` (the radial release's own entry point), the report is the client's own `ItemUpdateState`, and
the way back is the carried-set statement the refusal makes due.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A member reports a use of an item the host never judged | machine | **pass** | the member's item `#30967793301` (`energydrink`, 250 ml, condition 1) is created and held with NO report (`b11-member-hold.json`, `depth:0` — the internal scope was released); its use reports itself — `b14-member-settlement.txt` `[ItemUsed] energydrink (id 30967793301) reported (digest)` — and the host refuses it BY NAME: `b13-host-refusal.txt` `[WRN] Kernel command from <member> refused: item 30967793301 was never judged, and that member's carried-set statement already was — a use report is not the creation claim for what a member carries, so the member re-states the set instead.` |
| 2 | The refusal is ANSWERED, and the member's own settlement is the re-stated carried set | machine | **pass** | the answer names the kind: `b14` `[WRN] Kernel command rejected by host for item 30967793301: ItemUpdateState refused (UnclaimedItem)`; the member then does what the ticket says it does — `b14` `Kernel command for item 30967793301 rejected (UnclaimedItem) — the host has never judged it; re-stating the carried set.`, `[CarriedInventory] the next carried-set report is due now: the host refused a use report for an item it has never judged.`, and one line later `re-reported 4 carried item(s) with self-assigned ids` |
| 3 | The re-stated set LANDS the item on the host with the state the use produced | machine | **pass** | the host's registration line follows the refusal in the same second: `b13` `[INF] Registered 1/4 carried items of <member> in the transfer table (3 already registered, 0 refused by the kernel)`; the host's own kernel row then reads `b15-host-item-state.json` `source:kernel, kind:Carried, owner:<member>, revision:1, liquid:energydrink=150, condition:0.6` — 150 ml is what the use left of the 250 ml the item was created with (`b12-member-use.json` `liquidBefore:energydrink=250, liquidAfter:energydrink=150`), and the 0.6 is the definition's own condition-from-contents (it scales with the fill the use drained) |
| 4 | The refusal does not destroy the member's copy | machine | **pass** | the same item survives the refusal on the reporting client — `b12` `alive:true` immediately after the use, and `b16-member-item-state.json` `source:body, held:true, liquid:energydrink=150, condition:0.6` after the settlement: the two sides end up agreeing on the same state, which is the property the settlement exists for |
| 5 | An ordinary stretch of the session meets no such refusal | machine | **pass** | the world-entry window (world entry → before the forged use) carries ZERO S3a refusals — `b10a-host-baseline-refusals.txt` `0 line(s)` over 849,746 bytes of host log; the whole session carries exactly ONE (`b17-host-session-refusals.txt`, `b19-host-whole-log-refusals.txt`, the forged one in row 1) and ZERO protocol-violation refusals (`b18`), so the bound does not fire on ordinary play and the one line it did emit is the report this run provoked |

Judged from this run's own readings against the deployed artifact named above.

This is an acceptance run, not a development cycle: it does not reset and does not fill
`docs/evidence/delivery-checklist.md` (workflow §1), whose boxes belong to the S3a development commit it is
reading. The only tracked source-adjacent change this run commits is the scenario recipe above, under
`tools/acceptance/`, which is not part of the deployed artifact; its gates were run before the commit.

## Residuals for the user

None open. Every row is a host log line, a member log line or a probe's own answer, so nothing here waits on
an eye.

## Limits

- **What the run substituted, and what that leaves unproven.** The forged client is the run's recipe acting
  inside this build, not a hand-edited assembly: it reaches "an item the host never judged" through the
  product's own suppression rules rather than by disabling them. The row therefore proves the HOST's half
  (`b13`, `b15`) and the member's own settlement (`b14`, `b16`) on a report that really crossed the wire; it
  does not prove that a binary-level patched client cannot produce some other frame this run never sent.
- **One field of `b16` is not a reading.** The member-side `read` answer was produced by the recipe's body
  branch as it stood during the run, which echoed `kind:"Carried"` as a literal; the branch now reports
  `where:"body"` instead. Row 4 rests on `source:body` + `held` + `liquid` + `condition`, never on that
  literal.
- **The item's id is this client's own space**, stamped by the product's allocator, so the host could in
  principle have recognised the space (it did not judge the id at all — that is the row). A forged client
  sending an id from ANOTHER member's space is a different refusal (the id-space/watermark rules) and is not
  read here.
- **No third client was in this world**, so "no other member is written to" is not read from a third view:
  what the run reads instead is that nothing durable was created before the settlement — the kernel row that
  appears in `b15` appears only after the registration line of row 3.
- **The refusal's window is one report, not a rate**: this run sent one forged use. The re-report budget a
  refused report consumes (`GuestCommandReconciliation`) is a different family and is not exercised here.
- The probe leftovers of the run's own bisecting (items created while the recipe was being got right) are the
  same shape as the row's item; they were landed by the ordinary statements and are visible in the
  registration counts (1/2 → 2/4 → 1/5) rather than hidden.
