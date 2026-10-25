# Permissions and commands: who may do what

The console's command family, the host rules behind it, and who is allowed to use each.

## What the player does

Types a command into the console. Some commands are available to every player, some only to the host, some only to a
member the host has granted cheat verbs. A player expects a refusal to say which rule refused it.

## The command family

| Family | Purpose |
|---|---|
| `/heal`, `/reset` | restore a player's body (health, condition) |
| `/give` | create and deliver an item to a player, with its own state |
| `/data get|set` | read or write an item's own state in the item-data language |
| `/summon` | create an entity |
| `/gamerule` | read or change a host rule |
| `/spectate` | follow a member's body after death |

A command's shape follows Minecraft's current template (root rule 17): its arguments, their order and which are optional.
A verb may differ only where a recorded reason says why, and the reason is recorded where the shape is decided.

Selectors name players: `@a` every player including the executor, `@s` the executor, `@p` the nearest player, `@r` one at
random, `@e` the player-entity alias; each takes bracket filters (`type`, `name`, `distance`, `limit`, `sort`). A
selector that matches several players where one is required refuses and names the count.

## Host rules

- A host rule is decided by the host and reaches every member that reads it; the member's own check is an interception
  of the gesture it would send, not a second verdict.
- Member cheat verbs are a rule: off by default, granted per member, and announced to that member.
- Remote backpack access is a rule: it can forbid all remote inventory manipulation, in which case both the view and the
  gestures are refused by name.
- Reading a rule is open to every player; changing one is the host's.

## Refusals

| Condition | The player reads |
|---|---|
| the command exists but this player may not run it | a line naming the missing permission |
| a selector matched several players where one is required | a line naming the count and asking for one |
| a selector matched nobody | a line naming the selector |
| the host rule forbids remote inventory manipulation | a line naming the rule |
| the command's argument does not parse | the argument's own diagnostic |

Every line is rendered by the client that reads it, in that client's language.

## Who judges

The host validates a command that acts on the session and answers it; the console itself decides only whether the line
can be assembled. A permission refusal is decided by the host, never by the asking client alone.

## What travels

The command, its arguments, the answer (a key plus its values) and, for a member, the permission its roster carries.

## What the player sees and hears

The answer appears in the console history as one line; a refusal names the rule or the argument that stopped it.

## Records

Decisions 228 (the console is the only command surface), 263 (host rules), 264 (`/data`'s data language), 269
(`/gamerule`'s shape), and root rule 17 (Minecraft's current template). Accepted:
`../../backlog/done/in-game-command-console-interactive.md`, `done/remote-backpack-access-policy.md`. Open:
`../../backlog/todo/selectors-exclude-the-executor.md`.

## Gaps

- The complete per-verb permission table (which verb needs which grant) is not recorded here yet; the roster and the
  command implementations hold it.
