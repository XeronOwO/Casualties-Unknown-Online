# Heal must restore severed limbs and clear the hollow moodle

- Status: Todo
- Priority: Medium
- Category: Player state / console commands / KrokMP parity
- Source: the user's 2026-10-07 backlog request — the game's own `heal` command does not restore severed
  limbs and does not remove the hollow debuff; KrokMP modified it and CUO should support the same.
- Related: `reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs` (the native `heal` and `amputate`
  commands), `reversing/Assembly-CSharp/Assembly-CSharp/MoodleManager.cs` (the `"hollow"` moodle),
  `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesUtils/Util_BodyExtensions.cs` (KrokMP's
  `ResetHealth` body extension), `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/Con_.cs`
  (KrokMP's re-registration of the same command with a player argument),
  `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteHealApplication.cs` (CUO's own heal
  family), `review/save-native-character-field-parity.md` (the limb fields a restore has to carry)

## What is observed

- The native command is registered as `heal`, described as "Fully heals the player character." It writes each
  limb's `muscleHealth`, `skinHealth`, `boneHealTimer`, `dislocationTimer`, `infectionAmount`, `bleedAmount`,
  `pain`, `shrapnel` and `infected`, plus the body's `brainHealth` and `clawHealth` — it never touches a limb's
  `dismembered` state, and the sibling command `amputate` documents itself as "Irreversibly."
- The game has a hollow moodle of its own: `MoodleManager` registers an `"hollow"` moodle, so "clear the
  hollow debuff" is a native moodle, not a CUO invention.
- KrokMP does not extend the field list; it re-registers the same `heal` name (adding an optional player
  argument) and heals through its own `Body` extension
  `ResetHealth(this Body body, bool unmindwipe = true)`. That name appears nowhere in the decompiled
  `Assembly-CSharp`, so the extension is KrokMP's own composition over native fields and has to be read
  field by field before anything is copied.

## What is not known yet

- Which surface the report is about. The user names the vanilla command; CUO also has its own `/heal`
  ("Use a carried medical item on the selected player(s)") which is a different mechanism (a real item and a
  real treatment). The cycle's first step is to say which one is extended, and whether both should end up
  with the same limb/moodle outcome.
- Whether a restored limb and a cleared moodle replicate. CUO's character sync and the save parity ticket own
  the limb fields; a heal that only fixes the local body would leave every other view and the next restore
  wrong.
- Whether the hollow moodle is cleared by any native path at all today.

## Required work

1. Attribute first: read KrokMP's `ResetHealth` extension field by field and the native heal command's field
   list, and write down the difference (which limb state, which moodle, which timers).
2. Decide the surface and implement the missing half there, with an item-free console form and, if the user's
   report turns out to be about CUO's `/heal`, the same outcome through the item path.
3. Replicate the result: the healed limb state and the moodle must reach the host and every peer, and must
   survive a save/restore and a reconnect.
4. Verify with the exact reproduction on three clients: amputate a limb and take the hollow moodle on one
   member, heal, then read the limb state and the moodle on that member and on both other views.

## Non-goals

- Not a general "restore everything" command: the ask is the same outcome the vanilla command already gives,
  extended to limbs and the hollow moodle.
- Not a medical-item rework: CUO's remote treatment family keeps its own path; only the outcome parity is in
  scope.
## Step 1, done: what KrokMP actually adds, and the half it does NOT have (2026-10-18)

Read field by field, and the reading changes the shape of the work: **KrokMP's `ResetHealth` does not touch any
moodle at all.** Nothing in it mentions a moodle, a debuff or `MoodleManager`, so the user's second ask —
clearing the hollow debuff — is not something KrokMP answers and cannot be copied from it.

**Limb restoration is KrokMP's own invention, not a native call.** `body.RegrowAllLimbs()` reads like a game
method and is not one: it is an extension KrokMP defines itself
(`KrokoshaCasualtiesUtils/Util.cs`, `public static void RegrowAllLimbs(this Body body)`), and the native `Body`
does not declare it. What it does is a physics procedure rather than a field write:

- only when `body.IsAnyLimbMissing()`;
- save `transform.position`, `standing` and force the facing with `ForceSwitchDir()`, then `Ragdoll()`;
- restore every `ClientMain.SavedHingeJointState` under the body — each limb's and its connected body's local
  position/rotation, with the hinge's `referenceAngle` recomputed;
- deactivate every limb and set `dismembered = false`, then reactivate every limb and set it again;
- reconnect each hinge's `connectedBody`;
- re-apply `Physics2D.IgnoreCollision(limbCollider, collider, true)` for every limb against **every**
  `NetBody.all_instances` — a loop over limbs × bodies × colliders;
- `body.shock = 0f` and restore the position.

Two things in that procedure are worth naming before copying it: **`standing` is saved and never restored**, and
`Ragdoll()` is called with no matching un-ragdoll — so a heal built this way can leave the patient lying down.
A CUO restore should end in a state the character sync can carry, not in a physics state that happens to look
right on the operator's screen.

**The rest of `ResetHealth` beyond the native field list** (the native command writes nine limb fields plus
`brainHealth`/`clawHealth`): KrokMP additionally writes limb `furBloodAmount`; removes `SplintLimb` and
`TourniquetScript` (setting `condition = 0f` then `TakeOff()`); destroys `Painkillers`, `SleepingPills` and
`Antidepressants` components; cancels the body's `CoUtils` (and the personal one when the body is a player);
destroys `MindwipeScript` when `unmindwipe` is set; and writes roughly forty body fields (blood volume, oxygen,
pressure, heart rate, viscosity, respiratory rate, stroke, pulmonary embolism, fibrillation, hunger, thirst,
septic shock, temperature, sickness, consciousness, stamina, energy, happiness, radiation, disfigurement, eyes,
internal bleeding, hemothorax, trauma, dirtiness, wetness, sleep debt, hearing, adrenaline, venom, claw regrow,
and more).

**The hollow moodle has no native clear path.** The string `"hollow"` appears exactly ONCE in the entire
decompiled game assembly — at its own registration
(`MoodleManager.cs`, `this.AddMoodle(0, "hollow", Locale.GetMoodle("hollow"), Locale.GetMoodle("hollowdsc"), false, false)`).
Nothing else in the assembly names it, so no game code clears it, and `MoodleManager`'s only removal member is
`ClearMoodles()` — all of them, not one. So clearing hollow specifically is CUO's own work, and the first
question it has to answer is whether a per-moodle removal exists on the moodle object or whether the family has
to be re-derived after a full clear.

## What this leaves to decide

- **Which surface**: the vanilla console command (item-free) or CUO's own `/heal` (a real carried item and a
  real treatment). Unchanged by the reading, and still the first thing to settle.
- **How much of KrokMP's field list parity wants.** The native command's nine limb fields plus two body fields is
  the game's own idea of "fully heals"; KrokMP's extra forty are that author's composition. Copying them all is
  a design choice, not parity — and every one of them has to be replicated to peers and survive a save/restore
  to be worth anything.