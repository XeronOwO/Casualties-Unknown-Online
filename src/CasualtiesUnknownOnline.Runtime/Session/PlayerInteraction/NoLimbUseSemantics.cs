namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The base composition's answer: this side has no game scene, so no content
/// fact is known and every limb-use chain refuses. The plugin replaces it with
/// the Game Adapter's game-data implementation, the same way
/// <see cref="UnavailableLocalCharacterCapture"/> and
/// <see cref="AllowAllPlayerInteractionVisibility"/> are replaced.
/// </summary>
public sealed class NoLimbUseSemantics : ILimbUseSemantics
{
	public bool IsLimbUsableLiquidContainer(string itemId) => false;

	public bool IsInjectableLiquid(string liquidId) => false;

	public bool IsHealthUsableLiquid(string liquidId) => false;
}
