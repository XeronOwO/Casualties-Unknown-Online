# Acceptance record — Health on every client, reconciled to the host's value

- Ticket: `health-simulation-and-host-authority` — verdict: moved to `done/`
- Batch: `20261018-x` — tickets: `health-simulation-and-host-authority` (the batch the session
  handoff opened for this ticket)
- Commit: `85ff4f76` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+85ff4f76ea2395bad6d42f1af43dea3b384ae94b` (verified against this tree's build output)
- Run: 2026-10-18 19:49–20:00 · Host: physical machine · Guest: Sandboxie sandbox
- Dependencies: game, steam, sandboxie, dotnet, capture, logs, artifacts, hotrepl, input — the
  preflight reported every required dependency present
- Artifacts: the directory named by `acceptance-artifacts-dir`, batch `20261018-x`; frames and
  probe dumps are cited by id

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A guest bleeds while its own panel is open | visual + machine | **pass** | `g6-guest-panel-row2.json` reads the open panel at `bloodVolume=79.215, totalBleedSpeed=0.107, bloodText=4.48l, bleedText=0.16l/m`; `g7-guest-panel-row2.png` was read: the native UNI-HEALTH panel is open with the bleeding limb icon; the earlier sub-second samples `91/92/93-guest-panel-p*.json` read blood 90.936 → 90.857 → 90.76 within one window (no 1 Hz stepping); the host row agrees: `g8-guest-limb4-row2.json` bleed 11.049, `g9-host-limb4-row2.json` 11.032, `g2-host-display-limb4.json` 11.133 (within the recorded 0.05 limb tolerance) |
| 2 | The same on the host's screen for the GUEST's body | visual + machine | **pass** | `g1-host-display-open.json` opens the host's production remote display for the guest (`displayBodyName=MedicalDisplay_…`, `viewOpen=true`); `g3-host-display.png` was read: the host's WoundView shows the same `4.48l / 10.67 l/m` as the guest's own panel; `g2` display limb 4 (skin 66.561, bleed 11.133) agrees with `g9` host saved (66.580 / 11.032) and `g8` guest local (66.578 / 11.049); `g7` is the guest's own panel frame. The host's clone/display and the guest's own client carry the same state |
| 8 | A guest is wounded by a trap/explosion (bleed rises locally, no limb latch) | machine | **pass** | `80-guest-injury-one.json` applies the game's own `ExplosionBodyEffect.ApplyToLocalBodies` on the guest (one limb: bleed 200, skin 40) and sends the production owner-state event (`CharacterDataSync.ReportOwnerStateEvent`); `85-guest-limb4-one.json` reads 59.956 and `86-host-limb4-one.json` 59.939 with skin 40 on both; the host log (`host-vitals-lines.txt`) carries `[LimbEvent] applied 15 limbs`, `[LimbEvent] owner …` and `re-seeded from an authoritative write`; the 10 Hz row did not erase the wound (both sides ~60). A second wound through the same path persisted at ~11.05/11.03 (`g2/g8/g9`) |
| 3 | The host's value is forced away from the client's simulated value | machine | **pass** | `b0-guest-health-before-force.json` guest blood 65.43; `b1-host-force-20.json` writes the host's stored blood to 20 through `SaveCharacterData` (a test-only authority write); `b2-guest-health-after-force.json` guest 19.616 and `b3-host-health-after-force.json` host saved 19.047; the host log records the re-seed at blood 20.0; `b4-guest-panel-after-force.json` reads 18.845 a moment later (the guest collapsed from the low value, closing the panel; `b5` is the UI after the drop, not the numeric panel). Both sides read the host's branch; the sampling skew is 0.57 |
| 4 | An event the event chain missed (the report lost) | machine | **pass** | `f0/f1` arm the inbound blackout on both clients; `f2` has the guest edit blood to 42 and hunger to 12 and report immediately (dropped); `f3/f4` disarm both; `f5-host-health-after-blackout.json` reads the host saved blood 90.831 and hunger 11.911, `f6-guest-health-after-blackout.json` the guest local 90.496 / 11.874; the host log has `[Vitals] … divergence: blood volume model 91.38 reported 41.50 (-49.89)`. The next snapshot carried the hunger; the lost report's blood edit was warned by name — never a silent step |
| 5 | A client reports a value the host's model cannot produce | machine | **pass** | `e0-guest-edit-999.json` sets the guest's blood to 999 (the native body clamped it to 200 before the report); the host log has `[Vitals] … divergence: blood volume model 97.25 reported 200.00 (+102.74)`; `e2-host-health-after-999.json` host saved 96.698, `e1-guest-health-after-999.json` guest local 97.009 after the next row. The host kept its own value and the disagreement is a named Warning |
| 6 | A limb flag (broken, splinted, infected) changes | machine | **pass** | `a0` host saved and `a1` guest local read limb 4 broken=false before; `a2-guest-break.json` calls `Limb.BreakBone()` on the guest (false→true, bleed rose); `a3` host saved and `a4` guest local read broken=true after; the host log (`host-vitals-lines.txt`) carries `[LimbEvent] owner …` at 19:57:41. The host's copy showed the flag only after the event; no value predicted it before the event |
| 7 | A member edits its own blood volume locally (console or memory) | machine | **pass** | `d0-guest-edit-42.json` sets the guest's blood to 42; the host log has `[Vitals] … divergence: blood volume model 100.00 reported 42.00 (-58)`; `d2-host-health-after-42.json` host saved 99.847, `d1-guest-health-after-42.json` guest local 100 after the next row. The row overwrote the edit and the divergence line named it |

**Frame cost (Stage 2 item 5, artifact `host-cost-lines.txt`).** A steady ten-second window on the
host, one remote clone, `timeScale=1`, `fixedStep=20.00ms`:

`bodies=1 steps=501 passes=501 stepAvg≈122-126us passAvg≈110-114us frameCost=0.122-0.126ms frameBudget=0.61-0.63%`

The first window after the seed reads `0.809ms / 4.05%` (the one-time seed) and is not the steady
number. The per-clone cost is about 0.12 ms per fixed-step invocation; N clones project linearly
(2.4% of the 20 ms step for four). The run measured N=1 (host plus one guest), as recorded in the
ticket's limit.

## New finding outside the eight rows

`/heal` cannot restore the host-owned values. The batch's `/heal @a` instruction was sent and the
target's limbs/consciousness were reset, but the host saved and the guest local both stayed at
about -99.7 blood: the target's re-report is a plain snapshot, and
`SaveReportedCharacterData`/`ContinuousVitals.Preserve` keeps the host's three owned values. The
reset table itself sets `BloodVolume = 100` (`HealResetState`). The batch rescued the session with a
host-side `SaveCharacterData` write plus a revision bump (`70/72/73`), which is the authority shape
the fix needs. Filed as
`docs/backlog/review/heal-reset-does-not-restore-host-owned-vitals.md`; artifacts
`63-host-heal-all.json`, `66-host-health-healed2.json`, `67-guest-health-now.json`,
`70-host-force-heal.json`.

## Limits

- One two-client session, loopback/Steam link; no artificial delay/loss link was configured. The
  lost-report row injected inbound loss directly through the committed `net-receive-blackout`
  recipe, which drops every inbound frame on the armed client for the window.
- The frame cost is N=1; scaling to N>1 is a linear projection, not a measurement.
- The explosion-injury row used the game's own `ExplosionBodyEffect` production path and the
  production owner-state event, not a spawned trap/explosion entity; the handler and model are the
  production ones. A live trap entity was not spawned.
- The row-3 visible jump was not captured as a panel frame (the guest collapsed and the panel
  closed); the machine values carry the row. The frames read for rows 1/2 are the panel evidence.
- Row 6 exercised the broken latch; the splinted/infected flags were not separately changed.
- The host model's blood can go negative exactly as the native body does; no clamp is added by
  CUO, so the negative values above are the native behaviour, not a CUO read error.

## Residuals for the user

None: the eight rows were judged from frames and probe dumps the run produced; the new `/heal`
finding has its own ticket and its own acceptance rows.
