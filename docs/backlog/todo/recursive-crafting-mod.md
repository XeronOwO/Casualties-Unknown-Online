# Recursive crafting as a standalone mod

- Status: Todo
- Priority: Low-Medium
- Category: Mod / crafting / UI
- Source: the user's 2026-10-07 backlog request — recursive crafting (a recipe whose ingredients are themselves
  craftable, resolved over several steps) as a standalone mod, with the details the user names: favourite
  marks, item properties and merging two partially spent items' condition. The user recalls the behaviour from
  a "quality of life" mod and points at it as the reference.
- Related: `review/pinyin-search-standalone-mod.md` (the precedent for a CUO-family mod that is not the core
  plugin), `done/mod-content-ceiling.md` (the recipe surface: CUO can append recipes, not override the
  vanilla ones), `review/recipe-unlock-fallback.md` (the unlock half of the recipe panel),
  `src/CasualtiesUnknownOnline.Plugin/OnlineUiActions.cs` (a mod's UI surface if it draws its own panel)

## What is asked

- A crafting entry is offered when its ingredients are reachable through several crafting steps, not only when
  they are in the player's inventory: the mod resolves the chain and can queue or expose the steps.
- The recipe panel must say what kind of entry it is showing. The user's own requirement: an entry that is
  only reachable recursively must read differently from both "craftable now" and "cannot be crafted" — a third
  UI state the user remembers from the reference mod.
- The details that decide whether the feature is usable rather than broken:
  - **favourite marks**: a favourited recipe stays usable/visible when the recursive view is on;
  - **item properties**: resolutions must respect the ingredient's properties (a quality, a liquid, a condition
    requirement), not only its id — the recipe matcher in this game is quality-aware and amount-aware;
  - **condition merging**: two partially spent items must not lose their remaining condition when they are
    combined for a step (the user calls this the "two into one" case).

## What is not known yet

- The reference implementation is not in this repository (a search for a QoL/quality-of-life mod tree under
  `reversing/` finds none), so its UI wording and its exact rules have to be obtained before they are copied —
  or the behaviour has to be specified from scratch with the user.
- Whether the mod can reach the recipe panel at all from the mod surface, or whether it needs a Harmony patch
  into the native crafting UI. The project's rule is to reuse the game's own UI, so the answer decides the
  shape of the whole mod.
- How the chain is judged when a step needs a station, a tool that is not consumed, or a liquid measured in
  millilitres — the game's recipe matching has all three.

## Required work

1. Attribute first: read the native recipe matching and the crafting panel's own state vocabulary, and write
   down which of the three UI states already exists and which has to be added.
2. Get the reference behaviour (the QoL mod's rules and its wording) or specify it with the user before
   implementing; the third UI state is user-visible and is not the cycle's call.
3. Resolve the chain as a pure rule with the item model as input: every step names its ingredients, the
   consumed quantity and the condition each ingredient must have, and the resolution is verifiable without the
   game.
4. Ship it as a mod, not in the core plugin, and state its dependency on the core's content API.
5. Verify in a real session: a recursive entry is offered, its tooltip reads as the third state, a favourited
   recipe is unaffected, and two partially spent items merge without losing condition.

## Non-goals

- Not a crafting overhaul in the core plugin, and not a recipe-override capability (CUO can only append
  recipes today — `done/mod-content-ceiling.md`).
- Not an automation feature: the player still performs each craft.

## Stage 1 landed (2026-10-24) — the resolver, before any UI

`src/CasualtiesUnknownOnline.RecursiveCrafting.Core/` is the mod's game-free half: a plain C# model (recipe,
ingredient, held stack) and `CraftChainResolver`, which answers the three states the panel will show and the
steps a chain runs in order. It references NOTHING at all — not the game, not BepInEx, not CUO's abstractions —
so its rules are testable without a session, and the plug-in half (later stages) adapts the game's recipe table
and the player's inventory into the model.

The rules it makes explicit, each pinned by `CraftChainResolverTests` (11 cases):

- an ingredient line consumes matching stacks until it has the units AND the summed condition it asks for — the
  "two into one" case: two half-spent items satisfy a fresh line, and half a condition short stays short;
- a missing line is satisfied by a recipe producing it, resolved recursively, with the steps listed deepest
  first and the target's own click last (not listed);
- a recipe already on the chain's path is never reused (a cycle is not a chain), a line naming a quality takes
  only units carrying it, and the depth limit (8) ends a chain rather than hanging it;
- the line that stops a chain is returned as its blocker id, so the panel can say why instead of only
  "cannot be crafted".

What remains for the ticket: the plug-in half that reads the game's recipe table and the player's inventory
(the adapter), the panel's third state drawn through the existing recipe UI, the favourite mark (a UI filter the
resolver does not need), and the real-session row (a recursive entry offered, a favourite unaffected, two spent
items merged). Those are the later stages; this one is the rules the UI will show.
