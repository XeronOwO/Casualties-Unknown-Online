using System;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.World;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The trade replay executor (ReplayRunner partial — the 600-line split):
/// drives the TradeReplayWorld timeline with the trade action (one locally
/// executed interaction = one TraderActionMsg), the shared fault/clearfault
/// actions and the trade-domain assertions. The SimTrace result is the
/// immediate host-executor surface: Committed(1) = the authoritative
/// overwrite was produced and accepted, Rejected(kind) = the overwrite
/// carried a rejection marker, Sent = the report is still in flight under a
/// link fault (the later convergence assertion is the verdict).
/// </summary>
internal static partial class ReplayRunner
{
	internal static void Run(string sourceName, TradeReplayWorld world, ReplayStep[] steps, SimTrace simTrace) =>
		RunCore(sourceName, steps, step => Execute(world, step, simTrace), world.Driver, simTrace);

	private static void Execute(TradeReplayWorld world, ReplayStep step, SimTrace simTrace)
	{
		switch (step.Action)
		{
			case "trade":
				ExecuteTrade(world, step, simTrace);
				break;
			case "fault":
				ApplyFault(world.Driver, world.Node, step);
				break;
			case "clearfault":
				world.Driver.Network.ClearFaults(NodeId(world.Node(step.Args[0])), NodeId(world.Node(step.Args[1])));
				break;
			case "expect":
				Expect(world, step);
				break;
			default:
				throw new InvalidOperationException($"unhandled action '{step.Action}' (trade replay files run on the trade world)");
		}
	}

	private static void ExecuteTrade(TradeReplayWorld world, ReplayStep step, SimTrace simTrace)
	{
		var guest = world.Node(step.Args[0]);
		if (guest != world.Guest)
		{
			throw new InvalidOperationException("trade actions must originate from g1 — the guest reports its locally-executed interaction");
		}

		var msg = BuildTradeAction(step);
		var beforeBroadcasts = world.HostBroadcastCount;
		var op = simTrace.Begin(0, "trade", $"Trade:{msg.Action}");
		world.Report(guest, msg);

		var newBroadcasts = world.HostBroadcastCount - beforeBroadcasts;
		if (newBroadcasts > 0)
		{
			var last = world.LastHostBroadcast;
			if (last is null)
			{
				throw new InvalidOperationException("host broadcast count advanced without a broadcast being recorded");
			}

			var result = last.RejectedAction == 0 ? "Committed(1)" : $"Rejected({last.RejectedAction})";
			simTrace.End(op, 0, "trade", result, $"Trade:{msg.Action}");
		}
		else
		{
			// A link fault (delay/down) kept the report from reaching the host
			// during this step — the report is in flight and the following
			// trade_received/trade_converged expectation is the verdict.
			simTrace.End(op, 0, "trade", "Sent", $"Trade:{msg.Action}");
		}
	}

	/// <summary>Trade grammar: "trade g1 &lt;kind&gt; [item=&lt;id&gt;] [value=&lt;n&gt;]
	/// [offset=&lt;n&gt;] [scale=&lt;n&gt;] [post=&lt;n&gt;] [bleeding] [hasgun] [dirty]".
	/// Kinds: meet / purchase / give / haggle / threaten / hug / move.</summary>
	private static TraderActionMsg BuildTradeAction(ReplayStep step)
	{
		var kind = step.Args[1] switch
		{
			"meet" => TraderActionKind.MeetPlayer,
			"purchase" => TraderActionKind.Purchase,
			"give" => TraderActionKind.GiveItem,
			"haggle" => TraderActionKind.Haggle,
			"threaten" => TraderActionKind.Threaten,
			"hug" => TraderActionKind.Hug,
			"move" => TraderActionKind.MoveTo,
			_ => throw new InvalidOperationException($"unknown trade kind '{step.Args[1]}' (meet/purchase/give/haggle/threaten/hug/move)"),
		};

		var msg = new TraderActionMsg
		{
			Action = kind,
			Position = new NetVector2Msg(SimTraderHost.TraderPosX, SimTraderHost.TraderPosY),
			ReputationScale = 1f,
		};

		string? itemId = null;
		int? itemValue = null;
		foreach (var arg in step.Args.Skip(2))
		{
			if (arg == "bleeding")
			{
				msg.PlayerFlags |= TraderActionMsg.FlagBleeding;
				continue;
			}

			if (arg == "hasgun")
			{
				msg.PlayerFlags |= TraderActionMsg.FlagHasGun;
				continue;
			}

			if (arg == "dirty")
			{
				msg.PlayerFlags |= TraderActionMsg.FlagDirty;
				continue;
			}

			var eq = arg.IndexOf('=');
			if (eq <= 0)
			{
				throw new InvalidOperationException($"unknown trade argument '{arg}' (item=/value=/offset=/scale=/post=/bleeding/hasgun/dirty)");
			}

			var key = arg.Substring(0, eq);
			var value = arg.Substring(eq + 1);
			switch (key)
			{
				case "item":
					itemId = value;
					break;
				case "value":
					if (!int.TryParse(value, out var parsedValue))
					{
						throw new InvalidOperationException($"invalid trade value '{value}'");
					}

					itemValue = parsedValue;
					break;
				case "offset":
					msg.ReputationOffset = FloatText(step, value);
					break;
				case "scale":
					msg.ReputationScale = FloatText(step, value);
					break;
				case "post":
					msg.ReputationPostOffset = FloatText(step, value);
					break;
				default:
					throw new InvalidOperationException($"unknown trade argument '{arg}' (item=/value=/offset=/scale=/post=/bleeding/hasgun/dirty)");
			}
		}

		switch (kind)
		{
			case TraderActionKind.Purchase:
				if (itemId is null)
				{
					throw new InvalidOperationException("purchase needs item=<id>");
				}

				msg.ItemId = itemId;
				break;
			case TraderActionKind.GiveItem:
				if (itemValue is null)
				{
					throw new InvalidOperationException("give needs value=<n>");
				}

				msg.ItemValue = itemValue.Value;
				break;
			case TraderActionKind.MeetPlayer:
				break;
			default:
				if (itemId is not null || itemValue is not null)
				{
					throw new InvalidOperationException("item= is purchase-only and value= is give-only");
				}

				break;
		}

		return msg;
	}

	private static float FloatText(ReplayStep step, string text) =>
		float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
			? value
			: throw new InvalidOperationException($"invalid number '{text}'");

	private static void Expect(TradeReplayWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		Func<bool> predicate = kind switch
		{
			"trade_received" => () => world.Node(step.Args[1]) == world.Guest && world.ReceivedCount == Count(step, 2),
			"trade_converged" => () => world.Node(step.Args[1]) == world.Guest && world.LastReceived is { } received && world.IsConverged(received),
			"trade_rejected" => () => world.Node(step.Args[1]) == world.Guest && world.LastReceived is { } state && state.RejectedAction == TradeAction(step, 2),
			_ => throw new InvalidOperationException($"unknown assertion '{kind}' (trade_received / trade_converged / trade_rejected)"),
		};
		RunExpectation(world.Driver, step, predicate, () => ActualState(world, step));
	}

	private static byte TradeAction(ReplayStep step, int index) =>
		step.Args[index] switch
		{
			"meet" => (byte)TraderActionKind.MeetPlayer,
			"purchase" => (byte)TraderActionKind.Purchase,
			"give" => (byte)TraderActionKind.GiveItem,
			"haggle" => (byte)TraderActionKind.Haggle,
			"threaten" => (byte)TraderActionKind.Threaten,
			"hug" => (byte)TraderActionKind.Hug,
			"move" => (byte)TraderActionKind.MoveTo,
			_ => throw new InvalidOperationException($"unknown trade kind '{step.Args[index]}' in assertion"),
		};

	private static string ActualState(TradeReplayWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		return kind switch
		{
			"trade_received" => $"g1 received {world.ReceivedCount} TraderState frame(s) (expected {step.Args[2]})",
			"trade_converged" => world.LastReceived is { } received && world.IsConverged(received)
				? "g1's latest state converged"
				: "g1's latest state has not converged to the host authority",
			"trade_rejected" => world.LastReceived is { } state
				? $"g1's latest RejectedAction = {state.RejectedAction} (expected {step.Args[2]})"
				: "g1 has received no TraderState yet",
			_ => string.Empty,
		};
	}
}
