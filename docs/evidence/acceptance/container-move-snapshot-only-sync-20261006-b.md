# Acceptance record — Container moves reach the viewer as a snapshot, not an event

- Ticket: `container-move-snapshot-only-sync` — verdict: **back to `todo/`**, rejected on row A1g
  (`- Status: Todo — Rejected (batch 20261006-b: the expansion kind reports a pickup, not its target
  container's contents, in both owner directions and in a local control)`). The row the ticket has been
  waiting for since batch `20261005-c` was driven for the first time and it does NOT pass: the gesture
  runs, the owner expands the container, and the peers still warn.
- Batch: `20261006-b` — tickets `container-move-snapshot-only-sync` (the batch also carried the driver
  capability the row needed; its own smoke is part of this run and is recorded below)
- Commit: `0b6fcf35` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+0b6fcf3526a1d67356365b92def5f35fcc531594` (`deploy.ps1` exit 0; `verify-deploy.ps1` exit 0,
  "Deployment matches this tree's build output", 34 of 35 deployed files matched)
- Run: 2026-10-06 11:03 → 11:11 (+08:00) · Host: physical machine, launched through Steam (evaluator
  `18590`, SteamId `76561198281246659`) · Guest: primary sandbox box (evaluator `18591`, SteamId
  `76561198863287957`) · Third peer: alternate sandbox box (evaluator `18592`, SteamId
  `76561199526807662`) · lobby `109775244125103871`
- Dependencies: preflight `11 present`, exit `0` — the `input` row now covers the eval declarations the
  recipes name; the machine gate before launch read `active=cuo`, `game-running=false`,
  `swap-needed=false`, `launch=ok`; neither sandbox carried a plugin shadow, so both read the physical
  deployment
- Artifacts: the batch directory under `acceptance-artifacts-dir` — session facts (`s*`), the fixture
  staging (`f*`), the declaration and key steps (`g*`, `k*`), the local control (`p*`), the byte-marked
  log windows (`marks-a1.txt`, read with the batch's `log.ps1`). The independent adversarial review of
  the capability commit is `review-20261006-b.md` in the same directory (not committed).

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| A1g | Container expansion (`MoveContainerChildren`), remote-driven, BOTH owner directions | machine | **fail** | direction 1 (host owner, guest operator) 11:05:39 and direction 2 (guest owner, host operator) 11:06:57: the operator captures the intent and the owner expands the container, but the owner's load hook reports a **pickup** instead of the target container's content fact, and the operator's and the third peer's monitor warn `nested container contents changed without an event sync` for the target container — full readings below |
| A1g′ | The same gesture driven LOCALLY (the owner's own release, no intent, no operator) | machine | **fail** | host 11:09:07: the same hook pair (`OnItemUnloadedFromContainer` → `OnItemLoadedIntoContainer events=[Pickup]`) and the same warning on both peers — this is what attributes the row's failure to the hook's classification rather than to the remote-intent path |
| A1c′ | The driver capability the row needed: `declare` + a held key, read back per step | machine | **pass** | guest `g1-guest-declare.json` (`sent: true`, `declared: true`), host `g4-host-declare.json`; hold read back on the guest (`k2-guest-shift-held.json`: `heldAtEnd: true`, `osKeyAtEnd: 0`, `isForeground: false`) and on the physical host (`k8-host-shift-held.json`: `heldAtEnd: true`, `window: 593330`, `isForeground: false`); both releases read back clear (`k5`, `k11`); the owner's own tree moved its child in both directions (`f5`→`k6`, `f10`→`k12`) |
| A1z | Zero warnings on both viewers across the gesture window | machine | **fail** | `[CharSync] divergence` counts over `marks-a1.txt`: direction 1 — operator guest 1, third peer alt 1; direction 2 — operator host 2, third peer alt 2; local control — guest 1, alt 1 (the window also carries the unrelated `[Enemy] generation spawn pairing failed` notice, once per client per minute, and three fixture-path warnings named under Limits) |

## Row A1g: what the run read

The kind reached the owner and the owner's own native loop ran — that half is now proven, and it is the
half no batch had ever seen:

- **Direction 1** (the host owns; the guest operates the host's ring): guest
  `[RemoteIntent] MoveContainerChildren captured for item 1220091692995 of 76561198281246659 (container
  1224386660291, slot -1, body 0, limb -1, target item 0, trader none).` 11:05:39.559 → host
  `op=18 …` the unload half, then
  `[RemoteIntent] container expansion of item trashbag into container 1224386660291: 1 of 1 direct child
  item(s) entered the container; 0 did not (the native guard, or a native load refusal).` and
  `[RemoteIntent] replayed native MoveContainerChildren on item 1220091692995` 11:05:39.597. The owner's
  own tree confirms the native effect: before the gesture the dogfood sat inside the bag in slot 0
  (`f5-host-tree.json`), after it inside the bag in slot 1 (`k6-host-tree-after.json`), and neither bag
  changed slots.
- **The carrier is what fails.** The same second, on the owner, the child's two container calls report
  `[ItemTrace] op=5 item=1228681627587 origin=OnItemUnloadedFromContainer result=Committed(1)
  events=[Unload]` and `[ContainerLoad] dogfood (id 1228681627587) left the world into a body container —
  pickup report.` / `op=6 item=1228681627587 origin=OnItemLoadedIntoContainer result=Committed(1)
  events=[Pickup]`. The load into the TARGET container is therefore announced as a **pickup of the
  child**, and the target container's full fact — the one the peers' clone fact table needs — is never
  sent; 20 ms later both viewers say so: `[CharSync] divergence for 76561198281246659's trashbag (id
  1224386660291): nested container contents changed without an event sync (the 1 Hz snapshot carried
  it).` (guest and alt, 11:05:39.617).
- **Direction 2** (the guest owns; the host operates) reads the same way and costs the peers the item:
  host `MoveContainerChildren captured for item 9492956821 of 76561198863287957 (container 13787924117 …)`
  11:06:57.898 → guest (owner) `op=25 … OnItemUnloadedFromContainer result=Committed(1) events=[Unload]`,
  `[ContainerLoad] dogfood (id 18082891413) left the world into a body container — pickup report.`,
  `op=26 … OnItemLoadedIntoContainer result=Committed(1) events=[Pickup]` and the expansion line 11:06:57.910
  → the peers materialize the child as a WORLD item (`[ItemDrop] dogfood (id 18082891413) not present —
  requesting materialization at (0.8,483.6)`, `[ItemSpawn] … at (0.8,483.6)`) and remove it from the
  owner's clone (`[CarriedSync] removed 18082891413 from 76561198863287957's snapshot contents`), warn
  twice each (host and alt, 11:06:57.933/.046 and 11:06:57.960/.058), and the owner's own tree read no
  longer lists the dogfood at all (`k12`, `k13`): the child ends up nowhere it belongs on every viewer.
- **The local control closes the attribution.** With the same fixture the host drove its OWN release
  (ring opened with the committed key recipe, no operator and no intent) at 11:09:07: `op=19 …
  OnItemUnloadedFromContainer result=Committed(1) events=[Unload]`, `[ContainerLoad] dogfood (id
  1228681627587) left the world into a body container — pickup report.`, `op=20 …
  OnItemLoadedIntoContainer result=Committed(1) events=[Pickup]`, the child moved into slot 1's bag, and
  both peers warned (`nested container contents changed without an event sync`, 11:09:07.181/.187). So
  the failure is not the remote-intent path: **the container-load hook classifies a child that the
  expansion unloaded out of its source as a WORLD item** (`Patches/ContainerItemPatches.cs` captures
  `ItemWorldSync.IsWorldItem(item)` in the `Container.LoadItem` prefix, and the applier's own native pair
  — `source.UnloadItem(child, null)` then `target.LoadItem(child)` — has just detached it), so
  `ContainerItemSync.OnLoadedIntoContainer` takes its world→body pickup branch instead of reporting the
  target root's contents. The contrast sits in the same log: the fixture's `container-fill` (one load
  with no preceding unload) reports `origin=OnItemLoadedIntoContainer result=Committed
  events=[ContainerContent]` and `[ContainerLoad] dogfood … moved inside body container trashbag — root
  content event up to trashbag (id 1220091692995)`, and it warns nowhere.

Fix direction (for the ticket, not this run): the expansion's child load must announce the TARGET
container's fact — the same `SyncContainerItemsCommand` the other container kinds commit — instead of a
pickup of the child, either by not letting the transient "unloaded to the world" state decide the
classification or by having the batch's own carrier report the target root.

## What else the window held

- **The fixture path warns when it runs while the owner's remote view is already open.** The guest's three
  `item-provide mode=create` actions (`Utils.Create` + a forced pickup — the shape the game's own
  starting-supply grant uses) at 11:06:38–11:06:42 each produced, on the host and on the third peer,
  `[CarriedSync] trashbag (id …) not in 76561198863287957's snapshot and slot unknown — the 1 Hz snapshot
  will carry the change.` followed by `a new carried item the fact table never saw — a pickup without an
  event sync`. The items do arrive (`[ItemSpawn] materializing …` right before it), so the pickup event
  travels; what does not is the placement the clone fact table compares. The host's own creations in
  direction 1 (staged before any remote view was open) produced no such warning, so the condition is the
  open view, and this is recorded as an observation on the fixture path rather than as a row of this
  ticket.
- **The Online UI window swallows the toggle key.** The first ring toggle on the host did nothing
  (`buttonCount: 0`, `radialOpen: false`) while the Online UI window was visible and sitting on Home
  (`s4-host-state.json`); after `click window.close` the same key sequence opened the game's own ring
  (`p11-host-list.json`: `buttonCount: 6`, `radialOpen: true`). The world-capture rule ("close the
  Online UI first") extends to driving the game's own keys.
- The `[Enemy] generation spawn pairing failed (81 host vs 81 guest generated enemies)` notice repeats
  every minute on the guest and the third peer. It is an enemy-domain warning, unrelated to the item
  facts this row reads, and is named here so a reader of the window sees it attributed.

## Residuals for the user

None: every row above was judged from this run's own machine evidence, and no row is a visual or feel
judgement.

## Limits

- **One session, one reading per direction.** The expansion was driven once per owner direction plus once
  locally; that is consistent, not a claim about rarity. The failure reproduced three times out of three,
  in two directions and on two different clients as the owner.
- **The fixture is the batch's own staging.** Both owners' containers were created by
  `item-provide mode=create` and filled by `container-fill`; the gesture itself is the game's own release
  path with the game's own `expanddesc` bind held, and the local control shows the same outcome without
  any CUO intent, so the reading is not an artifact of the fixture.
- **The run's own artifact identity** is the deployed `0.1.0+0b6fcf35…`; the byte-marked windows are read
  from each client's rolling log, with the sandbox clients' `LogOutput.log` fallback unused this run.
- **A1f (battery unload) and the other rows of this ticket were not re-driven**: batch `20261006-a` read
  them on `0.1.0+ad6f73ee…`, decision 236 does not touch them, and this batch exists for row A1g. Their
  verdicts stand as that batch recorded them and are not re-claimed here.
- **The peers' warning wording is the monitor's own.** `nested container contents changed without an
  event sync` is `CloneFactTable.WarnOnDivergence`'s sentence for a contents change the previous snapshot
  comparison found unannounced; both the operator's and the third peer's tablets printed it, and neither
  client ever warned about its OWN items.
