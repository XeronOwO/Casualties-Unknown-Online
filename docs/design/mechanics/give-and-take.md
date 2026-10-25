# Give and take: moving an item between players

Handing an item to a teammate, and taking one from their displayed backpack.

## What the player does

Opens a teammate's backpack by dragging an item onto them (or through the member menu), then either releases an item of
their own over a slot or a container inside that pack (a GIVE), or drags an item out of the pack into their own inventory
(a TAKE). The player expects the item to change hands exactly once: it leaves the giver's inventory and appears in the
receiver's, in the place the player aimed at, with the container it was aimed inside if it was aimed inside one.

## What the game itself does

Both directions are the game's own custody operations: an item moves into another holder through the game's container and
pickup path (`Container.LoadItem` for a container, the body's own pickup for a slot), and leaves one through the
container's unload path. The transfer's sound, the container's refresh and the item's own presentation are produced by
that path, not by the gesture.

## Rules and states

- **A local item released onto the other player's DISPLAY PROXY is refused, not landed.** The accepted reading is that "the
  release is refused at the release seam before the native body runs — the owner's probe names the target as the other
  player's display proxy and reads its contents unchanged (`castContentsBefore: 0` → `castContentsAfter: 0`,
  `childInCast: false`), one `[RemoteIntent]` line names the item and the target, the owner's item tree is byte-identical to
  the pre-gesture one", and the local control still runs the native move.
- **The gesture's context is one pure decision, re-read every frame.** `src/CasualtiesUnknownOnline.GameAdapter/GiveGestureModePolicy.cs`
  decides three modes — own UI wins over a displayed backpack, and a displayed backpack wins over the world — reading them
  from the game's own state every frame rather than from the last input that changed it, "because the native while-dragging
  body forces the ring open when the cursor crosses a body collider, so a mode inferred from 'the player pressed Tab' would
  be wrong the moment the native path changes it".
- The two destinations are mutually exclusive: a release over an ITEM means "into that container", a release over a slot
  ring means "onto that body slot".
- A pack that is view-only accepts neither direction, and the gesture is refused before anything is sent.
- An item without an authoritative identity cannot be named, and the gesture is refused instead of transferring "the
  first match".
- A destination that no longer holds when the host applies the transfer is refused by name; the item stays where it was.
- **The answer names where the item WENT, not where it was aimed.** The host's ledger keeps the destination it was told and
  must be corrected by the placement outcome: the recorded defect is "The host's ledger keeps the destination it was told,
  not where the item went", with rows read on the machine and its 60 ms window judged by the user's ruling because it
  cannot be produced by hand.
- A container that cannot hold the item (weight, kind) refuses it by name.

## Refusals

| Condition | The player reads |
|---|---|
| the pack is view-only | the backpack notice's own "gesture refused" line |
| the item has no identity, or the target is not a member | the gesture is refused with no wire traffic |
| the destination is gone or full | the answer names the destination that no longer holds |
| the host rule forbids remote inventory manipulation | the host's rule name |

Each of these reaches the player as a sentence in their own language; the wire carries the key, never prose.

## Who judges

The host applies the custody change, because custody is a host fact. The receiving client then materialises the item in
its own inventory and shows it; the giving client removes it from its own. A member's request travels to the host and the
host's answer travels back, so a refusal is visible on the side that asked.

## What travels

The item's identity, the two players, the destination (a slot or a container), and — when it fits — the container inside
the destination. Nothing about the item's appearance travels: each client shows the item from the state it received.

## What the player sees and hears

The item leaves the giver's hands and appears in the receiver's pack or slot. The transfer is the game's own, so its
sound and the container's refresh accompany it.

## Records

`../../architecture/surfaces.md` (inventory give/take row), `../model/item-custody.md`, decisions 155 and 156. Accepted:
`../../backlog/done/local-item-into-remote-display.md`, `done/remote-backpack-access-policy.md`.

## Gaps

- The transfer's own sound is not yet reproduced; recorded in `../../backlog/todo/give-into-backpack-plays-no-sound.md`.
- A take that lands in the taker's own backpack container (rather than a body slot) has no dedicated page yet.
