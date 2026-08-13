using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A blueprint was used: the recipe at RecipeIndex is unlocked on every side
/// (Recipes.recipes[idx].INT = 0 — the static recipe table is per-process,
/// and the unlock would otherwise exist only on the user's side). RecipeIndex
/// 0 is a VALID index (blueprints roll RecipeRange(0, Count)) — protobuf
/// omits zero values, and the omission decodes back to 0 transparently.
///
/// Direction: guest → host report; host → guest broadcast relay (source
/// excluded). The blueprint item's own destruction rides the existing use
/// digest report (ItemUseMsg) — this frame is the unlock fact only.
/// </summary>
[ProtoContract]
public sealed class RecipeUnlockMsg
{
	[ProtoMember(1)]
	public int RecipeIndex { get; set; } // index into Recipes.recipes (0 is valid — omission transparent)
}
