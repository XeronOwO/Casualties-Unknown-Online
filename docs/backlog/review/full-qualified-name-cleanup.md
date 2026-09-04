# Full-qualified name cleanup

- Status: Review
- Priority: Low
- Category: Code quality / convention

Audit and clean up fully qualified type names across the repository so the
codebase follows the project convention: prefer `using` directives / `using`
aliases over fully qualified names; keep fully qualified names only when
unavoidable (for example HotRepl eval strings, or intentionally disambiguating
same-named types).

Scope:

- All `src/` projects (Runtime, Plugin, GameAdapter, Abstractions, Protocol,
  GameState, ModExample).
- All `tests/` projects.
- No behavior change; refactor only.
- Preserve architecture line-count gates; extract real types where cleanup
  would push a class over a gate.
