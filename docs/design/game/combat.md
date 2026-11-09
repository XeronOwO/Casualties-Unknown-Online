# The game's combat (1.0)

Cited from the 1.0 decompile (`reversing/release-1.0/Assembly-CSharp/`).

## Weapons are state machines

`GunScript` owns a weapon's live state: jam chance, racked round, safety, magazine, chamber
(`GunScript.cs:8`). The jam probability is a function of the item's condition — above half condition it ramps from 0
to 0.0025, below half from 0.0025 to 0.1 (`GunScript.cs:11-20`) — so a worn weapon misfires more, and the panel's
condition number is a combat stat. Loading goes through three paths: a magazine, a single round, or a direct-feed round,
each consuming the ammo item (`GunScript.cs:60-90`); `TryRack`, `ToggleSafety` and `UnloadMag` are the player's other
verbs (`:47`, `:53`, `:90`). Ammo items carry their own type, calibre and round count (`AmmoScript.cs`).

## Every attack is data

`AttackInfo` describes an attack: damage, knock-back, reach, structural damage, cooldown, stamina cost, a piercing flag,
the animation and the swing sounds (`AttackInfo.cs:8-32`). The same shape serves the player's melee and an animal's
bite, which is why a mod that mirrors an attack must read this object rather than hard-code numbers.

## What takes damage

- Bodies take limb damage through the body's own paths (see `medical.md`).
- Buildings and props implement `Damageable`, whose `Damage(float)` subtracts from the block's `health`
  (`Damageable.cs:15-19`); the block table and its sprite states are in `world-objects.md`/`traps-and-events.md`.
- `DamageNumberScript` is the floating number the game shows on a hit.

## The named enemies

- The spider (`SpiderHandler.cs`): its aggression is rolled against its own health share — a hurt spider is more
  likely to chase (`:68-75`) — its attack emission rate grows as its health drops (`:111`), and its movement force
  scales with health too (`:128`). A low-health retreat threshold exists (`:75`).
- The elder (`ElderThornbackBehaviour.cs`): a 6000-health boss with staged behaviour — a new stage at 3750 and at 2500
  health (`:59`, `:82`, `:94`), its material's progress driven by health (`:110`), a 45-unit engagement distance
  (`:117`), and a stage that zeroes its retreat threshold (`:100`).
- `CrystalEnemy.cs`: a mobile crystal hazard (the crystal effects are in `traps-and-events.md`).

## Not covered here

- The ordinary animals' AI scripts are not among the 301 decompiled files — the release's animal behaviour may live in
  another assembly or be data-driven. Read the managed folder before relying on an animal's rule; this page names the
  gap rather than inventing the behaviour.
- Bullet ballistics, penetration and the armour model: `GunScript` and `AmmoScript` describe the weapon side; the
  projectile path is not mapped yet.
