# Acceptance — the descent whoosh is a first-layer branch, and a member hears it

- Ticket: `docs/backlog/done/layer-descent-audio-parity.md`
- Artifact: the client the 2026-10-18 reading ran on (the ticket quotes the deployed session it sampled)
- Batch: `.acceptance/20261018-e/run1.log` (the ticket's own reading; no new run needed)
- Date: 2026-10-24
- Result: **both questions answered; there is no difference to fix.**

## The two questions

1. *Is the descent/landing sound first-layer-only in the game's own code?* Yes — `WorldGeneration.Update`
   plays the second source only under `biomeDepth == 0 && biomeOverride == None`, and that same flag enables
   the generation rects, so the whole pod-descent presentation is first-layer-only by design.
2. *When a member follows the host down a layer, does it hear what a single-player descent produces?* It hears
   the layer-1 descent because it runs its own generation: the reading sampled the member's own live
   `WorldGeneration` with `gen=true inst=false depth=0 override=None genTime` accumulating and
   `podPlaying=true podTime` advancing, then all fields cleared once the layer was up. The clip is played
   locally by whichever client generates; nothing about it is sent, and nothing is missing on the wire.

## Why the ticket closes rather than fixing something

The located condition says the deeper layers are silent BY DESIGN, so "parity" for them would be adding a
behaviour the game does not have; the ticket's own non-goal excludes an audio rework. The three-client descent
in its step 4 would only confirm a negative. What a headset would add over this reading is the user's own
subjective pass — not an acceptance row the agent can judge, and not a decision the ticket needs.

## Limits

- The reading is the ticket's own recorded 2026-10-18 machine run; it is quoted rather than re-run because
  the question is a code-branch fact plus the sampled live source, both already measured.
- No OS-level input was used.
