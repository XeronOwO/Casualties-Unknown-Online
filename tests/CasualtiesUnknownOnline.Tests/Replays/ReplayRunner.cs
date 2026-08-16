using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.World;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The replay executor: drives the shared-virtual-clock world (ItemSimWorld
/// for item files, EntityEventSimWorld for entity/fluid files) along the
/// file's timeline — advancing the clock to each step's timestamp, applying
/// its world effect (one player operation = one message, the phase-1 merge
/// rule) or its fault (FakeNetwork per-link knobs), and running the
/// assertions. "within" assertions pump (TickUntil) until they hold or the
/// window expires — a replay that never converges fails. Every failure
/// reports source:line plus the executed trace so far (the archive's audit
/// path: file → step → what the world did). Semantic validation lives here:
/// numeric arguments, fault specs and assertion kinds fail with the same
/// source:line shape as the parser. Every operation action also emits a
/// SimTrace line pair (the OperationTrace format — see SimTrace), written to
/// SimTraces/{file}.trace so the simulation's result sequence is diffable
/// against the game's real trace of the same gesture sequence.
/// </summary>
internal static partial class ReplayRunner
{
	private const long TickStepMs = 33;

	internal static void Run(string sourceName, ItemSimWorld world, ReplayStep[] steps, SimTrace simTrace) =>
		RunCore(sourceName, steps, step => Execute(world, step, simTrace), world.Driver, simTrace);

	internal static void Run(string sourceName, EntityEventSimWorld world, ReplayStep[] steps, SimTrace simTrace) =>
		RunCore(sourceName, steps, step => Execute(world, step, simTrace), world.Driver, simTrace);

	/// <summary>The SimTrace output path for a replay file (the ps1-diffable trace).</summary>
	internal static string SimTracePath(string sourceName) =>
		Path.Combine(AppContext.BaseDirectory, "SimTraces", sourceName + ".trace");

	private static void RunCore(string sourceName, ReplayStep[] steps, Action<ReplayStep> execute, SimulationDriver driver, SimTrace simTrace)
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
					driver.Tick(advance);
				}

				clockMs = step.Ms;
				executed.Add(step.ToString());
				execute(step);
			}
			catch (Exception e) when (e is not ReplayStepException)
			{
				throw new ReplayStepException(sourceName, step, e.Message, executed, e);
			}
		}

		simTrace.WriteTo(SimTracePath(sourceName));
	}

	// ===== Item world =====

	private static void Execute(ItemSimWorld world, ReplayStep step, SimTrace simTrace)
	{
		switch (step.Action)
		{
			case "spawn":
			case "pickup":
			case "drop":
			case "use":
			case "slot":
			case "destroy":
				ExecuteItemOperation(world, step, simTrace);
				break;
			case "craft":
				ExecuteCraft(world, step, simTrace);
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
			case "expect_no_reject":
				var rejects = world.Rejects(world.Node(step.Args[0]));
				if (rejects.Count > 0)
				{
					var ids = string.Join(", ", rejects.Select(r => r.ItemId));
					Fail(step, $"expected no reject at this point, {step.Args[0]} already received [{ids}]");
				}

				break;
			default:
				throw new InvalidOperationException($"unhandled action '{step.Action}' (item replay files run on the item world)");
		}
	}

	/// <summary>An item operation with its SimTrace pair: the begin line is
	/// written BEFORE the effect, the end line reports the visible result — a
	/// new reject = Rejected (the arbitration surface), a host-table flip for
	/// the item = Committed(1) (the registration/transfer landed — the source
	/// is excluded from the broadcast, so its own wire shows nothing), frames
	/// on either wire = Committed(n) (the use/slot broadcast surface), nothing
	/// = Skipped (an idempotency guard swallowed the duplicate — the
	/// silent-rejection family).</summary>
	private static void ExecuteItemOperation(ItemSimWorld world, ReplayStep step, SimTrace simTrace)
	{
		var action = step.Action;
		var node = world.Node(step.Args[0]);
		var itemId = ItemId(step, 1);
		var beforeRejects = world.Rejects(node).Count;
		var beforeFrames = world.ReceivedTotal(node);
		var beforeOthersFrames = world.ReceivedTotal(OtherGuest(world, node));
		var tableBefore = world.HostTable(itemId);
		var op = simTrace.Begin(itemId, action, action);
		switch (action)
		{
			case "spawn":
				world.Spawn(node, itemId, Item(step, 2, 3));
				break;
			case "pickup":
				world.Pickup(node, itemId, Item(step, 2, 3));
				break;
			case "drop":
				world.Drop(node, itemId, Item(step, 2, 3));
				break;
			case "use":
				world.Use(node, itemId, Item(step, 2, 3));
				break;
			case "slot":
				world.Slot(node, itemId, SlotIndex(step, 2), Item(step, 3, 4));
				break;
			case "destroy":
				world.Destroy(node, itemId);
				break;
		}

		var newRejects = world.Rejects(node).Count - beforeRejects;
		if (newRejects > 0)
		{
			simTrace.End(op, itemId, action, "Rejected", action);
		}
		else if (world.HostTable(itemId) != tableBefore)
		{
			simTrace.End(op, itemId, action, "Committed(1)", action);
		}
		else
		{
			var newFrames = (world.ReceivedTotal(node) - beforeFrames) + (world.ReceivedTotal(OtherGuest(world, node)) - beforeOthersFrames);
			simTrace.End(op, itemId, action, newFrames > 0 ? $"Committed({newFrames})" : "Skipped", action);
		}
	}

	/// <summary>One crafting operation (the one-operation-one-report convention):
	/// the complete terminal state as one CraftReportMsg. Grammar:
	/// entries = "d:&lt;id&gt;" (destroyed) | "c:&lt;id&gt;:&lt;cond&gt;" (changed), "|"-joined,
	/// "-" = none; products = "p:&lt;id&gt;:&lt;type&gt;:&lt;cond&gt;", "|"-joined, "-" = none.
	/// The trace's item id is 0 (an operation, not an item — the entity-actions
	/// precedent) and the result is the relay surface (frames on either wire).</summary>
	private static void ExecuteCraft(ItemSimWorld world, ReplayStep step, SimTrace simTrace)
	{
		var node = world.Node(step.Args[0]);
		var kind = step.Args[1] switch
		{
			"craft" => CraftOperationKind.Craft,
			"combine" => CraftOperationKind.Combine,
			"liquid" => CraftOperationKind.LiquidTransfer,
			_ => throw new InvalidOperationException($"unknown craft kind '{step.Args[1]}' (craft/combine/liquid)"),
		};
		var msg = new CraftReportMsg { Kind = kind, Entries = CraftEntries(step, 2), Products = CraftProducts(step, 3) };

		var beforeFrames = world.ReceivedTotal(node) + world.ReceivedTotal(OtherGuest(world, node));
		var op = simTrace.Begin(0, "craft", "Craft");
		world.Craft(node, msg);
		var newFrames = (world.ReceivedTotal(node) - beforeFrames) + (world.ReceivedTotal(OtherGuest(world, node)) - beforeFrames);
		simTrace.End(op, 0, "craft", newFrames > 0 ? $"Committed({newFrames})" : "Skipped", "Craft");
	}

	private static List<CraftEntryMsg> CraftEntries(ReplayStep step, int index)
	{
		if (step.Args[index] == "-")
		{
			return [];
		}

		var entries = new List<CraftEntryMsg>();
		foreach (var part in step.Args[index].Split('|'))
		{
			var p = part.Split(':');
			entries.Add(p[0] switch
			{
				"d" => new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Destroyed,
					Item = new CharacterItemMsg { InstanceId = ulong.Parse(p[1]), ItemId = "material" },
				},
				"c" => new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Changed,
					Item = new CharacterItemMsg
					{
						InstanceId = ulong.Parse(p[1]),
						ItemId = "material",
						Condition = float.Parse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture),
					},
				},
				_ => throw new InvalidOperationException($"unknown craft entry '{part}' (d:<id>|c:<id>:<cond>)"),
			});
		}

		return entries;
	}

	private static List<CharacterItemMsg> CraftProducts(ReplayStep step, int index)
	{
		if (step.Args[index] == "-")
		{
			return [];
		}

		var products = new List<CharacterItemMsg>();
		foreach (var part in step.Args[index].Split('|'))
		{
			var p = part.Split(':');
			if (p[0] != "p")
			{
				throw new InvalidOperationException($"unknown craft product '{part}' (p:<id>:<type>:<cond>)");
			}

			products.Add(new CharacterItemMsg
			{
				InstanceId = ulong.Parse(p[1]),
				ItemId = p[2],
				Condition = float.Parse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture),
				SlotIndex = 3,
			});
		}

		return products;
	}

	private static TestNode OtherGuest(ItemSimWorld world, TestNode node) => node == world.G1 ? world.G2 : world.G1;

	// ===== Entity world =====

	private static void Execute(EntityEventSimWorld world, ReplayStep step, SimTrace simTrace)
	{
		switch (step.Action)
		{
			case "event":
			case "snapshot":
			case "fluid":
				ExecuteEntityOperation(world, step, simTrace);
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
				throw new InvalidOperationException($"unhandled action '{step.Action}' (entity replay files run on the entity world)");
		}
	}

	private static void ExecuteEntityOperation(EntityEventSimWorld world, ReplayStep step, SimTrace simTrace)
	{
		switch (step.Action)
		{
			case "event":
				{
					var kind = EntityKind(step, 1);
					var before = world.HostExecutions.Value;
					var op = simTrace.Begin(0, "event", "Event");
					world.Trigger(world.Node(step.Args[0]), kind, Float(step, 2), Float(step, 3), step.Args.Length > 4 ? Byte(step, 4) : (byte)0);
					simTrace.End(op, 0, "event", world.HostExecutions.Value - before > 0 ? "Committed(1)" : "Skipped", "Event");
					break;
				}

			case "snapshot":
				{
					var node = world.Node(step.Args[0]);
					var before = world.Snapshots(node).Count;
					var op = simTrace.Begin(0, "snapshot", "Snapshot");
					world.HostChannel.SendTrapStateSnapshot(node.SteamId);
					var snapshots = world.Snapshots(node);
					var entries = snapshots.Count > before ? snapshots[snapshots.Count - 1].Count : 0;
					simTrace.End(op, 0, "snapshot", entries > 0 ? $"Committed({entries})" : "Skipped", "Snapshot");
					break;
				}

			case "fluid":
				{
					var node = world.Node(step.Args[0]);
					var x = Int(step, 1);
					var y = Int(step, 2);
					var w = Int(step, 3);
					var h = Int(step, 4);
					if (w < 1 || w > 255 || h < 1 || h > 255)
					{
						throw new InvalidOperationException($"fluid region dimensions must be within 1..255, got {w}x{h}");
					}

					var runs = step.Args.Skip(5).ToArray();
					var maxBytes = w * h * 2; // RLE: at most one value/count pair per cell
					if (runs.Length < 2 || runs.Length % 2 != 0 || runs.Length > maxBytes)
					{
						throw new InvalidOperationException($"fluid region {w}x{h} needs RLE runs as value/count byte pairs (2..{maxBytes} bytes), got {runs.Length}");
					}

					var cells = new byte[runs.Length];
					for (var i = 0; i < runs.Length; i++)
					{
						cells[i] = Byte(step, 5 + i);
					}

					var before = world.FluidRegions(node);
					var op = simTrace.Begin(0, "fluid", "Fluid");
					world.HostChannel.SendFluidRegion(node.SteamId, new FluidRegionMsg
					{
						Seq = (byte)(step.Line % 256), // per-file monotonic — the unreliable stream's ordering key
						OriginX = x,
						OriginY = y,
						Width = (byte)w,
						Height = (byte)h,
						Cells = cells,
					});
					simTrace.End(op, 0, "fluid", world.FluidRegions(node) - before > 0 ? "Committed(1)" : "Skipped", "Fluid");
					break;
				}

			default:
				throw new InvalidOperationException($"unhandled entity action '{step.Action}'");
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

	private static float Float(ReplayStep step, int index) =>
		float.TryParse(step.Args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
			? value
			: throw new InvalidOperationException($"invalid number '{step.Args[index]}'");

	private static int Int(ReplayStep step, int index) =>
		int.TryParse(step.Args[index], out var value) ? value : throw new InvalidOperationException($"invalid number '{step.Args[index]}'");

	private static byte Byte(ReplayStep step, int index) =>
		byte.TryParse(step.Args[index], out var value) ? value : throw new InvalidOperationException($"invalid byte '{step.Args[index]}'");

	private static EntityEventKind EntityKind(ReplayStep step, int index) =>
		Enum.TryParse<EntityEventKind>(step.Args[index], out var kind)
			? kind
			: throw new InvalidOperationException($"unknown entity kind '{step.Args[index]}'");

	private static ulong NodeId(TestNode node) => node.SteamId;

	// ===== Faults =====

	private static void ApplyFault(SimulationDriver driver, Func<string, TestNode> resolveNode, ReplayStep step)
	{
		var from = NodeId(resolveNode(step.Args[0]));
		var to = NodeId(resolveNode(step.Args[1]));
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

		driver.Network.SetFaults(from, to, faults);
	}

	private static long ParseMs(string spec, ReplayStep step) =>
		long.TryParse(spec.Substring(6), out var ms) ? ms : throw new InvalidOperationException($"invalid delay '{spec}'");

	// ===== Assertions =====

	private static void Expect(ItemSimWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		Func<bool> predicate = kind switch
		{
			"host_table" => () => world.HostTable(ItemId(step, 1)),
			"no_host_table" => () => !world.HostTable(ItemId(step, 1)),
			"reject" => () => world.Rejects(world.Node(step.Args[1])).Any(r => r.ItemId == ItemId(step, 2)),
			"received" => () => world.ReceivedCount(world.Node(step.Args[1]), MessageName(step, 2)) == Count(step, 3),
			_ => throw new InvalidOperationException($"unknown assertion '{kind}' (host_table / no_host_table / reject / received)"),
		};
		RunExpectation(world.Driver, step, predicate, () => ActualState(world, step));
	}

	private static void Expect(EntityEventSimWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		Func<bool> predicate = kind switch
		{
			"replayed" => () => world.ReplaysOf(world.Node(step.Args[1]), EntityKind(step, 2)) == Count(step, 3),
			"executed" => () => world.HostExecutionsOf(EntityKind(step, 1)) == Count(step, 2),
			"fluid" => () => world.FluidCell(world.Node(step.Args[1]), Int(step, 2), Int(step, 3)) == Byte(step, 4),
			_ => throw new InvalidOperationException($"unknown assertion '{kind}' (replayed / executed / fluid)"),
		};
		RunExpectation(world.Driver, step, predicate, () => ActualState(world, step));
	}

	/// <summary>The shared "within" semantics: an immediate check, else pump
	/// frames until the predicate holds or the window expires — a replay that
	/// never converges fails, with the observed state in the message.</summary>
	private static void RunExpectation(SimulationDriver driver, ReplayStep step, Func<bool> predicate, Func<string> actualState)
	{
		var withinIdx = Array.IndexOf(step.Args, "within");
		var withinMs = 0L;
		if (withinIdx >= 0)
		{
			if (withinIdx + 1 >= step.Args.Length || !long.TryParse(step.Args[withinIdx + 1], out withinMs))
			{
				throw new InvalidOperationException($"invalid 'within <ms>' in expect line");
			}
		}

		if (withinIdx >= 0)
		{
			if (!predicate())
			{
				try
				{
					driver.TickUntil(predicate, withinMs, TickStepMs);
				}
				catch (InvalidOperationException e)
				{
					throw new InvalidOperationException($"'{step.Args[0]}' did not hold — {string.Join(" ", step.Args)} — {actualState()} ({e.Message})");
				}
			}
		}
		else if (!predicate())
		{
			Fail(step, $"'{step.Args[0]}' did not hold — {string.Join(" ", step.Args)} — {actualState()}");
		}
	}

	private static NetMsg MessageName(ReplayStep step, int index) =>
		Enum.TryParse<NetMsg>(step.Args[index], out var msg) ? msg : throw new InvalidOperationException($"unknown message name '{step.Args[index]}'");

	private static int Count(ReplayStep step, int index) =>
		int.TryParse(step.Args[index], out var count) ? count : throw new InvalidOperationException($"invalid count '{step.Args[index]}'");

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

	/// <summary>The observed world state behind a failed entity assertion (actual vs expected).</summary>
	private static string ActualState(EntityEventSimWorld world, ReplayStep step)
	{
		var kind = step.Args[0];
		return kind switch
		{
			"replayed" => $"{step.Args[1]} replayed {step.Args[2]} {world.ReplaysOf(world.Node(step.Args[1]), EntityKind(step, 2))} time(s) (expected {step.Args[3]})",
			"executed" => $"host executed {step.Args[1]} {world.HostExecutionsOf(EntityKind(step, 1))} time(s) (expected {step.Args[2]})",
			"fluid" => $"{step.Args[1]} fluid at ({step.Args[2]},{step.Args[3]}) = {world.FluidCell(world.Node(step.Args[1]), Int(step, 2), Int(step, 3))} (expected {step.Args[4]})",
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
