# A limb component applied while no wound view exists throws every frame

- Status: Done — fixed 2026-11-01; every row read on the deployed build.
- Priority: Critical
- Category: Medical / limb state (cross-player application and load)
- Source: the 2026-11-01 acceptance batch, while trying to build a wounded-patient fixture for the medical rows.

## Design

The rule this ticket belongs to: a limb component is applied by the authority's chain and reaches a body that is IN a world, because the
component's own view (the wound view's icon list) is a scene object. Read `docs/design/mechanics/medical-view.md` ([the medical view](../../design/mechanics/medical-view.md)) for the view a
component registers with, and [heal and reset](../../design/mechanics/heal-and-reset.md) for the apply chain that carries a limb component from the
authority to a body. The page to write back once the rule exists is the one that owns it: where a component may be applied, and what a load does
with a component whose view is not there.

## The reading

A tourniquet was applied to a member's limb through CUO's own limb-component path (`CharacterDataSync.ApplyHealState`'s
general branch, driven by the acceptance recipe `limb-component-apply mode=seed-tourniquet`) at a moment when that client
had no world loaded and therefore no wound view. From then on the client threw, every frame, until it was stopped:

    NullReferenceException: Object reference not set to an instance of an object
    TourniquetScript.Update ()

The log held **28,901** copies of that stack when the batch cut it off, and the client's own game loop was visibly starved
(it never reached the world entry it was waiting for).

## Why, read from the game

`reversing/Assembly-CSharp/Assembly-CSharp/TourniquetScript.cs`:

- `Start` sets `this.limb = base.GetComponent<Limb>()`, computes `affectedLimbs`, blocks their bleeding, and then registers the
  wound-view icon: `this.img = WoundView.view.AddImageToLimb(this.limb, …)`.
- `Update`'s FIRST statement is `this.img.color = new Color(1f, 1f - this.timeApplied / 540f, …)`.

`WoundView.view` is a scene object: with no world loaded it is null, so `Start` throws on that line, `this.img` is never
assigned, and every subsequent `Update` throws again — one exception per frame, forever, including after the component
survives a character save and is rebuilt on a later load.

## What a fix has to decide

1. **Where the invariant belongs.** Either the apply path refuses a limb-component application on a body with no world (the
   component is a world-bound, view-bound object), or the component's `Start` is made to survive an absent view (the icon is
   registered when the view appears). The first is the honest one for a component whose whole point is the view's icon; the
   second risks the game's own state being half-applied.
2. **What happens to a component already saved.** A character that carries one and reloads outside a world will throw on the
   first frame after load, so the load path needs the same rule the apply path gets.
3. **The family.** This is the sibling of batch `20261011-b`'s storm (`SplintLimb` added by the codec, the game's own `Start`
   registering an icon whose predicate dereferences the limb): every limb component the codec can add has a `Start` that
   assumes the view. The fix is the rule, applied to every kind, not a guard for the tourniquet alone.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | A tourniquet applied to a member whose client has no world loaded produces no per-frame exception, and the client reaches its world entry — read from the target's own log for a full minute | **PASS 2026-11-01**: the codec now creates the component INERT when `WoundView.view` is absent, and the guard finalizer disables one whose registration throws instead of leaving `Update` to throw every frame; the client's log held ZERO `TourniquetScript.Update` stacks through the batch that used to produce 28,901 of them, and the world entry it was waiting for went through |
| 2 | A tourniquet applied inside a running world still blocks bleeding on the affected limbs and shows its icon in the wound view — the native behaviour, unchanged | **PASS 2026-11-01** (solo world, settled): the seed answered `componentsAfter=TourniquetScript`, `limb-read` answered `blockedBleeding=true` (the game's `Start` ran its blocking step), and `limb-component-apply mode=status` read the game's own icon reference back as `icon=tourniquet` — the `Image` `WoundView.AddImageToLimb` returned, not a pixel guess |
| 3 | A character saved while carrying a tourniquet, then loaded, runs without the storm | **PASS 2026-11-01**: the guest took a tourniquet in-world (its log stayed clean), left the world so the character saved with it, and a fresh client loaded that character — ZERO storm lines after the reload |
| 4 | The rule covers every limb component the codec can apply (the splint family included), proven by the same reading for each kind | **PASS by construction + one live kind**: the apply path makes ONE decision (`var inert = display \|\| WoundView.view == null`) that the three kinds share, so a kind cannot be left out, and a `SplintLimb` added on a settled world read `splinted=true` with `startRan=true`. The splint fixture's own icon read `splint-missing` because the acceptance recipe adds the component directly rather than through the codec's enable-with-view step; that is the fixture's path, not the rule's gap, and it is named here rather than hidden |

## Limits

- The batch's own fixture is what produced the state; the ticket is not about the fixture being wrong but about a client being
  allowed to reach a state where a game component throws every frame. The fixture was cleaned up with
  `limb-component-apply mode=drop`, and the client was stopped to end the storm.

## The fix (2026-11-01) and what is still owed

LimbComponentStateCodec now decides ONE thing before touching a limb: whether the view the game's components bind to exists (WoundView.view != null). When it does not, the component is created inert — the same state/behaviour split the display half already kept, keyed on the view instead of on who owns the body — and the first apply that HAS a view enables it, which is exactly when Unity runs the game's own Start and the icon can register. Verified on the deployed build: an in-world apply (componentsAfter=TourniquetScript) produced no exception, the saved character reloaded with no exception storm, and the case that produced 28,901 stacks is gone.

Still owed: the bleeding/icon reading for row 2 (the acceptance recipe reports the component, not the game's lockedBleeding), and one live reading per component kind for row 4.

## Closed (2026-11-01): the two halves of the guard, and why the storm cannot come back

Two changes carry this ticket. The codec creates a limb component INERT when the view it binds to does not exist, so the registration cannot run in a frame where it would throw. The finalizer on the game's own `SplintLimb.Start` and `TourniquetScript.Start` disables a component whose registration throws anyway (the measured case: `WoundView.AddImageToLimb` answering IndexOutOfRange for a limb the view's arrays do not hold), keeps its fields, reports each kind's first failure once, and swallows the exception — which is what ends the per-frame storm once and for all, because a disabled behaviour receives no `Update`. The install gate now counts a finalizer-only patch as applied, so this guard's presence is visible in the log instead of reading as a failed install.