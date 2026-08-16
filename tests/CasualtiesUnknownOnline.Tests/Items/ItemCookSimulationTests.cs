using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The heater-cook channel over the real wire stack: the host's scene already
/// ran the native conversion and reports ONE ItemCook event; the host's
/// authoritative world table flips source→cooked atomically and every guest
/// surfaces the full cooked-steak WorldItem exactly once. The GameAdapter's
/// scene apply (kill + materialize + Scald) is compile-excluded and covered by
/// the patch-surface/reflection tests; this locks the Runtime half of the
/// chain.
/// </summary>
public class ItemCookSimulationTests
{
	private static CharacterItemMsg Steak(float condition) => new()
	{
		ItemId = "steak",
		Condition = condition,
	};

	private static NetVector2 Pos(float x, float y) => new(x, y);

	[Fact]
	public void HostCook_BroadcastsOneItemCook_AndFlipsTheTableAtomically()
	{
		using var w = ItemSimWorld.Create();
		var sourceId = 42UL;
		var cookedId = 43UL;

		var g1Events = new List<(ulong SourceId, WorldItem Cooked)>();
		var g2Events = new List<(ulong SourceId, WorldItem Cooked)>();
		var g1Control = w.G1.Services.GetRequiredService<IItemControl>();
		var g2Control = w.G2.Services.GetRequiredService<IItemControl>();
		g1Control.ItemCookedReceived += (source, cooked) => g1Events.Add((source, cooked));
		g2Control.ItemCookedReceived += (source, cooked) => g2Events.Add((source, cooked));

		var g1Frames = new List<(NetMsg Msg, byte[] Frame)>();
		var g2Frames = new List<(NetMsg Msg, byte[] Frame)>();
		w.G1.Transport.MessageReceived += (_, frame) => g1Frames.Add(((NetMsg)frame[0], frame));
		w.G2.Transport.MessageReceived += (_, frame) => g2Frames.Add(((NetMsg)frame[0], frame));

		// The host table already holds the raw meat from an earlier item-domain
		// operation (the normal spawn path is not under test here).
		w.Spawn(w.G1, sourceId, new CharacterItemMsg { ItemId = "meat", Condition = 0.8f });
		w.Driver.Tick(33);
		Assert.True(w.HostTable(sourceId));

		w.Items.SendItemCooked(sourceId, cookedId, Steak(condition: 0.24f), Pos(10f, 20f), Pos(1f, 2f), 45f, 30f);

		// The host transition is immediate and atomic.
		Assert.False(w.HostTable(sourceId), "the raw meat must leave the world table");
		Assert.True(w.HostTable(cookedId), "the cooked steak must enter the world table");

		w.Driver.Tick(33);
		Assert.Single(g1Events);
		Assert.Single(g2Events);
		Assert.Equal(sourceId, g1Events[0].SourceId);
		Assert.Equal(cookedId, g1Events[0].Cooked.ItemId);
		Assert.Equal("steak", g1Events[0].Cooked.Item.ItemId);
		Assert.True(Math.Abs(g1Events[0].Cooked.Item.Condition - 0.24f) < 0.0001f,
			$"cooked condition must be 0.24, got {g1Events[0].Cooked.Item.Condition}");
		Assert.Equal(10f, g1Events[0].Cooked.Pos.X);
		Assert.Equal(20f, g1Events[0].Cooked.Pos.Y);
		Assert.Equal(45f, g1Events[0].Cooked.Rotation);
		Assert.Equal(30f, g1Events[0].Cooked.AngularVelocity);

		var g1Frame = g1Frames.Single(f => f.Msg == NetMsg.ItemCook).Frame;
		var msg = NetPacket.DecodePayload<ItemCookMsg>(g1Frame);
		Assert.Equal(sourceId, msg.SourceItemId);
		Assert.Equal(cookedId, msg.CookedItemId);
		Assert.Equal("steak", msg.Item.ItemId);
		Assert.Equal(0.24f, msg.Item.Condition);
		Assert.Equal(45f, msg.Rotation);
		Assert.Equal(30f, msg.AngularVelocity);
	}

	[Fact]
	public void GuestSide_NeverSendsACookReport()
	{
		using var w = ItemSimWorld.Create();
		var guestItems = w.G1.Services.GetRequiredService<ItemService>();

		guestItems.SendItemCooked(42, 43, Steak(0.3f), Pos(1f, 1f), Pos(0f, 0f), 0f, 0f);
		w.Driver.Tick(33);

		// The guest role guard suppresses the report and the host's table never
		// learns a conversion from a side that cannot run the physics collision.
		Assert.False(w.HostTable(43));
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.ItemCook));
	}
}
