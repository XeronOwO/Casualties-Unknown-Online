# Restoring a severed limb: the game has no path, so the heal needs one of its own

- Status: Todo — split out of `heal-command-limb-and-hollow-restore.md` on 2026-10-18, whose Stage A (clearing
  the hollow debuff through the native `heal` command) has landed and been read on the machine. This is the
  other half of the same user request: "the game's own `heal` command does not restore severed limbs".
- Priority: Medium
- Category: Player state / body limbs / console commands
- Source: the user's 2026-10-07 backlog request, whose limb half the parent ticket carried until the reading
  showed it is not a field write but a physics procedure with nothing to call.
- Related: `docs/backlog/todo/heal-command-limb-and-hollow-restore.md` (the parent: the surface decision, the
  hollow half and its runtime row), `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs`,
  `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesUtils/Util.cs` (KrokMP's `RegrowAllLimbs`),
  `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesUtils/Util_BodyExtensions.cs` (its `ResetHealth`,
  which calls it), `docs/error-record`-style notes are not used here: the evidence is below.

## What the reading established

- **The game has no limb-regrow path.** `dismembered` appears in `Body.cs` only as a READ — the broken/dislocated
  check, the distance check, and two branch guards — and is never assigned anywhere in the assembly. A severed
  limb is permanent by construction, and the sibling `amputate` command documents itself as "Irreversibly."
- **CUO has no restore helper either.** Every `dismembered` reference under `src/` is a REFUSAL (a limb tool, a
  topical dose, a syringe on the display body, a medical target pick), because those paths must not operate on a
  limb that is gone. Nothing restores one.
- **KrokMP wrote its own, and it is a procedure rather than a write.** `RegrowAllLimbs` runs only when
  `body.IsAnyLimbMissing()`; saves the position, forces the facing with `ForceSwitchDir()` and `Ragdoll()`s;
  restores every `ClientMain.SavedHingeJointState` under the body (each limb's and its connected body's local
  position/rotation, with the hinge's `referenceAngle` recomputed); deactivates every limb and clears
  `dismembered`, then reactivates every limb and clears it again; reconnects each hinge's `connectedBody`; and
  re-applies `Physics2D.IgnoreCollision` for every limb against every `NetBody.all_instances` collider. It ends
  with `body.shock = 0f` and the saved position.
- **Two gaps in that procedure, which is why copying it is not the plan.** `standing` is saved and never
  restored, and `Ragdoll()` is called with no matching un-ragdoll — so a heal built this way can finish with the
  patient lying down. The end state has to be one the character sync can CARRY, not one that merely looks right
  on the operator's screen.

## What the work has to decide

1. **Where the restore runs.** By the disclosure rule the affected player's own client decides its own state, and
   the native `heal` command already writes `PlayerCamera.main.body`, so the restore belongs on the same client
   as the command — the same shape Stage A used.
2. **What the end state is.** The limb fields the character sync carries (the save-parity work owns that list)
   have to end up describing a body with the limbs ON, and the operator's own scene has to reach that state
   through the game's own physics rather than by leaving a ragdoll behind. Deciding this is the first
   implementation question, and it is the reason the two KrokMP gaps matter.
3. **Whether every limb is restored or `dismembered` is cleared per limb that is gone.** KrokMP's version
   restores all of them unconditionally once any is missing; the heal's own outcome ("fully heals the player
   character") argues for all, but the choice has to be written down rather than inherited.
4. **What the peers see.** The restored limbs must reach the host and every other view, and must survive a
   save/restore and a reconnect. The component-removal groundwork landed for the hollow half; limb state travels
   by a different carrier and that carrier has to be checked, not assumed.

## Acceptance

A run on the deployed artifact: ampute a limb on a member, run the heal the user named, then read on that member
**and** on the other views that the limb is back, that the body is standing rather than ragdolled, and that the
state survives a save and a reconnect. The `medication-component-probe` recipe's shape (a mode that reads the
local body, plus the display-body target for the inspecting side) is the pattern to extend — it already proves
the two sides can be read independently.
## First implementation, and what a review found in it (2026-10-18)

The restore is implemented (`Character/LimbRestore.cs`, called by the native-heal wrapper before the hollow
clear) and is deliberately SMALLER than the reference: dismembering only sets `limb.dismembered = true` and
`limb.gameObject.SetActive(false)` — the object is never destroyed, so its hinge, its local transform and its
connected body all survive, and the geometry rebuild the reference performs exists because it rebuilds the body
from scratch. What the implementation does is clear the flag, re-activate, and re-apply the limb-versus-body
collision ignores (`FindObjectsOfType<Body>` rather than the reference's `NetBody.all_instances`, whose registry
is in an assembly this project does not reference; the sets differ only by the extra bodies the scene search
adds, which `BodyPatches.BodyStartPatch` already ignores with the same call).

An adversarial review found no blocker and three majors. One is fixed in the same commit — the comment claimed
the game's `OnEnable` reads the flag, and the game implements no `OnEnable` at all; the real reader is
`Limb.Start`, and the comment now says so. **The other two are owed:**

- **The restore flips the latch without firing the latch event.** Writing `dismembered = false` directly means
  no `LimbStateEventMsg` is sent, so peers converge only on the next 1 Hz snapshot (up to a second of a clone
  showing a severed limb) — and worse, `CloneFactTable.WarnOnDivergence` compares the latch against the
  event-built fact table, which the amputation already set, so every receiver logs
  `[CharSync] divergence … dismembered True → False — a dismember without an event sync`. That is the project's
  own watchdog firing on an intended operation. The fix is the one every other latch edge uses: report a
  limb-state event after the restore, through the existing limb-state patch path.
- **Nothing fails if this change is deleted** — no test names it. The acceptance row below is the proof, and it
  has not been taken.
## The acceptance row is being built, and what it has learned so far (2026-10-18)

The probe can now list a body's limbs (`mode=limb`), and the listing is the row's foundation rather than a
convenience: a run has to name a REAL limb because the game's own `amputate` resolves it through
`Body.LimbByName`, and a guessed spelling throws "is not a valid limb" — which would read as a missing feature.

Three things the attempt established, each read rather than assumed:

- **A body has fifteen limbs** and they read as `头部`, `胸部`, `腹部`, then arms and legs. The names are the
  game's own LOCALIZED ones, so picking by a spelling (`Arm`, `Leg`) finds nothing: the pick is by layout, and
  it starts at index 3 because 0/1/2 are head, chest and abdomen and amputating one of those kills the player.
- **`fullName` is not the name `LimbByName` resolves.** Passing the localized display name makes the command
  throw, so the listing now reports the object's own name beside it and a run must pass THAT.
- The body reads `standing=true shock=0` with every limb intact before anything is cut, which is the baseline
  the row compares against.

The row itself is still owed: cut a limb, read it severed, run the game's `heal`, read it back on the body and
still standing. The other views (a second client reading this body), the save/reconnect survival and the
limb-state event remain owed with it.