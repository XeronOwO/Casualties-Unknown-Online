# The game's console and save file (1.0)

Cited from the 1.0 decompile (`reversing/release-1.0/Assembly-CSharp/`).

## The console

`ConsoleScript` is the game's own console: a text input, a command table and an output log. A command is a
`ConsoleScript.Command` with a name, a help line and a delegate over the argument array (`ConsoleScript.cs:519-563`);
the input is split on spaces, with a variant that keeps empty entries (`:302-303`), and a user-defined command is parsed
the same way (`:115`).

- The table ships with `help`, `heal`, `coagulate`, `kill`, `spawn`, `spawncategory`, `tp`, `skiplayer`, `skippext`,
  `log`, `talk`, `framerate`, `alert`, `saveandquit`, `resetskills`, `timescale`, `clear`, `addxp`, `setbodyfield`,
  `setlimbfield`, `amputate`, `unchipped`, `addcustomcommand`, `addliquid`, `locate`, `removecustomcommand`, `music`,
  `bind`, `repeat`, `explode`, `floodfill`, `echo`, `ui`, `freecam`, `starterkit`, `noclip`, `playsound`, `fullbright`,
  `plushies`, `errorlogging` and more (the live table is the authority; the acceptance harness reads it).
- Every cheat-shaped command calls `CheckForCheats()` first (`:531`, `:546`, `:558`, `:565`) — the game's own
  anti-cheat switch. In a vanilla session it is off and the commands run locally; in a CUO session the host's session
  rules decide who may use them (the mod's cheat roster).
- A console command acts on the local client only; it never sends anything to another player. That is why a member
  running `heal` changes its own body and the host sees the change only as a report.

## The native save

`SaveSystem` writes one file, `save.sv`, under `Application.persistentDataPath` (`SaveSystem.cs:222`), zipped
(`:521-539`). `HasSave()` is just the file's existence (`:226-228`); `TryLoadGame` reads and parses it (`:232-253`) and
a corrupt file is reported, not repaired (`:253`). The file is a single world's snapshot for the game's own
single-player flow; deleting it is a plain `File.Delete` (`:511`).

The file stores the character, the world and the run's own values that the game's save path reads (the character's
body/limbs, the building and item state, the run settings). It is not a versioned archive and the game offers no
incremental save; a CUO archive is the mod's own layer beside it.

## Run settings persistence

A run's settings live in the world (`WorldGeneration.runSettings`, seeded from a preset) and are written into the
native save as part of the world; a loaded run restores them with the rest of the world (see `ui-and-settings.md` for
the getters and `worldgen-and-layers.md` for the multipliers).

## What the mod must respect

- The native save is the game's own single-player file; CUO never writes it and never deletes it. A CUO session reads
  the world through its own archive.
- A console command's local effect is judged by the client that ran it; a report that the game's own rates cannot
  produce is refused by the host (the limb plausibility bound).
- The cheat roster is the host's policy: the host announces who may use cheat-shaped commands, and a member without
  the grant is refused locally with a message that names the host's roster.

## Not covered here

- The compressing format's schema (the JSON inside `save.sv`) and every field the game reads: not mapped field by field.
- The console's autocompletion and history behaviour: mapped in the mod's own ticket, not here.
