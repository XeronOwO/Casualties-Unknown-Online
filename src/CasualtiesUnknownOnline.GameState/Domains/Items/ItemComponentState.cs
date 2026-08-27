using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// One typed component state. The field model is intentionally the same shape
/// as the wire component codec's supported simple kinds: float, int, bool,
/// string, string list, and enum-underlying-int. It is kernel-owned and must not
/// reference Protocol DTO types.
/// </summary>
public sealed record ItemComponentState(string TypeName, IReadOnlyList<ItemComponentField> Fields);
