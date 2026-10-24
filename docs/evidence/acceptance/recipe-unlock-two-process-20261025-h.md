# Recipe unlocks across two real processes: applied, relayed, converged, refused by name

- Ticket: `docs/backlog/done/recipe-unlock-fallback.md` (its declared coverage gaps 1, 2, 3 and row 6)
- Batch: `20261025-h`, deployed build `0.1.0+7605a140` (both clients report it in the window's build line)
- Instrument: new `tools/acceptance/recipes/recipe-unlock-read.cs` — reads the LOCAL client's own `Recipes.recipes` entry for one
  index and reports the game's `INT` unlock field plus every boolean member. `Recipes.recipes[idx].INT = 0` is what
  `RecipeUnlockApply` writes to make a recipe permanently visible, so `INT` is the state a player's crafting list is built from.

## The reading

Recipe 20 was locked on the guest (`INT = 4`):

| moment | guest `INT` (index 20) | host `INT` (index 20) |
|---|---|---|
| before | 4 | 4 |
| after the guest's report (`recipe-unlock-forge index=20`), ~4 s | **4** (its own report is not applied locally) | **0 — the host applied it** |
| after the host's 60 s repair cycle (~70 s) | **0 — converged** | 0 |

And a report for an index no table has (5000):

- The host refused it by name and applied nothing:
  `[Crafting] refused 76561198863287957's recipe 5000: this host's table has no such recipe (132 entries) — nothing applied,
  nothing relayed.`
- The local table holds 132 entries, so index 5000 has no entry to unlock on either side — the probe itself answers
  `index-out-of-range` for it, which is the "no unlock applied anywhere" half of the row.

## Which declared gaps this closes

1. **The adapter's Unity half** — the write really lands in the game's own `Recipes.recipes[idx].INT`: read back as 0 on the host
   within seconds of the report, on a real client, not a fake port.
2. **The protobuf round trip in a real two-process session** — the report travelled guest → host and changed the host's table.
3. **The real host's 60 s repair cycle** — the guest, whose own local copy was untouched by its report, converged to `INT = 0`
   after one cycle, which is the window the cycle exists to heal.
4. **Row 6 (host rejects / does not have the recipe)** — refused by name, nothing applied, nothing relayed.

## What this does not cover

- Gap 4 (the branch that arms the pending set before the session is active) stays **code-review only**, as the ticket declares:
  a live session has no such window, and no honest runtime reading of it exists.
- Gap 6 (the per-index give-up warning) is a **log signal only** by the ticket's own decision; this batch confirms the refusal
  line, not a user-visible surface, which is what the ticket says it is.
