using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The direct player-interaction control surface a player-interaction packet handler may use.
/// <see cref="Session"/> rides beside it because a host-to-member answer must be anchored to its sender:
/// the wire direction only proves the LOCAL role, so a frame whose sender is not this session's host is
/// a claim, and the handler drops it by name.</summary>
public interface IPlayerInteractionHandlerContext
{
	IPlayerInteractionControl PlayerInteraction { get; }

	ISessionControl Session { get; }
}
