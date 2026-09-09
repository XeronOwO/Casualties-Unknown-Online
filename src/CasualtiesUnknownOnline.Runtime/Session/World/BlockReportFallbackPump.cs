using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest block-report fallback's clock: once per frame it asks
/// <see cref="WorldService"/> to re-report the unacknowledged block mutations
/// whose window elapsed. WorldService stays reaction-only; this tiny service
/// is the time edge, exactly like the item domain's PendingPickupPump. The
/// cadence lives here (Runtime) rather than in the Game Adapter because the
/// pending table and the send path are both Runtime state; the adapter's own
/// 60 s cycle stays adapter-side because it also scans the game world for
/// keypads.
/// </summary>
internal sealed class BlockReportFallbackPump(WorldService world, ITimeSource time) : ICuoService
{
	private readonly WorldService _world = world;
	private readonly ITimeSource _time = time;

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => _world.PumpBlockReportFallback(_time.NowMs);

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
