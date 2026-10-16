using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A blueprint was used: the recipe at RecipeIndex is unlocked on every side — on the HOST only when
/// that index is one entry of this host's OWN recipe table, because every side writes
/// <c>Recipes.recipes[index].INT = 0</c> onto its own table: an index it has no recipe for is refused by
/// name and neither applied nor relayed
/// (Recipes.recipes[idx].INT = 0 — the static recipe table is per-process,
/// and the unlock would otherwise exist only on the user's side). RecipeIndex
/// 0 is a VALID index (blueprints roll RecipeRange(0, Count)) — protobuf
/// omits zero values, and the omission decodes back to 0 transparently.
///
/// Direction: guest → host report; host → guest broadcast relay (source
/// excluded). The blueprint item's own destruction rides the existing use
/// use report (kernel command) — this frame is the unlock fact only.
/// </summary>
[ProtoContract]
public sealed class RecipeUnlockMsg
{
	[ProtoMember(1)]
	public int RecipeIndex { get; set; } // index into Recipes.recipes (0 is valid — omission transparent)
}
