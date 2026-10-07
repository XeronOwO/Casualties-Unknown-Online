# Descent and per-layer ambience: does the guest hear what the host hears

- Status: Todo
- Priority: Low-Medium
- Category: Audio / layer transition
- Source: the user's 2026-10-07 backlog request — entering a layer plays a whooshing descent/landing wind, and
  the user believes that only happens on the first layer. They ask for the native code to be checked and for
  CUO's own descent (a guest following the host down a layer) to be verified against it.
- Related: `review/unhooked-item-and-body-sound-families.md` (local-only one-shot sounds outside the ingest
  family), `review/suppressed-native-call-sounds-stay-unheard.md` (a sound whose native call CUO suppresses),
  `docs/backlog/done/host-eating-sound-not-heard-on-guest.md` (the family's established shape),
  `todo/layer-descent-spawn-separation.md` (the same transition, another symptom)

## What is observed so far

- A search of the decompiled `Assembly-CSharp` tree for `wind`, `windSound`, `descent` and `whoosh` finds no
  member by any of those names. So the sound the user hears is not visible as a named branch in the game's
  own C# — it is either an `AudioSource` assigned in a prefab/scene, or a clip played by a resource id from a
  call site whose name says nothing about wind. That is a reading, not an answer: the trigger still has to be
  located.

## What is asked

Answer the user's two questions with evidence, then fix what the answer shows:

1. Is the descent/landing sound a first-layer-only effect in the game's own code (or scene), as the user
   remembers?
2. When a member follows the host down a layer in CUO, does the member hear what a single-player descent
   produces — the same clip, at the same moment, for every member?

## Required work

1. Locate the sound: find the clip and its trigger (scene/prefab `AudioSource`, or the call site that plays a
   resource id), and write down its condition. If it is a scene object rather than a code branch, say so and
   name the scene/prefab — the user's question is answered either way, and a wrong "it is a code branch"
   would send the next cycle hunting in the wrong tree.
2. Read CUO's layer-transition path against that condition: which side runs the generation, which side plays
   the clip, and whether a guest's own descent is driven by its local generation, by the host's state, or by
   neither.
3. Fix the difference if there is one — a member that descends hears the descent, once, at the right moment —
   through the existing sound-carrying mechanism rather than a new one; if the carrier cannot express it,
   record that as the finding with the carrier's own reason.
4. Verify on three clients: descend twice from a settled world and compare each client's log and the audible
   result, including a member that enters the layer late.

## Non-goals

- Not an audio rework: this is one transition's parity.
- Not a claim about head-feel: "sounds right" is the user's call, and the machine half (which clip, which
  moment, which clients) is what this ticket can prove.
