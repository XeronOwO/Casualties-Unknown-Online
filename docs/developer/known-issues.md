# Known issues

English | [中文](known-issues.zh.md)

This page collects the technical limitations that are declared rather than hidden: what is known not
to work yet, what is known to be fragile, and where each one is tracked.

A limitation is listed here only once it has a home in the backlog or a recorded decision, so that
"known" means somebody owns it rather than somebody remembers it.

## Declared gaps

- Client-side prediction and rollback of the local player are not implemented; a visible correction takes their place.
- Generic physics synchronisation is out of scope, so physical interactions outside the synced set can diverge.
- Anti-cheat is deliberately absent: the host trusts reports until the feature set is complete.
- Host migration is not supported; the session ends with the host.
- Three kernel replication types still live in the Runtime, because their dependency closure leaves the Application layer.

## Fragile areas

- The Game Adapter depends on private game types, so a game update can break hooks until the adapter is updated.
- Presentation timing (particles, some sounds) is local, so two machines can look slightly different.
- Cross-player interactions with tight timing are the hardest paths, and the likeliest place to find a new defect.

## How to report

Bring the reproduction, the role you played, and the logs from both machines if you have them.
`docs/backlog/README.md` is where an issue lands, and the delivery checklist is what a fix has to
satisfy before it is called done.
