using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.World;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The two-node trade replay world: a handshaken host + guest on the shared
/// virtual clock, with the production-shaped trade executor wired to the real
/// <see cref="TradeStockMachine"/> (via <see cref="SimTraderHost"/>). A
/// guest's interaction is executed on the host and broadcast back as a FULL
/// state overwrite; the guest's received sequence is the replay assertion
/// surface (order, content, rejection marker).
/// </summary>
internal sealed class TradeReplayWorld : IDisposable
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private readonly List<TraderStateMsg> _received = [];
	private readonly List<TraderStateMsg> _hostBroadcasts = [];

	private TradeReplayWorld(SimulationDriver driver, TestNode host, TestNode guest, SimTraderHost trader)
	{
		Driver = driver;
		Host = host;
		Guest = guest;
		Trader = trader;
	}

	internal SimulationDriver Driver { get; }

	internal TestNode Host { get; }

	internal TestNode Guest { get; }

	internal SimTraderHost Trader { get; }

	/// <summary>How many authoritative overwrites the host has produced (cumulative).</summary>
	internal int HostBroadcastCount
	{
		get
		{
			lock (_hostBroadcasts)
			{
				return _hostBroadcasts.Count;
			}
		}
	}

	/// <summary>The latest authoritative overwrite the host produced (the immediate-result surface).</summary>
	internal TraderStateMsg? LastHostBroadcast
	{
		get
		{
			lock (_hostBroadcasts)
			{
				return _hostBroadcasts.Count > 0 ? _hostBroadcasts[_hostBroadcasts.Count - 1] : null;
			}
		}
	}

	/// <summary>How many authoritative overwrites the guest has received (cumulative).</summary>
	internal int ReceivedCount
	{
		get
		{
			lock (_received)
			{
				return _received.Count;
			}
		}
	}

	/// <summary>The latest overwrite the guest received (the convergence surface).</summary>
	internal TraderStateMsg? LastReceived
	{
		get
		{
			lock (_received)
			{
				return _received.Count > 0 ? _received[_received.Count - 1] : null;
			}
		}
	}

	/// <summary>Resolve a replay-file node alias (trade files use host/g1 — g1 is the guest).</summary>
	internal TestNode Node(string alias) => alias switch
	{
		"host" => Host,
		"g1" => Guest,
		_ => throw new ArgumentException($"unknown node alias '{alias}' (host/g1)"),
	};

	public void Dispose()
	{
		Host.Dispose();
		Guest.Dispose();
	}

	internal static TradeReplayWorld Create(int seed = 1)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var driver = new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest);
		driver.TickUntil(
			() => host.Session.Members.Count(m => m.Handshaken) == 1 && guest.Session.Members.Any(m => m.Handshaken),
			maxMs: 5000);

		var trader = new SimTraderHost(SimTraderHost.CreateDefaultState(), seed);
		var world = new TradeReplayWorld(driver, host, guest, trader);

		var hostWorld = host.Services.GetRequiredService<IWorldControl>();
		hostWorld.TraderActionReceived += (_, msg) =>
		{
			// The production TradeStateSync.OnTraderActionReceived shape: the
			// real machine executes the trader-side change, then the
			// authoritative full overwrite (with the rejection marker) goes out.
			var rejected = trader.Execute(msg);
			var state = trader.ToStateMsg();
			state.RejectedAction = rejected;
			lock (world._hostBroadcasts)
			{
				world._hostBroadcasts.Add(state);
			}

			hostWorld.BroadcastTraderState(state);
		};

		guest.Services.GetRequiredService<IWorldControl>().TraderStateReceived += msg =>
		{
			lock (world._received)
			{
				world._received.Add(msg);
			}
		};

		return world;
	}

	/// <summary>One locally-executed trader interaction reported over the wire.</summary>
	internal void Report(TestNode guest, TraderActionMsg msg) =>
		guest.Services.GetRequiredService<IWorldControl>().SendTraderAction(msg);

	/// <summary>The received overwrite equals the host's authoritative state (full-fidelity comparison — every field, every stock entry).</summary>
	internal bool IsConverged(TraderStateMsg received)
	{
		var expected = Trader.ToStateMsg();
		if (Math.Abs(received.Reputation - expected.Reputation) >= 0.001f
			|| Math.Abs(received.Hostility - expected.Hostility) >= 0.001f
			|| received.ValueGiven != expected.ValueGiven
			|| received.TotalValueGiven != expected.TotalValueGiven
			|| received.FreeAmount != expected.FreeAmount
			|| received.FreeDressing != expected.FreeDressing
			|| received.DidHug != expected.DidHug
			|| received.DidMove != expected.DidMove
			|| received.StartedConvo != expected.StartedConvo
			|| Math.Abs(received.HaggleAmount - expected.HaggleAmount) >= 0.001f)
		{
			return false;
		}

		if (received.Items.Length != expected.Items.Length)
		{
			return false;
		}

		for (var i = 0; i < expected.Items.Length; i++)
		{
			if (received.Items[i].Id != expected.Items[i].Id
				|| received.Items[i].Value != expected.Items[i].Value
				|| received.Items[i].Preference != expected.Items[i].Preference
				|| received.Items[i].Bought != expected.Items[i].Bought)
			{
				return false;
			}
		}

		return true;
	}
}
