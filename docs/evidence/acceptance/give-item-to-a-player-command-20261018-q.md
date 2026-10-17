# Acceptance record — the `/give` command, SNBT form

- Ticket: `give-item-to-a-player-command` — verdict: **stays in `todo/`** (the destination ladder's rows 4, 5
  and 11 are the remaining work, and row 9 was not re-run). Rows 1, 2, 3, 6, 7, 8, 10 and 12 pass; row 2 is
  the liquid give that rejected the previous build.
- Batch: `20261018-q` — tickets `snbt-state-language`, `item-data-read-commands`,
  `item-and-entity-data-commands`.
- Commit: `30dbb252` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+30dbb2525ccd07d72ddd810c1781d068de2959a2` (verified by `tools/verify-deploy.ps1`, exit 0 before the
  clients started).
- Run: 2026-10-17 23:42:24 → 23:50:37 by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts` (preflight eleven present).
- Artifacts: the `g*`, `d*`, `m*`, `r*`, `s*` and `z*` readings in the directory named by
  `acceptance-artifacts-dir`. Steam IDs are written `<host>`/`<member>`; the artifact files carry the exact
  values.

The session: lobby `109775241230780534`, both clients `inWorld: true` and session-active before the first
row (`prelude.log`); the member is `<member>` and the host `<host>`. The batch drove every row through the
committed in-process driver (`tools/acceptance/drive-in-process.ps1`), one step per invocation, and read
each answer back before the next step.

| # | Row (the ticket's own, SNBT form) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host `/give @p bandage`: the answer names a new id and slot; `/data get id <id>` on BOTH peers reads the same document, owned by the member | machine | **pass** | `g1-host-give.json`: `Gave bandage (#1194321889219) to <member> in slot 0.`; `g1-host-read.json` and `g1-guest-read.json` both: `#1194321889219 bandage [carried by <member>, slot 0] revision 1` / `{Condition:1f,Favourited:false,Liquids:[],Components:{}}`; `m1-guest-local-tree.json`: the member's own scene holds `{"type":"bandage","id":"1194321889219","slot":0}` — the receiving client materialised it |
| 2 | Host `/give @p waterbottle{Liquids:[{LiquidId:"water",Amount:500}]}`: the answer carries the state, and `/data get id <id> Liquids[0].Amount` reads `500f` on the member's side | machine | **pass** | `g2-host-give.json`: `Gave waterbottle (#1198616856515) to <member> in slot 1 with state {Condition:1f,Favourited:false,Liquids:[{LiquidId:"water",Amount:500f}],Components:{}}.`; `g2-guest-amount.json`: `#1198616856515 waterbottle: Liquids[0].Amount = 500f`; `g2-host-read.json` reads the same document; the host log (`m3-host-give-data.log`) has `[Give] prepared waterbottle (#1198616856515) from its prefab with the operator's state.` and no exception |
| 3 | Host `/give @p bandage slot 9` (beyond the six-slot body): the answer names the slot it really used, not 9; `/data get` agrees | machine | **pass** | `g3-host-give.json`: `Gave bandage (#1202911823811) to <member> in slot 2.`; the host log: `[Give] destination slot 9 is not free for <member>; using slot 2 instead.`; `g3-host-read.json` and `g3-guest-read.json` both `slot 2` |
| 4 | A full body: the ladder's next rung (a carried container with weight headroom) takes the item and the answer names it | machine | **unproven** | the ladder is not implemented; this is the ticket's next unit |
| 5 | A granted member with a full body runs `/give @s bandage`: the same ladder, driven from the member's console | machine | **unproven** | same — the ladder |
| 6 | Member, switch open + grant: `/give @p bandage{Condition:0.5}` — the answer prints on the member's console and the host's `/data get` reads the new row | machine | **pass** | `d8-guest-rules.json`: `allowMemberCheatVerbs:true, forbidsMemberCheatVerbs:false`; the command returns no line, and `d9-guest-history-after.json`'s newest line is the host's answer: `Gave bandage (#1215796725699) to <host> in slot 2 with state {Condition:0.5f,Favourited:false,Liquids:[],Components:{}}.`; `d9-host-read.json`: `#1215796725699 bandage [carried by <host>, slot 2] revision 1`; the member's log carries `[Give] <member> requested bandage for <host> (slot -1, state {Condition:0.5d}) from the host.` — the state travelled as SNBT and the host re-bound it |
| 7 | Member, switch closed (announced): refused on the member's own console, the host's record unchanged | machine | **pass** | `g7-guest-rules.json`: `allowMemberCheatVerbs:false, forbidsMemberCheatVerbs:true`; `g7-member-give-closed.json`: `Refused locally: the host's cheat switch (AllowMemberCheatVerbs) is off, so no member may run a cheat verb (the host announces its rules to this client). Nothing was done here.`; `g7-host-after.json` still lists the same 4 items |
| 8 | Member, switch open, no grant: refused by the roster's sentence, not the switch's | machine | **pass** | `g8-guest-rules.json`: `allowMemberCheatVerbs:true, forbidsMemberCheatVerbs:false`; `g8-member-give-no-grant.json`: `Refused locally: the host's cheat roster (announced to this client) does not name you, so /give cannot land. The host grants it with /op. Nothing was done here.` |
| 9 | Member with the roster announcement swallowed: the request reaches the host and its roster refusal is the answer | machine | **not judged** | the blackout staging was not run in this batch; the previous batch judged the path, and nothing in this change touched the gate or the answer handler |
| 10 | A definition the game does not know (`/give @p notanitem`): the named refusal, nothing created | machine | **pass** | `g10-unknown-definition.json`: `Refused: No item definition 'notanitem' is known to this game — /give names a definition from the game's own item table.`; the host log has the same refusal and no `prepared notanitem` line |
| 11 | The ladder's last rung: no free slot, no carried container with headroom, no worn container — dropped at the player's position, both peers read it there | machine | **unproven** | same — the ladder |
| 12 | `/give @p bandage{Quality:5}` (unknown top-level key) and `/give @p bandage{Liquids:[…]}` on a definition with no liquid container: each refused BY NAME by the adapter and nothing created | machine | **pass** | `g12a-unknown-key.json`: `Refused: 'Quality' is not part of an item's state — the state carries Condition, Favourited, Liquids, Components.`; `g12b-not-a-container.json`: `Refused: this item has no liquid container, so its state cannot carry Liquids.`; `g12-member-before.json` and `g12b-member-after.json` both list the member's 4 items — nothing was created |

## Row 2 — the liquid give, the row that rejected the previous build

The previous batch's rejection was `ItemStateCodec.CaptureLiquids` calling `.Select` on a prefab template's
null stack list. This run's answer for the same verb is the state it bound, and the host log shows the
preparation line without an exception:

```text
[Give] prepared waterbottle (#1198616856515) from its prefab with the operator's state.
[Give] <member> receives created waterbottle (#1198616856515) in slot 1 (requested -1).
[Give] <host> gave waterbottle (#1198616856515) to <member> in slot 1 with state {Condition:1f,Favourited:false,Liquids:[{LiquidId:"water",Amount:500f}],Components:{}}.
```

The 500 ml is the definition's own capacity, read from `Item.Stats` before any instance exists; the empty
prefab capture is filled by the state, and the member's `/data get` reads `500f` back.

## What the run adds beyond the rows

- **The receiving client materialises the item, not only the record.** The previous record's limit ("the run
  did not read the receiving client's own local object") is closed: `m1-guest-local-tree.json` is the
  member's own scene tree — `bandage` at slot 0, `waterbottle` at slot 1 and `bandage` at slot 2, the three
  gives, with the same instance ids the kernel rows carry.
- **A container's condition follows its fill.** `d1-host-give.json` asked for `Condition:0.5` with 400 ml in
  the 500 ml bottle and the answer reports `0.8f` — the adapter's own rule (`ItemStateDocument`: a
  container's condition is its fill ratio, the value the game's `WaterContainerItem.UpdateCondition`
  maintains), not a lost key. The same rule moved the condition to `0.6f` when a `modify` changed one
  stack's amount (`d3-host-read.json`), which is why the merge row was judged on a non-container item.
- **The substitution is named, and only the substitution.** `slot 9` was substituted with `slot 2`, and the
  log line says so before the give line. A named slot that is free is not part of this batch's rows (the
  previous batch read it); here every give without a slot took the first free one (member slots 0 and 1, then
  the host's slot 2 for the member's give).
- **One log-wording nit, recorded rather than fixed here:** the write verbs' local-scene refusal line ends
  with a double period (`… Components..`), because the sentence appends `.` to an answer that already ends
  with one. It is the log's sentence, not the console's; the console answer is clean.

## Residuals for the user

None. Every judged row is a console answer, a history line, a scene tree or a log excerpt the run captured.

## Limits

- **Rows 4, 5 and 11 are `unproven` because their capability does not exist yet**, not because the run could
  not reach the world state: the destination ladder is the next unit, and the batch deliberately did not
  stage a full body to observe the old refusal (that expectation is gone).
- **Row 9 was not re-run.** It needs the inbound-blackout window; the gate and the answer handler are
  unchanged by this build, and the member's request path is judged by rows 6 and 12 here, but this batch
  does not claim row 9's verdict.
- **One member, no third client.** "Only the recipient's projection fires" is read from the two
  participants' records; a third peer's view is not claimed.
- **No reconnect.** Whether the given row survives a rejoin is not exercised here.
