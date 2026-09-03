using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One additive worn-sprite assignment for a specific vanilla limb on a custom
/// item. It is plain data in Abstractions: the limb name is a stable vanilla
/// body-part name and the sprite path is a resource path that the Game Adapter
/// resolves at runtime-template build time.
/// </summary>
[DataContract]
public sealed class ModItemLimbWornSprite
{
	/// <summary>Vanilla limb name that receives the additive sprite while the item is worn.</summary>
	[DataMember(Order = 1)]
	public string LimbName { get; set; } = "";

	/// <summary>Resource path of the additive sprite shown on the named limb.</summary>
	[DataMember(Order = 2)]
	public string SpritePath { get; set; } = "";

	/// <summary>Local X offset applied to the additive sprite.</summary>
	[DataMember(Order = 3)]
	public float OffsetX { get; set; }

	/// <summary>Local Y offset applied to the additive sprite.</summary>
	[DataMember(Order = 4)]
	public float OffsetY { get; set; }
}
