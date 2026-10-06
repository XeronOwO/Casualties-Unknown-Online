# Acceptance record — A local item released onto a remote display proxy

- Ticket: `local-item-into-remote-display` — verdict: **every row passes**, the ticket moves to `done/`
- Batch: `20261006-e` — ticket `local-item-into-remote-display`
- Commit: `6e07b388` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+6e07b3882bdfa4733eff30b5dfd93d72daf041ca` (`verify-deploy.ps1` exit 0, "Deployment
  matches this tree's build output")
- Run: 2026-10-06, the three row windows at 13:37:42 / 13:38:35 / 13:39:49 local · Host: physical machine ·
  Guest (the focused player) + third peer: Sandboxie sandboxes
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`,
  `artifacts` (preflight: 11 present, `RESULT: OK`; `session-environment.ps1`: `active=cuo`,
  `game-running=false`)
- Artifacts: the probes, byte-marked log windows and window captures named below, in the directory
  `acceptance-artifacts-dir` under `20261006-e/`

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| E1 | The reported gesture: the owner releases its OWN carried bag onto a ring slot that shows the other player's bag as a display proxy, expansion key held | machine | **pass** | `m1-host-release-onto-proxy.json` (target named, `castContentsAfter: 0`, `childInCast: false`), the owner's one-line refusal in `e1-host-window.txt`, the owner's tree unchanged (`m7-host-tree-after-e1.json` = `m5-host-tree-before.json` by SHA-256), both viewers quiet (`e1-guest-window.txt`, `e1-alt-window.txt`), frames `w2`/`w3`/`w4` |
| E2 | The empty-slot half of the same guard: the same owner releases its own item onto an EMPTY body slot of the displayed clone | machine | **pass** | `m2-host-release-onto-empty-body-slot.json` (`castIsBody: true`, `castSlot: 1`, `castItem: "none"`), the second refusal wording in `e2-host-window.txt`, the owner's tree unchanged (`m9` = `m5`) with `[ItemTraffic] … Drop=0; Destroy=0` in that window |
| E3 | The local control: the same owner releases its own item onto its OWN bag, no remote view — the native move must still run and NO refusal may appear | machine | **pass** | `m6-host-local-control.json` (`castItemProxy: false`, `castContentsBefore: 0` → `castContentsAfter: 2`, `childInCast: true`) and the native `[ContainerUnload]`/`[ContainerLoad]` pair, with no `[RemoteIntent]` line, in `e3-host-window.txt`; the owner's tree shows bag A empty and bag B holding both children (`m7-host-tree-after-e3.json`) |

## E1: what the run read

The owner is the host (`76561198281246659`); the focused player is the Steam1 guest
(`76561198863287957`), whose own `trashbag` is the display-proxy target; the third peer is the Steam2 guest
(`76561199526807662`). The fixture is the previous batch's shape: the host carries bag A holding two
`dogfood`s and an empty bag B, the guest carries the target `trashbag`.

The gesture is the game's own release path driven from the process: the Online UI is closed first, the ring is
opened focused on the guest (`remote-gesture mode=open owner=76561198863287957`, `viewOpen: true`,
`focusSet: true`), the button list is taken immediately before the release (`i=2` resolves to slot 0, the
guest's bag proxy), the game's own `expanddesc` key is held and read back (`heldAtEnd: true`,
`osKeyAtEnd: 0`), and the release is `mode=release,item=local0,cast=2,moved=1`.

The release names its own target and refuses it (`m1-host-release-onto-proxy.json`): `castItemProxy: true`,
`castItemOwner: 76561198863287957`, `castIsContainer: true`, `castContentsBefore: 0` →
`castContentsAfter: 0`, `childInCast: false`, `dragAfter: "none"`, `calls: "none"`. Against batch
`20261006-d`'s pre-fix reading of the same shape (`castContentsBefore: 0` → `castContentsAfter: 1`,
`childInCast: true`) this is the fix's own reading: the proxy received nothing.

The owner's log window (`marks-e1-before.txt`, 20 s including a full traffic cycle) holds exactly one line:

```text
[2026-10-06 13:37:42.815] [WRN] … [RemoteIntent] refused the release of this client's own item trashbag onto
display proxy item trashbag (owner 76561198863287957): an item of this client cannot be moved into another
player's inventory — the drag is cancelled before the native body can act on the displayed inventory, and the
item stays where it is.
```

and no `[ContainerUnload]`, `[ContainerLoad]`, `[ItemDropped]`, `[ItemDestroyed]` or `[CharSync] divergence`
in that window. The owner's item tree is byte-identical to the pre-gesture one
(`m7-host-tree-after-e1.json` = `m5-host-tree-before.json`, SHA-256
`A292FDD8925C210806FB74331150DB3409E71F33AE89E77B6583B97F551059C2`): both `dogfood`s are still inside bag A.
The two viewers' windows (`e1-guest-window.txt`, `e1-alt-window.txt`) are empty of warnings, and the frames
read: the owner's own view after the release (`w2`, no item left on the ground), the guest's own screen
(`w3`, its bag reads "no items"), the third peer's screen (`w4`, nothing of the sort appears).

## E2: what the run read

The clone's slot 1 is empty (the guest carries only the bag in slot 0 and a lamp in slot 3) while the host's
own slot 1 holds bag B — so the native R8/R9 sequence had something of the host's own body to swap or drop,
which is exactly what this half of the guard exists to prevent. The release is
`mode=release,item=local0,cast=4,moved=1` with the view still focused on the guest, and the probe
(`m2-host-release-onto-empty-body-slot.json`) reads `castIsBody: true`, `castSlot: 1`,
`castItem: "none"`, `castItemProxy: false`, `viewOpen: true`, `focusSet: true`, `dragAfter: "none"`. The
second refusal wording is the one that names no item:

```text
[2026-10-06 13:38:35.556] [WRN] … [RemoteIntent] refused the release of this client's own item trashbag onto
a slot of the displayed inventory (owner 76561198863287957): an item of this client cannot be moved into
another player's inventory — the drag is cancelled before the native body can act on the displayed inventory,
and the item stays where it is.
```

The owner's tree is again unchanged (`m9-host-tree-after-e2.json` = `m5`, same SHA-256): bag B was neither
swapped nor dropped, and the window carries no `[ItemDropped]`/`[ItemDestroyed]` (the periodic
`[ItemTraffic] … Drop=0; Destroy=0; Pickup=0` line is in it).

## E3: what the run read (the control that keeps the guard honest)

The remote view is closed (`remote-gesture mode=close`), the ring is opened the way a player opens their own
(`toggleinventory`), and the same bag is released onto the owner's own slot: `m6-host-local-control.json`
reads `castItemProxy: false`, `castItemOwner: 0`, `castItemParent: InvSlot (4)`, `viewOpen: false`,
`castContentsBefore: 0` → `castContentsAfter: 2`, `childInCast: true` — the same numbers batch `20261006-d`
read for this control. No `[RemoteIntent]` line appears; instead the native pair runs
(`[ContainerUnload] dogfood (id 1202911823811) left its container into the world from the CarriedInventory
side` then `[ContainerLoad] … moved inside body container trashbag — root content event up to trashbag
(id 1198616856515)`, once per child), and the owner's tree afterwards shows bag A empty and bag B holding both
children (`m7-host-tree-after-e3.json`, a different hash from `m5`, which is the move).

## What this run does not prove

- **One session, one direction, one reading per row.** The owner was the host and the target was the Steam1
  guest; the reverse direction (a guest owner releasing onto the host's displayed bag) was not driven. Nothing
  here is a claim about rarity.
- **Two of the guard's three target wordings were exercised**, the ring's proxy item and the displayed body
  slot. The third — the container window's own back panel (`display proxy container <id>`) — was not driven:
  no proxy container window was open in this session. Its read is the same one the native branch makes, and it
  stays a code fact rather than a reading.
- **The guard is per pointer target, not per native order.** The run did not stage a pointer that carries both
  a proxy target and a target the native body would have consumed earlier (the craft button, the trader, a
  wound-view limb), so the breadth the self-check names is not measured here.
- **No wire, save or transfer-table check**, no operating-feel judgement, and the third peer's view is judged
  from its own screen and log window only.
- **The favourite-gate change is not covered by this run**: the state it guards (the focus cleared while the
  container window is still up) was not entered.

## What the run taught (folded into `docs/acceptance/lessons.md`)

The refusal line's target wording is the discriminator between the guard's branches; `childParentAfter` cannot
tell a refused release from a landed one (a carried bag is an instance either way); and a refused release has
already cleared the drag, so the next gesture needs none of the previous batch's hover/empty-release staging.
