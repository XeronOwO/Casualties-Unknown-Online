using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/data</c> verb group: read or write ONE item's recorded state as SNBT, shaped after
/// Minecraft's <c>/data get|merge|modify|remove</c>.
///
/// <para>
/// The command is the control plane only — the grammar, the target resolution and the answers. The
/// kernel holds the record (<see cref="IKernelItemFacts"/>), the adapter renders it as SNBT
/// (<see cref="IItemStateLanguage"/>), and the writes go through <see cref="ItemDataWriteCommands"/>.
/// </para>
///
/// <para>
/// Every answer names its KIND: a read that finds nothing, a selector that names several bodies and a
/// path that does not resolve are refusals, so they print in the console's error colour instead of the
/// success one. The renderers answer <c>string?</c> where null means "this build cannot describe the
/// state" — a missing language, which every caller turns into the same refusal.
/// </para>
/// </summary>
internal sealed class ItemDataCommands(
	IKernelItemFacts items,
	IItemStateLanguage language,
	ILogger log,
	ItemDataTargets targets,
	ItemDataWriteCommands writes)
{
	/// <summary>The sentence a build with no Game Adapter answers: it cannot name the state's keys.</summary>
	internal const string NoLanguage = "This build cannot read an item's state: no Game Adapter is loaded, so its keys are not known here.";

	private const string Verbs = "/data get | /data merge | /data modify | /data remove";

	/// <summary>The body-slot ordering key: carried items by slot, then limb-installed ones by limb.</summary>
	private const int AppliedOrderBase = 1000;

	private readonly IKernelItemFacts _items = items;
	private readonly IItemStateLanguage _language = language;
	private readonly ILogger _log = log;
	private readonly ItemDataTargets _targets = targets;
	private readonly ItemDataWriteCommands _writes = writes;

	[ConsoleCommand("data", "Read or write one item's recorded state as SNBT: get, merge, modify or remove.", CommandPermission.Anyone, ItemDataVerbParser.Usage, CommandArgumentKind.Text)]
	internal ConsoleAnswer Data(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused($"Usage: {ItemDataVerbParser.Usage}", "data.answer.usage", ItemDataVerbParser.Usage);
		}

		var tail = Tail(args, 2);
		return args[1].ToLowerInvariant() switch
		{
			"get" => Get(tail),
			ItemDataVerbParser.MergeOperation => _writes.Merge(tail),
			ItemDataVerbParser.ModifyOperation => _writes.Modify(tail),
			ItemDataVerbParser.RemoveOperation => _writes.Remove(tail),
			_ => ConsoleAnswer.Refused($"Unknown /data verb '{args[1]}'. The verbs are {Verbs}. Usage: {ItemDataVerbParser.Usage}", "data.answer.unknown_verb", args[1], Verbs, ItemDataVerbParser.Usage),
		};
	}

	private ConsoleAnswer Get(IReadOnlyList<string> tokens)
	{
		if (!ItemDataVerbParser.TryParseGet(tokens, out var request, out var error))
		{
			return ConsoleAnswer.Refused(error!);
		}

		var rows = _items.QueryItems();
		var all = new List<ItemState>(rows.Values);
		return request.Target.Kind switch
		{
			ItemDataQueryKind.InstanceId => ReadOne(rows, request.Target.InstanceId, "id", request.Path),
			ItemDataQueryKind.Slot => ReadSlot(all, request.Target, request.Path),
			ItemDataQueryKind.Cursor => ReadCursor(rows, request.Path),
			_ => ReadBodies(all, request.Target, request.Path),
		};
	}

	private ConsoleAnswer ReadOne(IReadOnlyDictionary<ulong, ItemState> rows, ulong instanceId, string target, NbtPath path)
	{
		if (!rows.TryGetValue(instanceId, out var item))
		{
			return ConsoleAnswer.Refused($"No item #{instanceId} is recorded in this session.", "data.answer.no_item", instanceId.ToString(CultureInfo.InvariantCulture));
		}

		if (!_language.TryDescribe(item.Data, out var document))
		{
			return ConsoleAnswer.Refused(NoLanguage);
		}

		_log.LogInformation("[Command] /data get {Target} read #{InstanceId} ({Definition}){Path}.",
			target, instanceId, item.Identity.DefinitionId, path.IsRoot ? "" : " at " + path.Text);
		var rendered = path.IsRoot
			? ItemDataRenderer.One(item, document, [.. rows.Values])
			: RenderPath(item, document, path);
		return rendered is null ? ConsoleAnswer.Refused(NoLanguage) : ConsoleAnswer.Ok(rendered);
	}

	private ConsoleAnswer ReadCursor(IReadOnlyDictionary<ulong, ItemState> rows, NbtPath path)
	{
		if (!_targets.TryResolveCursor(out var instanceId, out var refusal))
		{
			_log.LogWarning("[Command] /data get cursor refused: {Reason}", refusal);
			return ConsoleAnswer.Refused(refusal!);
		}

		return ReadOne(rows, instanceId, "cursor", path);
	}

	private ConsoleAnswer ReadSlot(IReadOnlyList<ItemState> all, ItemDataQuery query, NbtPath path)
	{
		var owners = _targets.ResolveOwners(query.Selector, out var refusal);
		if (refusal is not null)
		{
			return ConsoleAnswer.Refused(refusal);
		}

		if (owners.Count > 1)
		{
			_log.LogWarning("[Command] /data get slot {Slot} refused: the selector '{Selector}' matched {Count} bodies ({Owners}).",
				query.Slot, query.Selector, owners.Count, ItemDataTargets.Owners(owners));
			return ConsoleAnswer.Refused(RefuseAmbiguousBody(owners));
		}

		var builder = new StringBuilder();
		var empty = new List<ulong>();
		var listed = 0;
		foreach (var owner in owners)
		{
			var item = ItemDataTargets.SlotItem(all, owner, query.Slot);
			if (item is null)
			{
				empty.Add(owner);
				continue;
			}

			var rendered = RenderItem(item.Value, path, all);
			if (rendered is null)
			{
				return ConsoleAnswer.Refused(NoLanguage);
			}

			listed++;
			Append(builder, rendered);
		}

		ReportEmpty(builder, empty, $"No item is carried by {{0}} in slot {query.Slot}.");
		_log.LogInformation("[Command] /data get slot {Slot} named {Bodies} body/bodies and read {Items} item(s).",
			query.Slot, owners.Count, listed);
		return listed == 0 ? ConsoleAnswer.Refused(builder.ToString()) : ConsoleAnswer.Ok(builder.ToString());
	}

	private ConsoleAnswer ReadBodies(IReadOnlyList<ItemState> all, ItemDataQuery query, NbtPath path)
	{
		var owners = _targets.ResolveOwners(query.Selector, out var refusal);
		if (refusal is not null)
		{
			return ConsoleAnswer.Refused(refusal);
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

			var rendered = RenderBody(owner, held, all, path);
			if (rendered is null)
			{
				return ConsoleAnswer.Refused(NoLanguage);
			}

			listed += held.Count;
			Append(builder, rendered);
		}

		ReportEmpty(builder, empty, EmptyBodyAnswer(query, empty));
		_log.LogInformation("[Command] /data get {Selector} named {Bodies} body/bodies and listed {Items} item(s).",
			query.Selector ?? "(self)", owners.Count, listed);
		return listed == 0 ? ConsoleAnswer.Refused(builder.ToString()) : ConsoleAnswer.Ok(builder.ToString());
	}

	/// <summary>One body's block: every item with its whole document, or the one path's value per item.</summary>
	private string? RenderBody(ulong owner, IReadOnlyList<ItemState> held, IReadOnlyList<ItemState> all, NbtPath path)
	{
		if (path.IsRoot)
		{
			var documents = new List<SnbtValue>(held.Count);
			foreach (var item in held)
			{
				if (!_language.TryDescribe(item.Data, out var document))
				{
					return null;
				}

				documents.Add(document);
			}

			return ItemDataRenderer.List(owner, held, documents);
		}

		var builder = new StringBuilder();
		builder.Append("Items recorded for ").Append(owner).Append(" (").Append(held.Count).Append("):");
		foreach (var item in held)
		{
			var rendered = RenderItem(item, path, all);
			if (rendered is null)
			{
				return null;
			}

			builder.Append('\n').Append(rendered);
		}

		return builder.ToString();
	}

	private string? RenderItem(ItemState item, NbtPath path, IReadOnlyList<ItemState> all)
	{
		if (!_language.TryDescribe(item.Data, out var document))
		{
			return null;
		}

		return path.IsRoot
			? ItemDataRenderer.One(item, document, all)
			: RenderPath(item, document, path);
	}

	private static string RenderPath(ItemState item, SnbtValue document, NbtPath path) =>
		SnbtTree.TryResolve(document, path, out var value, out var reason)
			? ItemDataRenderer.PathLine(item, path, value)
			: ItemDataRenderer.PathFailure(item, path, reason!);

	/// <summary>A verb that names ONE item needs one body to name it on; the verdict is taken on the resolved set, at execution.</summary>
	private static string RefuseAmbiguousBody(IReadOnlyList<ulong> owners) =>
		$"That selector matched {owners.Count} bodies ({ItemDataTargets.Owners(owners)}) — a slot read names ONE item, so name one body: @p, a selector with [limit=1], or the item's own id.";

	/// <summary>What an empty answer says; the caller's own body has its own sentence (a carried item joins the record on its first report).</summary>
	private static string EmptyBodyAnswer(ItemDataQuery query, IReadOnlyList<ulong> empty) =>
		query.Selector is null && empty.Count == 1
			? "No item is recorded for {0} yet — a carried item joins this record on the holding client's first report of it (a use, a move or a drop), which is also when it gets the id a target can name."
			: "No item is recorded for {0}.";

	/// <summary>Every body a target names is resolved by <see cref="ItemDataTargets"/>; this class only words its answers.</summary>
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

		Append(builder, string.Format(CultureInfo.InvariantCulture, format, ItemDataTargets.Owners(empty)));
	}

	private static List<string> Tail(IReadOnlyList<string> args, int start)
	{
		var tail = new List<string>(Math.Max(args.Count - start, 0));
		for (var i = start; i < args.Count; i++)
		{
			tail.Add(args[i]);
		}

		return tail;
	}
}
