using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The Game Adapter's answer to <see cref="ILimbUseSemantics"/>: the game's own
/// registries, read through <see cref="GameLimbUseFacts"/>. Registered as its
/// own singleton and replacing the Runtime's
/// <see cref="NoLimbUseSemantics"/> default (the same standalone-service shape
/// the visibility oracle and the local-body capture use, and for the same
/// reason: reaching it through <c>GameAdapter</c> would close a DI constructor
/// cycle).
/// </summary>
internal sealed class GameLimbUseSemantics : ILimbUseSemantics
{
	public bool IsLimbUsableLiquidContainer(string itemId) => GameLimbUseFacts.IsLimbUsableLiquidContainer(itemId);

	public bool IsInjectableLiquid(string liquidId) => GameLimbUseFacts.IsInjectableLiquid(liquidId);
}
