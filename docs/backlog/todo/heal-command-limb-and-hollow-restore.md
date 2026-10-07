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
