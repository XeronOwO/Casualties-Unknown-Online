using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The Game Adapter's answer to <see cref="IConsumeSemantics"/>: the game's own
/// registry, read through <see cref="GameConsumeFacts"/>. Registered as its own
/// singleton and replacing the Runtime's <see cref="NoConsumeSemantics"/>
/// default — the same standalone-service shape
/// <see cref="GameLimbUseSemantics"/> uses, and for the same reason: reaching it
/// through <c>GameAdapter</c> would close a DI constructor cycle.
/// </summary>
internal sealed class GameConsumeSemantics : IConsumeSemantics
{
	public bool IsUsableLiquidContainer(string itemId) => GameConsumeFacts.IsUsableLiquidContainer(itemId);
}
