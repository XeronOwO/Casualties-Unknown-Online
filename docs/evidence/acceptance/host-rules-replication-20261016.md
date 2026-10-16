# Acceptance record — Host rules reach the members, and both sides check them

- Ticket: `host-rules-replication` — verdict: **every row of the ticket's acceptance table passes on this
  run's own readings**, and the ticket moves to `done/`.
- Batch: `20261016-a` — tickets: this one. The batch is five client sessions inside one artifact identity
  (the directory's own `run*.ps1`, `run.log` and `run2..5.log`); run 1 is VOID as evidence and its files are
  named in the Limits, runs 2–5 are the readings below.
- Commit: `ae17cc68` (HEAD at the run; its own delta over the feature commit is the acceptance recipe it
  added) · Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion
  `0.1.0+105bc6dbd81770fd6a45ee81fa18149315975be3` — the build of `105bc6db`, the ticket's own feature
  commit, verified against this tree by `tools/verify-deploy.ps1` (exit 0; `a1-`, `b1-`, `d1-`, `e1-verify-deploy.txt`).
- Run: 2026-10-16, 18:05 → 18:14 · Host: physical machine · Guest: `Steam1` sandbox, reading the physical
  install through and shadowing its own log.
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (host and guest endpoints), `capture`,
  `input` (the in-process driver and its recipes), `logs`, `artifacts` — the preflight reported 11 present,
  `RESULT: OK - a full two-client run is possible`.
- Artifacts (batch `20261016-a/`): the readings named per row below, the two clients' logs
  (`b15`/`b16`, `c41`/`c42`, `d45`/`d46`), and `run*.log`. The frames this run hoped to add for row 7 were
  not captured — see the Limits.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A host flips a rule the member's screen reads, while both are in a world: the member's next look reflects it within one announcement, and no local gesture is refused on the old value | machine | **pass** | In a world, run 3: `c7-host-flip-open.json` then `c8-guest-rules-open.json` — the member's own reading flips to `announced=true, allowMemberItemDataWrite=true`, and `c41`/`c42` carry the two `[Rules]` lines. Run 4 does the same on an item the member really holds: `d19` (the host opens) → `d20` (`true`) → `d21-guest-write-open.json` answers `#5197989525 bandage: condition 1 -> 0.5 — written here and reported to the host`, i.e. the gesture is NOT refused on the value the member held a moment before, where `d15`–`d17` (same verb, same item, the set saying `false`) answered `Refused locally: …` — `d45`/`d46` carry the change edge at the host and its arrival at the member |
| 2 | A member's client holds a rule the host has just closed: the forbidden gesture is refused LOCALLY, in the rule's own words, and nothing is applied and undone | machine | **pass** | `d15-guest-rules-closed.json` (the member holds `allowMemberItemDataWrite=false`, `announced=true`) → `d17-guest-write-refused.json`: `Refused locally: the host's rule AllowMemberItemDataWrite is off, so a member's item-data write does not land (the host announces its rules to this client). Nothing was written here.` — `d46-guest-log.txt` carries the same branch as `[Command] /data set refused locally: … nothing was applied here`, and NOTHING WAS APPLIED is proven by the next accepted write's own before-value (`d21`: `condition 1 -> 0.5`, so the refused `0.5` never reached the local object). The lobby twin of the same branch is run 2's `b11`–`b14` with `b16` |
| 3 | The same gesture, forced past the local check (a modified client): the host refuses it exactly as it does today, so the local check is not the boundary | machine | **pass** | `d27` (the member holds the closed set) → `d28-guest-probe-forget.json`: the announced set is dropped through the store's own session-reset path (`hadSet=true, hasSet=false, forbidsMemberItemDataWrite=false`), read back by `d29-guest-rules-no-set.json` (`announced=false`) → `d30-guest-write-forced.json` answers `… condition 0.5 -> 0.9 — written here and reported to the host …`, so the local check did not fire → the host's own refusal: `d45-host-log.txt` `Command admission refused (answered): UpdateItemStateCommand actor <member steam id> sender <member steam id> — HostRuleDenied: the host's rule does not open the item-data write verbs to members.`, answered to the operator as `d31-guest-console-host-refusal.json` (`The host refused the write to #5197989525 bandage (this host's rule does not open the item-data write verbs to members) — the recorded value is back on this client.`) with `d46`'s settle line, and `d33-guest-data-get-after-refusal.json` reads the record back at `condition 0.5` |
| 4 | A member joins a running world after the host changed a rule: the member enters with the current set, not the values from the host's config file at its own launch | machine | **pass** | Run 2: `b5-host-flip-open.json` (the host changes the rule BEFORE the member joins) → `b8-guest-rules-admitted.json`, the member's FIRST reading: `announced=true, allowMemberItemDataWrite=true` — while the member's own config entry of the same name is `false`, which is the discriminating half (the two nodes hold opposite values). The world half: run 3's `c1`–`c4` read the same set from inside the world and in run 4 the member's log records the world-entry group re-sending it (`d46`: `[Rules] the announcement from … repeats the set this client already holds`) |
| 5 | An ordinary session, unmodified clients: nothing changes for the host, and nothing is refused that was not refused before | machine | **pass** | `b6`/`b10-host-rules-after-join.json`: the host's own reading is its own configuration (`announced=false`, and the value it wrote itself), unchanged by announcing to the member; `d13-guest-data-get.json` shows the member's READ verb still answering while the write verb is closed (`#5197989525 bandage [carried by … slot 0] revision 1`); `d44-host-rules-restored.json` reads `write=false, reviveFromTrader=true, permadeath=false` — the batch put the host's entries back where it found them |
| 6 | A process that was a MEMBER later hosts its own lobby: it judges and announces its OWN rules, never the ones it was told; a member that left one host stops refusing on that host's values | machine | **pass** | `c30-guest-rules-open-again.json` (the member holds the host's `true`) → `c31`–`c34` (it leaves the world and the lobby) → `c35-guest-rules-after-leave.json`: `announced=false`, with `c42`'s `[Rules] the session ended — the announced host rules are dropped; this client reads its own entries, and skips the local check, until the next host announces.` → `c37-guest-rules-own-host.json`: after `create-lobby`, `role=Host, announced=false, allowMemberItemDataWrite=false` — its OWN entry, while the host it left was still announcing `true` |
| 7 | A member whose rules forbid a trader recruit: the card offers no recruit button, and the host's own gate is unchanged behind it | machine | **pass** | `d32-host-body-down.json` puts the host's body down (`alive=false, brainHealth=0`), so the member's card has a dead peer; `d36-guest-state-players-allowed.json` — the member's own offered controls carry the recruit control for the host's row beside `member.carry`, `member.open_backpack`, `member.open_medical`, `member.push`; `d38` (the host forbids it) → `d39-guest-rules-revive-off.json` (`reviveFromTrader=false`, announced) → `d40-guest-state-players-forbidden.json`: the SAME control list with the recruit control gone and every other control still offered. The affordance is read where the card registers it (`OnlineUiMemberListDrawer` adds `member.recruit.<steamid>` on `row.CanRecruit`), so the reading IS the rendered card's own action set |

Judged from this run's own readings against the deployed artifact named above; no row is a pass by an
earlier batch's evidence.

## Residuals for the user

None open. Every row of the ticket's table is a state, a message or a control set read from a client, so
nothing here waits on an eye.

## Limits

- **Run 1 is void, and its files are left in place as the record of why.** `run.ps1` typed `/hostrules`
  before the lobby existed (the verb is host-only, `a4-host-flip-open.json`: `Permission denied: this
  command is host-only`) and read everything inside the six seconds after the member's lobby join, before
  the CUO handshake had completed — `a8`/`a11` read `announced=false, sessionActive=false` and the member's
  console answered `No active session — a write reports into the session's record …` (`a12`) instead of the
  local refusal. The paced re-run (`run2.ps1`) is what produced rows 2/4/5's readings. This is a script
  defect, not a product finding: the member's session is not active at the lobby join, and the local check
  needs one (its own first branch is the session).
- **The member carries nothing when it enters a fresh world.** `item-census mode=body` answered `count=0`
  in run 3, so rows 2/3's writes fell through to `No item #0 is recorded in this session` (`c5`, `c6`,
  `c9`): run 4 declares the substitution — the game's own grant through `item-provide mode=create
  type=bandage`, which the recipe's own header names as the grant the run's `startingsupplies` setting did
  not hand out, and which is also how a carried item acquires its instance id at all (`d11`, `d12`).
- **`item-census mode=body` does not name a carried item's own instance id.** Its `instanceId` column is
  read from the `RemoteInventoryItemId` marker, which only a remote display proxy carries, so a client's own
  item reads `0` there while its CUO id lives on `ItemInstanceId`. Rows 2/3's "nothing was applied" and
  "the recorded value is back" halves are therefore read from the write's own before-value and from the
  log's settle line rather than from a census; the gap is filed as its own ticket,
  `todo/item-census-own-instance-id.md`.
- **Row 7 has no frame.** The run meant to add one (run 5) had its sandbox client come up with no main
  window while the box's own process list went empty, so `capture-window.ps1` had nothing to capture for
  the guest; the run 4 reading above is the client's own control set, which is the same registration the
  card renders, and the row is classified `machine` for that reason. The frames stay a nice-to-have the
  next sandbox session can add.
- **The change edge lands one frame later than the write** (the ticket's own recorded limit): the run reads
  it at its 2-second poll granularity, so "within one announcement" here means "at the first reading the run
  took after the write", not a frame-exact measurement.
- **The "modified client" of row 3 is the real client with its announcement dropped**, not a rebuilt binary:
  the probe calls the store's own public session-reset path, which is exactly the state a client whose
  announcement was lost, never sent or forged away is in — the local check is skipped and the host's answer
  settles the record.
- **One session per row family**: rows 1–5 are read in a two-client world, row 6 in the same session after
  the member leaves it, row 7 on the same world with the host's body down. The ticket's rows about a
  process that was a member and then hosts are covered in the single-session shape its own table names
  ("the run covers the ordinary single-session shape"); the two-node cases behind them are the ticket's unit
  cases, which this run does not re-run.
