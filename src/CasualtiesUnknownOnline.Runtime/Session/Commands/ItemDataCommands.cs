using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data command group (<c>/data</c>): an operator's read of what CUO
/// records about one item — condition, favourited, liquid stacks, the typed
/// component fields and the container contents — addressed by a player selector,
/// a body slot or a CUO instance id.
///
/// It reads the KERNEL fact, not the local scene: the kernel row is the same on
/// every peer (the host's authority, a guest's replay of the committed batches),
/// it is what replication, the save archive and a rejoin all carry, and it is
/// therefore the row a later write has to change. Reading the local scene would
/// answer a different question — what THIS client rendered — which the remote
/// inventory already shows.
///
/// Permission is <see cref="CommandPermission.Anyone"/> because the verb only
/// reads: another player's carried items are already visible in the remote
/// inventory view. Writes are the group's later stages and are the host's call.
/// </summary>
internal sealed class ItemDataCommands(
	IKernelItemFacts items,
	ISessionControl session,
	IEntitySyncControl entities,
	ILogger log)
{
	private const string GetVerb = "get";

	/// <summary>The body-slot ordering key: carried items by slot, then limb-installed ones by limb.</summary>
	private const int AppliedOrderBase = 1000;

	private readonly IKernelItemFacts _items = items;
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly ILogger _log = log;

	[ConsoleCommand("data", "Read one item's recorded state: condition, liquids, component fields and contents.", CommandPermission.Anyone, ItemDataQueryParser.Usage, CommandArgumentKind.Text)]
	internal string Data(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return $"Usage: {ItemDataQueryParser.Usage}";
		}

		if (!string.Equals(args[1], GetVerb, StringComparison.OrdinalIgnoreCase))
		{
			var verb = args[1].ToLowerInvariant();
			return verb is "set" or "merge" or "remove"
				? $"/data {verb} is not implemented yet — this family landed its read half first. Usage: {ItemDataQueryParser.Usage}"
				: $"Unknown /data verb '{args[1]}'. Usage: {ItemDataQueryParser.Usage}";
		}

		var tail = new List<string>(Math.Max(args.Count - 2, 0));
		for (var i = 2; i < args.Count; i++)
		{
			tail.Add(args[i]);
		}

		if (!ItemDataQueryParser.TryParse(tail, out var query, out var error))
		{
			return error!;
		}

		var rows = _items.QueryItems();
		var all = new List<ItemState>(rows.Values);
		return query.Kind switch
		{
			ItemDataQueryKind.InstanceId => ReadOne(rows, query.InstanceId),
			ItemDataQueryKind.Slot => ReadSlot(all, query),
			_ => ReadBodies(all, query),
		};
	}

	private string ReadOne(IReadOnlyDictionary<ulong, ItemState> rows, ulong instanceId)
	{
		if (!rows.TryGetValue(instanceId, out var item))
		{
			return $"No item #{instanceId} is recorded in this session.";
		}

		_log.LogInformation("[Command] /data get id {InstanceId} ({Definition}).", instanceId, item.Identity.DefinitionId);
		return ItemDataRenderer.One(item, [.. rows.Values]);
	}

	private string ReadBodies(IReadOnlyList<ItemState> all, ItemDataQuery query)
	{
		var owners = ResolveOwners(query.Selector, out var refusal);
		if (refusal is not null)
		{
			return refusal;
		}

		var builder = new StringBuilder();
		var empty = new List<ulong>();
		var listed = 0;
		foreach (var owner in owners)
		{
			var held = Held(all, owner);
			if (held.Count == 0)
			{
				empty.Add(owner);
				continue;
			}

			listed += held.Count;
			Append(builder, ItemDataRenderer.List(owner, held, all));
		}

		ReportEmpty(builder, empty, EmptyBodyAnswer(query, empty));
		_log.LogInformation("[Command] /data get {Selector} named {Bodies} body/bodies and listed {Items} item(s).",
			query.Selector ?? "(self)", owners.Count, listed);
		return builder.ToString();
	}

	private string ReadSlot(IReadOnlyList<ItemState> all, ItemDataQuery query)
	{
		var owners = ResolveOwners(query.Selector, out var refusal);
		if (refusal is not null)
		{
			return refusal;
		}

		var builder = new StringBuilder();
		var empty = new List<ulong>();
		var listed = 0;
		foreach (var owner in owners)
		{
			var item = SlotItem(all, owner, query.Slot);
			if (item is null)
			{
				empty.Add(owner);
				continue;
			}

			listed++;
			Append(builder, ItemDataRenderer.One(item.Value, all));
		}

		ReportEmpty(builder, empty, $"No item is carried by {{0}} in slot {query.Slot}.");
		_log.LogInformation("[Command] /data get slot {Slot} named {Bodies} body/bodies and read {Items} item(s).",
			query.Slot, owners.Count, listed);
		return builder.ToString();
	}

	/// <summary>
	/// What an empty answer says. The caller's own body is the one case where an
	/// empty record does not mean "you carry nothing": a carried item joins CUO's
	/// model — and gets the instance id any target needs — on the holding client's
	/// first report of it, so the caller is told which of the two facts it is
	/// reading. Every other body is answered plainly.
	/// </summary>
	private static string EmptyBodyAnswer(ItemDataQuery query, IReadOnlyList<ulong> empty) =>
		query.Selector is null && empty.Count == 1
			? "No item is recorded for {0} yet — a carried item joins this record on the holding client's first report of it (a use, a move or a drop), which is also when it gets the id a target can name."
			: "No item is recorded for {0}.";

	/// <summary>
	/// Every body a target names. A selector that matches nobody is refused by
	/// name rather than answered with an empty list, because "no such player" and
	/// "that player carries nothing" are different facts to the operator.
	/// </summary>
	private IReadOnlyList<ulong> ResolveOwners(string? selector, out string? refusal)
	{
		refusal = null;
		if (selector is null)
		{
			return [_session.LocalSteamId];
		}

		var owners = CommandPlayerTargets.Resolve(_session, _entities, selector);
		if (owners.Count == 0)
		{
			refusal = $"No players match selector '{selector}'.";
		}

		return owners;
	}

	private static List<ItemState> Held(IReadOnlyList<ItemState> all, ulong owner)
	{
		var held = new List<ItemState>();
		foreach (var item in all)
		{
			if (item.Location.Kind is ItemLocationKind.Carried or ItemLocationKind.Applied
				&& item.Location.Owner.Value == owner)
			{
				held.Add(item);
			}
		}

		held.Sort((left, right) => Order(left).CompareTo(Order(right)));
		return held;
	}

	private static int Order(ItemState item) => item.Location.Kind switch
	{
		ItemLocationKind.Carried => item.Data.SlotIndex,
		ItemLocationKind.Applied => AppliedOrderBase + item.Location.LimbIndex,
		_ => AppliedOrderBase * 2,
	};

	private static ItemState? SlotItem(IReadOnlyList<ItemState> all, ulong owner, int slot)
	{
		foreach (var item in all)
		{
			if (item.Location.Kind == ItemLocationKind.Carried
				&& item.Location.Owner.Value == owner
				&& item.Data.SlotIndex == slot)
			{
				return item;
			}
		}

		return null;
	}

	private static void Append(StringBuilder builder, string block)
	{
		if (builder.Length > 0)
		{
			builder.Append('\n');
		}

		builder.Append(block);
	}

	/// <summary>A body the target named but that holds no such item is named too, so a partly empty answer is never read as a whole one.</summary>
	private static void ReportEmpty(StringBuilder builder, IReadOnlyList<ulong> empty, string format)
	{
		if (empty.Count == 0)
		{
			return;
		}

		Append(builder, string.Format(CultureInfo.InvariantCulture, format, Owners(empty)));
	}

	private static string Owners(IReadOnlyList<ulong> owners)
	{
		var builder = new StringBuilder();
		foreach (var owner in owners)
		{
			if (builder.Length > 0)
			{
				builder.Append(", ");
			}

			builder.Append(owner.ToString(CultureInfo.InvariantCulture));
		}

		return builder.ToString();
	}
}
