# Design corpus index

Every page of the corpus, one line each. `AGENTS.md` states the rules the pages follow; this page is the map.

## The system

| Page | Holds |
|---|---|
| `model/authority-and-judgement.md` | actors, judgement sides, authority kinds, member reports, host rules, custody invariants |
| `model/item-custody.md` | item identity, custody, incarnations, what travels |
| `model/session-lifecycle.md` | lobby, run, cut, continue, rejoin |

## Mechanics

| Page | Holds |
|---|---|
| `mechanics/README.md` | the mechanic taxonomy |
| `mechanics/item-use.md` | applying an item to a teammate |
| `mechanics/give-and-take.md` | handing over and taking an item |
| `mechanics/console.md` | the command and chat surface |
| `mechanics/medical-view.md` | treating a teammate's body |
| `mechanics/cpr-and-revival.md` | compressing a patient, revival |
| `mechanics/sleep.md` | sleep, forced sleep, sleeping bodies |
| `mechanics/backpack-view.md` | a teammate's pack on your screen |
| `mechanics/layer-advance.md` | ending a layer, the boundary |
| `mechanics/death-and-drops.md` | dying, drops, the corpse, revival |
| `mechanics/spectator.md` | following a teammate after death |

## Items

| Page | Holds |
|---|---|
| `items/README.md` | item families and what their own actions do |
| `items/food.md` | eating: one bite per use |
| `items/drink.md` | drinking: the container's own path |
| `items/wear.md` | placing a garment on a teammate |
| `items/topical.md` | applying a medicine to a limb |
| `items/injection.md` | injecting through the medical operation |
| `items/limb-tool.md` | tools whose action is a limb action |
| `items/container.md` | bags and packs: contents, capacity, custody |

## Surfaces and cross-cutting rules

| Page | Holds |
|---|---|
| `surfaces/README.md` | the surface pages, one row each |
| `surfaces/status-line.md` | the notice and refusal line |
| `surfaces/drag-cue.md` | the use circle and its label |
| `surfaces/member-menu.md` | the menu a teammate opens |
| `surfaces/medical-panel.md` | a patient's body on the operator's screen |
| `surfaces/command-overlay.md` | the console as a surface |
| `surfaces/online-window.md` | the session's own panel |
| `surfaces/backpack-panel.md` | a teammate's pack as a panel |
| `surfaces/world-overlays.md` | nameplates, readout, arrows, pings |
| `cross-cutting/localization.md` | one language per reader |
| `cross-cutting/permissions-and-commands.md` | the command family and the host rules behind it |
| `cross-cutting/observability.md` | what a run leaves behind |
| `cross-cutting/saves-and-recovery.md` | what a save holds, what a restore may do |

## The register by domain

| Page | Holds |
|---|---|
| `decisions-by-domain.md` | the decision numbers grouped by the area a reader arrives from |

## Related reading

- `AGENTS.md` — the rules, the page template and the reading order.
- `../architecture/surfaces.md` — the surface map this corpus expands.
- `../decisions/index.md` — the decision register.
