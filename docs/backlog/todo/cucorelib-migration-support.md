# CUCoreLib migration support

- Status: Todo
- Priority: Medium
- Category: Mod ecosystem / migration
- Source: External project — <https://github.com/jimmyking9999999/CUCoreLib> (based on KrokMP)

Evaluate the external CUCoreLib project and either:

1. Implement its feature set directly in CUO when the feature is within CUO's
   architecture boundaries, or
2. Provide/adjust CUO functional interface seams so CUCoreLib (or the KrokMP
   patterns it builds on) can migrate to CUO with minimal adapter work.

Constraints:

- Do not commit external source code, assets, or reverse-engineered material
  from CUCoreLib/KrokMP; only understand its public feature/API surface.
- Respect existing architecture rules: no new wire protocol unless the feature
  genuinely requires it; `Abstractions` remains the only package mods reference.
- Prefer interface support/migration guidance over porting code verbatim.
- Any landed functionality must pass the normal build/test/architecture gates.
