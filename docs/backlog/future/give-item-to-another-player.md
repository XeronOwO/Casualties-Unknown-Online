# Give an item to another player (the push direction)

- Status: Future — deferred by decision, not refused: the receiver-initiated half is landed, and the giver-initiated half is the capability this item records.
- Priority: Low-Medium
- Category: Remote inventory / native interaction parity / new capability
- Source: User question (2026-10-06), asked right after the occupied-slot swap fix landed — "is there a way to actively put my own item into another player's backpack, e.g. drop it on the ground, open their backpack and drag it in?" The user judged it not a must-have but a real raise of the skill ceiling, so it is recorded as deferred work rather than answered with a refusal.
- Related: `todo/remote-inventory-native-parity-rework.md` (the rework this extends), `done/local-item-into-remote-display.md` (the refusal that stands in for it today; its own non-goals name this ticket), `docs/architecture/remote-inventory-native-parity.md` (the intent vocabulary and the `TransferToBody` row), decisions 217/218 (one mutation site, the wire carries the native intent, the viewer never mutates a proxy), `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md` §4.3.

## What exists today (both directions of one hand-over, only one carried)

- **The receiver takes it — landed.** The operator opens the other player's backpack, drags a display proxy and
  releases it with the ring back on its OWN body (the double-Tab flow): the window classifies that release as
  `TransferToBody` (`RemoteDragIntentCapture.CapturePickUp`, taken when `DestinationIsOwnerBody` is false), the
  host validates and runs the landed custody transfer
  (`PlayerInventoryTakeService.HandleRemoteBackpackTransferToBody`), and the destination body then runs R9's own
  sequence on its own slots. So a hand-over works today as "the other player takes it out of my backpack".
- **The giver pushes it — refused, observably.** With another player's backpack open, releasing THIS client's
  own item onto anything of that displayed inventory — a ring slot, an item shown in it (their bag included), or
  the container window's back panel — is cancelled at the release seam and answered with one Warning line
  (`PlayerCameraDragUsePatch.ResolveDisplayProxyTarget` → `IRemoteBackpackPatchBridge.ReportLocalReleaseOntoProxy`).
  The refusal is deliberate and read on the machine (batch `20261006-e` rows E1/E2, with E3 as the local
  control): the native container branch would load the item INTO the display proxy, and the clone rebuild then
  destroys it — the reported item loss of batch `20261006-d`. No member of the frozen intent vocabulary
  (`RemoteInventoryIntentKind`) names "give my item to that player" either, so the family's rule — the viewer's
  backpack renders the owner's facts and its gestures produce intents, never a half-applied local move — leaves
  the gesture refused.
- **The routes that do work today**: the receiver takes it (above), or the item is dropped and the other player
  picks it up (or auto-picks it up by walking over it) — normal world pickup, host-validated as always.

## What it would take

- **A new intent kind** whose operands are the item, the destination body and the destination slot or container —
  the mirror of `TransferToBody`, where the item's owner is the ACTOR and the destination body belongs to the
  other player. The wire changes, so `ProtocolVersion.Current` is bumped in the same change (the handshake stays
  the compatibility boundary; no dual shape is kept).
- **The owner-side half stays native and two-sided** (decisions 217/218): the giver's release mutates nothing on
  a proxy, and the RECEIVING client runs the native pickup on its own body, so its slot rules, its container
  weight/tag rules, its animations and its sounds decide whether the item fits. The host-side mirror edit is what
  the rework deleted and must not come back.
- **The host validates** permission, membership, the ownership fact (the item is the requester's, the destination
  body is not), the operands and the destination, arbitrates the item first-writer-wins and records the resulting
  fact; it still never models inventory contents.
- **The gesture itself is CUO's own.** The native game has no remote backpack, so there is no native release
  branch to inherit here — unlike every other row of the rework. The nearest native precedent is the trader
  hand-in (`TraderScript.GiveItem`, already carried as `GiveToTrader`), which is a one-sided give to a world
  entity; this one is a two-sided custody move between two clients' item domains.

## What a fix has to decide (user-visible, so the user's call)

1. Which release means "give": onto a ring slot of the displayed backpack, onto an item shown in it (so their bag
   takes it), onto their body in the world, or all of them.
2. What the receiver sees and hears. There is no native counterpart to inherit, so this is a presented decision:
   the item simply appears in the slot its own native rules accepted (with the native pickup feedback), or a
   hand-over presentation (the giver's throw, a catch). The 2026-09-21 ruling's target — indistinguishable from
   operating your own inventory — cannot be inherited here and has to be answered on purpose.
3. What a full or refusing destination does: the native weight/tag/slot rules refuse it, with the native feedback
   and an observable line — the same "refused, never a silent no-op" rule the rest of the family follows.

## Non-goals now

- Not part of the rework's current acceptance matrix: this is a new capability, not a defect, and no row of
  `todo/remote-inventory-native-parity-rework.md` depends on it.
- No code work, no wire change and no protocol bump until the user promotes this item out of `future/`.
