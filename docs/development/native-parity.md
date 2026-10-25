# Native parity: how a CUO behaviour is required to mirror the game's own

A behaviour that mirrors a native action, or that lets one player act on another, is judged by **parity**: the same
player action, through the same native call chain, producing the same effects. This page is the standard those
changes are held to, and it exists because a whole family of defects shipped while every gate stayed green.

## Why this page exists

Between 2026-10-24 and 2026-10-25 a session collected defects that were all one mistake wearing different clothes:
the cross-player item path reached for whatever mechanism the code already had — a materialised stand-in for the
item, an intent message, a "give then use" ordering, a clip played to cover a missing sound — instead of running the
game's own chain. The gates could not catch it: they measure consistency, documentation shape and test outcomes, and
none of those answer "is this what the game itself does". The consequences were a stone fruit eaten without its peel
step, a whole pancake gone in one gesture, a transfer with no sound, and a "use" gesture whose visible state said
success while nothing was drawn.

## Which authority governs which surface

Native parity governs an action the GAME already has — using an item, dragging it, moving it into a container. A surface whose
design authority is something else is NOT judged against the game's own version of it:

- **The console and its command family follow Minecraft's current template** (`AGENTS.md` rule 17, decision 264): an input rule
  there is designed from Minecraft's behaviour, and the game's own console is only the host surface that carries it. An empty Enter
  that does nothing is a fact about the game's console, not an argument against Minecraft's "empty Enter closes the prompt".
- **CUO's own surfaces** (the online window, the panels) are governed by the player-experience rules and the native-UI-reuse rule,
  not by a native counterpart.

Getting this backwards is a real failure: it was mis-applied once, when the game's native console was treated as the authority for a
console behaviour and a Minecraft-shaped expectation was filed as a "deviation from native" (2026-10-25, corrected the same day).

## The three lines (required before any code)

Every behavioural change that crosses players or mirrors a native action states these three lines **in its ticket,
before the first code edit**. The checklist refuses the commit while they are missing.

1. **The player's action and expectation.** What the player presses and holds, what is on their screen, and what they
   expect to see happen. Written in player terms only — no type names, no messages, no slots.
2. **What the game itself runs for that same action locally.** The native call site (file and quoted line from
   `reversing/`), and the state it depends on (the hand, a body slot, a container, time). This line **confirms or
   corrects** line 1. It may never rewrite line 1: if the native chain cannot express what the player expects, that
   is a design question for the user, not a licence to substitute a mechanism.
3. **How the remote case reaches that same chain.** The path from the gesture to the native call on the client that
   owns the effect, and the effects the player will perceive on each side. When the native chain cannot run remotely,
   line 3 says so **by name, with the reason**, and the substitute is a user decision.

A design written in the other order — from the code's available mechanisms outward — is the defect this page
forbids. The forbidden shapes, each of which shipped once:

- **"Give it to them first, then they use it"** — invents a player-facing rule to fit a constraint.
- **"A stand-in object will do"** — the game's own delegates read slots; a stand-in answers `-1` and every slot step
  misfires silently.
- **"Play the sound / set the state / draw the highlight"** — covering a missing native effect with a CUO copy of it.
- **"The message already exists, use it"** — a mechanism's availability is never a design reason.

## The parity probe (the evidence, not the opinion)

A ticket under this standard is not verified by reading state. It is verified by running **the same action twice in
one session** — once locally (single-player path) and once across players — and comparing the two effect lists:

| What is compared | How it is captured |
|---|---|
| The native call made | the same log line or trace the local path emits (function and arguments), both runs |
| Player-perceptible effects | a frame of the moment (visual) and the audio capture (sound), both runs |
| The resulting state | the item's condition/identity and the body's state on **each** client, before and after |

Any difference is a defect of the change, not a note in a limits section. When a difference cannot be removed, the
ticket names it, quotes the two lists, and the user decides — the substitute is not chosen by the implementer.

## What "done" means for such a ticket

- The three lines are in the ticket, and the native call site in line 2 is a quoted line from `reversing/`.
- The probe table above is filled with two runs, and the diff is quoted.
- The effects the player perceives come from the native chain: the sound the game plays, the animation the game runs,
  the state the game writes.
- The ticket's acceptance rows are judged from frames and captures, not from probe JSON (`AGENTS.md`, "look, do not
  read fields").
- Where a substitute was unavoidable, the ticket carries: the native chain that was unreachable, the quoted reason,
  the difference list, and the user decision that approved it.

## Related reading

- `AGENTS.md` — *Engineering Discipline* (gameplay first, entry mapping) and *Development Workflow*.
- `docs/evidence/delivery-checklist.md` — the boxes that enforce this standard per cycle.
- `docs/development/review-prompt.md` — the adversarial pass that asks these questions.
- `docs/en/internals/` — why the runtime/adapter split exists.
