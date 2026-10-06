# Acceptance record — A same-frame second drop overwrites the pending report of the first

- Ticket: `drop-pending-single-slot-overwrite` — verdict: **moved to `done/`**: the re-scoped row's MACHINE half
  passes 2/2 again and its WORLD half now passes 2/2 — both children of one expansion reach the host's world
  table and are materialized by the third peer, with no `terminal` entry and no kernel refusal for either.
- Batch: `20261007-b` — tickets `drop-pending-single-slot-overwrite` and
  `second-drop-report-loses-its-world-object` (one fixture, one gesture shape, serves both)
- Commit: `208bab68` (the run's tree; the two commits above the fix commit are documentation-only) · Deployed
  artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+2675221cc96cbe1e8bc39a84e5ac1dfbeab21671`,
  verified against this tree's build output before the clients started
- Run: 2026-10-07 00:28 → 00:34 local · Host: physical machine (the operator) · Guest: sandbox `Steam1` (the
  owner) · Third peer: sandbox `Steam2`
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible;
  the alternate third client is configured`)
- Artifacts: `20261007-b/` in the directory named by `acceptance-artifacts-dir` — the session bring-up
  (`b1`–`b9`), the window closes (`c1-*`), the fixture staging (`f-s1`–`f-s12`, `f-tree-before`,
  `f-tree-before2`), the remote panel and button lists (`g1`, `g2`, `g8`, `g9`), the key holds and their read
  steps (`g3`–`g7`, `g10`–`g13`), the two releases (`r1`, `r2`), the host's authoritative tables (`w0`, `w1`,
  `w2`), the clone views (`c2-*`), the three log marks (`marks-h1-before.txt`, `marks-h2-before.txt`,
  `marks-h3-after.txt`), the local log helper (`log.ps1`) and the probe files (`p1`–`p2`)

## Batch scope

The run served the two tickets that wait on ONE world: a carried container holding an EMPTY nested container,
onto whose panel button an operator releases a second carried container with two light children. The gesture
is batch `20261006-h`'s route (b) fixture, re-driven on the same three roles so the two readings are
comparable. The other 65 tickets in `review/` are not served: 47 carry no acceptance record and each needs its
own world, save or fixture plan, and 18 carry records whose open rows were not re-planned here. Nothing in
this run changes their state.

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The gesture is reachable: a remote release of a carried container onto a NESTED container's panel button is captured as ONE `MoveContainerChildren` intent | machine | **pass (2/2)** | `r1-host-release.json`, `r2-host-release-repeat.json` — the cast resolves to the nested container (`castItem: "plasticbag"`, `castItemId: 13787924117`, `castIsContainer: true`, `castOverlaps: true`), the dragged proxy carries `childCount: 2` with `canHold: true` on both children, `calls: "none"` and `dragAfter: "none"`; the operator's window writes `[RemoteIntent] MoveContainerChildren captured for item 18082891413 of 76561198863287957 (container 13787924117, slot -1, body 0, limb -1, target item 0, trader none).` at 00:30:04.239 |
| 2 | R5's expansion onto a target that refuses every admitted child registers TWO departures out of the same frame and BOTH commit | machine | **pass (2/2)** | Owner window, gesture 1: `[ItemTrace] op=17 begin item=22377858709 origin=OnItemUnloadedFromContainer event=Unload` and `op=18 … item=26672826005 …`, both at 00:30:04.274/.275, each with its `[ContainerUnload] … left its container into the world from the CarriedInventory side`, beside the applier's own account `container expansion of item duffelbag into container 13787924117: 0 of 2 direct child item(s) entered the container; 2 did not`; then `[ItemDropped] dogfood (id 22377858709) at (0.6,423.0)` → `op=17 … origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]` and `[ItemDropped] dogfood (id 26672826005) …` → `op=18 … Committed(1)`, all at 00:30:04.310. Gesture 2 (`op=23`/`op=24` on ids 30967793301/35262760597) reads the same shape at 00:30:50.671 → `.688` |
| 3 | BOTH children reach the host's world table | machine | **pass (2/2)** | `w0-host-tables-before.json` (world 259, terminal 0) → `w1-host-tables-after.json` (**world 261, terminal 0**: `22377858709` and `26672826005` both in `world`) → `w2-host-tables-after-repeat.json` (**world 263, terminal 0**: `30967793301` and `35262760597` both in `world`, both earlier children still in `world`). No id of either gesture appears in `terminal` at any read |
| 4 | BOTH children are found by the third peer | machine | **pass (2/2)** | Third peer window: `[ItemSpawn] materializing dogfood (id 22377858709) at (0.6,423.0)` 00:30:04.340 and `[ItemSpawn] materializing dogfood (id 26672826005) …` 00:30:04.349, each preceded by its own `[ItemDrop] … not present — requesting materialization`; gesture 2 the same for `35262760597` (00:30:50.731) and `30967793301` (00:30:50.739). **No `[ItemBind]` line for any of the four ids on any client**, and no `[ItemTrace] … origin=OnItemDestroyed`, no `Kernel command rejected` and no `InvalidTransition` in the whole session on all three clients |
| 5 | The operator's and the third peer's clone-fact monitor stays at zero across the gesture and a quiet cycle | machine | **pass (2/2)** | Both gesture windows (`marks-h1-before.txt`, `marks-h2-before.txt`) filtered for `divergence` read `NO MATCHING LINES` on the operator and on the third peer; the quiet window (`marks-h3-after.txt` → the closing read, 00:31:40 → 00:32:15, ~35 s of the monitor's 1 Hz snapshot cycles) reads `NO MATCHING LINES` for `divergence`, `InvalidTransition`, `Kernel command rejected`, `OnItemDestroyed` and `ItemBind` on all three clients. The whole session carries exactly four `[CharSync] divergence` lines per viewer, all of them the FIXTURE STAGING's create shape (below) |

## What the run read about the row's premise

- **The shape the ticket re-scoped onto reproduces exactly, twice.** Both gestures captured ONE
  `MoveContainerChildren` intent, the owner's applier reported `0 of 2 direct child item(s) entered the
  container; 2 did not` for both, and each gesture registered its two departures out of ONE frame
  (00:30:04.274/.275 and 00:30:50.671/.671) and committed both on the next frame (.310 and .688). Two runs on
  independently staged children, same shape — the machine half this ticket owns is unchanged and stands 2/2.
- **The world half, which failed 2/2 in batch `20261006-h`, now passes 2/2.** Neither viewer lost the second
  child: the host's kernel holds all four children in `world` with `terminalCount: 0` after each gesture, and
  the third peer materialized every one of them with its own `[ItemSpawn]`. The line shape changed exactly as
  `done/second-drop-report-loses-its-world-object.md` predicted for its fix: the second child's report no
  longer takes `[ItemBind] bound existing dogfood … (no materialization)`, so the retired clone proxy is no
  longer adopted, and the destroy that used to follow it is absent from the whole session.
- **The zero-monitor criterion holds, and the divergences in the log are the staging's.** The only
  `[CharSync] divergence` lines the session carries are four per viewer, all the known create shape of
  `item-provide mode=create` + `container-fill` — `a new carried item the fact table never saw — a pickup
  without an event sync` for `trashbag (id 9492956821)` at 00:29:34.436/.456, for
  `duffelbag (id 18082891413)` at 00:29:36.437/.445, and for the second fixture's two created `dogfood`s at
  00:30:36.480/.491 and 00:30:37.474/.478 (operator / third peer). Every one of them predates the mark that
  opens its gesture's window, and neither gesture window nor the quiet window contains one. These belong to
  the declared setup substitution of the recipes, not to the gesture.
- **The owner's own reading is unchanged, which is what the sibling ticket's runtime row asks.** Its own
  client re-placed both of its own objects in both gestures (`[ItemDrop] dogfood (id 22377858709) present —
  re-placing at (0.6,423.0), container 0.` 00:30:04.343 and its sibling at .346; `35262760597` and
  `30967793301` at (-1.2,423.4) 00:30:50.731/.732), and the two viewers' clone trees afterwards hold four
  proxies for the owner with `orphanCount: 0` — the two children are gone from the clone tree because they
  left the inventory, not because an id was stamped onto a proxy (`c2-host-clone-owner.json`,
  `c2-alt-clone-owner.json`).
- **The hold the gesture needs was verified at both edges.** `key-hold bind=expanddesc,action=down` reported
  `posted: true` (the bind resolves to LeftShift, vk 160) and the FOLLOWING read step reported
  `heldAtEnd: true` before each release; after each release the `up` step's read reported
  `heldAtEnd: false`. `osKeyAtEnd: 0` and `isForeground: false` in every read: the key is delivered to the
  client's own window queue and never through OS-level input.

## Residuals for the user

None: every row of this ticket is a machine row, and each one names its evidence.

## Limits

- **One owner direction.** The owner was the sandbox guest and the operator the physical-machine host in
  every row; the mirrored direction (a guest operating the host's inventory) was not driven.
- **Two gestures, one target definition, one world.** The gesture was driven twice on independently created
  children inside one session; the run cannot say how often a receiver finds an id-less same-prefab copy
  inside the adopt tolerance in other worlds — that shape belongs to the sibling ticket, whose fixture this
  run reused.
- **No live object census on the receivers.** "Found by the third peer" is judged from the third peer's and
  the operator's own `[ItemSpawn] materializing` lines plus the host's authoritative table, exactly as the
  row writes it; no per-receiver object scan of the four ids was taken, and the ad-hoc probe written for one
  was refused by the evaluator (it cannot compile a submission carrying a string literal through the local
  probe client, while the committed recipes run through the driver's own channel).
- **The owner's carried containers are all slot occupants.** The nested `plasticbag` target was EMPTY, as the
  fixture requires; a nested container whose own contents must be rendered is a different renderer path and
  is `todo/nested-container-clone-proxy-leaks-as-world-item.md`'s row.
- **The staging divergences are the recipes' declared substitution.** `item-provide mode=create` adds an item
  after the first registration report, so the 1 Hz snapshot carries it before the fact table knows it. The
  run did not try to remove that shape, and no row here depends on it.
- **No wire, save or feel reading.** No messages and no saves were read, and no hand-feel was judged.
