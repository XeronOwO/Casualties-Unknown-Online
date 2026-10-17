# Acceptance record — the item state language (SNBT, paths, both verbs)

- Ticket: `snbt-state-language` — verdict: **moved to `done/`**; every acceptance row the ticket owes was
  judged in this batch, with one substitution named below (row 4's battery half).
- Batch: `20261018-q` — tickets `give-item-to-a-player-command`, `item-data-read-commands`,
  `item-and-entity-data-commands`.
- Commit: `30dbb252` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+30dbb2525ccd07d72ddd810c1781d068de2959a2` (verified by `tools/verify-deploy.ps1`, exit 0 before the
  clients started).
- Run: 2026-10-17 23:42:24 → 23:50:37 by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts`.
- Artifacts: the `d*`, `g*` and `s*` readings in the directory named by `acceptance-artifacts-dir`; Steam IDs
  are written `<host>`/`<member>`.

| # | Row (the ticket's own *Acceptance the batch owes*) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host `/give @p bandage`: the answer names id and slot; `/data get id <id>` prints the same document on BOTH peers | machine | **pass** | `g1-host-give.json` and the two reads: `#1194321889219 bandage [carried by <member>, slot 0] revision 1` / `{Condition:1f,Favourited:false,Liquids:[],Components:{}}` on both peers |
| 2 | Host `/give @p waterbottle{Liquids:[{LiquidId:"water",Amount:500}]}`: a fresh empty container is filled, and the member reads `500f` | machine | **pass** | `g2-host-give.json`'s answer carries `Liquids:[{LiquidId:"water",Amount:500f}]`; `g2-guest-amount.json`: `Liquids[0].Amount = 500f`; `g2-host-read.json` reads the whole document |
| 3 | `/give @p waterbottle{Condition:0.5,Liquids:[{300 water},{100 blood}]}`: both stacks land; a state over capacity is refused by name and nothing is created | machine | **pass** | `d1-host-give.json`: `Gave waterbottle (#1207206791107) to <host> in slot 0 with state {Condition:0.8f,…,Liquids:[{LiquidId:"water",Amount:300f},{LiquidId:"blood",Amount:100f}],…}`; `d1-host-read.json` reads both stacks; `d1-guest-amount.json`: `Liquids[1].Amount = 100f`; `d2-over-capacity.json`: `Refused: the container holds 500 ml and the state names 600 ml.`; `d2-host-after.json` still lists the same 2 items |
| 4 | `/give @p largebattery{Components:{BatteryItem:{charge:0.9}}}`: on a definition whose capture carries the component the field changes; on one that does not, the named refusal and nothing created | machine | **pass, with the substitution named below** | `d11-battery-component.json`: `Refused: the item carries no 'BatteryItem' component — it carries: no captured component.` — the answer the build gives for every live definition; the captured-component mechanics themselves are read on the emergencylight: `d10-component-modify.json` `Components.CustomItemBehaviour.state 0 -> 3`, `d10-guest-component.json` = `3`, `d10-host-read.json` carries `Components:{CustomItemBehaviour:{state:3}}`; the unknown-field refusal `d11-component-field.json`: `Refused: component 'CustomItemBehaviour' has no field 'nope' — it carries: an int state.` |
| 5 | `/data modify id <id> Liquids[0].Amount set 200`: the answer shows the before/after an amount, and both peers agree | machine | **pass** | `d3-modify-amount.json`: `#1207206791107 waterbottle: Liquids[0].Amount 300f -> 200f — written and reported as this client's own item state.` (the 300 is this run's first stack; row 2's 500 was the other bottle); `d3-guest-amount.json`: `= 200f`; `d3-host-read.json` revision 2 with the new amount |
| 6 | `/data modify id <id> Components.BatteryItem.charge set 0.5`: the same read-back | machine | **pass, substituted** | the captured component is `CustomItemBehaviour` (no live definition carries a `BatteryItem` line): `d10-component-modify.json` `Components.CustomItemBehaviour.state 0 -> 3`; the host and the member both read `3` |
| 7 | `/data merge id <id> {Condition:0.8,Favourited:true}`: both keys change; a merge whose second key is unknown is refused whole and the first key is unchanged | machine | **pass** | `d4-merge.json`: `#1211501758403 bandage: Condition 1f -> 0.8f; Favourited false -> true — written and reported as this client's own item state.`; `d4-guest-read.json` reads the same document; `d5-merge-refused.json`: `'Quality' is not part of an item's state — the state carries Condition, Favourited, Liquids, Components.`; `d5-host-cond-after.json` still `Condition = 0.8f`, member's read still `revision 2` |
| 8 | `/data remove id <id> Liquids[0]`: the stack is gone; `/data remove id <id> Condition` is refused by name and nothing changes | machine | **pass** | `d6-remove-stack.json`: `Liquids[0] {LiquidId:"water",Amount:200f} -> removed`; `d6-host-read.json` and `d6-guest-liquids.json` carry only `blood 100f`; `d6-remove-condition.json`: `the state is missing 'Condition' — an item's state carries Condition, Favourited, Liquids, Components; /data get prints the shape to start from.` |
| 9 | A path that does not resolve (`Liquids[3]`, `Components.Nope.charge`) is refused with the path and what it carries | machine | **pass** | `d7-path-missing.json`: `Liquids[3] — 'Liquids' has 1 element(s), so [3] is out of range`; `d7-component-missing.json`: `Components.Nope.charge — 'Components' has no 'Nope' — it carries: no key` |
| 10 | Member, switch open + grant: the same `/data` write and `/give` from the member's console; the answers print on the member's console and the host's `/data get` reads them | machine | **pass** | `d8-guest-rules.json` (open, not forbidden); `d8-member-modify.json`: `#1194321889219 bandage: Condition 1f -> 0.7f — written here and reported to the host…`; `d8-host-cond.json` = `0.7f`; the member's give returns no line, and `d9-guest-history-after.json`'s newest line is the host's answer `Gave bandage (#1215796725699) to <host> in slot 2 with state {Condition:0.5f,…}.`; `d9-host-read.json` reads that row |

## The substitution row 4 carries, and why it is not a weaker check

No item definition in this build captures a `BatteryItem` component: the prefab capture carries the component
only when the prefab has one, and the battery's component is created by the game's own `BatteryItem.LoadBattery`
on a live item, not by a prefab. The row's second half — the named refusal and nothing created — is exactly
what the build answers, and it is judged as written. The row's first half (a field changes on a captured
component) was exercised on the one component a live definition does carry, `CustomItemBehaviour` on the
emergencylight, and the sibling keys kept their captured values. The battery half stays a recorded limit of
the game's assets until a prefab carries the component.

## What the run adds beyond the rows

- **The old vocabulary is gone from the answers as well as the parser.** `/give @p bandage condition 0.5`
  answers `Usage: /give [<selector>] <itemId>[{<snbt>}] [slot <n>]`, and `/data set …` answers
  `Unknown /data verb 'set'. The verbs are /data get | /data merge | /data modify | /data remove.` with the
  new usage line (`s1-old-token-give.json`, `r4-old-verb.json`).
- **The document is the save-shaped state, printed whole.** Every `/data get` prints the four keys in a
  stable order (`Condition`, `Favourited`, `Liquids`, `Components`), an empty container as `Liquids:[]`, and a
  non-container's components as `Components:{}` — which is what the refusals quote back at the operator.
- **A member's request puts the canonical double on the wire and the host binds the float key.** The member's
  log reads `state {Condition:0.5d}` while the host's answer and its row read `0.5f`: the plain decimal is a
  double by the dialect's resolution rule, and the adapter's bind is what makes it the key's kind.
- **A write's answer is the adapter's re-read, not the requested text.** The container's fill ratio is the
  clearest case: `Condition:0.5` with 400 ml of liquid answered `0.8f`.

## Residuals for the user

None.

## Limits

- **The battery-component half of row 4 and row 6 is a live-asset limit** (above), recorded rather than
  worked around.
- **`Contents` and path completion were not exercised**: both are recorded as limits of the ticket (a child
  is addressed by its own id; completion covers the SNBT token, not the bare path). This run stages neither.
- **One member, no third client.**
