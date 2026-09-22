# Getting started

English | [中文](getting-started.zh.md)

Before you can play together, three things have to agree: the game build, the mod build, and the
way you connect. This page walks through installing the mod, hosting, joining, and the failures
you are most likely to meet.

CUO is a BepInEx plugin. It does not modify the game installation: its files live in the plugin
folder, its settings file under `BepInEx/config/`, and its worlds in CUO's own save folder, and it
never writes the game's own save file.

## Installing

1. Install BepInEx 5 into the game folder if it is not there yet.
2. Copy the CUO plugin folder into `BepInEx/plugins/CasualtiesUnknownOnline/`.
3. Start the game once so BepInEx creates the configuration file, then close it.

## Hosting

The host starts the game and plays as usual; the world that is already open becomes the shared
world. Open the CUO online panel, create a Steam lobby and start the session. Friends see and join
you through that lobby, so keep it while you play.

## Joining

A guest starts the game, opens the same panel and joins the host's lobby from the friend list. The
configuration file also carries a direct IP mode under the `IpDirect` section: set `JoinAddress` to
the host's address and `JoinPort` to the port the host listens on.

## When it does not work

- The host refuses a join, or a guest drops back to the menu right after connecting: the two sides
  disagree about the protocol version, which happens when the builds differ. Everyone should run
  the same build.
- The lobby shows nothing: check that Steam is running and that the game was started through Steam.
- A feature misbehaves after a game update: the adapter could not find something it depends on and
  disables that part instead of crashing. The logs name what was skipped.
- Logs live in `BepInEx/LogOutput.log`, `BepInEx/logs/latest.log` and `CUO.log`.
