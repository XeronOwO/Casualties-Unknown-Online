using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 trade-domain simulation — the trade domain's acceptance (the
/// deployed feature was never acceptance-verified against the game; the
/// simulation covers its sync logic): a guest's interaction reports over the
/// real wire path, the HOST executes the trader-side change through the real
/// TradeStockMachine (the GameAdapter shell's pure re-creation) and broadcasts
/// the authoritative overwrite; the guest's received state must CONVERGE to
/// the host's state after every interaction — under link delay and across
/// long random interaction sequences (seeded, replayable). The "guest applies
/// the overwrite" side is the wire surface: the state message the guest
/// receives IS the applied state.
/// </summary>
public class TradeSimulationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	/// <summary>The valid trader actions the random sequence draws from — derived
	/// from the enum so a new <see cref="TraderActionKind"/> is automatically
	/// exercised (a hand-written count silently missed MoveTo=7 and drew the
	/// invalid 0; the coverage guard locks this pool to the full enum).</summary>
	internal static readonly TraderActionKind[] RandomActionKinds =
		(TraderActionKind[])Enum.GetValues(typeof(TraderActionKind));

	private sealed record SimSession(TestNode Host, TestNode Guest, SimTraderHost Trader, List<TraderStateMsg> Received, Func<int> ReceivedCount, List<TraderStateMsg> HostBroadcasts);

	private static TradeStockState InitialState() => SimTraderHost.CreateDefaultState();

	private static SimSession CreateTradeSession(int seed = 1)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var trader = new SimTraderHost(InitialState(), seed);
		var received = new List<TraderStateMsg>();
		var hostBroadcasts = new List<TraderStateMsg>();
		var world = host.Services.GetRequiredService<IWorldControl>();

		// The host executor: a guest's interaction → the real machine → the
		// authoritative broadcast (TradeStateSync.OnTraderActionReceived's shape).
		// The broadcasted message is recorded — the fidelity assertion compares
		// the guest's received sequence against the host's broadcast sequence.
		world.TraderActionReceived += (_, msg) =>
		{
			var rejected = trader.Execute(msg);
			var state = trader.ToStateMsg();
			state.RejectedAction = rejected;
			lock (hostBroadcasts)
			{
				hostBroadcasts.Add(state);
			}

			world.BroadcastTraderState(state);
		};

		// The guest's apply side: every authoritative overwrite the guest receives.
		guest.Services.GetRequiredService<TradeChannel>().TraderStateReceived += msg =>
		{
			lock (received)
			{
				received.Add(msg);
			}
		};

		return new SimSession(host, guest, trader, received, () =>
		{
			lock (received)
			{
				return received.Count;
			}
		}, hostBroadcasts);
	}

	private static void Report(TestNode guest, TraderActionMsg msg) =>
		guest.Services.GetRequiredService<IWorldControl>().SendTraderAction(msg);

	/// <summary>The guest's received state must equal the authoritative state
	/// (full overwrite — every field, every stock entry). The rejection marker is
	/// asserted separately (it rides the broadcast, not the trader's state).</summary>
	private static void AssertConverged(TraderStateMsg received, TraderStateMsg expected)
	{
		Assert.True(Math.Abs(received.Reputation - expected.Reputation) < 0.001f, $"reputation {received.Reputation} != {expected.Reputation}");
		Assert.True(Math.Abs(received.Hostility - expected.Hostility) < 0.001f, $"hostility {received.Hostility} != {expected.Hostility}");
		Assert.True(Math.Abs(received.ValueGiven - expected.ValueGiven) < 0.001f, $"valueGiven {received.ValueGiven} != {expected.ValueGiven}");
		Assert.True(Math.Abs(received.TotalValueGiven - expected.TotalValueGiven) < 0.001f, $"totalValueGiven {received.TotalValueGiven} != {expected.TotalValueGiven}");
		Assert.True(received.FreeAmount == expected.FreeAmount, $"freeAmount {received.FreeAmount} != {expected.FreeAmount}");
		Assert.True(received.FreeDressing == expected.FreeDressing, $"freeDressing {received.FreeDressing} != {expected.FreeDressing}");
		Assert.True(received.DidHug == expected.DidHug, $"didHug {received.DidHug} != {expected.DidHug}");
		Assert.True(received.DidMove == expected.DidMove, $"didMove {received.DidMove} != {expected.DidMove}");
		Assert.True(received.StartedConvo == expected.StartedConvo, $"startedConvo {received.StartedConvo} != {expected.StartedConvo}");
		Assert.True(Math.Abs(received.HaggleAmount - expected.HaggleAmount) < 0.001f, $"haggleAmount {received.HaggleAmount} != {expected.HaggleAmount}");
		Assert.True(received.Items.Length == expected.Items.Length, $"items {received.Items.Length} != {expected.Items.Length}");
		for (var i = 0; i < expected.Items.Length; i++)
		{
			Assert.True(received.Items[i].Id == expected.Items[i].Id, $"item[{i}] id");
			Assert.True(received.Items[i].Value == expected.Items[i].Value, $"item[{i}] value");
			Assert.True(received.Items[i].Preference == expected.Items[i].Preference, $"item[{i}] preference");
			Assert.True(received.Items[i].Bought == expected.Items[i].Bought, $"item[{i}] bought");
		}
	}

	[Fact]
	public void MeetPlayer_Converges()
	{
		var s = CreateTradeSession();
		var before = s.ReceivedCount();
		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.MeetPlayer, Position = new NetVector2Msg(SimTraderHost.TraderPosX, SimTraderHost.TraderPosY) });

		var driver = new SimulationDriver(s.Guest.Clock, s.Guest.Transport.Network, s.Host, s.Guest);
		driver.TickUntil(() => s.ReceivedCount() > before, maxMs: 1000);

		Assert.True(s.Received.Count > 0, "the authoritative state must arrive");
		AssertConverged(s.Received[s.Received.Count - 1], s.Trader.ToStateMsg());
	}

	[Fact]
	public void FullInteractionSequence_ConvergesAfterEveryStep()
	{
		var s = CreateTradeSession();
		var driver = new SimulationDriver(s.Guest.Clock, s.Guest.Transport.Network, s.Host, s.Guest);

		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.MeetPlayer, Position = Pos() });
		driver.TickUntil(() => s.ReceivedCount() >= 1, maxMs: 1000);
		AssertConverged(s.Received[0], s.Trader.ToStateMsg());

		// A successful purchase: the +7 WantsTrade rep bump, the entry removed.
		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.Purchase, Position = Pos(), ItemId = "bandage" });
		driver.TickUntil(() => s.ReceivedCount() >= 2, maxMs: 1000);
		AssertConverged(s.Received[1], s.Trader.ToStateMsg());
		Assert.True(s.Trader.State.Items.All(i => i.Id != "bandage"), "the sold entry is removed");

		// A give — the credit lands (still under the 60 cap).
		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.GiveItem, Position = Pos(), ItemValue = 20 });
		driver.TickUntil(() => s.ReceivedCount() >= 3, maxMs: 1000);
		AssertConverged(s.Received[2], s.Trader.ToStateMsg());

		// The haggle, the threaten (no gun), the hug, the move.
		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.Haggle, Position = Pos() });
		driver.TickUntil(() => s.ReceivedCount() >= 4, maxMs: 1000);
		AssertConverged(s.Received[3], s.Trader.ToStateMsg());

		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.Threaten, Position = Pos() });
		driver.TickUntil(() => s.ReceivedCount() >= 5, maxMs: 1000);
		AssertConverged(s.Received[4], s.Trader.ToStateMsg());

		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.Hug, Position = Pos() });
		driver.TickUntil(() => s.ReceivedCount() >= 6, maxMs: 1000);
		AssertConverged(s.Received[5], s.Trader.ToStateMsg());

		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.MoveTo, Position = Pos() });
		driver.TickUntil(() => s.ReceivedCount() >= 7, maxMs: 1000);
		AssertConverged(s.Received[6], s.Trader.ToStateMsg());
	}

	[Fact]
	public void Purchase_BeyondCredit_RejectedMarkerArrives()
	{
		var s = CreateTradeSession();
		var driver = new SimulationDriver(s.Guest.Clock, s.Guest.Transport.Network, s.Host, s.Guest);

		// The rifle costs 100, the credit is 50 — the purchase is refused and the
		// marker must ride the broadcast (the acting side rolls its copy back).
		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.Purchase, Position = Pos(), ItemId = "rifle" });
		driver.TickUntil(() => s.ReceivedCount() >= 1, maxMs: 1000);

		Assert.True(s.Received[0].RejectedAction == (byte)TraderActionKind.Purchase, $"rejected marker expected, got {s.Received[0].RejectedAction}");
		AssertConverged(s.Received[0], s.Trader.ToStateMsg());
		Assert.True(s.Trader.State.Items.Any(i => i.Id == "rifle"), "the refused item stays in stock");
		Assert.True(Math.Abs(s.Trader.State.Reputation - (100f - 2f)) < 0.001f, "the refusal penalty (TraderScript.cs:800) applies");
	}

	[Fact]
	public void Interaction_UnderLinkDelay_StillConverges()
	{
		var s = CreateTradeSession();
		s.Host.Transport.Network.SetFaults(GuestId, HostId, new LinkFaults { DelayMs = 400 });
		s.Host.Transport.Network.SetFaults(HostId, GuestId, new LinkFaults { DelayMs = 400 });
		var driver = new SimulationDriver(s.Guest.Clock, s.Host.Transport.Network, s.Host, s.Guest);

		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.MeetPlayer, Position = Pos() });
		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.Haggle, Position = Pos() });
		Report(s.Guest, new TraderActionMsg { Action = TraderActionKind.MoveTo, Position = Pos() });
		driver.TickUntil(() => s.ReceivedCount() >= 3, maxMs: 5000);

		// The received sequence must be EXACTLY the host's broadcast sequence —
		// order and content (the reliable channel's FIFO + the full overwrite).
		Assert.True(s.Received.Count == s.HostBroadcasts.Count,
			$"every broadcast must arrive exactly once ({s.HostBroadcasts.Count} broadcasts, {s.Received.Count} received)");
		for (var i = 0; i < s.HostBroadcasts.Count; i++)
		{
			AssertConverged(s.Received[i], s.HostBroadcasts[i]);
		}

		Assert.True(s.Received[2].RejectedAction == 0, "no rejection on a delayed but valid sequence");
	}

	[Theory]
	[InlineData(11)]
	[InlineData(23)]
	[InlineData(37)]
	[InlineData(91)]
	public void RandomInteractionSequence_ConvergesAfterEveryStep(int seed)
	{
		var s = CreateTradeSession(seed);
		var driver = new SimulationDriver(s.Guest.Clock, s.Guest.Transport.Network, s.Host, s.Guest);
		var rng = new Random(seed);
		var expectedCount = 0;

		for (var step = 0; step < 40; step++)
		{
			var msg = RandomAction(rng, s.Trader);
			Report(s.Guest, msg);
			expectedCount++;
			driver.TickUntil(() => s.ReceivedCount() >= expectedCount, maxMs: 2000);

			AssertConverged(s.Received[expectedCount - 1], s.Trader.ToStateMsg());
		}
	}

	private static TraderActionMsg RandomAction(Random rng, SimTraderHost trader)
	{
		var kind = RandomActionKinds[rng.Next(RandomActionKinds.Length)];
		var msg = new TraderActionMsg
		{
			Action = kind,
			Position = Pos(),
		};
		switch (kind)
		{
			case TraderActionKind.Purchase:
				// Half the time a real stock entry (the price path), half an
				// unknown id (the not-found path) — both must converge.
				msg.ItemId = rng.NextDouble() < 0.5 && trader.State.Items.Count > 0
					? trader.State.Items[rng.Next(trader.State.Items.Count)].Id
					: $"unknown_{rng.Next(100)}";
				break;
			case TraderActionKind.GiveItem:
				msg.ItemValue = 1 + rng.Next(80); // straddles the 60 lifetime cap
				break;
			case TraderActionKind.MeetPlayer:
				msg.ReputationOffset = (float)(rng.NextDouble() * 40 - 20);
				msg.ReputationScale = rng.NextDouble() < 0.3 ? 0.7f : 1f; // the mindWipe branch
				msg.ReputationPostOffset = (float)(rng.NextDouble() * 30 - 15);
				break;
			case TraderActionKind.Threaten:
				if (rng.NextDouble() < 0.5)
				{
					msg.PlayerFlags |= TraderActionMsg.FlagHasGun;
				}

				break;
			case TraderActionKind.Hug:
				if (rng.NextDouble() < 0.3)
				{
					msg.PlayerFlags |= TraderActionMsg.FlagDirty;
				}

				break;
		}

		return msg;
	}

	private static NetVector2Msg Pos() => new(SimTraderHost.TraderPosX, SimTraderHost.TraderPosY);
}
