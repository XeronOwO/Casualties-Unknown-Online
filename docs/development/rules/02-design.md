# Step 2 — design from the player's action and the game's own chain

Read this file and `02-design.local.md` completely before writing any behaviour change.

## Write three lines before the first edit

1. **The player's action and expectation** — the input sequence (button held, window open, what is still clickable) and
   what the player expects to happen.
2. **What the game itself runs for that same action** — the native call site and the state it reads, quoted from
   `reversing/` with file and line. This line exists only to check line 1; it never rewrites it.
3. **How the remote case reaches the same chain** and sees the same effects.

A stand-in object, a "give it first" ordering, or a replayed effect means line 1 was not taken seriously.

## Native parity is the standard

Anything that mirrors a native action or crosses players runs the game's own entry points and side effects: sound,
animation, container refresh, durability, prompts. A substitute is allowed only when the native chain is objectively
unreachable, and then the ticket carries the unreachability evidence and the difference list. "Add a sound / tweak the
text / add a highlight" is not a fix.

## Read the design record first, write it back after

The surface's decision page and its accepted ticket are read before the change and updated in the same commit.

## CUO's own code is never evidence of what the game does

Native behaviour comes from `reversing/` alone. Reading a patch and concluding "the game cannot X" is the defect this
rule names.
