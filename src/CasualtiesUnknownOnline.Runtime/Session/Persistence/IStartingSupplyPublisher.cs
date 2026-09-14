namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Where the Game Adapter reports a starting-supplies grant (S4.3). The adapter is the
/// only layer that can put an item on a body, and the Runtime is the only layer that
/// owns a player-visible surface, so the decision travels as one report through this
/// narrow port — the same shape <see cref="WorldRestoreAudit"/> gives a restore's
/// live-world half.
///
/// It is deliberately a port of its own rather than a member of
/// <see cref="IWorldSaveControl"/>: the grant is not a save event (nothing is written),
/// it only shares the surfaces the save system already prints to. The console
/// subscribes to <see cref="IStartingSupplyControl"/>; a composition without a console
/// still gets the adapter's own log line.
/// </summary>
public interface IStartingSupplyPublisher
{
	/// <summary>
	/// One grant attempt resolved — for every disposition, not only the granted ones:
	/// "this run hands out nothing" is an answer a player needs exactly as much as
	/// "here is what you got", and a silently absent report is indistinguishable from a
	/// mechanism that never ran.
	/// </summary>
	void Publish(StartingSupplyGrantReport report);
}
