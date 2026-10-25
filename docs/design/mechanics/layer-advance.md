# Layer advance: ending a layer and entering the next one

Reaching the end of a layer, and the choice to go down.

## What the player does

Reaches the bottom of a layer, where the game offers the way down, and chooses it. The player expects the whole party to
move together: whoever chooses the descent does not leave the others behind, and no fact from the layer being left
survives into the layer being entered.

## What the game itself does

The descent is the game's own layer transition: the world generates the next layer and the bodies are placed in it. In
co-op the choice must be one member's choice applied to a host-owned world.

## Rules and states

- The choice is the MEMBER's; the layer is the HOST's. A member who chooses the descent reports the intent; the host
  decides when the party moves, so two members choosing at once cannot generate two worlds.
- A fact about the layer being LEFT must not survive into the layer being ENTERED: the layer boundary is one family under
  one rule, and every such fact is classified rather than silently kept or silently dropped.
- Items the player carries cross the boundary with them; world facts belong to the layer they were created in.
- A member who is not in the world when the host moves is placed when it arrives, not left behind.- **A layer change never drops a member.** The accepted reading of the drop-out defect is that with both members' inbound parked inside one command "both members stay in the world with the item they carried across the change", and every client's growth "read as a size stays at 0.35–… MB/min against the ~8.2 MB/min storm"; the ticket's own control shows the unparked path is what dropped them.

## Refusals

| Condition | The player reads |
|---|---|
| the descent is not offered yet | the game's own condition, no CUO line |
| the layer moved while the request was in flight | the request is abandoned with the layer it belonged to |

## Who judges

The host owns the layer transition; a member's request is an intent, never a prediction of the new world. The member's
own client shows its body moving only when the host's new layer state arrives.

## What travels

The intent to descend, the host's decision, and the new layer's state: the generated world, the bodies' placement, and
the items that cross with their owners.

## What the player sees and hears

The descent plays the game's own transition; the party arrives in the same layer at the same moment, and every client
shows the same new world.

## Records

`../../architecture/surfaces.md` (world overlays row is unrelated; the layer boundary is a kernel rule), decisions 174
(the boundary is one family under one rule) and 242 (the choice is the member's, the layer is the host's). Accepted:
`../../backlog/done/entity-destruction-drop-guest-fresh-state-loss.md` (the boundary family's record).

## Gaps

- The exact set of facts classified at the boundary (which are captured, which are resolved before the move, which are
  dropped) is stated by the transient policy; it belongs on this page once read from there.
