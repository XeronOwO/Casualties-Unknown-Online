using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The surface half of the starting-supplies port (S4.3): every player-visible
/// consumer of the account subscribes here, and the Game Adapter reports through
/// <see cref="IStartingSupplyPublisher"/>. The two directions are separate interfaces
/// on purpose — the adapter must not be able to consume its own reports, and a surface
/// must not be able to invent one.
/// </summary>
public interface IStartingSupplyControl
{
	/// <summary>Raised once per resolved grant attempt (see <see cref="IStartingSupplyPublisher.Publish"/>).</summary>
	event Action<StartingSupplyGrantReport>? Reported;
}
