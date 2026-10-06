# Acceptance record — Container moves reach the viewer as a snapshot, not an event

- Ticket: `container-move-snapshot-only-sync` — verdict: **back to `todo/`**, rejected on row A1g′ (unproven)
  and A1z, with the carrier defect itself verified fixed (row A1g passes in BOTH owner directions)
- Batch: `20261006-c` — tickets `container-move-snapshot-only-sync`,
  `drop-pending-single-slot-overwrite`
- Commit: `8da00be3` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+8da00be32f5ae66c8ea5e45957b86d0051823cf4`
- Run: 2026-10-06 12:14 → 12:21 local · Host: physical machine · Guest + third peer: Sandboxie sandboxes
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`,
  `artifacts` (preflight: 11 present, `RESULT: OK`)
- Artifacts: the JSON probes and the byte-marked log windows named below, in the directory
  `acceptance-artifacts-dir` under `20261006-c/`; the failure analysis is
  `20261006-c/review-20261006-c.md` (the cycle's independent review)

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| A1g | Container expansion (`MoveContainerChildren`), remote-driven, BOTH owner directions: the owner's child load announces the TARGET container's fact and no viewer warns | machine | **pass** | direction 1 (host owner, guest operator) 12:15:33 and direction 2 (guest owner, host operator) 12:16:49 — both named below |
| A1g′ | The same gesture driven LOCALLY (the owner's own release, key held, no operator) | machine | **unproven** | four attempts (12:17:42, 12:19:21, 12:20:47 and one discarded fixture): the native expansion's first half ran and its second half did not, so the world never reached the state this row needs — `m5`/`m12`/`p7` releases, `p4`/`p9` trees, `marks-a4/a5/a8` log windows |
| A1c′ | The driver capability the row needs: `declare` + a held key, read back per step | machine | **pass** | guest `g1-guest-declare.json` (`declared: true`), `k1`/`k2` (`heldAtEnd: true`, `osKeyAtEnd: 0`, `isForeground: false`) and `k4`/`k5` (released); host `g4-host-declare.json`, `k7`/`k8` (same reading), `k10`/`k11`; the Online UI window was closed first on both |
| A1z | Zero warnings on both viewers across the gesture window | machine | **fail** | the remote-driven windows read ZERO on all three clients over the gesture plus a quiet cycle; the warning the row fails on belongs entirely to the A1g′ window (guest + third peer, once each) |
| A1d | Container insert, take-out, slot release and the drop kinds in both owner directions (the batch-`20261006-a` row set) | machine | **not re-driven** | this batch exists for A1g and the new departure machine; their `20261006-a` verdicts stand and are not re-claimed here |

## Row A1g: what the run read

**Direction 1 — the host owns, the guest operates** (`[RemoteIntent] MoveContainerChildren captured for item
1117012477891 of 76561198281246659 (container 1121307445187 …)`, 12:15:33.632). The owner's own client ran the
native pair and reported it as ONE container move:

```text
12:15:33.659 [ItemTrace] op=5 begin item=1125602412483 origin=OnItemUnloadedFromContainer event=Unload
12:15:33.661 [ContainerUnload] dogfood (id 1125602412483) left its container into the world from the
           CarriedInventory side — the drop report waits one frame (a container load in this bracket re-homes it
           and reports the target's fact).
12:15:33.662 [ItemTrace] op=5 item=1125602412483 origin=OnItemLoadedIntoContainer result=Cancelled
           events=[LoadedIntoContainer]
12:15:33.664 [ItemTrace] op=6 item=1125602412483 origin=OnItemLoadedIntoContainer result=Committed
           events=[ContainerContent]
12:15:33.664 [ContainerLoad] dogfood (id 1125602412483) moved inside body container trashbag — root content
           event up to trashbag (id 1121307445187).
12:15:33.664 [RemoteIntent] container expansion of item trashbag into container 1121307445187: 1 of 1 direct
           child item(s) entered the container; 0 did not (the native guard, or a native load refusal).
```

The departure is registered and then CONSUMED by the load that completes the pair (op 5 ends `Cancelled`), and the
one report the move produces is the TARGET bag's carried-root contents fact (op 6, `ContainerContent`, root =
`1121307445187`, the target) — where batch `20261006-b` read a pickup of the child instead. Both viewers applied
exactly that fact (`[CarriedSync] applied trashbag (id 1121307445187) to …'s snapshot — re-rendering the clone.`,
guest 12:15:33.674, third peer 12:15:33.688) and neither materialized a world copy: the `[ItemDrop] … not present —
requesting materialization` and `[CarriedSync] removed …` pair of the rejected batch is absent. The owner's own tree
confirms the native effect: the dogfood sat in the bag in slot 0 before (`f5-host-tree.json`) and in the bag in slot 1
after (`k6-host-tree-after.json`).

**Direction 2 — the guest owns, the host operates** (`[RemoteIntent] MoveContainerChildren captured for item
9492956821 of 76561198863287957 (container 13787924117 …)`, 12:16:49.270). The owner's client reads the same way
(`op=17 … OnItemUnloadedFromContainer` → `[ContainerUnload] … from the CarriedInventory side` → `op=17 … Cancelled
events=[LoadedIntoContainer]` → `op=18 … Committed events=[ContainerContent]` → `[ContainerLoad] … root content
event up to trashbag (id 13787924117)`), the guest's tree shows the child in the target bag (`k12`), and the peers
applied the target root's fact. This is the direction whose peers USED to materialize the child as a world item and
drop it out of the owner's clone.

**The monitor.** `marks-a2.txt` (direction 1) and `marks-a3.txt` (direction 2) were read on all three clients,
each covering the gesture plus a quiet window longer than one 1 Hz character snapshot cycle:
`[CharSync] divergence` count = **0 on all three clients in both directions**.

## Row A1g′: why the local control is unproven

The local release was driven four times with batch `20261006-b`'s own fixture (a bag holding one dogfood in slot 0,
an empty bag in slot 1, the `expanddesc` key held and read back, the Online UI window closed). The target button
was resolved by the game itself (`castSlot: 1`) and the key read `heldAtEnd: true` immediately before each release.
Every attempt read the same way, and the last clean fixture (`p4-tree-before.json`: bag `1117012477891` slot 0 with
1 child, bag `1142782281667` slot 1 empty) is the one below:

```text
12:20:47.933 [ItemTrace] op=31 begin item=1147077248963 origin=OnItemUnloadedFromContainer event=Unload
12:20:47.933 [ContainerUnload] dogfood (id 1147077248963) left its container into the world from the
           CarriedInventory side — …
12:20:49.717 [ItemDropped] dogfood (id 1147077248963) at (3.0,460.6), vel (0.0,0.0) — container contents 0.
12:20:49.717 [ItemTrace] op=31 item=1147077248963 origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]
12:20:49.728 [ItemTrace] op=32 item=1147077248963 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
```

- The expansion's FIRST half ran (`Container.UnloadItem` fired and the departure was registered).
- The expansion's SECOND half did not: no `[ContainerLoad]` line and no `OnItemLoadedIntoContainer` op exist in any
  attempt, and the tree read two seconds after the release (`p9-tree-after.json`) shows the child in NEITHER bag —
  while the target's own `CanHoldItem` had admitted it (that guard is what let the unload run).
- The departure therefore settled 1.2–1.8 s later as a REAL drop (the item was not a standalone world item during
  that window, so the frame-end settle kept refusing it), the game destroyed the item 11 ms after the report, and
  both peers — whose clone still showed the child in the source bag — warned once each
  (`nested container contents changed without an event sync` for bag `1117012477891`) and materialized the child as
  a world item from the drop report. A body drop measured in the same session stayed fast (`op=23 begin … 12:19:48.624`
  → `[ItemDropped]` 12:19:48.640), so the deferral is not a general pump stall.
- No exception, no alert line and no error line appears in either client's window; the native refusal is silent
  (`Container.LoadItem` refuses without a log).
- The world was therefore never put into the state this row needs: the row is `unproven`, not a pass and not a
  demonstration that the classification is wrong. What the run DOES prove about the local path is the negative half:
  the pipeline reported what natively happened (a real, verified world drop — no phantom pickup, no false container
  content), and its one report was the drop the peers materialized.

## Residuals for the user

None: every row above was judged from this run's own machine evidence, and no row is a visual or feel judgement.

## Limits

- **One session, three readings of the fixed pair.** Row A1g was driven once per owner direction; that is consistent,
  not a claim about rarity.
- **The local control's native refusal is unexplained and is the finding this batch hands over.** Four attempts, the
  same fixture batch `20261006-b` drove successfully, the key verified held, the target resolved by the game itself,
  and no exception: the load half of the native pair did not land. Whether that is a state this fixture leaves behind,
  a `LoadItem` guard (`Container.cs:116-151`: stacking, self, weight, or the 10-unit distance) or something this
  session did earlier is NOT attributed here. The next cycle should drive it on a fresh session with a position probe
  on the child and the target before the release.
- **The deferred report lost the race to the 1 Hz snapshot in that window.** With the child not standalone for
  1.2–1.8 s, the departure could not settle, and `CloneFactTable` printed the snapshot-carried wording — the monitor
  behaving exactly as designed. The measured body-drop latency in the same session (16 ms) shows the machine itself is
  not slow; the row's window is what was slow.
- **`A1f` (battery unload) and the drop/slot/wearable rows were not re-driven**: batch `20261006-a` read them on
  `0.1.0+ad6f73ee…`, decision 238 does not touch the battery branch, and this batch exists for row A1g and the
  departure machine. Their verdicts stand as that batch recorded them.
- **The fixture is the batch's own staging** (`item-provide mode=create` + `container-fill`), as in batch
  `20261006-b`; the gesture itself is the game's own release path with the game's own `expanddesc` bind held.
- **The run's own artifact identity** is the deployed `0.1.0+8da00be3…`; the byte-marked windows are read from each
  client's rolling log (`latest.log`), with `LogOutput.log` unused this run.
