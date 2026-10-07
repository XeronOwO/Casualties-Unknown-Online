using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One crafting-quality label carried by a mod content definition: the
/// crafting-quality id and the amount it provides. The label surface is shared
/// by items and liquids — the vanilla <c>CraftingQuality</c> is the same type on
/// both — so the CUO API has one DTO for it rather than two identical ones.
///
/// <para>
/// The id is either a vanilla label (a bare lower-case token such as
/// <c>rippable</c>, which says the content provides that vanilla label) or a
/// mod-authored label namespaced with the content-id grammar
/// (<c>mymod:material</c>), which is what keeps two mods' vocabularies apart.
/// The game matches qualities with an ordinal string comparison, so the id is
/// used verbatim.
/// </para>
/// </summary>
[DataContract]
public sealed class ModCraftingQuality
{
	/// <summary>The crafting-quality id.</summary>
	[DataMember(Order = 1)]
	public string Id { get; set; } = "";

	/// <summary>The quality amount.</summary>
	[DataMember(Order = 2)]
	public float Amount { get; set; } = 1f;
}
