using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The Runtime half of the starting-supplies report (S4.3): the Game Adapter calls
/// <see cref="Publish"/> once per grant attempt and every player-visible surface
/// subscribes to <see cref="Reported"/>. It holds nothing — the adapter owns the
/// decision and its once-per-body rule — so it is a broadcast point rather than an
/// account, unlike <see cref="WorldRestoreAudit"/> whose contributions have to be
/// counted before they may be read as one outcome.
/// </summary>
public sealed class StartingSupplyAudit : IStartingSupplyControl, IStartingSupplyPublisher
{
	/// <summary>The last report published, for diagnostics and tests; null before the first one.</summary>
	public StartingSupplyGrantReport? Last { get; private set; }

	public event Action<StartingSupplyGrantReport>? Reported;

	/// <summary>One grant attempt resolved (granted, disabled by the run's setting, or already owned through the game's own first-layer grant).</summary>
	public void Publish(StartingSupplyGrantReport report)
	{
		Last = report;
		Reported?.Invoke(report);
	}
}
