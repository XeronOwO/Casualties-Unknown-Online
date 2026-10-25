# Observability: what a run leaves behind

What every path must leave in the logs, what a player reads on screen, and what a run can be proved from afterwards.

## What it is for

A defect in co-op is invisible until someone can see the two clients' own accounts of the same moment. Observability is
therefore a property of the design: a key path that leaves no trace is unfinished, and one pass of logging must be
enough to reconstruct what happened.

## Rules

- Every key path and logical branch is observable: the branch, the state, the identities involved, the input and the
  result. Frequency decides the level — a per-frame path is verbose, a rare or exceptional one is a warning or an error.
- A refusal is never silent: the client that refused logs why and the player reads a line.
- Logs are a diagnostic channel: always one language, never a player-facing surface, and never the place a player's
  answer lives.
- Volume is bounded: a repeated warning is rate-limited, and a channel that would flood a frame is summarised.
- A player-facing status line and the log line for the same event answer different readers, and both exist.

## Who judges

The client that took the action logs it and shows it to its player; the host logs its own decisions. A log line is never a verdict about another client.

## What travels

A status line's key and values, and the log's own structured values; nothing a player reads is sent as a sentence.

## What a run leaves

| Trace | What it answers |
|---|---|
| the host's log | what the host decided, in order, with identities |
| each member's log | what that client saw, sent and applied |
| status lines on screen | what the player was told at the moment it happened |
| the acceptance record | which rows a run judged, with the frames and captures it read |

## What is never recorded

Machine paths, credentials, another player's personal identifiers beyond what the session itself needs, and any value
that would let a log leak a player's environment.

## Records

`../../en/reference/logs.md` (the human reference for the log channels), the log-volume and documentation gates in
`../../evidence/normative-gates.md`.

## Gaps

- The per-subsystem list of stable log prefixes, and which of them a run's acceptance may rely on, is not recorded here
  yet; it belongs on this page once collected.
