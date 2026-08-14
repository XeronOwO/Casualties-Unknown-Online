using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The world-event relays that ride the thin handlers: the host's earthquake
/// announcement (host authority — guests suppress their own timer and show the
/// host's) and the building-entity damage reports (star semantics: a guest's
/// report applies on the host and relays to the other guests, the source
/// excluded — it already applied locally).
/// </summary>
public class WorldEventRelayTests
{
	[Fact]
	public void EarthquakeStart_HostBroadcast_ReachesEveryGuest()
	{
		using var w = ItemSimWorld.Create();
		var g1Quakes = new List<(float Duration, float NextDelay)>();
		var g2Quakes = new List<(float Duration, float NextDelay)>();
		w.G1.Services.GetRequiredService<IWorldControl>().EarthquakeStartReceived += (d, n) => g1Quakes.Add((d, n));
		w.G2.Services.GetRequiredService<IWorldControl>().EarthquakeStartReceived += (d, n) => g2Quakes.Add((d, n));

		w.Host.Services.GetRequiredService<IWorldControl>().BroadcastEarthquakeStart(duration: 12f, nextDelay: 240f);
		w.Driver.Tick(50);

		Assert.True(g1Quakes.Count == 1 && g2Quakes.Count == 1,
			$"every guest must get the quake (g1: {g1Quakes.Count}, g2: {g2Quakes.Count})");
		Assert.True(g1Quakes[0].Duration == 12f && g1Quakes[0].NextDelay == 240f,
			$"the duration and the next-delay ride through, got {g1Quakes[0]}");
	}

	[Fact]
	public void BuildingDamaged_GuestReport_RelayedToOtherGuest_SourceExcluded()
	{
		using var w = ItemSimWorld.Create();
		var hostDamages = new List<(float X, float Y, float Damage)>();
		var g1Damages = new List<(float X, float Y, float Damage)>();
		var g2Damages = new List<(float X, float Y, float Damage)>();
		w.Host.Services.GetRequiredService<IWorldControl>().BuildingEntityDamagedReceived += (p, d) => hostDamages.Add((p.X, p.Y, d));
		w.G1.Services.GetRequiredService<IWorldControl>().BuildingEntityDamagedReceived += (p, d) => g1Damages.Add((p.X, p.Y, d));
		w.G2.Services.GetRequiredService<IWorldControl>().BuildingEntityDamagedReceived += (p, d) => g2Damages.Add((p.X, p.Y, d));

		// g1's attack (local compute) — report → host applies to its own copy
		// (which rolls the host-side drops) and relays, the source excluded.
		w.G1.Services.GetRequiredService<IWorldControl>().SendBuildingEntityDamaged(new Runtime.Protocol.NetVector2(7f, 8f), 3.5f);
		w.Driver.Tick(50);

		Assert.True(hostDamages.Count == 1, $"the host must apply the report, got {hostDamages.Count}");
		Assert.True(hostDamages[0].X == 7f && hostDamages[0].Damage == 3.5f, "the position key and the damage ride through");
		Assert.True(g2Damages.Count == 1, $"the other guest must get the relay, got {g2Damages.Count}");
		Assert.True(g1Damages.Count == 0, $"the source already applied locally — no echo, got {g1Damages.Count}");
	}

	[Fact]
	public void BuildingDamaged_HostBroadcast_ReachesEveryGuest()
	{
		using var w = ItemSimWorld.Create();
		var g1Damages = new List<(float X, float Y, float Damage)>();
		var g2Damages = new List<(float X, float Y, float Damage)>();
		w.G1.Services.GetRequiredService<IWorldControl>().BuildingEntityDamagedReceived += (p, d) => g1Damages.Add((p.X, p.Y, d));
		w.G2.Services.GetRequiredService<IWorldControl>().BuildingEntityDamagedReceived += (p, d) => g2Damages.Add((p.X, p.Y, d));

		w.Host.Services.GetRequiredService<IWorldControl>().SendBuildingEntityDamaged(new Runtime.Protocol.NetVector2(1f, 2f), 9f);
		w.Driver.Tick(50);

		Assert.True(g1Damages.Count == 1 && g2Damages.Count == 1,
			$"the host's attack must reach every guest (g1: {g1Damages.Count}, g2: {g2Damages.Count})");
		Assert.True(g1Damages[0].Damage == 9f && g2Damages[0].Y == 2f, "the damage and the position ride through");
	}
}
