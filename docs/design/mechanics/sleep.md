# Sleep, forced sleep and the sleeping body

Going to sleep, being forced to sleep by an effect, and what a sleeping body allows.

## What the player does

Sleeps in a bed or is put to sleep by an effect, and later wakes. In co-op the player also meets a SLEEPING teammate: a
body lying down, not conscious, that may need treatment or food.

## What the game itself does

Sleep is the game's own body state: the body lies down, its consciousness is suppressed, and the game's own wake rules
apply. Forced sleep (a mushroom tail and other mandatory effects) is the same state reached from an effect rather than a
bed, and the game treats it as ordinary sleep.

## Rules and states

- A sleeping body is not conscious; a body that is not conscious does not act, does not judge its own actions, and cannot
  be asked to consume anything.
- Normal sleep and forced sleep are the same state; the design does not distinguish them for treatment.
- **How well a body slept multiplies what it recovers, on the game's four steps.** `Body.SleepQualityToRegen`
  (`Body.cs:1030-1045`) returns `0.7f` for a bad sleep, `0.85f` for a mediocre one, `1f` for okay and `1.25f` for a good one —
  so a CUO sleep rule that invents its own multiplier is a different game.
- A sleeping body may still be TREATED: bandaging, splinting, an injection and a topical are exactly what an
  unconscious teammate needs, so they do not require consciousness.
- The two families where the TARGET consumes the item (food and drink) require a conscious target, because the target's
  own body must swallow or chew.
- Whether a sleeping (rather than injured-unconscious) teammate may be FED is not yet decided; it is recorded as an open
  question rather than answered by a guess.
- **A nap needs more than being tired.** `Body.canTakeNap` (`Body.cs:34-40`) is
  "`energy < 35f && averagePain < 31f && sicknessAmount < 80f`" (or a component that grants it), so a body in pain or sickness
  cannot simply sleep it off.
- **Waking applies the night it ended on.** `WakeUp()` (`Body.cs:3030-3050`): a Mediocre sleep sets `badSleepAmount = 60f`, a
  Bad one `150f`, and a Good one raises `caffeinated` to at least `180f` and stamps `goodSleepTime` — the quality is not only a
  regeneration multiplier, it leaves a state behind.

## Refusals

| Condition | The player reads |
|---|---|
| the target is unconscious and the family needs it awake | `use.refused.target_unconscious` |
| the target is dead | `use.refused.target_dead` |
| the operator itself is not conscious | `use.refused.not_conscious` |

## Who judges

Sleep and waking are the sleeping player's own body facts: the client that owns the body writes them, and the host
arbitrates. A treatment applied to a sleeping body is executed by that body's client, which is awake enough to apply an
item even while the body sleeps.

## What travels

The body's sleeping and consciousness state, as part of the character data every client holds; a treatment's outcome as
the item's and the body's state.

## What the player sees and hears

A sleeping teammate is shown lying down and unresponsive; the medical view still lists their limbs, and a treatment
applied to them plays the game's own animation and sound on their body.

## Records

`../../architecture/surfaces.md` (cross-player use row), decision 160 (sleep policy). Open:
`../../backlog/todo/use-on-sleeping-target-refused.md`.

## Gaps

- The sleeping-target feed question above is the open ticket's decision, not a design this page may settle.
- What wakes a sleeping body (damage, an effect's end, an alarm) and whether a treatment can wake it is not recorded here
  yet.
