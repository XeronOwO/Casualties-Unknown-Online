# Step 9 — adversarial self-check

Read this file and `09-self-review.local.md` completely before the commit.

## Attack your own change

Against the frozen tree, ask each question in `docs/development/review-prompt.md`: what did I assume without reading,
which entrance did I not walk, which role or direction is untested, what happens on a refusal, on a second call, on a
stale message, on a client that joined late.

## Findings are fixed in the same commit

A blocker or major found here is fixed now. Findings that cannot be fixed are written into the ticket as the next
input, not left in the conversation.

## No paper review

A review that only reads the diff and never runs the thing is not a review. Key paths need runtime evidence.

## Red before review is honest

The tree under review is frozen: no edits during the gates, and no "I will fix it after the run".
