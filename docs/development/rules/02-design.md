# Step 2 — design from the player's action and the game's own chain

Read this file and `02-design.local.md` completely before writing any behaviour change. What measured these rules is in
[why.md](why.md); the corpus's own page shape is in [../../design/AGENTS.md](../../design/AGENTS.md).

## Write three lines before the first edit

1. **The player's action and expectation** — the input sequence (button held, window open, what is still clickable) and
   what the player expects to happen.
2. **What the game itself runs for that same action** — the native call site and the state it reads, quoted from
   `reversing/` with file and line. This line only checks line 1; it never rewrites it.
3. **How the remote case reaches the same chain** and sees the same effects.

A stand-in object, a "give it first" ordering, or a replayed effect means line 1 was not taken seriously.

## The player's frame, not the code's

Write what the PLAYER sees, in order, on the frame it happens — where the body is, what the camera shows, what sounds,
what the other players see at that moment — and only then ask what code produces it. Two tests: would a designer have
shipped this, or is it the smallest edit that makes the symptom go away? And what does the game already do here? If it
has no equivalent, the design comes from the product's own fiction and from what the player expects the service to be.

## Native parity is the standard

Anything that mirrors a native action or crosses players runs the game's own entry points and side effects: sound,
animation, container refresh, durability, prompts. A substitute is allowed only when the native chain is objectively
unreachable, and then the ticket carries the unreachability evidence and the difference list. "Add a sound / tweak the
text / add a highlight" is not a fix.

## Records and evidence

- The surface's decision page and its accepted ticket are read before the change; the page is written back after it,
  with `docs/design/audit/code.md` updated in the same commit.
- CUO's own code is never evidence of what the game does: native behaviour comes from `reversing/` alone. Reading a
  patch and concluding "the game cannot X" is the defect this rule names.

## The wire and the surface

- Compatibility is not a design input. The handshake's protocol check is the only boundary: change the wire the
  mechanism needs and let the number follow. Never trade correctness for a quiet wire, and never add a message that
  already exists — trace where the fact travels first.
- A wire field whose 0 is a legal value is encoded as `value + 1` (0 = absent), because protobuf omits 0-valued
  integers and the omission silently changes meaning.
- The game's own UI is reused when a player-facing feature already has one; a parallel CUO UI needs a recorded blocker
  and the user's direction. The console is diagnostic and administrative — normal gameplay never ships as a console
  command; its shape follows Minecraft's current template, fetched rather than remembered, and a deviation records its
  reason.
