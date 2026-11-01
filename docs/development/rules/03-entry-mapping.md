# Step 3 — map every entrance before claiming anything

Read this file and `03-entry-mapping.local.md` completely whenever a change crosses players or mirrors a native action.

## One line per entrance

For each way a player can reach the behaviour (item use, context menu, hotkey, console verb, HUD button, drop, drag,
hand slot), write: the input sequence, the native call site it mirrors, and whether the action carries the information
the behaviour needs.

## "Impossible" is a claim about every entrance

An impossibility claim enumerates all entrances and quotes the code for each, or it is not made. "There is no way to do
this" after checking one menu is the same defect as a wrong fix.

## A gesture that cannot carry the information refuses

Silent fallback, a default that looks like success, or inventing the missing fact from local state are all forbidden.
If the gesture cannot carry it, the behaviour refuses with a reason the player can read — or the design asks the user.

## Keep entries isolated

The item's own data decides which entry runs. One entry must never be a superset that silently handles another's case.
## Fallbacks ask a reachability question first

Before claiming a refusal or a fallback path is reachable, point at the production code path that reaches it. A branch
that only a test can reach is not a behaviour, and a fallback that exists "just in case" is a second entry nobody asked
for.