# Block-break first-writer-wins dual-side runtime confirmation

- Status: Done
- Acceptance (20261001-x): rows 1–5 pass — two senders broke one cell in both orders and once concurrently; the host refused the second report and the loser's drop was destroyed on its own client, while the winner's drop was claimed by every peer; the reverse round (guest first, host 800 ms late) showed the host applying the guest's write. Record: `docs/evidence/acceptance/block-break-first-writer-wins-20261001-x.md`.
- Priority: High
- Category: Final acceptance

Dual-side runtime confirmation of first-writer-wins block-break behavior.
