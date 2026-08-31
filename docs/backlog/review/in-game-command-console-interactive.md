# Interactive in-game command console

- Status: Review
- Priority: Medium
- Category: Tooling / UI

Expand the initial modal Online UI console into a standalone interactive
in-game command console: `/` opens a focused input box, text lines fade,
completion/history/hints are available, and the input state blocks other game
interaction. The local command execution chain and text-chat forwarding stay in
Runtime; the new interaction model is a first-class UI surface that does not
require opening the Online UI window. The selector argument provider now also
feeds a real command: `/heal <selector>` resolves player selectors and sends a
host-authoritative heal request.

Phase plan: `docs/phases/command-console-interactive/README.md`.
Selfcheck: `docs/evidence/selfchecks/ui/command-console-selfcheck.md`.

