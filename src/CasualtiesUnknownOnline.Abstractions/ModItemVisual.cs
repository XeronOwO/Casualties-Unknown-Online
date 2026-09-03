using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Optional visual presentation for a custom item. The values are plain data in
/// Abstractions: no Unity sprite and no game type. The Game Adapter resolves
/// the resource paths at runtime-template build time and applies the visuals
/// to the vanilla <c>SpriteRenderer</c> / <c>WaterContainerItem</c> surfaces
/// through its own component state, so mods never need to touch Unity.
/// </summary>
[DataContract]
public sealed class ModItemVisual
{
	/// <summary>
	/// Resource path of the sprite shown while the item is worn on a body.
	/// Empty disables the worn-sprite override.
	/// </summary>
	[DataMember(Order = 1)]
	public string WornSpritePath { get; set; } = "";

	/// <summary>Local X offset applied to the worn sprite.</summary>
	[DataMember(Order = 2)]
	public float WornSpriteOffsetX { get; set; }

	/// <summary>Local Y offset applied to the worn sprite.</summary>
	[DataMember(Order = 3)]
	public float WornSpriteOffsetY { get; set; }

	/// <summary>
	/// Optional sorting-order override for the worn sprite. When null the base
	/// sprite's sorting order is left unchanged.
	/// </summary>
	[DataMember(Order = 4)]
	public int? WornSpriteSortingOrder { get; set; }

	/// <summary>
	/// Resource path of the liquid fill-mask sprite used by a
	/// <c>WaterContainerItem</c>. Empty disables the liquid-mask override.
	/// </summary>
	[DataMember(Order = 5)]
	public string LiquidMaskPath { get; set; } = "";

	/// <summary>
	/// Optional additive worn sprites keyed to vanilla limb names. Each entry
	/// is rendered as its own secondary sprite while the item is worn, on top
	/// of the primary item sprite. The Game Adapter filters entries whose limb
	/// does not exist on the target body at wear time.
	/// </summary>
	[DataMember(Order = 6)]
	public List<ModItemLimbWornSprite> MultiWornSprites { get; set; } = [];

	/// <summary>
	/// Optional frame animation for the item's primary/held sprite when the item
	/// is not wearing a dedicated worn sprite. The first frame is also used as
	/// the static fallback icon.
	/// </summary>
	[DataMember(Order = 7)]
	public ModItemSpriteAnimation? BaseSpriteAnimation { get; set; }

	/// <summary>
	/// Optional frame animation for the worn sprite. When present it supersedes
	/// the static worn sprite at runtime; the first frame is used as the fallback
	/// worn sprite when no separate <see cref="WornSpritePath"/> is authored.
	/// </summary>
	[DataMember(Order = 8)]
	public ModItemSpriteAnimation? WornSpriteAnimation { get; set; }

	/// <summary>
	/// Optional frame animation for the contained-liquid fill mask. When present
	/// it supersedes the static liquid mask; the first frame is used as the
	/// fallback liquid fill sprite when no separate <see cref="LiquidMaskPath"/>
	/// is authored.
	/// </summary>
	[DataMember(Order = 9)]
	public ModItemSpriteAnimation? LiquidMaskAnimation { get; set; }
}
