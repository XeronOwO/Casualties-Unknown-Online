# src/ — code lives here, the knowledge lives under docs/

- Adding or changing a mod surface: [`../docs/en/start/your-first-mod.md`](../docs/en/start/your-first-mod.md)
  and [`../docs/en/how-to/README.md`](../docs/en/how-to/README.md) (Chinese block: `../docs/zh/`, same paths).
- Changing the runtime, protocol or saves: [`../docs/en/internals/`](../docs/en/internals/README.md)
  explains why it works this way; the records behind it are
  [`../docs/architecture/current.md`](../docs/architecture/current.md) and
  [`../docs/decisions/active.md`](../docs/decisions/active.md).
- Red lines: only `GameAdapter` references the game assemblies; a wire change bumps
  `ProtocolVersion.Current` in the same change; business logic references `Abstractions` only.
- Build, gates and commit rules: [`../AGENTS.md`](../AGENTS.md).
