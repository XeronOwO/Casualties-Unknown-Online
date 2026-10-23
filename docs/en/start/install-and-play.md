# Install and play

[Documentation](../README.md) > [Start](README.md) > Install and play

---

**After this page** CUO is installed, you have hosted or joined a session, and you know what the
three most common failures mean. A mod author needs the same steps to run a build of their own.

## What gets installed, and where

CUO is a BepInEx plugin. It does not modify the game installation:

| What | Where |
|---|---|
| the mod itself | `BepInEx/plugins/CasualtiesUnknownOnline/` |
| its settings | `BepInEx/config/` |
| its worlds | CUO's own save folder |
| the game's own save file | never written by CUO |

CUO comes in two forms and they are installed the same way:

- **The plugin folder** — `CasualtiesUnknownOnline.dll` beside the libraries it needs.
- **The [single-file package](../reference/glossary.md)** — one `CasualtiesUnknownOnline.dll` that carries
  those libraries inside it, plus the native `steam_api64.dll`, which Steam's own wrapper loads from disk.

Extension mods are never part of either form: a mod such as Pinyin Search is its own DLL, it goes beside the
plugin, and CUO hands it the core's libraries when it loads.

## Install

1. Install **BepInEx 5** into the game folder if it is not there yet.
2. Copy the CUO plugin folder into `BepInEx/plugins/CasualtiesUnknownOnline/`; for the single-file package,
   copy that one `CasualtiesUnknownOnline.dll` and `steam_api64.dll` into the same folder instead.
3. Start the game once so BepInEx writes the configuration file, then close it.

## Host a session

The host starts the game and plays as usual — the world already open becomes the shared world. Open
the CUO online panel, create a Steam lobby and start the session. Friends see and join you through
that lobby, so keep it open while you play.

## Join a session

A guest starts the game, opens the same panel and joins the host's lobby from the friend list.

Without Steam, the configuration file carries a direct-IP mode under the `IpDirect` section: set
`JoinAddress` to the host's address and `JoinPort` to the port the host listens on.

## When it does not work

- **A join is refused, or a guest drops back to the menu right after connecting.** The two sides
  disagree about the [protocol version](../reference/glossary.md), which happens when the builds
  differ. Everyone should run the same build.
- **The lobby shows nothing.** Check that Steam is running and that the game was started through
  Steam.
- **Something misbehaves after a game update.** The [adapter](../reference/glossary.md) could not
  find what it depends on and disables that part instead of crashing; the logs name what it skipped.
- **Where the logs are:** `BepInEx/LogOutput.log` for load-time problems, and
  `BepInEx/logs/latest.log` for what the session did ([Logs](../reference/logs.md)).

## Related reading

- [What CUO is](what-is-cuo.md) — what a session is and who decides what
- [Set up a development checkout](set-up-dev-environment.md) — build CUO yourself
- [Glossary](../reference/glossary.md) — host, guest, lobby, protocol version

---

[Documentation](../README.md) > [Start](README.md) > Install and play
