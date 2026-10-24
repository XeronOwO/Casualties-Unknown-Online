# Batch 20261024-p — the strength gate refuses at the source

- Ticket: `docs/backlog/done/remote-medical-cpr.md` (stage 4's strength gate)
- Artifact: `0.1.0+289d82e9`, deployed and verified against `<game-dir>` before the run
- Session: host + one member; host forced into arrest; the member opened the chart through `medical-open`, then
  `body-force strength=0` made the OPERATOR unable to compress, and the control was pressed through `cpr-press`.

## The reading

| evidence | reading |
|---|---|
| the member's own log | exactly ONE `[Cpr]` line: `the gesture was refused by this operator's own body: TooWeak` |
| the member's log, absence | no `asked to start CPR` line at all — the refusal happened BEFORE the start request |
| the host's log | no `started Cpr operation` line — no operation ever existed on the patient's side |

So a weak operator's gesture dies on their own client: the line is shown through the interaction family's own refusal
entry (which also plays the game's deny sound), and nothing is sent. The patient's side never learns the gesture happened,
which is the right shape — a request that the sender's own body already ruled out should not become the host's problem.

## The rule, and where it lives

`CprOperatorGate.CanCompress` is the reference's `skills.STR * handMaxTotalForce > 6`, with the hand force read from the
game's own `limbs[i].totalForce` on the two hand limbs (`Body` treats 5 and 8 as the hands). It is answered on the
operator's client because the quantity is their own body's (`totalForce` is not in the character snapshot, and a player's
own capability belongs to their own client). The gate runs BEFORE the start request, not only before the compressions: a
body that cannot compress must not open an operation the patient's side would then honour.

## What the batch also fixed

`body-force` gained `strength` (`Body.skills.STR`) as an argument and in its answer — without it there was no way to make
a weak operator on the machine, and a gate no run can reach is not a gate.
