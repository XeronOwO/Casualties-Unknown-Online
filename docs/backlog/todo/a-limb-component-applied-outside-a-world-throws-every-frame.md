# A limb component applied while no wound view exists throws every frame

- Status: Todo
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
| 1 | A tourniquet applied to a member whose client has no world loaded produces no per-frame exception, and the client reaches its world entry — read from the target's own log for a full minute | unjudged |
| 2 | A tourniquet applied inside a running world still blocks bleeding on the affected limbs and shows its icon in the wound view — the native behaviour, unchanged | unjudged |
| 3 | A character saved while carrying a tourniquet, then loaded, runs without the storm | unjudged |
| 4 | The rule covers every limb component the codec can apply (the splint family included), proven by the same reading for each kind | unjudged |

## Limits

- The batch's own fixture is what produced the state; the ticket is not about the fixture being wrong but about a client being
  allowed to reach a state where a game component throws every frame. The fixture was cleaned up with
  `limb-component-apply mode=drop`, and the client was stopped to end the storm.
