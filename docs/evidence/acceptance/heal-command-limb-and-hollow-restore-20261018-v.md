# Acceptance record — heal restores severed limbs and clears hollow

- Ticket: `heal-command-limb-and-hollow-restore` — verdict: **moved to `done/`**. The one row it still owed
  (the save/reconnect survival of the native `heal`'s limb restore and hollow clear) passes on the fixed
  artifact, together with the peer view of the same outcome; the batch's shared setup is
  [`20261018-v-scope.md`](20261018-v-scope.md).
- Batch: `20261018-v` — tickets `item-identity-across-transforms`, `heal-command-limb-and-hollow-restore`.
- Commit: `ef05371c` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+ef05371cc1e0996aa8bab308f066a5e6faa9e773` (`tools/verify-deploy.ps1`, exit 0 before the clients
  started).
- Run: 2026-10-18 13:59 → 14:12 (+08:00), by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox · Bystander: `Steam2` sandbox.
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl` (three endpoints),
  `input`, `logs`, `artifacts`.
- Artifacts: the `r*`/`j*` readings and `x-save-*` extracts in the batch directory named by
  `acceptance-artifacts-dir`; ids are written `<host>`/`<member>`/`<bystander>` here.

The row table was written before the run. The ticket's earlier cycles read the hollow clear (one client, then
the overlay), the display clone's removal direction, and the native `heal`'s limb + hollow outcome on the
member's own client (batch `20261018-u`'s H5). What it still owed was the save/reconnect survival of that
outcome, and this batch ran it.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| R1 | The native `heal`'s limb restore and hollow clear survive a save/reconnect, and the outcome reaches the other views | machine | **pass** | the member's `DownArmF` severed limbs 4 and 5 (the game's own `amputate`, `r01`; `r05`: `dismembered=true skin=0 muscle=0`) with the mindwipe planted and read (`r02`/`r03`: `mindwipe=true active=true`) and its overlay present (`r04`: `count=1 MindwipeVignette(Clone)`); the **host's display view** of the member read the same before the heal — limb 4 `dismembered=true skin=0` (`r07`), display mindwipe `true active=true` (`r08`). The member then ran the game's own `heal` delegate (`r10`, `game-console command=heal`, the registered command's wrapped action). After it: the member's limb 4 and 5 read `dismembered=false skin=100 muscle=100` (`r12`), `mindwipe=false` (`r13`) and the overlay `count=0` (`r14`), `brainHealth=100 mindwipePresent=false` (`r11`/`r19`); the host's display view reads limb 4 restored (`r15`) and its mindwipe gone (`r16`); the host's saved copy reads the restored limb and `mindwipePresent=false` (`r17`/`r18`). `/save` committed the cut (`x-save-seam.txt`) and the archive's own character file carries limb 4 `dismembered=false skin=100 muscle=100`, `mindwipeScriptPresent=false`, `alive/conscious=true brainHealth=100 shock=0` (`x-save-character.json`). The member quit (`j11`), relaunched (`j12`), rejoined (`j13`) and re-entered (`j14`/`j15`); after the restore its own client reads `mindwipePresent=false` (`j20`), limbs 4 and 5 `dismembered=false skin=100 muscle=100` (`j21`), `mindwipe=false` and overlay `count=0` (`j23b`/`j23c`), and the host's saved copy reads the restored limb and the cleared hollow (`j26`/`j27`) |

## What the run established beside the row

- **The native `heal`'s outcome is carried by the character state, not by the command.** The heal runs on the
  member's client and writes the limb/body fields; the same `LimbStateEvent`/snapshot path the earlier batches
  read carries them, and the archive's own character file is the fact the reconnect restores from.
- **The peer view follows the same removal direction the ticket's prerequisite fixed.** Before the heal, the
  host's display body carried the member's `SplintLimb`-family state and the mindwipe component; after it,
  both were gone — the same "a component the owner no longer has disappears on the receiver" behaviour the
  earlier cycle proved on the display clone.
- **The two commands stay distinguishable.** The native `heal` restored the limbs and cleared the hollow but
  did not clear shock (the member's own read was `conscious=false` immediately after the heal with the body's
  condition being resolved by the field writes; the later reads settle at `conscious=true shock=0`). The
  `/heal` cheat reset's own distinguishing fields are its ticket's rows, not this one's.

## Residuals for the user

None. Every judged fact is a console answer, a client read, an archive file or a log excerpt.

## Limits

- **The bystander's own view is not read**: no surface reaches the member's body on the third client, so the
  peer view claimed here is the host's native display body. The third client's own kernel row is not a
  limb/moodle view.
- **The limb restore is read through the limb fields and the component set** (`dismembered`, health, the
  `SplintLimb` component), not a frame or a physics reading.
- **The save/reconnect is a rejoin of the running session**; the archive was read directly from the committed
  cut, not reloaded by a Continue.
- **The body's consciousness at the heal moment is not one of this row's claims**: the heal's field list does
  not write the unconsciousness state, and the run's later reads settle the point without judging it.
