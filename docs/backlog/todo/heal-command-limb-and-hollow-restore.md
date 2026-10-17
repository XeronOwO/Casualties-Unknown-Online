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
## Correction, and the hollow half answered (2026-10-18)

**The previous section was wrong about the hollow half and this corrects it.** It said KrokMP "does not touch
any moodle at all, so clearing the hollow debuff cannot be copied from it". The first half is true and the
conclusion does not follow: KrokMP removes the thing that PRODUCES the moodle, which is the only thing that
works.

The evidence, read in order:

- `MoodleManager` draws the icon every frame from body state, not once:
  `if (this.body.mindWipe && this.body.mindWipe.active) { this.AddMoodle(0, "hollow", …); }` — the only place in
  the whole assembly that names `"hollow"`. So destroying the icon is USELESS: the next frame draws it again,
  and `ClearMoodles()` (the manager's only removal member, all moodles, not one) is no better.
- `Body.mindWipe` is a component reference, not a flag: `this.mindWipe = base.GetComponent<MindwipeScript>();`
  with `public MindwipeScript mindWipe;`, and `MindwipeScript.active` is the bool the manager tests.
- The moodle is not the whole debuff either. `Body`'s effective-happiness getter multiplies by
  `(this.mindWipe ? 0f : 1f)`, so the same component forces effective happiness to zero — which is what a player
  actually feels.

So **the clear path is removing `MindwipeScript` from the body**, and that is exactly what KrokMP's `unmindwipe`
arm does (`if (unmindwipe && body.TryGetComponent<MindwipeScript>(ref mindwipeScript)) Object.Destroy(mindwipeScript);`).
The ticket's open question — "whether the hollow moodle is cleared by any native path at all today" — is now
answered with the mechanism rather than a yes/no: **no path clears it, and the reason is that nothing removes
the component**, which no game code does.

**What that leaves for the implementation.** The removal is a mutation of the PATIENT's body, so by decision 184
it belongs on the affected player's own client, and it has to reach the host and every peer to be worth
anything — a heal that removes the component only locally leaves every other view showing hollow and the next
character sync putting it back. That replication question, not the removal itself, is the work.
## The replication question, answered: a removal cannot travel today (2026-10-18)

The channel exists. CUO already carries `MindwipeScriptPresent` / `MindwipeScriptActive` on the character health
message (`KernelDomainWireMapper` and `KernelPlayerInteractionWireMapper` both map them), and
`MedicationComponentsSync` is the helper that moves that component state — its own doc comment says it exists
because "those fields live on components, not on `Body`".

**But the channel only ever ADDS, never REMOVES**, and that is a defect rather than a limitation of this ticket:

- `Capture` writes the presence flag only when the component is there (`if (mindwipe != null) { Present = true; … }`),
  so a body WITHOUT the component sends the field's default `false`.
- `Apply` reads it as `if (health.MindwipeScriptPresent) { … add or set … }` — with **no else arm**. A `false`
  therefore means "do nothing", not "remove", and a client that already has the component keeps it.

The same hole runs through the other two components in the same helper: `SleepingPillsAmount` and
`AntidepressantsAmount` are only acted on when non-zero, so a dose that has decayed to zero — or a component the
owner no longer has — leaves the receiver's copy standing.

**What that means for this ticket.** The heal's hollow half is exactly "make the component absent", so it cannot
replicate through this helper as written: the operator would remove it, the wire would carry `false`, and every
other view (and the restored save) would keep showing hollow. Fixing the helper is therefore a PREREQUISITE of
this ticket's limb/moodle outcome, and it is a fix in its own right — three components, one shape, measurable on
its own — so it is filed as its own item rather than smuggled into the heal.

**Why it was not caught**: every existing case for these components drives them from zero to a value. Nothing
exercised arrival at zero or the removal of a component the receiver already holds, which is the direction that
was never written.

## What this leaves to decide (unchanged)

- Which surface: the vanilla console command (item-free) or CUO's own `/heal`.
- How much of KrokMP's ~40 extra body fields parity wants — that author's composition, not the game's own idea
  of "fully heals", and each one has to replicate and survive a save to be worth having.
## The prerequisite is fixed (2026-10-18), and what a review changed in it

The removal direction now exists in both helpers. `MedicationComponentPolicy` maps the wire values to
`Absent` / `Inactive` / `Active` (pure, so the rule is a case rather than an `if` arm somebody has to notice is
missing), and `MedicationComponentsSync.Apply` / `PainkillersSync.Apply` destroy the component when the sender
has none instead of doing nothing. Five cases pin the rule.

**The review found no blocker and three things worth carrying:**

- **The removal arms have no case that fails without them.** The five cases drive the pure policy, and the two
  existing test classes for these helpers assert only their reflective SHAPE (parameter count, static), so
  deleting every `Object.Destroy` would leave the suite green. What would prove it is a two-client row: let a
  dose decay to zero on one client and read `GetComponent<Painkillers>() == null` plus a happiness reading on
  the other. **That row is owed.**
- **Destroying the component is not enough on its own** (fixed here): the body caches it in `mindWipe` and
  refreshes that handle on its own half-second pass, so without clearing the handle the body keeps multiplying
  effective happiness by zero. `Apply` now clears it, which is what `RemoteCharacterDisplayProjection` already
  did by hand — that arm is now redundant and is the next cleanup.
- **A behaviour change nobody asked for, recorded rather than hidden**: `TraderRecruitPolicy.PrepareRevive`
  hand-builds a health message with every medication field zeroed, and it reaches `Apply`. So revive and
  next-level respawn now clear medication where they previously left it standing. Death clearing medication is
  the intended end state and both entry points require `Alive == false`, but it is a change inside a fix whose
  subject was the heal, so it is named here.
## The owed row, attempted and re-aimed (2026-10-18)

A probe now exists (`tools/acceptance/recipes/medication-component-probe.cs`: plant / read / clear-local on the
local body) and a two-client batch ran it on the deployed artifact. It produced a reading, and the reading says
the row was aimed at the wrong side.

**What the run showed.** Planting worked (`sleepingPills=true` on the next read, `mindwipe=true active=true`
after planting it), and six seconds of authoritative snapshots later both were **still there**. The batch
called that a failure. It is not: **the client the component was planted on is the OWNER of that body**, and by
the disclosure rule the affected player's own client is the authority for their own state. So the owner's next
capture reports "present" — the system is self-consistent, and no snapshot has a reason to remove anything.
Planting on the owner tests the one direction that cannot fail, so a green there would have proved nothing
either; the red is the same defect in the fixture.

**Where the removal direction is actually reachable**, from the review's call-site audit:

- the **remote medical display clone** — a player inspecting a teammate applies that teammate's health to a
  display body, so a component the teammate cleared must disappear from the clone;
- the **restore** path (`CharacterRestoreApplier`) — a restored snapshot that says "absent" must not leave the
  body's previous component standing;
- the **local body** when the host sends an authoritative result for it — which is the path a heal takes.

The cleanest of the three to drive is the display clone: it needs the medical view open on a teammate, no
combat, and its own read. The next row should plant on the INSPECTED player's body (or clear it there) and read
the CLONE, which is the side that must follow.

**Also recorded**: the batch's first attempt read `missing` for every field — the recipe was passed a bare mode
name where the driver requires `key=value`, so no answer file existed and every comparison fell to a default.
The verdict lines printed FAIL for a reading that was never taken. That is the same shape the earlier batches
were taught to avoid: a check that reports an absence from a reading it has not shown it can make.