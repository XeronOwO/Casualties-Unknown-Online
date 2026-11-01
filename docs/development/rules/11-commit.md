# Step 11 — commit and push

Read this file and `11-commit.local.md` completely before committing.

## Message

Conventional Commits: `type(scope): summary`, imperative, lowercase, no trailing period; the body carries the evidence.
Docs and backlog changes use `docs(scope): …`.

## Content

Committed content is English — every language, not only the one that slipped last time. Only the Chinese doc block and
`zh.json` carry another language, plus a fixture that must. No machine path, host name, port, account or credential ever
enters the repository or its history; those live in the local rule files.

## Order

Commit before deploying. Never deploy an uncommitted tree or one whose gates are red. A commit is signed; history is
never rewritten to hide a mistake — a correction is its own commit with its reason.

## Stage by path, never the whole tree

`git add -A` is how a commit becomes a lie: it sweeps an unfinished change into a commit whose message describes something
else. Stage the files the reviewed unit actually touched, by path, and read `git status` before the commit. Measured
2026-11-02: a rules commit took three unfinished product files with it because the staging command was `add -A`, and its
message said nothing about them. History is not rewritten to hide that; the correction is its own commit and says so.

## One commit per reviewed unit

Implementation plus its affected tests, frozen, reviewed, fixed once, then one commit. Review never gets batched into a
later commit.
## Before the commit

Run `tools/check-delivery.ps1` (the delivery checklist): it validates the cycle's boxes, archives the cycle's record and resets the boxes for
the next cycle. Gates green in the same commit as the change; the language check reads the staged diff.

## Release discipline

This project has not released, and the protocol number is never a design input: a breaking change is free while the
handshake is the only compatibility boundary. After the first release every behaviour-affecting wire change bumps the
number in the same change, and a version mismatch refuses the connection instead of adapting to an old client.
## No compatibility debt

Before the first release nothing needs a compatibility layer, a deprecation window or a path for old clients: delete the
old shape and say what it was for. A branch that exists only to stay compatible is debt, and debt is removed, not
carried.