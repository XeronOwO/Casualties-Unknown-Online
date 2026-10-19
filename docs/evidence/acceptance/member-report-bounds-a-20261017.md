# Acceptance record — the member-report identity bound (section A of the unbounded-reports ticket)

- Ticket: `docs/backlog/done/member-report-bounds.md` — **section A's five surfaces (seven wire members)
  are read and PASS on the machine.** The ticket does NOT move: sections B (a number the host could
  derive) and C (a claim with no anchor at all) are still undeveloped code.
- Batch: `20261017-a`, run 1 — THREE clients in one world (host on the physical machine, members in the
  `Steam1` and `Steam2` sandboxes). Three clients are not decoration: the identity a forgery steals has
  to be one the host keeps a record for, and the host keeps no presence row for itself, so a two-client
  session cannot read the scene, limb or speech rows at all.
- Commit: `53542e1d` (the bound itself) — Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion
  `0.1.0+53542e1d9482b97e8ad44512a8feb19d3d54a5ea`, verified against this tree by
  `tools/verify-deploy.ps1` (exit 0; `a1-verify-deploy.txt`). The two recipes the run drives
  (`member-id`, `member-identity-forge`) are committed with this record and are read from the tree by the
  driver, so they are not part of the deployed artifact.
- Run: 2026-10-17, 01:17:22 → 01:18:42 · Host: physical machine · Members: `<member-a>` (the forger,
  `Steam1`) and `<member-b>` (the identity it steals, `Steam2`), both reading the physical install.
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (all three endpoints), `capture`,
  `input` (the in-process driver and the scenario recipes), `logs`, `artifacts` — the preflight reported
  9 present before the run.
- Artifacts (batch `20261017-a/`): `run1.ps1`/`run1.log` (the script and its own trace), `a0`–`a1`
  (preflight, environment, deployed-artifact check), `a2`–`a4c` (the three endpoints coming up),
  `a5`–`a11` (lobby, the three clients in world), `a12`/`a12b` (the two ids, read from the members
  themselves), `a13`–`a17` (the forged pickup's target id, from the HOST's own item record),
  `a13`/`a13-host-baseline.txt`, and one window per surface: `a21`–`a27` (the recipes' answers) with
  `a21`–`a27-host-<surface>-lines.txt` (the host's own lines for each forgery).

| # | Surface the forgery names | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | `WireCommandKind.ItemPickup` — one member hands an item the host already judged to ANOTHER member | machine | **pass** | `a21-host-pickup-lines.txt` — `[WRN] [KernelCommandGateway] Command admission refused (no answer, as before): PickUpItemCommand actor <member-a> sender <member-a> — NotAuthorized: item <id> is handed to <member-b> instead of the sender.` The id was read from the host's own record (`a17-host-items.json`, mode=host), so the refusal is the identity rule and not the creation-before-operation invariant above it; no answer went back (`no answer, as before`), and the same line reappears in the later windows (`a22`, `a23`, `a24`) — the reconciliation budget re-reporting a refused pickup kind, which is the shape the census row describes |
| 2 | `WireCommandKind.RecordEnemyBite` — a bite on another member's body | machine | **pass** | `a22-host-bite-lines.txt` — `Command admission refused (no answer, as before): RecordEnemyBiteCommand actor <member-a> sender <member-a> — NotAuthorized: the bite names victim <member-b> instead of the sender.` |
| 3 | `WireCommandKind.RecordEnemyLunge` | machine | **pass** | `a23-host-lunge-lines.txt` — `… RecordEnemyLungeCommand actor <member-a> sender <member-a> — NotAuthorized: the lunge names victim <member-b> instead of the sender.` |
| 4 | `WireCommandKind.RecordEnemyEffect` | machine | **pass** | `a24-host-effect-lines.txt` — `… RecordEnemyEffectCommand actor <member-a> sender <member-a> — NotAuthorized: the effect names victim <member-b> instead of the sender.` |
| 5 | `NetMsg.LimbStateEvent` — a latch on another member's limb | machine | **pass** | `a25-host-limb-lines.txt` — `[WRN] [LimbStateEventHandler] [LimbEvent] dropped spoofed latch from <member-a> claiming owner <member-b>.` Nothing durable was written and nothing was relayed: the member the forgery names has no line in that window and stayed in the world (`a30`: `inWorld=true active=true`) |
| 6 | `NetMsg.SceneState` — another member's scene state | machine | **pass** | `a26-host-scene-lines.txt` — `[WRN] [SceneStateHandler] Scene state dropped: transport=<member-a> claimed=<member-b>.` The handler's old fallback (`msg.SteamId != 0 ? msg.SteamId : sender`) is gone, so the frame neither entered `<member-b>` into the world nor earned it the entry fan-out |
| 7 | `NetMsg.SpeechMsg` — a bubble put in another member's mouth | machine | **pass** | `a27-host-speech-lines.txt` — `[WRN] [SpeechHandler] [Speech] dropped spoofed bubble from <member-a> claiming speaker <member-b>.` |
| 8 | An ordinary, unmodified world meets no refusal | machine | **pass** | `a13-host-baseline.txt` — the host window over a quiet stretch BEFORE any forgery carries **zero** refusal lines (`admission refused`, `dropped spoofed`, `Scene state dropped`), while all three clients were in the world and streaming (`a9`–`a11`, `inWorld=true` on each). Every refusal above is the forger's own, and each was read on its own mark so no window overlaps another |

Judged from this run's own readings against the deployed artifact named above. The one row this batch
could not read on its first four attempts is named in the Limits rather than counted — the pickup row
needed an id the HOST had judged, and the member's own view of the world carries none
(`item-world-read` answers `hasId=false` for every world item near it).

## Residuals for the user

None open. Every row is a host log line, a recipe's own answer or a client state, so nothing here waits
on an eye.

## Limits

- **What the bound does NOT cover, by construction**: the three handler shapes are `Bidirectional`, so a
  forged frame addressed straight at a GUEST is still applied on that client — a latch into its clone
  fact table, a scene row into its presence table, a bubble on the named clone. That is presentation on
  that client, overwritten by the owner's own next report or keyframe, with no durable record and no host
  write; the census rows name it (`NAMED as left open`) and it would need a guest-side identity rule the
  star topology has no anchor for. The pickup and enemy families are seam-side and have no such gap: the
  host is the only judge.
- **The speech length cap is still open** and is NOT this bound's business: it belongs with the abuse
  bound the chat handler's rate cap is also waiting for, and no reading of a native bubble's length
  exists to bound it with.
- **The forgery rides the product's own senders** (`IWorldControl.SendSpeech`, `EnemySyncService`,
  `ICharacterDataControl`, `ISessionControl.Broadcast`, the kernel command envelope), which is the shape a
  modified client takes and no CUO gesture emits. A client whose code was modified rather than driven
  through those senders is out of this run's reach.
- **Row 1's id is a world item the host had recorded**, not one the forger was carrying: the member's own
  carried items report `instanceId = 0` in its own view, so the seam's eligibility half for a
  self-carried item is unchanged by this bound and is exercised by the unit suite instead.
- The four attempts before this one failed on the probe, not on the product: the recipe named
  `IKernelProtocolControl` and `EnemyEffectKind` under the wrong namespaces, and then read the pickup id
  from a field name the host record does not use. The batch log keeps all five attempts in order, and the
  lesson is folded into `docs/acceptance/lessons.md`.
