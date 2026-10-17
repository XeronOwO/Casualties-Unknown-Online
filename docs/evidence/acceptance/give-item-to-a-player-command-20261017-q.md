# Acceptance record — the `/give` command

- Ticket: `give-item-to-a-player-command` — verdict: **back to `todo/`** (status field
  `- Status: Todo — Rejected (row 2: a liquid give throws in the prefab capture)`); rows 1 and 3-10 pass,
  row 2 fails.
- Batch: `20261017-q` — tickets `cheat-permission-and-op-commands`, `item-and-entity-data-commands`.
- Commit: `bb2350e7` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+bb2350e760208d6e3cce3effd5917caef141bae4` (verified by `tools/verify-deploy.ps1`, exit 0).
- Run: 2026-10-17 21:46:18 → 21:52:14 · Host: physical machine · Member: `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts` (preflight eleven present).
- Artifacts: the `d*` readings, `d33-give-exception.log`, `r1-host-startup.log` and `r2-host-log.txt` in the
  directory named by `acceptance-artifacts-dir`. Steam IDs are written `<host>`/`<member>` below; the
  artifact files carry the exact values.

The session: lobby `109775241214232602`, both clients `inWorld: true` (`prelude.log`), member
`<member>`, host `<host>`; the host's own verb needs neither the switch nor the roster (its standing is the
session), and the member's permission state is stated per row.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host `/give @p bandage`: the answer names a new id and slot; both peers read the same row, owned by the member | machine | **pass** | `d1-host-give.json`: `Gave bandage (#2353963059139) to <member> in slot 0.`; `d2-host-read.json` and `d3-guest-read.json` both: `#2353963059139 bandage [carried by <member>, slot 0] revision 1 / condition 1` |
| 2 | Host `/give @p waterbottle liquid water 500`: the answer carries `liquid water … -> 500 ml`, read back on the member | machine | **fail** | `d4-host-give.json`: `Command error: Value cannot be null. Parameter name: source`; `d33-give-exception.log` (the host's own log): the throw is `System.ArgumentNullException` in `ItemStateCodec.CaptureLiquids` → `CaptureItem` → `ItemGrantSource.TryCreate` → `PlayerItemGiveService.Apply`; the log carries NO `prepared waterbottle` line (zero `waterbottle` occurrences), so nothing was created and nothing was delivered |
| 3 | Host `/give @p bandage slot 9` (beyond the six-slot body): the answer names the slot it really used | machine | **pass** | `d5-host-give-slot9.json`: `Gave bandage (#2358258026435) to <member> in slot 1.`; `d6-guest-read.json`: `slot 1` on the member |
| 4 | The member fills its own body; the host's give is the named no-free-slot refusal and the member's record is unchanged | machine | **pass** | the member's body filled through the game's own grant (`d8-guest-provide-1..3.json`: slots 2, 4, 5 — the created fact carries `slotIndex -1`, which is the fixture's shape); `d9-host-give-full.json`: `Refused: <member> has no empty inventory slot.`; `d10-guest-data-after-full.json` still lists 6 items and the same ids |
| 5 | A granted member whose own body is full runs `/give @s bandage`: the refusal prints on the member's console | machine | **pass** | `d11-host-op.json` (granted), `d12-guest-give-self.json` (the command returns no line of its own), `d13-guest-history.json`: the member console's newest line is `Refused: <member> has no empty inventory slot.` |
| 6 | Member, switch open + grant, `/give @p bandage`: the answer prints on the member's console and the host's `/data get` reads the new row | machine | **pass** | `d14` (command), `d15-guest-history.json`: newest line `Gave bandage (#2371142928323) to <host> in slot 0.` (`@p` on a member is the nearest REMOTE body, i.e. the host); `d16-host-read.json`: `#2371142928323 bandage [carried by <host>, slot 0] revision 1` |
| 7 | Member, switch closed (announced): refused on the member's own console, host record unchanged | machine | **pass** | `d17-host-rules-closed.json`, `d18-guest-rules.json`: `allowMemberCheatVerbs:false, forbidsMemberCheatVerbs:true`; `d19-guest-give-closed.json`: `Refused locally: the host's cheat switch (AllowMemberCheatVerbs) is off, so no member may run a cheat verb (the host announces its rules to this client). Nothing was done here.`; `d32-host-data-final.json` lists the host's 2 items only (its own and row 6's) |
| 8 | Member, switch open, no grant: refused by the roster's sentence | machine | **pass** | `d21`/`d22` (switch open, grant revoked), `d23-guest-rules.json`: `allowMemberCheatVerbs:true, forbidsMemberCheatVerbs:false`; `d24-guest-give-roster.json`: `Refused locally: the host's cheat roster (announced to this client) does not name you, so /give cannot land. The host grants it with /op. Nothing was done here.` |
| 9 | Member with the roster announcement swallowed: the request reaches the host, the host's refusal is the answer, printed on the member's console | machine | **pass** | staged: grant (`d25`), the member's inbound blackout armed (`d26`: `subscribersAfter:0, parked:true`), the host's `/deop` announced into the blackout (`d27`), blackout off (`d28`: `subscribersAfter:1`); the member's `/give` produced NO new local refusal (the console count went 19 → 20 with one new line) and `d30-guest-history.json`'s newest line is the HOST's wording: `Refused: the host's cheat roster does not name you, so /give cannot land. The host grants it with /op.` — a stale ALLOW list admitted the request locally and the host's verdict settled it |
| 10 | A definition the game does not know (`/give @p notanitem`): the named refusal, nothing created | machine | **pass** | `d20-host-give-unknown.json`: `Refused: No item definition 'notanitem' is known to this game — /give names a definition from the game's own item table.`; the host log carries no `prepared notanitem` line |

## Row 2 — the failure, and its root cause

`/give @p waterbottle liquid water 500` never reached the property application: `ItemGrantSource.TryCreate`
captures the definition from its PREFAB template, and the capture reads the liquid container's public stack
field:

```text
System.ArgumentNullException: Value cannot be null.
Parameter name: source
  at System.Linq.Enumerable.Select[TSource,TResult] (...)
  at CasualtiesUnknownOnline.GameAdapter.Items.ItemStateCodec.CaptureLiquids (Item item)
  at CasualtiesUnknownOnline.GameAdapter.Items.ItemStateCodec.CaptureItem (Item item, Int32 slotIndex)
  at CasualtiesUnknownOnline.GameAdapter.Items.ItemGrantSource.TryCreate (...)
  at CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.PlayerItemGiveService.Apply (...)
```

A prefab template is not an instantiated item: its `WaterContainerItem.stack` is null until the game
initializes it, and `CaptureLiquids` calls `.Select(...)` on it unconditionally. The bandage rows pass
because a bandage has no `WaterContainerItem` at all (the capture returns an empty list before the stack is
read). The console prints the exception as `Command error:` and nothing is created or delivered — the
verb's named-refusal contract is broken for every liquid-carrying definition, not only the waterbottle.

This is a product defect, and the ticket moves back to `todo/`; the fix (capture a null stack as an empty
one) is ordinary development work and belongs to the next cycle, together with the final-pass review's
findings recorded on the ticket.

## The row-2 re-run after the fix (2026-10-17 22:04, artifact `0.1.0+02bc1f6a`)

The exception is gone and the verb answers again, but the row's 500 ml expectation is NOT reached: the
give creates the waterbottle and the property application then refuses, by name, because the created fact
carries no water stack.

- `e6-host-give-liquid.json`: `Refused: #1241566529475 carries no 'water' — its liquids are: none (an item
  with no liquid container reads the same way).`
- the host's log (`r3-host-log.txt`): `[Give] prepared waterbottle (id 1241566529475) from its prefab.` and
  then the refusal — no exception, one created-then-refused give, nothing delivered.

This is the SHARED grammar's own semantics, not a new give defect: `ItemDataWriteValues.ApplyLiquid` sets the
amount of an EXISTING stack and refuses when none matches, and its comment says the capture cannot tell "no
liquid container" from "an empty container" (both capture as no stacks — `e7-waterbottle-info.json` shows the
definition is a `LiquidItemInfo` that loads). So a fresh, empty container cannot be filled by
`/give … liquid water 500` under the landed grammar; the row as written asks for a capability the property
vocabulary does not have. Whether that capability should exist (a container flag in the save shape plus an
add-a-stack semantic, shared with `/data set`) is a design question the run hands to the user rather than
deciding itself.

The row-2 DEFECT the batch rejected (the exception) is fixed and its re-run evidence is above; the row's
expectation is what needs re-deriving.

## What else the run shows

- **The user's destination ladder (2026-10-17) supersedes rows 4 and 5's expectation.** The user ruled, after
  this run, that a give fills a body slot first, then a carried container with weight headroom, then the
  contents of a worn container, and finally drops at the player's position. Rows 4 and 5 above were judged
  against the ticket's PRE-ruling text ("the named no-free-slot refusal"), which the run read exactly as
  written; the ruling replaces that expectation with the ladder, so those two rows are re-derived in the
  ticket and the follow-up cycle — the run's evidence for them stands as what the old design did.
- **The answer path is asynchronous, as documented.** Rows 5, 6 and 9 each had the command return no line
  and the host's answer arrive a frame later on the member's console; reading the command call's own
  `lastLine` would have judged the previous line (the first attempt at row 6 did exactly that).
- **The `@p` selector on a member names the host**, not itself (`d15`), which is the selector's own
  definition (nearest REMOTE body); the ticket's rows assume exactly that and the answers confirm it.
- **The host's record is the shared read-back** for every row: `d2`, `d3`, `d16`, `d32` all read the same
  kernel rows from both sides.

## Residuals for the user

None. Every row is a console answer, a history line or a log excerpt the run captured.

## Limits

- **The property grammar was exercised with one liquid and no component.** Row 2 is the liquid case and it
  failed before applying anything; a component give (`/give … component …`), a condition give and a
  favourited give were not part of the batch's rows.
- **The body-filled row's fixture items carry `slotIndex -1`** (the game's own grant shape); the row is
  about the destination's free-slot verdict, not about how those items landed.
- **One member, no third client.** "Only the recipient's projection fires" is read from the two
  participants' records, not from a third peer's view.
- **The run did not read the receiving client's own materialization** of the given item (its local object):
  the rows read the kernel record on both peers, which is the ticket's own evidence. A local-scene probe of
  the member's body was not part of the batch.
