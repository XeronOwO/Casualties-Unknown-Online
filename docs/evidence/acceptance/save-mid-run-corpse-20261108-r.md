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

## What this settles, and the gap it leaves

- The death itself is not re-rolled by the cut: the dead body's slots before and after the save are the same three items.
- The corpse-loot clause ("a corpse's loot is not re-rolled by the restored deaths") is NOT read through a restore: the dead
  member could not leave the world (`leave-world` refused while dead), so its body never went through the restore path; the
  reading above is the live body's continuity, not the restored corpse's contents. A restore-side reading needs a dead
  member's client to leave and rejoin the restored world, which the current channel cannot drive while the body is dead.
