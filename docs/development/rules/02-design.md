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
## The wire is never a design input

Compatibility is not a design input. The only boundary is the handshake's protocol check: change the wire the mechanism
needs and let the number follow. Never trade correctness for a quiet wire, and never propose a message that already
exists — trace where the fact travels before adding a channel. A wire field whose 0 is a legal value is encoded as
`value + 1` (0 = absent), because protobuf omits 0-valued integers and the omission silently changes meaning.

## Reuse before inventing

The game's own UI is reused when a player-facing feature already has one; a parallel CUO UI needs a recorded blocker and
the user's direction. The console is diagnostic and administrative — normal gameplay never ships as a console command.
A console command's shape follows Minecraft's current template, fetched rather than rewritten from memory, and a
deviation records its reason at the point of design.

## The design page moves with the change

A design page is read before a behaviour change and written back after it, with `docs/design/audit/code.md` updated in
the same commit. A corpus neither read nor written back is dead weight.
## Write the player's frame before the code's frame

The three lines above are the *check*; this is the *act*. Before any behaviour change, write what the PLAYER sees, in order, on the frame it happens — where the body is, what the camera shows, what sounds, what the other players see at the same moment — and only then ask what code produces it. A change described as "set the position field" is a code frame and answers a question nobody asked.

Two tests that catch the code frame:

- **Would a designer have shipped this?** If the answer is "it is the smallest edit that makes the symptom go away", it is not a design, it is a patch.
- **What does the game already do for this situation?** If the game has no equivalent, the design has to come from the product's own fiction and from what the player expects the service to be — a trader who revives people does it AT the trader, not wherever the body fell, because the player paid for it there and is standing there watching.

Measured 2026-11-02: a trader revive was implemented as "heal the body where it lies", so the operator revived a teammate at the trader and the teammate stood up somewhere they could not see. The follow-up fix was then written as "add two floats and set the position", which is the same mistake one layer down — the position was right and the reasoning was still the code's.