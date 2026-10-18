# Acceptance record — a trader give's credit is judged against the host's own item row

- Ticket: `member-report-bounds` — row `NetMsg.TraderAction` (the GIVE half) **passes all four owed rows**; the
  ticket stays in `todo/` because its section C rows (`WireCommandKind.ItemSpawn`, and the `NetMsg.EntityEvent`
  drops row's remaining lever) are still open
- Batch: `20261018-z` — this run served one ticket, the one the session handoff opened it for; the tickets in
  `review/` that need a different world, a mod fixture or a save cycle were not part of it (see "Why this batch")
- Commit: `ad5d3bc7` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+ad5d3bc79735c0a83645b40c470939e8af2b0ee5`, checked with `tools/verify-deploy.ps1` (exit 0;
  `prelude-verify-deploy.txt`)
- Run: 2026-10-18 22:52 → 22:56 (+08:00) · Host: the physical machine · Member: the `Steam1` sandbox · No
  third client
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (the host and member endpoints named
  by `hotrepl-host-url` / `hotrepl-guest-url`), `input`, `logs`, `artifacts` — the preflight reported eleven
  present (`prelude-preflight.txt`); the active BepInEx tree was CUO's (`prelude-session-environment.txt`)
- Artifacts: the `20261018-z` subdirectory of the directory named by `acceptance-artifacts-dir`

## Why this batch

The GIVE half of the trade report landed at `ad5d3bc7` with its machine reading explicitly owed to the next
batch (the ticket's "Acceptance owed (next batch)" paragraph), and the three remaining tickets in `review/`
that touch the same domain need different setups (a price-repair case, a save cycle, a mod fixture). Under
workflow §1 the run was opened for this ticket alone and says so rather than implying the whole backlog was
covered: no row of any other ticket is claimed here.

## Method

The prelude started the host and the member through `.acceptance/live/up.ps1`, created and joined one lobby
and entered one run (`up.log`: `host up`, `member up`, `inWorld` true on both). The rows were then driven one
step at a time:

- **Trader fixture.** `trade-drive mode=list` on both clients read the same eight traders with the same
  positions; the nearest to the member's body is at `(-32,222)` (character 2, build 1250, nine stock entries),
  and both sides read `valueGiven=0 totalValueGiven=0`.
- **Row 1's item.** `item-drop type=bandage height=2` created a world bandage above the member's body, so the
  game's own `Item.Start` ran, the item got an instance id and its creation report was judged by the host
  (`[ItemSpawn] materializing bandage (id 9492956821) at (0.0,493.6)`). A follow-up eval then called
  `TraderScript.GiveItem` on that live item — the native call site `TraderPatches.GiveItemPatch` reports.
- **Rows 2 and 3** used the committed acceptance verb `/traderreport <x> <y> <itemInstanceId> <value>` from the
  member's console, after the host opened the cheat switch (`/hostrules {"AllowMemberCheatVerbs":true}` →
  `Updated 1 host rule(s).`) and granted the member (`/op <member>` — the member's own account id, written
  `<member>` here per the machine-information rule → `Granted the cheat verbs to …; the roster is announced to
  every member.`).
- **Row 4** is an absence: the host log was read whole, and the stretch from its first trade line to the first
  forged step was counted for refusal lines.

The member carried exactly one item when the fixture was read (`carried-values.cs`, the local body read with
the native `item.Stats.GetValue`: `emergencylight` in slot 3, instance `5197989525`, condition 1, value 8), which
is the item row 2 claims against.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A real hand-in credits the host's own number and the member converges | machine | **pass** | the native call on the member returned `totalBefore 0 → totalAfter 10` for `bandage` (instance `9492956821`, world item) (`s5-member-handin.json`); the host judged the claim against its own row and credited exactly that number: `[Trade] …'s give credited 10 (bandage (#9492956821, World, condition 1) is worth 10 on the host's record; the report claimed 10).` and `[Trade] executed action=GiveItem … acceptedAsClaimed=True` (`z-host-trade-lines.txt`); the member's own report line carries the same instance and value (`[Trade] report action=GiveItem trader=(-32.0,222.0) item=bandage instance=9492956821 value=10.`) and the authoritative overwrite arrived with `reject=0`; after convergence both clients read `valueGiven=10 totalValueGiven=10` (`s6-member-traders-after-give.log`, `s7-host-traders-after-give.log`) |
| 2 | A claim above the host's row is CLAMPED to the host's number and answered | machine | **pass** | the member's `/traderreport -32 222 5197989525 500` answered `Reported give of item #5197989525 worth 500 at (-32,222)` (`s10-member-report-high.log`); the host clamped it: `[WRN] [Trade] …'s give claimed more than the host's own record makes the item worth — emergencylight (#5197989525, Carried, condition 1) is worth 8 on the host's record; the report claimed 500. Credited 8, the answer rides the state broadcast.`, then `acceptedAsClaimed=False`; the answer reached the member — `[Trade] rejected action=3 — … it applied 8 (local totalValueGiven 10, authoritative 18)` and `state received … reject=3`; both clients then read `valueGiven=18 totalValueGiven=18` (`s11`, `s12`), i.e. 10 from row 1 plus the host's 8, never the claimed 500 |
| 3 | An unjudged instance and a repeated instance are refused BY NAME and credit nothing | machine | **pass** | `/traderreport -32 222 999999999 50` drew `[WRN] [Trade] …'s give of item #999999999 credited nothing: the host has never judged item #999999999. Nothing applied.` and `acceptedAsClaimed=False`; `/traderreport -32 222 5197989525 5` drew `[WRN] [Trade] …'s give of item #5197989525 credited nothing: item #5197989525 was already given to a trader in this session. Nothing applied.` and `acceptedAsClaimed=False`; each was answered (`[Trade] rejected action=3 … it applied 0 (local totalValueGiven 18, authoritative 18)`) and the totals stayed 18 on both sides after both refusals (`s13`–`s16`) |
| 4 | An unmodified session meets no refusal | machine | **pass** | the host's trade channel runs from 22:52:45.396 (`[Trade] snapshot n=0 members=0`) and the first forged step is 22:53:52.946: that 67-second stretch carries the real hand-in, eleven periodic snapshot rounds (`[Trade] snapshot n=11 members=1`) and **zero** `credited nothing` lines; the whole session carries exactly the two this run provoked, and no `[Trade] trader not found` warning appears (`z-host-trade-lines.txt`, 25 lines) |

## Limits

- **One member, no third client.** No third-peer view is claimed; the host's own log and the member's
  converged state are the evidence for every row.
- **Row 1's gesture is the native method, not the walk.** The run called `TraderScript.GiveItem` on the member's
  client over a world item the run had created, instead of walking the body to the trader and dragging the item
  in the trade UI. The native method gates on the item's own value, `BoughtItem` and its contents and has NO
  distance check (`reversing/…/TraderScript.cs`: `if (item.Stats.GetValue(item) > 0 &&
  !item.GetComponent<BoughtItem>())` and then the credit), and reach is out of scope by decision 265. Read
  honestly: the member's body stood 271.5 world units from the trader and the credit still landed, because the
  trade domain never judged distance; the row claims the CREDIT path, and the walking gesture is the UI's, not
  the domain's.
- **The forged rows are claims, never local hand-ins.** `/traderreport` sends one report; the member's copy of
  the item is untouched and stays giveable, which is why row 3's repeat is reachable in the same session.
- **The clamp's other direction is not exercised.** The "unrecorded GAIN" case the design names (a repaired,
  refilled or reloaded item whose live value is above the host's recorded row, so the claim sits BELOW the
  host's number and nothing is clamped) needs an item whose state moved after the row was captured; this run
  read the clamp itself, not that direction.
- **The native `!GetComponent<BoughtItem>()` guard is still not mirrored.** The row's hand-in item was not a
  bought item, so handing a bought item back stayed unexercised, exactly as the ticket's "NAMED as left open"
  says.
- One session, no artificial delay or loss. The run proves the four rows once, not the absence of a rare race.

## Not in this batch

- The ticket's `WireCommandKind.ItemSpawn` row has no anchor at all (a design decision, not a check) and needs
  none of this setup.
- The `NetMsg.EntityEvent` drops row's remaining acceptance needs a lever that sends a forged `EntityEventMsg`
  with a hand-built drop list; no acceptance recipe or verb constructs one today, so the row stays open until
  that lever lands.
