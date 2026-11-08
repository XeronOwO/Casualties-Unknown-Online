# Mid-run cut with a dead member (batch 20261108-r)

Session: lobby `109775243854859418`, host + Steam1 + Steam2 in world 14:07:07 on `0.1.0+9869d734` (acceptance channel).

## What was staged

- The host granted Steam1 the cheat verbs (`/op 76561198863287957`) and Steam1 gave itself a bandage (`/give @p bandage` →
  `0:bandage|1:trashbag|3:lantern|4:dogfood|5:waterbottle`).
- Steam1 killed itself (`/kill @p`); its body read `alive false, brainHealth 0`; its slots then read
  `3:lantern|4:dogfood|5:waterbottle` (the bandage and the trashbag left the body on death).
- The host queued a mid-run cut (`/save` → "Save queued: the cut is taken at the next pump boundary…").
- The clients left the world: Steam1's `leave-world` answered **`ok:false` while dead**; Steam2 and the host left; the host ran
  `continue-run` → `inWorld true`.

## The readings

- After the Continue the host's own body carried `1:trashbag|3:lantern|4:dogfood|5:waterbottle` — the character restore's
  items, unchanged by the cut.
- Steam1's client stayed `inWorld true, role Guest` with `3:lantern|4:dogfood|5:waterbottle` — the same corpse slots it had
  before the cut.

## What this settles, and the follow-up

- The death itself is not re-rolled by the cut: the dead body's slots before and after the save are the same three items.
- The first `leave-world` on the dead client answered `ok:false`; a second attempt a minute later answered `ok:true,
  inWorld:false`. The refusal was transient (the death flow owned the frame when the first call landed), not a dead-player
  block; the channel does not need a work-around.
- The corpse-loot clause ("a corpse's loot is not re-rolled by the restored deaths") has no reachable scenario in this
  product: a member who stays in the world keeps its live dead body through the cut (the reading above), and a member who
  leaves is deliberately NOT restored as a corpse — decision 162 makes an absent character a NEW character on the next
  join, with starting supplies and new item ids (read for the item rows in batch `20261108-e`). CUO never rebuilds the dead
  body's corpse, so nothing in CUO can re-roll its loot; the native `save.sv` is untouched by CUO's cut and Continue
  (proven in the layer-end batch, row 5's mtime).

