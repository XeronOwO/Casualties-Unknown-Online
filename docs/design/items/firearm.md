# Firearms: what a shot does and what the other clients are told

A gun is the one item whose action is a **ranged world effect**, so its page answers two questions the other families do not:
what the game's own shot does to the shooter and the world, and which half of that a remote client has to be told rather
than compute.

## What the player does

The player holds a gun in hand and presses the fire control. The gun spends a round, kicks the arm back, plays its own fire
sound and puts a muzzle flash on the barrel; the shot itself is a hitscan ray from the barrel with a vertical spread. Racking
the action, loading a magazine, unloading a round and toggling the safety are separate gestures on the same item, and each one
changes the gun's persistent state rather than firing.

## What the game itself does

`GunScript.Fire(bool suicide = false)` (`reversing/Assembly-CSharp/Assembly-CSharp/GunScript.cs:185-225`) is the whole chain,
in this order: it pushes the body back with `rb.velocity -= transform.right * num * knockBack`, sets `body.eyeScareTime = 1f`,
adds `hearingLoss += loudness * ((hearingLoss > 20f) ? 0.2f : 1f)`, plays the muzzle particle, sets
`roundInChamber = RoundInChamber.Casing`, wears the item by `condition -= conditionLossPerShot * 0.01f`, rolls the jam, plays
`fireSound`, and finally calls `TurretScript.Shoot` once per `shotsPerFire` with a `FireInfo` whose direction is
`transform.right + transform.up * (Random.Range(-1f, 1f) * verticalSpread)` and whose `ignoreBody` is `!suicide` and `forceHead`
is `suicide`.

Ammunition is its own component: `AmmoScript.LoadRound(ammo)` accepts only a `Round` into a `Magazine`, plays `gunloadshell`
and destroys the round item; `UnloadRound()` instantiates the matching round (`AmmoTypeToItem` maps pistol to `9mmround`,
rifle to `556round`, shotgun to `12gauge`), hands it to `AutoPickUpItem` and decrements `rounds`.

## Rules and states

- **A jam is a roll, and a jam leaves the action uncertain.** With `Random.value > JamChance()` the action racks normally; on a
  jam the game sets `racked = Random.value > 0.5f` and plays `gunjam` — so a jammed gun may be left racked or not.
- **Firing damages the shooter's HEARING, and the damage is damped once it is already above `20f`** — the `0.2f` branch is the
  game's own protection against a single magazine deafening a player.
- **Every shot costs the item `conditionLossPerShot * 0.01f`**, i.e. a hundredth of a condition point per percent — the same
  "condition is the dose" model the medical family uses.
- **A shot is instantly resolved, not simulated.** `TurretScript.Shoot` is a hitscan: there is no bullet object to replicate,
  so what travels is the fact that a shot happened and what it did, never a projectile.
- **The suicide path is the same call with two flags flipped** (`ignoreBody = false`, `forceHead = true`, barrel position
  replaced by the body's own position) — it is not a separate mechanism.

## Refusals

| Condition | What the player sees |
|---|---|
| the safety is on | the trigger does nothing, and the gun's own state stays as it was |
| the chamber and magazine are empty | no shot, no sound |
| a jam | the `gunjam` sound, and the action in whatever state the roll left it |

The refusal sentence a CUO surface prints for a gun it will not let a remote player fire is the status line's rule (key plus
values, rendered by the reading client), not a new one.

## Who judges

The FIRING side judges the shot: the owner's own client runs `GunScript.Fire`, so the raycast, the jam roll, the condition
loss and the hearing loss all happen where the player pressed. The host does not re-roll a shot. What crosses is the
persistent state and the audible/visible consequence, because a remote client shows a gun it does not own.

## What travels

- **Persistent gun state** — chamber, magazine, `racked`, `safe` and `condition` — reported as a STATE fact through the item fact
  route by `GameAdapter/Items/GunStateSync.cs`, which owns the per-instance "last reported snapshot" and reports a transition
  that the 1 Hz character snapshot would otherwise carry late.
- **The fire event** — the exact clip name, position, volume/2D mode and the recoil degrees — carried as one dedicated reliable
  `CharacterSound` fact by `GameAdapter/Patches/GunFirePatch.cs`, whose postfix reports `CharacterSoundKind.GunFire` so the
  owner's render clone lerps the extra `gunangle` back to the synced aim ("which is exactly what recoil looks like").
- **The aim** is not part of either: `Body.HandleVisuals` already computes `gunangle` from the peer's `targetLookPos`, so a
  clone's barrel points where that player is looking without a fire event.

## What the player sees and hears

The shooter sees the muzzle flash, the arm kick and the fire sound; the others hear the same clip and see the clone's arm kick
through the relayed recoil, then watch the arm settle back to the aim. A jam is audible to the shooter only, because the jam
roll is the shooter's own.

## Records

- `docs/design/audit/code.md` row 119 (the `GameAdapter/Items` block) and row 110 (the one-shot sound rule on
  `surfaces/remote-body-presentation.md`), where the gun's fire event is the worked example of "a one-shot sound is relayed
  with its clip identity".
- `docs/design/surfaces/remote-body-presentation.md` — the presentation rules the fire event obeys.
- `docs/decisions/active.md` — decision 184 (the affected side judges) is what puts the shot on the shooter's client.

## Gaps

- **Hitscan damage to another player is not designed here.** `TurretScript.Shoot` damages world structures and animals through
  `FireInfo`; whether a shot may damage a remote player's body, and which side applies that damage, is not recorded on any page
  and is not implemented. It is a `GAP`, not a limit: it needs the user's ruling before it is built.
- **The jam roll is unseeded and unshared**, so a remote client must never predict a jam — the state report is the only source.
  This is recorded because a reader will ask, not because anything is owed.
