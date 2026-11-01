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

## One commit per reviewed unit

Implementation plus its affected tests, frozen, reviewed, fixed once, then one commit. Review never gets batched into a
later commit.
