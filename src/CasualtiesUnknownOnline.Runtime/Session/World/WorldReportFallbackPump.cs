using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The pending-report fallbacks' clock: once per frame it asks each owner to
/// re-send the unacknowledged reports whose window elapsed — the guest's world
/// reports, all three channels of them (<see cref="WorldService"/>: block state,
/// partial damage, break drops — see <see cref="GuestReportFallbacks"/>), the
/// guest's runtime entity creations (<see cref="RuntimeEntityChannel"/>), and the
/// guest's recipe-unlock set (<see cref="ICraftControl.PumpRecipeUnlockFallback"/>
/// — audit I6; the one channel whose fact is a set rather than a per-key row, and
/// whose "answer" is the host's absolute set carrying it). Every domain stays
/// reaction-only; this tiny service is the single time edge. The cadence policy
/// itself lives in <see cref="PendingReportFallback"/>; the Runtime owns both
/// because the pending tables and the send paths are Runtime state, while the
/// adapter's own 60 s cycle stays adapter-side because it also scans the game
/// world.
/// </summary>
internal sealed class WorldReportFallbackPump(WorldService world, RuntimeEntityChannel entities, ICraftControl craft, ITimeSource time) : ICuoService
{
	private readonly WorldService _world = world;
	private readonly RuntimeEntityChannel _entities = entities;
	private readonly ICraftControl _craft = craft;
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
		_world.PumpReportFallbacks(nowMs);
		_entities.PumpEntityReportFallback(nowMs);
		_craft.PumpRecipeUnlockFallback(nowMs);
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
