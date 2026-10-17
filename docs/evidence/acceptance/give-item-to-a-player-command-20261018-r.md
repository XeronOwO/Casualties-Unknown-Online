# Acceptance record — the `/give` destination ladder (and the stale-roster row)

- Ticket: `give-item-to-a-player-command` — verdict: **moved to `done/`**. All twelve rows are now judged
  pass: rows 1, 2, 3, 6, 7, 8, 10 and 12 in batch `20261018-q` (record
  `give-item-to-a-player-command-20261018-q.md`), and rows 4, 5, 11 and 9 in this batch.
- Batch: `20261018-r` and its continuation `20261018-r2` (the ladder rows, then the swallowed-announcement
  row, each a fresh two-client run).
- Commit: `510b9cd1` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+510b9cd190a9d8ba52136a491b9ffebf43c58e7c` (verified by `tools/verify-deploy.ps1`, exit 0 before the
  clients started).
- Run: 00:23:15 → 00:26:32 and 00:27:06 → 00:28:42 by the clients' own log clock (2026-10-18) · Host:
  physical machine · Member: `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts` (preflight eleven present).
- Artifacts: the `r*`, `s*`, `m*` and `z*` readings in the directory named by `acceptance-artifacts-dir`;
  Steam IDs are written `<host>`/`<member>`.

## The ladder rows (`20261018-r`)

The session: lobby `109775241236139595`, both clients in the world; the member's body has six slots and one
starting supply (`emergencylight`, slot 3). The batch first re-read rung 1 (five gives landed in slots 0, 1,
2, 4 and 5), then staged the ladder.

| # | Row (the ticket's own) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 4 | The member fills its own body, then the host `/give`s: a container the member carries with weight headroom takes the item and the answer names it | machine | **pass** | the host gave a `plasticbag` (slot 0) and four bandages (slots 1, 2, 4, 5) — `r1-give-bag.json`, `r2-fill-1..4.json`; the next give answered `Gave bandage (#1142782281667) to <member> inside the carried container plasticbag (#1121307445187).` (`r4-give-into-container.json`); the host log: `[Give] container 1121307445187 (plasticbag) can hold bandage (0.90 g): holding 0.00/3.50 g, per item 3.50 g.` and `receives created bandage (id …) inside container 1121307445187 (parent slot 0)` (`m2-host-ladder.log`); the member's own scene tree holds the bandage with `"parentType":"plasticbag","slot":-1` (`r4-guest-local-tree.json`); both peers read the child as `#1142782281667 bandage [carried by <member>] revision 1` (`r4-host-child.json`, `r4-guest-child.json`) |
| 5 | A granted member whose own body is full runs `/give @s bandage` from its own console: the same ladder, the answer prints on the member's console and names where the item went | machine | **pass** | switch open + grant (`r5-rules-open.json`, `r5-op.json`); the command returns no line and the member's history's newest line is the host's answer `Gave bandage (#1147077248963) to <member> inside the carried container plasticbag (#1121307445187) with state {Condition:0.6f,Favourited:false,Liquids:[],Components:{}}.` (`r5-history-after.json`); the host log's capacity line for that give: `can hold bandage (0.58 g): holding 0.90/3.50 g, per item 3.50 g.` |
| 11 | The ladder's last rung: no free slot, no carried container — the item is DROPPED at the player's own position, the answer says so, and both peers read the item where it landed | machine | **pass** | the member dropped its container through the game's own drop (`r11-drop-bag.json`), the body was refilled to six items (`r11-member-full.json`), and the next give answered `Gave bandage (#1155667183555) to <member>, dropped at its position (the ladder's last rung).` (`r11-give-drop.json`); the host log: `receives created bandage (id 1155667183555) as a world drop at its own position (the ladder's last rung).` and `[ItemSpawn] materializing bandage (id 1155667183555) at (0.0,492.6)`; the member's log: `[Give] dropped bandage (id 1155667183555) at the local body's position (0.0,492.6) and reported its creation.`; the member's traffic line carries `Send ItemSpawnCommand=85B/1f`; BOTH peers read `#1155667183555 bandage [world (0, 492.615)] revision 1` (`r11-host-drop-read.json`, `r11-guest-drop-read.json`) |

**What row 11 proves about the report.** The dropped item has a world row on the host and on the member with
the same id and state — the receiving client's creation report is what put it there. The host's own
`[ItemSpawn] materializing` line is the accepted report arriving, not the host creating the item.

**The capacity reading** (a fixture fact for later container rows): the `plasticbag` prefab answers
`maxWeightPerItem 3.50 g`, `maxWeight 3.50 g`; the granted bandages weighed `0.90 g` and `0.58 g`. The bag
therefore takes only a few small items, which is why row 11's fixture drops the bag rather than filling it.

## The stale-roster row (`20261018-r2`)

The session: lobby `109775241236696743`; the member was granted (`s1-op.json`) and held an announced allow
roster (`s1-guest-rules.json`). The batch then armed the member's inbound blackout, revoked the grant on the
host, and disarmed — a 3-step window: `s2-blackout-on.json` (`armed:true, subscribersAfter:0, parked:true`),
`s2-host-deop.json` (`Revoked the cheat verbs …`), `s2-blackout-off.json` (`armed:false,
subscribersAfter:1, parked:false`).

| # | Row (the ticket's own) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 9 | A member whose roster announcement was swallowed: the request reaches the host, the host's roster refusal is the answer, printed on the member's console | machine | **pass** | the member's local roster still allowed it (the deop announcement was parked and dropped), so its `/give @p bandage` produced no local refusal — the command's own answer was the previous line (`s3-member-give.json`); `s3-history-after.json`'s newest line is the HOST's wording: `Refused: the host's cheat roster does not name you, so /give cannot land. The host grants it with /op.`; the host's record still lists its own single item and nothing was created (`s4-host-record.json`) |

## Residuals for the user

None.

## Limits

- **The live-container refusal branch is not staged.** If the container the host named refuses the item when
  it arrives (its live contents grew since the snapshot), the receiving client drops it there and reports
  that; the run did not race the snapshot, so this branch is read from code and left to a later race row.
- **One member, no third client; no reconnect.** The drop's row is read on the two participants, not from a
  third peer, and no rejoin is exercised.
- **The kernel's contents convention is unchanged**: a container child reads `[carried by <owner>]` with no
  slot (its parent's slot lives in the character snapshot), and when the member dropped the whole bag the two
  child rows stayed `[carried]` in the kernel — a pre-existing model boundary, not this change's, and not a
  row of this ticket.
