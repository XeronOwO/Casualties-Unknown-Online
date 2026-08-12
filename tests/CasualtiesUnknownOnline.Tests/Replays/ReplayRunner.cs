using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The replay executor: drives the shared-virtual-clock world (ItemSimWorld)
/// along the file's timeline — advancing the clock to each step's timestamp,
/// applying its world effect (one player operation = one message, the
/// phase-1 merge rule) or its fault (FakeNetwork per-link knobs), and running
/// the assertions. "within" assertions pump (TickUntil) until they hold or
/// the window expires — a replay that never converges fails. Every failure
/// reports source:line plus the executed trace so far (the archive's audit
/// path: file → step → what the world did). Semantic validation lives here:
/// numeric arguments, fault specs and assertion kinds fail with the same
/// source:line shape as the parser.
/// </summary>
internal static class ReplayRunner
{
	private const long TickStepMs = 33;

	internal static void Run(string sourceName, ItemSimWorld world, ReplayStep[] steps)
	{
		var executed = new List<string>();
		var clockMs = 0L;
		for (var i = 0; i < steps.Length; i++)
		{
			var step = steps[i];
			try
			{
				var advance = step.Ms - clockMs;
				if (advance > 0)
				{
					world.Driver.Tick(advance);
				}

				clockMs = step.Ms;
				executed.Add(step.ToString());
				Execute(world, step);
			}
			catch (Exception e) when (e is not ReplayStepException)
			{
				throw new ReplayStepException(sourceName, step, e.Message, executed, e);
			}
		}
	}

	private static void Execute(ItemSimWorld world, ReplayStep step)
	{
		switch (step.Action)
		{
			case "spawn":
				world.Spawn(world.Node(step.Args[0]), ItemId(step, 1), Item(step, 2, 3));
				break;
			case "pickup":
				world.Pickup(world.Node(step.Args[0]), ItemId(step, 1), Item(step, 2, 3));
				break;
			case "drop":
				world.Drop(world.Node(step.Args[0]), ItemId(step, 1), Item(step, 2, 3));
				break;
			case "use":
				world.Use(world.Node(step.Args[0]), ItemId(step, 1), Item(step, 2, 3));
				break;
			case "slot":
				world.Slot(world.Node(step.Args[0]), ItemId(step, 1), SlotIndex(step, 2), Item(step, 3, 4));
				break;
			case "destroy":
				world.Destroy(world.Node(step.Args[0]), ItemId(step, 1));
				break;
			case "fault":
				ApplyFault(world, step);
				break;
			case "clearfault":
				world.Driver.Network.ClearFaults(NodeId(world, step.Args[0]), NodeId(world, step.Args[1]));
				break;
			case "expect":
				Expect(world, step);
				break;
			case "expect_no_reject":
				var rejects = world.Rejects(world.Node(step.Args[0]));
				if (rejects.Count > 0)
				{
					var ids = string.Join(", ", rejects.Select(r => r.ItemId));
					Fail(step, $"expected no reject at this point, {step.Args[0]} already received [{ids}]");
				}

				break;
			default:
				throw new InvalidOperationException($"unhandled action '{step.Action}'");
		}
	}

	// ===== Argument conversion =====

	private static ulong ItemId(ReplayStep step, int index) =>
		ulong.TryParse(step.Args[index], out var id) ? id : throw new InvalidOperationException($"invalid item id '{step.Args[index]}'");

	private static int SlotIndex(ReplayStep step, int index) =>
		int.TryParse(step.Args[index], out var slot) ? slot : throw new InvalidOperationException($"invalid slot index '{step.Args[index]}'");

	private static CharacterItemMsg Item(ReplayStep step, int typeIndex, int conditionIndex)
	{
		var condition = float.TryParse(step.Args[conditionIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var c)
			? c
			: throw new InvalidOperationException($"invalid condition '{step.Args[conditionIndex]}'");
		return new CharacterItemMsg { ItemId = step.Args[typeIndex], Condition = condition };
	}

	private static ulong NodeId(ItemSimWorld world, string alias) => world.Node(alias).SteamId;

	// ===== Faults =====

	private static void ApplyFault(ItemSimWorld world, ReplayStep step)
	{
		var from = NodeId(world, step.Args[0]);
		var to = NodeId(world, step.Args[1]);
		var faults = new LinkFaults();
		foreach (var spec in step.Args.Skip(2))
		{
			if (spec == "down")
			{
				faults.Down = true;
			}
			else if (spec.StartsWith("delay=", StringComparison.Ordinal))
			{
				faults.DelayMs = ParseMs(spec, step);
			}
			else if (spec.StartsWith("drop=", StringComparison.Ordinal))
			{
				var rate = double.TryParse(spec.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
					? r
					: throw new InvalidOperationException($"invalid drop rate '{spec}'");
				if (rate < 0 || rate > 1)
				{
					throw new InvalidOperationException($"drop rate must be within 0..1, got {rate}");
				}

				faults.UnreliableDropRate = rate;
			}
			else if (spec == "dup")
			{
				faults.Duplicate = true;
			}
			else
			{
				throw new InvalidOperationException($"unknown fault spec '{spec}' (down / delay=<ms> / drop=<rate> / dup)");
			}
		}

		world.Driver.Network.SetFaults(from, to, faults);
	}

	private static long ParseMs(string spec, ReplayStep step) =>
		long.TryParse(spec.Substring(6), out var ms) ? ms : throw new InvalidOperationException($"invalid delay '{spec}'");

	// ===== Assertions =====

	private static void Expect(ItemSimWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		var withinIdx = Array.IndexOf(step.Args, "within");
		var withinMs = 0L;
		if (withinIdx >= 0)
		{
			if (withinIdx + 1 >= step.Args.Length || !long.TryParse(step.Args[withinIdx + 1], out withinMs))
			{
				throw new InvalidOperationException($"invalid 'within <ms>' in expect line");
			}
		}

		Func<bool> predicate = kind switch
		{
			"host_table" => () => world.HostTable(ItemId(step, 1)),
			"no_host_table" => () => !world.HostTable(ItemId(step, 1)),
			"reject" => () => world.Rejects(world.Node(step.Args[1])).Any(r => r.ItemId == ItemId(step, 2)),
			"received" => () => world.ReceivedCount(world.Node(step.Args[1]), MessageName(step, 2)) == Count(step, 3),
			_ => throw new InvalidOperationException($"unknown assertion '{kind}' (host_table / no_host_table / reject / received)"),
		};

		if (withinIdx >= 0)
		{
			if (!predicate())
			{
				try
				{
					world.Driver.TickUntil(predicate, withinMs, TickStepMs);
				}
				catch (InvalidOperationException e)
				{
					throw new InvalidOperationException($"{DescribeExpectation(step)} — {ActualState(world, step)} ({e.Message})");
				}
			}
		}
		else if (!predicate())
		{
			Fail(step, $"{DescribeExpectation(step)} — {ActualState(world, step)}");
		}
	}

	private static NetMsg MessageName(ReplayStep step, int index) =>
		Enum.TryParse<NetMsg>(step.Args[index], out var msg) ? msg : throw new InvalidOperationException($"unknown message name '{step.Args[index]}'");

	private static int Count(ReplayStep step, int index) =>
		int.TryParse(step.Args[index], out var count) ? count : throw new InvalidOperationException($"invalid count '{step.Args[index]}'");

	private static string DescribeExpectation(ReplayStep step) => step.Action switch
	{
		"expect" => $"'{step.Args[0]}' did not hold — {string.Join(" ", step.Args)}",
		_ => step.ToString(),
	};

	/// <summary>The observed world state behind a failed assertion (actual vs expected).</summary>
	private static string ActualState(ItemSimWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		return kind switch
		{
			"host_table" => $"table {(world.HostTable(ItemId(step, 1)) ? "has" : "lacks")} {ItemId(step, 1)}",
			"no_host_table" => $"table {(world.HostTable(ItemId(step, 1)) ? "still has" : "already lacks")} {ItemId(step, 1)}",
			"reject" => $"{step.Args[1]} rejects: [{string.Join(", ", world.Rejects(world.Node(step.Args[1])).Select(r => r.ItemId))}]",
			"received" => $"{step.Args[1]} received {world.ReceivedCount(world.Node(step.Args[1]), MessageName(step, 2))} {step.Args[2]} frames (expected {step.Args[3]})",
			_ => string.Empty,
		};
	}

	private static void Fail(ReplayStep step, string message) => throw new InvalidOperationException(message);

	/// <summary>A step failed: source:line + what failed + the executed trace so far.</summary>
	internal sealed class ReplayStepException : Exception
	{
		internal ReplayStepException(string sourceName, ReplayStep step, string message, List<string> executed, Exception inner)
			: base(BuildMessage(sourceName, step, message, executed), inner)
		{
		}

		private static string BuildMessage(string sourceName, ReplayStep step, string message, List<string> executed)
		{
			var sb = new StringBuilder();
			sb.Append($"{sourceName}:{step.Line}: step [{step}] failed — {message}");
			if (executed.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine("executed trace:");
				foreach (var line in executed)
				{
					sb.AppendLine($"  {line}");
				}
			}

			return sb.ToString();
		}
	}
}
