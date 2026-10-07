namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The game's own content facts a cross-player limb-use chain judges with,
/// instead of a CUO id table. The Runtime holds the QUESTIONS and every call
/// site; the Game Adapter answers them from the game's registries
/// (<c>Item.GlobalItems</c>, <c>Liquids.Registry</c>), because no game assembly
/// may cross into the Runtime.
/// <para>
/// The predicates are the native dispatch itself, not a copy of it:
/// <c>PlayerCamera.ApplyWoundItem</c> sends an item whose
/// <c>ItemInfo.usableOnLimb</c> is set to <c>useLimbAction</c>,
/// <c>WaterContainerItem.Inject</c> runs a liquid's <c>onHealthUse</c> only when
/// its <c>LiquidType.injectable</c> is set, and <c>WaterContainerItem.ApplyToLimb</c>
/// runs it only when <c>healthUsable</c> is. A liquid the tables never carried
/// therefore needs no CUO change to work between players.
/// </para>
/// </summary>
public interface ILimbUseSemantics
{
	/// <summary>
	/// The item's own data says it is a liquid container a limb action may draw
	/// from: its <c>ItemInfo.Stats</c> is a <c>LiquidItemInfo</c> with
	/// <c>usableOnLimb</c> set. False for an unknown id and for an item the game
	/// cannot use on a limb at all.
	/// </summary>
	bool IsLimbUsableLiquidContainer(string itemId);

	/// <summary>
	/// The liquid's own data says it can be injected: its <c>LiquidType.injectable</c>
	/// is set. A liquid that is not injectable is still drawn by the native path —
	/// it simply contributes no <c>onHealthUse</c> effect — so this predicate
	/// decides admission, never the effect.
	/// </summary>
	bool IsInjectableLiquid(string liquidId);

	/// <summary>
	/// The liquid's own data says the game applies it to a limb: its
	/// <c>LiquidType.healthUsable</c> is set. That is the single flag
	/// <c>WaterContainerItem.ApplyToLimb</c> gates the liquid's own
	/// <c>onHealthUse</c> on, so it decides the topical chain's admission exactly
	/// the way <see cref="IsInjectableLiquid"/> decides the injection chain's.
	/// A liquid without it is still drawn by a topical container the game admits —
	/// it simply contributes no effect — so this predicate decides admission,
	/// never the effect.
	/// </summary>
	bool IsHealthUsableLiquid(string liquidId);
}
