using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data command group (<c>/data</c>): an operator's read AND write of what
/// CUO records about one item — condition, favourited, liquid stacks, the typed
/// component fields and the container contents — addressed by a player selector,
/// a body slot, the pointer or a CUO instance id.
///
/// The READ reads the KERNEL fact, not the local scene: the kernel row is the same on
/// every peer (the host's authority, a guest's replay of the committed batches), it
/// is what replication, the save archive and a rejoin all carry, and it is therefore
/// the row a write has to change. Reading the local scene would answer a different
/// question — what THIS client rendered — which the remote inventory already shows.
///
/// The WRITE takes the other order on purpose: a carried item's state is its
/// holder's own report, so the command computes the property against the item's
/// LOCAL capture (<see cref="ILocalItemAccess"/>), writes it there, and reports the
/// result through the same two calls a native use's host branch reports with — the
/// row a command writes is the row the game would have written. A write is therefore
/// the holder's own act: an item this client does not hold, and an item the record
/// does not name yet, are both refused by name rather than written somewhere else.
///
/// Permission is <see cref="CommandPermission.Anyone"/> because the READ is open —
/// another player's carried items are already visible in the remote inventory view —
/// and the WRITE is judged by the HOST rather than by the console: a write is the
/// holder's own report, so every client may type it, and the host's own rule decides
/// whether a member's write lands (<c>HostRulesOptions.AllowMemberItemDataWrite</c>,
/// off by default).
///
/// <para>
/// A MEMBER's client refuses the verb before anything is applied when it holds the
/// host's announced rules and they say the verb is closed
/// (<see cref="AnnouncedHostRules.Forbids"/>): the write used to be applied locally and
/// reported first, so on a host whose rule is closed the operator watched the value
/// appear and go back. That refusal is an INTERCEPTION, never the verdict — it fires
/// only on an announced rule, so a member whose belief is stale, lost or absent applies
/// and reports as it always did and the host's own answer settles the record.
/// </para>
/// </summary>
internal sealed class ItemDataCommands(
	IKernelItemFacts items,
	ISessionControl session,
	ILocalItemAccess localItems,
	IItemControl itemControl,
	ILogger log,
	AnnouncedHostRules hostRules,
	ItemDataTargets targets)
{
	/// <summary>The verb list a caller reads before it knows which usage line it needs: an operator typing a bare <c>/data</c> has to learn that merge and remove exist.</summary>
	private const string Verbs = "/data get | /data set | /data merge | /data remove";

	private const string GetVerb = "get";

	private const string SetVerb = "set";

	private const string MergeVerb = "merge";

	private const string RemoveVerb = "remove";

	/// <summary>The body-slot ordering key: carried items by slot, then limb-installed ones by limb.</summary>
	private const int AppliedOrderBase = 1000;

	private readonly IKernelItemFacts _items = items;
	private readonly ISessionControl _session = session;
	private readonly ILocalItemAccess _localItems = localItems;
	private readonly IItemControl _itemControl = itemControl;
	private readonly ILogger _log = log;
	private readonly AnnouncedHostRules _hostRules = hostRules;
	private readonly ItemDataTargets _targets = targets;

	[ConsoleCommand("data", "Read or write one item's recorded state: condition, liquids, component fields and contents.", CommandPermission.Anyone, Verbs + " | " + ItemDataQueryParser.Usage + " | " + ItemDataWriteParser.Usage, CommandArgumentKind.Text)]
	internal string Data(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return $"Usage: {ItemDataQueryParser.Usage} | {ItemDataWriteParser.Usage} | {ItemDataWriteParser.MergeUsage} | {ItemDataWriteParser.RemoveUsage}";
		}

		var verb = args[1].ToLowerInvariant();
		if (verb == SetVerb)
		{
			var writeTokens = Tail(args, 2);
			return ItemDataWriteParser.TryParse(writeTokens, out var one, out var setError)
				? Write(SetVerb, [one])
				: setError!;
		}

		if (verb == MergeVerb)
		{
			var writeTokens = Tail(args, 2);
			return ItemDataWriteParser.TryParseMany(writeTokens, out var many, out var mergeError)
				? Write(MergeVerb, many)
				: mergeError!;
		}

		if (verb == RemoveVerb)
		{
			var writeTokens = Tail(args, 2);
			return ItemDataWriteParser.TryParseRemove(writeTokens, out var clearing, out var removeError)
				? Write(RemoveVerb, clearing)
				: removeError!;
		}

		if (!string.Equals(args[1], GetVerb, StringComparison.OrdinalIgnoreCase))
		{
			return $"Unknown /data verb '{args[1]}'. The verbs are {Verbs}. Usage: {ItemDataQueryParser.Usage} | {ItemDataWriteParser.Usage} | {ItemDataWriteParser.MergeUsage} | {ItemDataWriteParser.RemoveUsage}";
		}

		var tail = Tail(args, 2);
		if (!ItemDataQueryParser.TryParse(tail, out var query, out var error))
		{
			return error!;
		}

		var rows = _items.QueryItems();
		var all = new List<ItemState>(rows.Values);
		return query.Kind switch
		{
			ItemDataQueryKind.InstanceId => ReadOne(rows, query.InstanceId, "id"),
			ItemDataQueryKind.Slot => ReadSlot(all, query),
			ItemDataQueryKind.Cursor => ReadCursor(rows),
			_ => ReadBodies(all, query),
		};
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

	private string ReadOne(IReadOnlyDictionary<ulong, ItemState> rows, ulong instanceId, string target)
	{
		if (!rows.TryGetValue(instanceId, out var item))
		{
			return $"No item #{instanceId} is recorded in this session.";
		}

		_log.LogInformation("[Command] /data get {Target} read #{InstanceId} ({Definition}).", target, instanceId, item.Identity.DefinitionId);
		return ItemDataRenderer.One(item, [.. rows.Values]);
	}

	/// <summary>
	/// The item the pointer names: the local scene answers what is under the mouse, and
	/// the record answers what is known about it. The two refusals differ — the scene
	/// could not name an item at all, or it named one this session does not record — so
	/// each keeps its own sentence.
	/// </summary>
	private string ReadCursor(IReadOnlyDictionary<ulong, ItemState> rows)
	{
		if (!_targets.TryResolveCursor(out var instanceId, out var refusal))
		{
			_log.LogWarning("[Command] /data get cursor refused: {Reason}", refusal);
			return refusal!;
		}

		return ReadOne(rows, instanceId, "cursor");
	}

	private string ReadBodies(IReadOnlyList<ItemState> all, ItemDataQuery query)
	{
		var owners = _targets.ResolveOwners(query.Selector, out var refusal);
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
		var owners = _targets.ResolveOwners(query.Selector, out var refusal);
		if (refusal is not null)
		{
			return refusal;
		}

		if (owners.Count > 1)
		{
			_log.LogWarning("[Command] /data get slot {Slot} refused: the selector '{Selector}' matched {Count} bodies ({Owners}).",
				query.Slot, query.Selector, owners.Count, ItemDataTargets.Owners(owners));
			return RefuseAmbiguousBody(owners);
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

			listed++;
			Append(builder, ItemDataRenderer.One(item.Value, all));
		}

		ReportEmpty(builder, empty, $"No item is carried by {{0}} in slot {query.Slot}.");
		_log.LogInformation("[Command] /data get slot {Slot} named {Bodies} body/bodies and read {Items} item(s).",
			query.Slot, owners.Count, listed);
		return builder.ToString();
	}

	// ===== Write =====

	/// <summary>
	/// The write path every write verb shares: the checks that must pass once, the ONE
	/// item the command names, then each property applied to the in-memory capture
	/// before a single commit. Applying every property first is what makes a merge and
	/// a remove atomic — a property that cannot be applied refuses the whole command
	/// and nothing is written — so the loop must not commit per property.
	/// </summary>
	private string Write(string verb, IReadOnlyList<ItemDataWrite> writes)
	{
		if (!_session.SessionActive)
		{
			// The report the write ends with is session-scoped (both routes are guarded on
			// an active session), so without one the local object would change and the
			// record would not — a silent divergence the answer must not call "reported".
			_log.LogWarning("[Command] /data {Verb} refused: no active session ({Role}).", verb, _session.Role);
			return "No active session — a write reports into the session's record, so it needs one (a solo world keeps no item record for this verb to change).";
		}

		if (_session.Role == SessionRole.Guest && _hostRules.Forbids(HostRuleId.AllowMemberItemDataWrite))
		{
			// The host has announced that a member's item-data write does not land, so it
			// is refused HERE — before the value is written into the local object and
			// reported — instead of being applied, refused and put back. The branch is
			// reachable only on an ANNOUNCED rule: with no announcement this client
			// applies and reports as it always did and the host's answer settles it, so
			// the interception can never be the reason a write the host would accept is
			// refused.
			_log.LogInformation("[Command] /data {Verb} refused locally: the host's announced rule {Rule}=false forbids a member's item-data write; nothing was applied here.",
				verb, HostRuleId.AllowMemberItemDataWrite);
			return $"Refused locally: the host's rule {HostRuleId.AllowMemberItemDataWrite} is off, so a member's item-data write does not land (the host announces its rules to this client). Nothing was written here.";
		}

		var all = new List<ItemState>(_items.QueryItems().Values);
		if (!_targets.TryResolveWrite(writes[0].Target, all, out var instanceId, out var refusal))
		{
			_log.LogWarning("[Command] /data {Verb} refused: {Reason}", verb, refusal);
			return refusal!;
		}

		var local = _localItems.ReadLocalItem(instanceId);
		if (local is null)
		{
			return RefuseNotHeld(instanceId);
		}

		var changes = new List<string>(writes.Count);
		var described = new List<string>(writes.Count);
		foreach (var write in writes)
		{
			if (!ItemDataWriteValues.TryApply(local, write, out var before, out var after, out var valueError))
			{
				_log.LogWarning("[Command] /data {Verb} {Property} on #{InstanceId} refused: {Reason}",
					verb, write.Property, instanceId, valueError);
				return valueError!;
			}

			// Two lists on purpose: the answer line names each property itself, while the LOG needs the
			// name inline — a merge whose log read only the values could not be attributed afterwards.
			changes.Add($"{before} -> {after}");
			described.Add($"{write.Property.ToString().ToLowerInvariant()} {before} -> {after}");
		}

		var result = _localItems.WriteLocalItem(instanceId, local);
		if (result != LocalItemWriteResult.Applied)
		{
			_log.LogWarning("[Command] /data {Verb} on #{InstanceId} refused by the local scene: {Result}.", verb, instanceId, result);
			return RefuseWrite(result, instanceId);
		}

		if (_session.Role == SessionRole.Guest)
		{
			// A MEMBER's write is the same act with one difference: the host judges it
			// as a console write under its own rule, so the report travels under its own
			// kind and the answer is provisional. The values are applied here because the
			// rule above did not refuse them — either the host has announced that the verb
			// is open, or no announcement has arrived and this client does what it always
			// did — and a refusal still arrives later, puts the recorded state back and
			// prints its own line (ItemStateSettlement, ItemDataWriteRefusal).
			_itemControl.SendItemDataWrite(instanceId, local);
			_log.LogInformation("[Command] /data {Verb} wrote #{InstanceId} ({Definition}): {Changes}; reported as a console write for the host's rule to judge.",
				verb, instanceId, local.ItemId, string.Join("; ", described));
			return ItemDataRenderer.WroteMany(local, writes, changes, awaitingHost: true);
		}

		Report(instanceId, local);
		_log.LogInformation("[Command] /data {Verb} wrote #{InstanceId} ({Definition}): {Changes}; reported as this client's own item state.",
			verb, instanceId, local.ItemId, string.Join("; ", described));
		return ItemDataRenderer.WroteMany(local, writes, changes, awaitingHost: false);
	}

	/// <summary>
	/// The record names an item the local scene does not hold: another player's
	/// carried item (whose local form here is a render proxy), or an id this client
	/// never loaded. Both are answered with WHERE the row says it is, because "not
	/// yours" and "not loaded here" are different facts to the operator.
	/// </summary>
	private string RefuseNotHeld(ulong instanceId)
	{
		var row = _items.FindItem(instanceId);
		if (row is null)
		{
			return $"No item #{instanceId} is recorded in this session.";
		}

		_log.LogWarning("[Command] /data write refused: #{InstanceId} is recorded as {Location} but this client does not hold it.", instanceId, row.Value.Location.Kind);
		return $"#{instanceId} is recorded as {ItemDataRenderer.LocationOf(row.Value)} but this client does not hold it — a write is the holder's own act, and the holder is the client whose copy of the item is the fact.";
	}

	/// <summary>The local scene refused the write: each reason has its own sentence, because each has a different remedy.</summary>
	private string RefuseWrite(LocalItemWriteResult result, ulong instanceId) => result switch
	{
		LocalItemWriteResult.NotHeld => $"This client does not hold #{instanceId}.",
		_ => $"#{instanceId} is not this client's fact to write (a render proxy, or another member's item incarnated here).",
	};

	/// <summary>
	/// The report route a native change of this item would take, chosen the way the
	/// native path chooses it: by whether the item's location chain is rooted in the
	/// WORLD — the kernel's own container walk (<see cref="ItemLocationChain"/>), which
	/// the layer reset and the archive read the same way. A child inside a dropped
	/// backpack is therefore a world item's content (1) even though its own row says
	/// "contained", and a child inside a carried one is not. World: the local copy is
	/// the fact and every other member's copy adopts it (the #194 correction). Anything
	/// else: this client's carried fact, which is the call <c>ItemUseSync</c>'s host
	/// branch makes for a native use of the same item.
	/// </summary>
	private void Report(ulong instanceId, CharacterItemMsg state)
	{
		var row = _items.FindItem(instanceId);
		if (row is not null && ItemLocationChain.IsWorldRooted(row.Value, _items.FindItem))
		{
			_itemControl.SendWorldItemCorrection(_session.LocalSteamId, state);
			return;
		}

		_itemControl.SendItemCarriedSync(_session.LocalSteamId, state);
	}

	/// <summary>
	/// A verb that names ONE item needs one body to name it on, and the verdict is
	/// taken HERE — on the resolved set, at execution — rather than by narrowing
	/// the grammar: the player may write any selector, and the answer says how many
	/// it matched and what to write instead (user ruling 2026-10-14: detect the
	/// target's uniqueness when the command runs, do not make a single-target
	/// parameter mandatory).
	///
	/// <para>
	/// The write verbs inherit the rule: "apply this to everybody the selector
	/// touched" is a fan-out nobody asked for, one typo wide.
	/// </para>
	/// </summary>
	private static string RefuseAmbiguousBody(IReadOnlyList<ulong> owners) =>
		$"That selector matched {owners.Count} bodies ({ItemDataTargets.Owners(owners)}) — a slot read names ONE item, so name one body: @p, a selector with [limit=1], or the item's own id.";

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
	/// Every body a target names is resolved by <see cref="ItemDataTargets"/>; this
	/// class only words its answers.
	/// </summary>
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
}
