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
## Located: the descent sound is a first-layer-only CODE BRANCH (2026-10-18)

The user's memory is right, and the reason is one condition in the game's own code rather than a scene object.

`WorldGeneration.cs` has `this.caveAudio = base.GetComponent<AudioSource>();` and, in `Update`, plays a second
source under an explicit depth test:

```
if (this.generatingWorld || this.instantiatingWorld)
{
    bool flag = this.biomeDepth == 0 && this.biomeOverride == WorldGeneration.OverrideSceneType.None;
    if (flag)
    {
        …
        if (!this.genPodSource.isPlaying)
        {
            this.genPodSource.Play();
        }
    }
    for (int j = 0; j < this.genRects.Length; j++) { … this.genRects[j].GetComponent<Image>().enabled = flag; }
```

So the descent/landing whoosh is gated on **`biomeDepth == 0`** — the first layer — and on there being no scene
override. The same flag drives the generation RECTS (`genRects[5]` is enabled exactly when it is false), so the
whole "pod descent" presentation is first-layer-only, not just its audio. That answers the ticket's first
question: **it is a code branch, not a scene or prefab object, and it is deliberately first-layer-only.** Deeper
layers are supposed to be quiet, so there is no parity to add for them.

It also answers the second question's mechanism. The sound is played **locally, by whichever client is running
its own generation**, for as long as `generatingWorld || instantiatingWorld` holds. It is not sent, and nothing
about it is host-driven — so a member that generates layer 1 itself plays it itself. The remaining question is
not "does the wire carry it" but "does a member reach that branch at all": CUO regenerates the layer on every
client (the sibling ticket `layer-descent-spawn-separation` records that each client generates and places only
its own body), which is exactly the condition this branch needs. **What the run has to confirm** is that a
member's layer-1 generation really does pass through `generatingWorld`/`instantiatingWorld` with `biomeDepth`
still 0 at that moment, and that its own `genPodSource` is audible when it does — a member's generation may be
driven by the host's state rather than by the member's own start, and that is the one way this could still be
silent.