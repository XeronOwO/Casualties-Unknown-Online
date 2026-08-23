using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The direct player-interaction control surface a player-interaction packet handler may use.</summary>
public interface IPlayerInteractionHandlerContext
{
	IPlayerInteractionControl PlayerInteraction { get; }
}
