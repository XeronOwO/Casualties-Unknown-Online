# Batch 20261024-h — the radiation line's descent rate, and the end-of-layer panel's rows on the final artifact

- Artifact: `0.1.0+8b5baee6`, deployed and verified against `<game-dir>` before the run
- Session: host + one member, two clients, formed by the driver (`leave-world` → `create-lobby` → `join-lobby` →
  `start-run`); world up (`world.height = 1024`, the layer's floor line `-halfHeight + 3.1 = -508.9`)
- Setup substitutions, both declared recipes: `body-place` moved a client's own body; `body-force` set one body's
  simulation inputs. Every reading comes from the clients' own probes (`radiation-line-state`, `layer-panel-state`).

## Row 1 — the member's line descends at the HOST's rate (`radiation-line-descent-rate-per-side`)

The line was activated by the host's own straggler rule (one living body at the floor, the other above it), then the
MEMBER's body was forced unconscious (`consciousness=0`, alive) while the host's stayed conscious — the state that used
to make the member's line crawl at `0.2 × 1.5 = 0.3` units/s and jump on every 5 Hz resend:

| client | body state | `timeGone` first read | second read (~4.1 s later) | delta |
|---|---|---|---|---|
| host | conscious | 26.406 | 32.580 | **6.174** |
| member | **unconscious** | 24.819 | 30.961 | **6.142** |

The two rates agree (6.14 vs 6.17 over the same window, the difference being one resend's worth of motion), so the
member follows the host's speed instead of its own body's. The absolute values differ by **1.59 units** — a constant
lag of the member behind the host, which is the extrapolation base's age, not a rate difference; the line is 1024 units
tall, so the visible difference is 0.16% of a layer.

**Judged**: the row the user reported (a member's descent looking wrong) reads as fixed on the machine. The lag is
recorded as a residual, not claimed away.

## Row 2 — the end-of-layer panel on the same artifact

| state | host panel | host choices | member panel | member choices |
|---|---|---|---|---|
| both living bodies at the floor (2/2) — three consecutive reads | **active** | **visible** | **inactive** | hidden |
| the member back up top (1/2) | **inactive** | hidden | — | — |

So on the current artifact: the host's panel follows the living share, its two choices are there for the host, and a
member's own panel is gone entirely (the ruling that the choice belongs to the host). The earlier batch
(`layer-panel-visibility-20261024-g.md`) read the dead-host half (host body dead, member alive at the floor → panel
active) and the member's hidden buttons on the build before the member-panel fix.

## Not read here

- A drag gesture (the mouse tool exists now — `tools/acceptance/send-input.ps1` — but this batch did not need it).
- Anything about the radiation line's DAMAGE: the reading above is the descent only.
