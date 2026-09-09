using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The pending-report fallbacks' clock: once per frame it asks each owner to
/// re-send the unacknowledged reports whose window elapsed — the guest's block
/// mutations (<see cref="WorldService"/>) and the guest's runtime entity
/// creations (<see cref="RuntimeEntityChannel"/>). Both domains stay
/// reaction-only; this tiny service is the single time edge, exactly like the
/// item domain's PendingPickupPump. The cadence policy itself lives in
/// <see cref="PendingReportFallback"/>; the Runtime owns both because the
/// pending tables and the send paths are Runtime state, while the adapter's own
/// 60 s cycle stays adapter-side because it also scans the game world.
/// </summary>
internal sealed class WorldReportFallbackPump(WorldService world, RuntimeEntityChannel entities, ITimeSource time) : ICuoService
{
	private readonly WorldService _world = world;
	private readonly RuntimeEntityChannel _entities = entities;
	private readonly ITimeSource _time = time;

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		var nowMs = _time.NowMs;
		_world.PumpBlockReportFallback(nowMs);
		_entities.PumpEntityReportFallback(nowMs);
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
