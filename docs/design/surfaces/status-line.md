# Status line: noticing and refusals on screen

The one line a player reads after an action, and the sound that goes with a refusal.

## What the player does

Performs an action and reads the line that appears; hears the game's deny sound when the action was refused.

## What it is

A transient line drawn by CUO over the game's screen, shown for a short time, replaced by the next notice, and never
faded while a full surface (the console) is open. It carries two kinds of message: a notices line (something happened) and
a refusal line (something did not).

## Rules and states

- **The deny sound is the game's own, and only on a refusal.** The code states the pairing: the operator's refused cross-player use gets "the game's own deny sound beside it — the cue the user asked for (2026-10-24: the label plus the deny sound, and NO extra success sound, because the drag already has the game's own cloth rustle)", wired at `GameAdapterSessionBinding` (`UseRefused` → `OnPlayerItemUseRefused`).

- Every refusal reaches the player: a gesture never silently does nothing. The accepted reading of the family is explicit — the actor's own client printed the refusal "for a denied open and nothing at all for an accepted one, with no host-side traffic", which is what "both answering sites" means: the host answers the remote case, the local surface answers the local one, and an accepted action gets no line.
- A refusal's sentence is one key plus its values, rendered by the reading client in its own language; the frame and the
  reason are both localized, and the reason is never another language's prose.
- A refusal is accompanied by the game's own deny sound; a successful action's line is never given the deny sound.
- The line belongs to the player who performed the action: a refusal decided by the host travels back to that player, and
  a refusal decided locally is shown without any traffic.
- A notice that reports a state (a session ending, a peer leaving) uses the same surface and the same rules.

## Refusals it can carry

Every `use.refused.*` key of the cross-player use family, the console's own refusals, and the backpack gesture's refusal.

## Who judges

The side that refused decides the sentence; the client that reads it renders it. The line asserts nothing about the
world beyond the refusal itself.

## What travels

A key plus its values, from the deciding side to the reading side.

## What the player sees and hears

One line in the player's language, for a few seconds, plus the deny sound on a refusal.

## Records

Decisions 228 and 265. Accepted: `../../backlog/done/console-command-replies-bilingual.md`. Open:
`../../backlog/todo/player-facing-text-localized-per-receiver.md`.

## Gaps

- How many notices may be queued at once, and what replaces what, is not recorded here yet.
