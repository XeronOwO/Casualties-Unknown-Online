# Mechanics: the player's actions, one page each

A mechanic page answers one question: *what happens when a player performs this action, and what governs it*. The pages
below follow the template in `../AGENTS.md`; a mechanic without a page is a `GAP` and its first work item is the page.

## The taxonomy

| Mechanic | The player's action | Page | Status |
|---|---|---|---|
| **Item use on another player** | drags an item onto a teammate's use circle and releases | `item-use.md` | seeded |
| **Give and take** | moves an item to or from a teammate's displayed backpack | `give-and-take.md` | seeded |
| **Wear** | drags a wearable onto a teammate | inside `item-use.md` | seeded |
| **Medical view** | opens a patient's wound view, picks a limb, applies an item | `medical-view.md` | seeded |
| **CPR and revival** | holds the CPR button on a patient | `cpr-and-revival.md` | seeded |
| **Backpack view** | opens a teammate's backpack, takes or gives inside it | `backpack-view.md` | seeded |
| **Console** | opens the command surface, types, submits, completes | `console.md` | seeded |
| **Layer advance** | chooses to leave the current layer at its end | `layer-advance.md` | seeded |
| **Sleep and forced sleep** | sleeps, or is forced to sleep by an effect | `sleep.md` | seeded |
| **Death, drops and revival** | dies, drops items, is revived | `death-and-drops.md` | seeded |
| **Spectator** | follows a member's body after dying | `spectator.md` | seeded |
| **Crafting** | turns carried materials into an item | `crafting.md` | seeded |
| **Carry** | picks a teammate up, walks with them, puts them down | `carry.md` | seeded |
| **World interaction** | breaks a block, chipping it until it falls | `world-interaction.md` | seeded |

`GAP` pages are ordered by how often a change lands on them; the item, medical and console families come first because
they are where the player-visible behaviour is dense.

## What every page must state

The nine sections of the template, each in the corpus's own words: the input sequence, the native call the entry mirrors,
the rules and states, every refusal with its key, the judging side, what travels, what the player perceives, the
records, and the gaps.

## Records

`../model/authority-and-judgement.md` for the rules shared by every mechanic; `../../architecture/surfaces.md` for the
surface each mechanic belongs to.
