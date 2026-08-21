using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item-traffic observation pump: once per frame it asks <see cref="ItemService"/>
/// to roll and log a finished traffic window. ItemService stays reaction-only;
/// this tiny service is the time edge, exactly like <see cref="PendingPickupPump"/>.
/// </summary>
internal sealed class ItemTrafficPump(ItemService items, ITimeSource time) : ICuoService
{
	private readonly ItemService _items = items;
	private readonly ITimeSource _time = time;

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => _items.PumpItemTraffic(_time.NowMs);

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
