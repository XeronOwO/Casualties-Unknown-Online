using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The visual presentation of a custom item. This is the contract a consumer
/// reads: <see cref="IModItemDefinition.Visual"/> is interface-typed, so a mod that
/// computes a visual value hands over its own implementation instead of filling in
/// the framework's.
///
/// <see cref="ModItemVisual"/> is the framework's ready-made implementation: use it
/// when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back a
/// filled default for the members it does not touch — never inheritance from the
/// data class, which stays <c>sealed</c>.
///
/// The nested members are contracts in the same way
/// (<see cref="IModItemSpriteAnimation"/>, <see cref="IModItemLimbWornSprite"/>),
/// and the collections that carry them are read through
/// <see cref="ModDeclarationCollections"/>: null means "none".
/// </summary>
public interface IModItemVisual
{
	/// <summary>
	/// Resource path of the sprite shown while the item is worn on a body.
	/// Empty disables the worn-sprite override.
	/// </summary>
	string WornSpritePath { get; }

	/// <summary>Local X offset applied to the worn sprite.</summary>
	float WornSpriteOffsetX { get; }

	/// <summary>Local Y offset applied to the worn sprite.</summary>
	float WornSpriteOffsetY { get; }

	/// <summary>
	/// Optional sorting-order override for the worn sprite. When null the base
	/// sprite's sorting order is left unchanged.
	/// </summary>
	int? WornSpriteSortingOrder { get; }

	/// <summary>
	/// Resource path of the liquid fill-mask sprite used by a
	/// <c>WaterContainerItem</c>. Empty disables the liquid-mask override.
	/// </summary>
	string LiquidMaskPath { get; }

	/// <summary>
	/// Optional additive worn sprites keyed to vanilla limb names. Each entry
	/// is rendered as its own secondary sprite while the item is worn, on top
	/// of the primary item sprite. The Game Adapter filters entries whose limb
	/// does not exist on the target body at wear time.
	/// </summary>
	List<IModItemLimbWornSprite> MultiWornSprites { get; }

	/// <summary>
	/// Optional frame animation for the item's primary/held sprite when the item
	/// is not wearing a dedicated worn sprite. The first frame is also used as
	/// the static fallback icon.
	/// </summary>
	IModItemSpriteAnimation? BaseSpriteAnimation { get; }

	/// <summary>
	/// Optional frame animation for the worn sprite. When present it supersedes
	/// the static worn sprite at runtime; the first frame is used as the fallback
	/// worn sprite when no separate <see cref="WornSpritePath"/> is authored.
	/// </summary>
	IModItemSpriteAnimation? WornSpriteAnimation { get; }

	/// <summary>
	/// Optional frame animation for the contained-liquid fill mask. When present
	/// it supersedes the static liquid mask; the first frame is used as the
	/// fallback liquid fill sprite when no separate <see cref="LiquidMaskPath"/>
	/// is authored.
	/// </summary>
	IModItemSpriteAnimation? LiquidMaskAnimation { get; }
}
