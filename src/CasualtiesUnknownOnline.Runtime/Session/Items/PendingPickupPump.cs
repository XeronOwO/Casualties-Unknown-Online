using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The pending-pickup expiry pump: once per frame it asks <see cref="ItemService"/>
/// to expire queued pickup claims whose hold window elapsed. ItemService stays
/// reaction-only (no own pump); this tiny service is the time edge, exactly like
/// the session/entity/enemy pumps. Registered after the item domain so it can
/// resolve the concrete service without creating a cycle.
/// </summary>
internal sealed class PendingPickupPump(ItemService items, ITimeSource time) : ICuoService
{
	private readonly ItemService _items = items;
	private readonly ITimeSource _time = time;

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => _items.PumpPendingPickups(_time.NowMs);

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
