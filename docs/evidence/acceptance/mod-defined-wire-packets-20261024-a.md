# Acceptance — mod-defined packets over a real three-client session

- Ticket: `docs/backlog/done/mod-defined-wire-packets.md`
- Artifact: `0.1.0+1ae6b691`
- Batch: `.acceptance/20261024-c/` (local, gitignored; run `pk`)
- Date: 2026-10-24
- Result: **Limit 1's two-client rows pass** — the example mods' declared packets work over a real star.

## What was run

The built example mod (`CasualtiesUnknownOnline.ModExample.dll`, declaring `cuo.example` and
`cuo.example.machine` — neither is shipped by the deploy scripts, so it was copied into the deployed plugins
directory by hand, as the ticket's Limit 1 says) was loaded on three clients. The host created a Steam lobby
and both members joined.

## Rows

| Row | Observed |
|---|---|
| A guest's `/machineuse` reaches the host (`machine.use`, GuestOnly → HostOnly) | the guest's console answered `machine use 3 reported to the host (local charge 0).`; the HOST's log carries `[Machine] <guest> charged the machine by 3 — charge is now 3; announcing.` |
| The host's `machine.state` reaches every member (HostOnly → EveryMember) | the host's log `[Machine] charge 3 applied on the host.`; both members' logs `[Machine] charge 3 applied on a member.` |
| `/exampleecho` on both sides (`example.echo`, AnyMember → EveryOtherMember) | guest → host and the other member: the host logged `[Example] example.echo from <guest>: hello-from-guest` and the third client logged it too; host → both members: each logged `[Example] example.echo from <host>: hello-from-host`. The guest's own copy did not receive its own echo (the declared `EveryOtherMember` policy) |

The third client's line for the GUEST's text names the HOST as the sender — the documented relayed-frame
sender contract (the ticket's Limit 3), observed rather than assumed.

## Limits and substitutions

- The example mod is the fixture; the deploy scripts still do not ship it, which is Limit 1's own statement
  and stays true (this batch copied it by hand and removed it afterwards; the deployment was re-verified).
- The remaining limits (a third-party consumer, the relayed sender, the shared rate limit, an out-of-stage
  refusal) are unchanged and still named in the ticket.
- No OS-level mouse or keyboard input was used.
