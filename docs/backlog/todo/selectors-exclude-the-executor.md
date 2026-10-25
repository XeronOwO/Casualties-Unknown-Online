# `/heal @a` skips the player who runs it

- Status: Todo — opened 2026-10-25 from the user's report: "主机执行 heal @a 为什么没有 heal 自己啊!".
- Priority: Medium
- Category: Console / cheat verbs / player selectors
- Source: user report (2026-10-25). The measured reply on the host: `/heal @a` answered `Reset 76561198863287957: … Reset
  76561199526807662: …` — the two MEMBERS only — and the host's own body was untouched.

## Goal

`@a` selects every player in the session, the one who typed the command included, on every verb that takes a player selector
(`/heal`, `/give`, `/summon`, `/gamerule`). `@s` stays the executor alone; `@p` the nearest.

## Current behaviour

The host's `/heal @a` reset the two members and not itself. The selector resolution therefore enumerates the remote members without
the local player, which also means a solo host can never be reset by `@a` at all.

## Why this is the wrong shape

The command family follows Minecraft's template (root `AGENTS.md` rule 17): there `@a` is **all players, including the executor**,
`@s` is the executor, `@p` the nearest. A selector set that silently drops the sender makes the most convenient form of every cheat
verb unusable on the one client an operator debugs most — the host.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | On a three-client session, `/heal @a` typed by the host resets all three bodies, read from each client's own body state |
| 2 | `/heal @s` on a member resets only that member; `/heal @p` resets the nearest player to the executor |
| 3 | The reply names every player it reset, including the executor, and the line reads in the operator's language (the localization ticket) |
| 4 | A solo host with no session can use `@a` on a cheat verb (the degenerate case the current shape makes impossible) |

## Limits

- The reply text is a player-facing string, so this ticket inherits the localization requirement; the rows above only require that
  the same players are named in both places.
