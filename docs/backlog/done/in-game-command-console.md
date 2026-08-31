# In-game command console

- Status: Done
- Priority: Low (future item completed)
- Category: Tooling / UI

Landed as a local command/chat console inside the Online UI. It has a registered
command chain (help, players, rtt, whoami, kick, ban, unban) with role-based
permission, an input field that sends non-command text through the existing
text-chat domain, and a bounded output buffer. The old standalone chat input
surface is not resurrected; this console is the modal Online UI replacement.

Selfcheck: `docs/evidence/selfchecks/ui/command-console-selfcheck.md`.
