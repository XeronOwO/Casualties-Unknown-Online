# Step 1 — frame the task from the user's side

Read this file and `01-task-frame.local.md` completely before starting a work item.

## State it before touching anything

Three things, in the player's words:

1. **Reproduction** — what the player does, and what happens.
2. **Expectation** — what the player expects instead.
3. **Acceptance matrix** — every role, direction and view the row must be judged in (host, member, third client,
   own screen, peer view, after a reload).

A work item without those three is not ready to start; the missing piece is the first thing to find.

## Completion is the user's original request

Not the ticket's wording, not the tests, not a green gate. The delivery is done when the exact reproduction the user
described no longer happens, judged from the agent's own evidence.

## Question first, action second

A question from the user is answered before any change is made. "The design is wrong" is a question about the correct
design, not permission to replace it with one of your own.

## Keep unfinished work open

No completion claim, no "future work", no moving on. A cycle that ends incomplete is recorded as failed, with the point
it stopped at.
## Ask on design, execute on implementation

Ambiguity with user-visible trade-offs goes to the user; routine detail does not. A rejection is a question, not a
mandate — inferring a removal, rename or new mechanism from "this is wrong" is the violation. Which ticket comes next is
not such a decision: it comes from priority and dependencies.

## Incidental work

"While I am here" covers only closely related small work, and one meta cycle may interrupt at most every two or three
product cycles unless the user asks for it.
## Deferred means decided, not pending

A `future/` item is deferred by a decision; it is implemented only when the user promotes it.