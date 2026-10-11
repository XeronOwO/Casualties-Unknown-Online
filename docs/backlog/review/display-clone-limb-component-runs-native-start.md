# A live body put under the remote medical focus runs the game's own limb component

- Status: Review — fixed 2026-10-11; waiting for the acceptance run of batch `20261011-c` (rows below).
  **Found 2026-10-11 by the acceptance run of batch `20261011-b`**, while it set up the Stage-3 removal
  scenario: the moment a peer's limb carried a splint, the client that had the remote medical panel open on
  that peer threw a NullReferenceException **every frame** until the panel was closed.
- Priority: High — thousands of log lines a minute on a user-reachable surface, and the panel's own icon
  bookkeeping ends in a broken state. It did not corrupt state in this run, and the corrected root cause
  below narrows WHERE it can happen, but the call that produced it is one call away from any body CUO does
  not own.
- Category: Remote medical / display-only body / native component behaviour
- Related: `review/medical-removal-limb-component-not-applied.md` (the same component-state plumbing, opposite
  direction: what a removal must take OFF a body), `review/remote-medical-stage-3-other-actions.md`,
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md` (the run that found it).
- Source: the agent's own three-client run, batch `20261011-b`; corrected and reproduced from the code by
  batch `20261011-c`.

## What the run showed

The run put a splint on the host's view of a guest in three steps, and the storm begins at the third: the
guest's limb was seeded at 12:26:23, the host's panel was opened on that peer at 12:26:34, the renderer built
the peer's body at **12:26:47.162** (`Remote body created for <guest>`), and the first exception is at
**12:26:47.737** — one frame later. From then on the host logged this per frame
(`host-splint-icon-nre-storm-archive.log.txt`, recovered from the rotated archive `2026-10-11-8.log.gz`):

```
[ERR] [CasualtiesUnknownOnline.Plugin] [Unity:Exception] NullReferenceException
UnityEngine.Component.GetComponent[T] () (at <c39a522eee05469b8171a6cfb646c59>:0)
SplintLimb+<>c.<Start>b__4_0 (Limb x) (at <ab887c07f51841c98ea015c9c29ae1af>:0)
OnLimbWound.Update () (at <ab887c07f51841c98ea015c9c29ae1af>:0)
```

- **6065 `NullReferenceException` lines** in that client lifetime: **4473** before the panel was closed
  (last at 12:28:07.530), **0 while it was closed** (12:28:08 → 12:28:19), and **1592** after it was
  reopened (first at 12:28:19.544).
- **391 more** in a 25-second window during the real splint-removal drive
  (`host-splint-removal-and-storm.log.txt`).
- The two sandbox clients logged **zero** in the same runs (`guest-alt-exception-census.txt`).

## Root cause (corrected)

The first cut of this ticket said the display path instantiates the game's component on the display-only
copy. The code does instantiate it there — and that copy is created INACTIVE, so nothing runs on it — but
**that is not what produced the storm**. The storm's body was a LIVE one, and the acceptance driver put it
there:

1. `tools/acceptance/recipes/grip-slip-drive.cs` and `remote-wound-special.cs` opened the focus by hand —
   `RemoteMedicalView.Open(<the peer's LIVE render clone>)`, taken from
   `RemotePlayerRenderer.TryGetRemoteBody` — instead of through production's `RemoteMedicalCoordinator.Open`,
   which builds a separate parked `MedicalDisplay_*` copy. The shortcut was taken because the run's member
   probe reported `InWorld: false` for every peer (`run2-host-probe-members.json`), which made the
   coordinator's `IsRemoteInWorld` guard look like the reason the view would not open.
2. **That reading was the probe's own bug**: `MemberPresenceTable.MemberPresence.InWorld` is a public FIELD
   and the probe read it with `GetProperty("InWorld")`, which returns null — so it printed `false` for every
   peer, in every direction, regardless of the session. The production predicate
   (`SessionService.IsRemoteInWorld` → `member.InWorld`) was true throughout, which the run's own successful
   medical operations prove: `PlayerCharacterAccess.IsInWorld` is a guard on the same field, and those
   operations were accepted. The "unreconciled contradiction" recorded as a limit of batch `20261011-b` is
   therefore resolved: the field never lied, the probe did.
3. With the focus on the live clone, `RemoteMedicalCoordinator.Update()` — pumped every frame from
   `GameAdapter.Update` — applied the peer's snapshot to it, including
   `LimbComponentStateCodec.Apply(limb, limbData.Components)`, which does
   `limb.gameObject.AddComponent<SplintLimb>()`. That body is ACTIVE, so the game's own `SplintLimb.Start`
   ran: it set `limb.splinted = true` and registered a wound-view icon
   (`WoundView.view.AddImageToLimb(this.limb, Resources.Load<Sprite>("Special/splintIcon"), false, (Limb x) =>
   !x.GetComponent<SplintLimb>())`, `reversing/Assembly-CSharp/Assembly-CSharp/SplintLimb.cs:15`). The icon
   is parented to the PANEL's limb rect, not to the body.
4. The focus's own teardown then destroyed that clone — `RemoteMedicalView.Open` calls `Close()` first, and
   `Close()` destroys whatever body it was handed (`Object.Destroy(display.gameObject)`); the renderer
   rebuilt it one frame later (the `Remote body created` line). The icon survived, its `limb` did not, and
   `OnLimbWound.Update` calls the icon's predicate — which dereferences the limb — every frame for as long as
   the panel stays open.

The three observations follow: the storm starts the frame the clone is replaced, stops when the panel is
closed (the icon is a panel child and stops updating), and resumes when it is reopened (the stale icon is
still there).

## What the fix does

`RemoteMedicalCoordinator` still builds its own parked copy, and now that difference is enforced rather than
assumed:

1. **The display copy is marked.** `RemoteMedicalDisplayBody` is a field-less marker on the copy's root, read
   through the limb's own `transform.root` so it answers on the parked hierarchy too.
2. **The display half refuses a body CUO does not own.**
   `LimbComponentStateCodec.ApplyDisplay` returns without touching a limb whose root carries no marker, so the
   live-body shape the driver produced is inert by construction instead of producing a storm.
3. **On a display copy the instances are inert state holders**: created with `enabled = false`. What keeps
   the game's own `Start` off that copy is that the hierarchy is parked inactive when it is built
   (`RemoteMedicalCoordinator.TryCreateDisplayBody`'s `go.SetActive(false)`, the only `SetActive` on it
   anywhere in `src/`); the flag is the second layer, and the layer that DOES cover a live body is the
   refusal in item 2. The pair is reproducible with
   `tools/acceptance/recipes/limb-component-apply.cs`: `mode=add-disabled` followed by a later `mode=status`
   reads the game's own `Start` unrun for the disabled instance, while the same component left enabled reads
   it run. The panel's own `TryGetComponent<ChilledLimb>` read (`WoundView.cs:396`) still finds its instance.
4. **CUO draws the icons the game would have drawn**, from the state, through the game's own API
   (`RemoteMedicalDisplayIcons` → `WoundView.AddImageToLimb` with the game's own sprites): a teammate's splint
   or tourniquet is visible in the panel, which is the surface where it matters — the panel's special-action
   dispatch reads exactly that state. The icons are owned by both lifetimes: the state going away removes the
   icon, `RemoteMedicalView.Close()` (the one place that destroys the display copy) clears the rest, and the
   icon's own removal predicate is `(Limb x) => x == null` — it may only ask whether its limb is still alive.
   The game's own predicates dereference the limb, which is what made a replaced body throw every frame.
5. **The acceptance driver opens the view the way production does** — `RemoteMedicalCoordinator.Open` through
   the domain object — and reports the guard readings beside the result, so a refusal names itself. The
   action steps require the focus the open left behind and refuse by name otherwise (calling `Open` again
   would `Close()` first, cancelling the session under test). Both recipes report `displayBodyName` and refuse
   a focus that is not a `MedicalDisplay_*` copy: the distinction this ticket turned on is now a witness in
   every drive. `grip-slip-drive mode=probe` reads the member's `InWorld` as a field OR a property, and
   reports production's own `IsRemoteInWorld` beside it.

## Acceptance rows

| # | Row | Fails when |
|---|---|---|
| 1 | with a peer whose limb carries a splint, one client keeps the remote medical panel open on that peer for a full minute and logs zero `[Unity:Exception]` lines | any exception appears |
| 2 | in the same window the panel shows the splint (the icon is drawn from the state, not from the game's own `Start`) | the display stops rendering the splint |
| 3 | the same holds for a tourniquet (`TourniquetScript`) | the sibling component floods, or its icon is missing |
| 4 | the same holds for a chilled limb (`ChilledLimb`), whose own `Update` acts on the limb it is attached to | the third component of the codec's set floods or mutates a display limb |
| 5 | leaving and re-entering the focus on that peer (the display copy is rebuilt) stays quiet | the storm returns on the rebuild |
| 6 | the production open path is the one the run drives, and its guards report the readings that let it through | the run falls back to a hand-built focus, or a refusal is reported without its guard readings |

Rows 1-5 are the rows the earlier cut of this ticket carried; row 2 now names what the fix actually owns (the
icon is CUO's), and row 6 pins the drive shape whose absence hid the real cause for a whole batch.

## Limits

- The run observed the storm only on the client that had the panel open; a client that never opens the panel
  was quiet in the same session.
- Rows 1-4 are read from the client's log and the panel; row 2's "the panel shows the splint" is judged from a
  captured frame by the run's own rule for a visual row.
- The display copy's `ChilledLimb` remains a real (disabled) instance, because the native panel asks for the
  game's own type. If a future game update gives that component work in `Awake` (which does run for a
  component added to a live object), the display half would need its own state carrier instead.
