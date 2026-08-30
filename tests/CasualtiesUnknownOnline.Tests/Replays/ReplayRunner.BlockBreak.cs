using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The block-break replay executor (ReplayRunner partial — the 600-line split):
/// drives the BlockBreakReplayWorld timeline with the airwrite/break actions,
/// the shared fault/clearfault actions and the block-domain assertions. One
/// break = one BlockDamagedMsg carrying the drops (the production one-message
/// one-verdict shape); the SimTrace result is the immediate arbitration
/// surface (Committed(1) = accepted + registered + relayed, Rejected(1) = the
/// drops were rolled back to the breaker).
/// </summary>
internal static partial class ReplayRunner
{
	internal static void Run(string sourceName, BlockBreakReplayWorld world, ReplayStep[] steps, SimTrace simTrace) =>
		RunCore(sourceName, steps, step => Execute(world, step, simTrace), world.Driver, simTrace);

	private static void Execute(BlockBreakReplayWorld world, ReplayStep step, SimTrace simTrace)
	{
		switch (step.Action)
		{
			case "airwrite":
				{
					var node = world.Node(step.Args[0]);
					if (node == world.Host)
					{
						throw new InvalidOperationException("airwrite records a guest's applied air-write — the node must be g1/g2");
					}

					world.AirWrite(node, Int(step, 1), Int(step, 2));
					break;
				}

			case "break":
				ExecuteBreak(world, step, simTrace);
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
				throw new InvalidOperationException($"unhandled action '{step.Action}' (block-break replay files run on the block-break world)");
		}
	}

	private static void ExecuteBreak(BlockBreakReplayWorld world, ReplayStep step, SimTrace simTrace)
	{
		var node = world.Node(step.Args[0]);
		if (node == world.Host)
		{
			throw new InvalidOperationException("break actions must originate from g1/g2 — the host applies its own breaks locally");
		}

		var cellX = Int(step, 1);
		var cellY = Int(step, 2);
		var drops = ParseDrops(step, out var metalBonus);

		var beforeAccepted = world.AcceptedCount;
		var beforeRejects = world.ItemRejectsReceived(node);
		var op = simTrace.Begin(0, "block", "Break");
		world.Break(node, cellX, cellY, drops, metalBonus);

		// The fake network delivers no-delay sends synchronously, so the host's
		// verdict (accept + relay, or reject the drops) is already observable.
		var acceptedDelta = world.AcceptedCount - beforeAccepted;
		var rejectDelta = world.ItemRejectsReceived(node) - beforeRejects;
		if (acceptedDelta > 0)
		{
			simTrace.End(op, 0, "block", "Committed(1)", "Break", "Drop");
		}
		else if (rejectDelta > 0)
		{
			simTrace.End(op, 0, "block", "Rejected(1)", "Break", "Drop");
		}
		else
		{
			throw new InvalidOperationException("break produced no verdict — a break with drops must be accepted (recorded air-write) or rejected (ItemReject)");
		}
	}

	/// <summary>Break grammar: "drops=&lt;id&gt;:&lt;itemId&gt;[|...]" is required
	/// (a break carries its drops — the block-break domain's one-message
	/// one-verdict rule); "metal" is the optional bonus-metal flag.</summary>
	private static List<BlockDropEntryMsg> ParseDrops(ReplayStep step, out bool metalBonus)
	{
		var drops = new List<BlockDropEntryMsg>();
		metalBonus = false;
		foreach (var arg in step.Args.Skip(3))
		{
			if (arg == "metal")
			{
				metalBonus = true;
				continue;
			}

			if (!arg.StartsWith("drops=", StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"unknown break argument '{arg}' (drops=<id>:<itemId>[|...] / metal)");
			}

			var spec = arg.Substring("drops=".Length);
			foreach (var part in spec.Split('|'))
			{
				var fields = part.Split(':');
				if (fields.Length != 2 || !ulong.TryParse(fields[0], out var itemId) || fields[1].Length == 0)
				{
					throw new InvalidOperationException($"invalid drop spec '{part}' (drops=<id>:<itemId>[|...])");
				}

				drops.Add(new BlockDropEntryMsg
				{
					ItemId = itemId,
					Item = new CharacterItemMsg { ItemId = fields[1], Condition = 1f },
				});
			}
		}

		if (drops.Count == 0)
		{
			throw new InvalidOperationException("break needs drops=<id>:<itemId> — the block-break replay domain models breaks, not damage-only reports");
		}

		return drops;
	}

	private static void Expect(BlockBreakReplayWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		Func<bool> predicate = kind switch
		{
			"block_accepted" => () => world.AcceptedCount == Count(step, 1),
			"block_accepted_by" => () => world.AcceptedBy(world.Node(step.Args[1])) == Count(step, 2),
			"block_received" => () => MessageName(step, 2) == NetMsg.BlockDamaged && world.BlockDamagedReceived(world.Node(step.Args[1])) == Count(step, 3),
			"block_reject" => () => MessageName(step, 2) == NetMsg.KernelEnvelope && world.ItemRejectsReceived(world.Node(step.Args[1])) == Count(step, 3),
			"block_registered" => () => world.IsDropRegistered(ItemId(step, 1)),
			_ => throw new InvalidOperationException($"unknown assertion '{kind}' (block_accepted / block_accepted_by / block_received / block_reject / block_registered)"),
		};
		RunExpectation(world.Driver, step, predicate, () => ActualState(world, step));
	}

	private static string ActualState(BlockBreakReplayWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		return kind switch
		{
			"block_accepted" => $"host accepted {world.AcceptedCount} break(s) (expected {step.Args[1]})",
			"block_accepted_by" => $"{step.Args[1]} accepted {world.AcceptedBy(world.Node(step.Args[1]))} time(s) (expected {step.Args[2]})",
			"block_received" => $"{step.Args[1]} received {world.BlockDamagedReceived(world.Node(step.Args[1]))} BlockDamaged frame(s) (expected {step.Args[3]})",
			"block_reject" => $"{step.Args[1]} received {world.ItemRejectsReceived(world.Node(step.Args[1]))} KernelEnvelope CommandRejected frame(s) (expected {step.Args[3]})",
			"block_registered" => $"drop {step.Args[1]} is {(world.IsDropRegistered(ItemId(step, 1)) ? "registered" : "not registered")} in the host item table",
			_ => string.Empty,
		};
	}
}
