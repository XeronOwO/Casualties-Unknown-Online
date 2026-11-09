# The gameplay principles the systems imply

Derived from the ten pages beside this one. A principle here is a rule the game's own code enforces across several
systems; it is written as what the player experiences, then what the code does to produce it. The page cites the area
pages rather than repeating their lines.

## 1. The body is the resource

A player's state is not a hit-point pool but a body with eleven limbs, blood, oxygen, temperature, infection and
consciousness (`medical.md`). Death is a state the body reaches, not a counter a hit decrements: the game has a "dying"
set and a "death" set, and the second follows the first only when the body cannot recover (`Body.cs:332`, `:342`).

Implication: a mod must sync the body's own values, not a health number. A wound is state on a limb; a rescue is a
change to that state through the game's own paths.

## 2. The client that experiences a thing judges it

The game's own design is local-first: a client's body, its wounds, its console and its windows are simulated on that
client; the world is the host's. The mod's decision 184 makes it explicit: injury, interaction gates and concurrency are
judged by the client that experiences them, and the host keeps the world state and the arbitration between conflicts.

Implication: a member's limb report is a claim the host can bound; a host's world fact is a fact a member must accept.
The two directions are not symmetric, and a design that treats them as symmetric breaks one side.

## 3. The world is the host's, the body is the client's

Blocks, fluids, buildings, enemies and loot are the host's world (`world-objects.md`, `fluids-and-climate.md`,
`traps-and-events.md`); a member's copy is a projection of the host's state. The body, the wounds and the console are
the member's own (`medical.md`, `console-and-save.md`).

Implication: a mod's wire carries two kinds of facts — the host's world state (accepted, never re-derived) and the
client's body claims (bounded, corrected when implausible). A single "sync everything" design is wrong for both.

## 4. A run is a countdown of layers, and each layer starts a checkpoint

The layer count comes from the world's own temperature curves; the descent is a depth trigger; the layer-end save
panel is the game's own checkpoint (`worldgen-and-layers.md`). The run's multipliers accumulate per layer.

Implication: the save's unit is the layer, and a mid-layer cut is the exception. A mod's archive should treat a
layer-end cut as the canonical save and a mid-run cut as the recoverable one.

## 5. Rules are data, and a mod reads them

Recipes are records (`crafting.md`), loot is a registry table (`world-objects.md`), run settings are a seeded dictionary
(`ui-and-settings.md`), liquids are callbacks (`fluids-and-climate.md`). Nothing in the game hard-codes a recipe list or
a loot table in logic.

Implication: a mod that copies a table or a constant goes stale on the next content update; a mod that reads the
registry or the run setting follows the game.

## 6. Failure is legible

The game names why a body is dying (the dying set), why a recipe cannot be made (the inventory lookup returns nothing),
why an item is not usable (the condition and the liquid's health flag), and what a lock is (the minigame's angle).

Implication: a mod's refusals should be equally legible — a key plus its arguments, rendered in the player's language,
never a silent drop. A refusal that the player cannot read is a bug even when the rule is right.

## 7. The console is diagnostic, not a gameplay surface

Console commands act locally and are gated by the game's own cheat switch (`console-and-save.md`); the game's normal
loop never requires them. The mod follows: the console is administrative, and a session's cheat roster is the host's
policy.

## 8. Two bodies can be required

Some operations are inherently two-operator (the manual defibrillator, the surgery's second hand); the game's own
minigames encode that by reading both hands.

Implication: a mod's interaction design must allow an operation to be owned by a pair, not only by one player.

## What this means for the mod

These eight principles are the ground the mod's own design pages (`docs/design/mechanics/`, `surfaces/`, `items/`)
stand on. Where a mod design disagrees with one of them, the principle wins and the design is rewritten — the same
rule as "the documents outrank the plan", one level up.
