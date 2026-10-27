using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data WRITE verbs (<c>/data merge</c>, <c>modify</c> and <c>remove</c>): a
/// document edit followed by ONE bind-and-write through the local scene.
///
/// <para>
/// The write takes the order decision 264 fixes: a carried item's state is its holder's own
/// report, so the command edits the DOCUMENT the adapter projected from the local capture,
/// hands the result to <see cref="ILocalItemAccess.WriteLocalItem"/>, which binds it against
/// the live item (the adapter's schema and legality), writes it once and returns the state
/// it re-read; the command then reports THAT capture through the same two calls a native
/// use's host branch reports with.
/// </para>
///
/// <para>
/// Every verb is atomic for one reason: the tree edit is pure and the bind validates the
/// whole document before mutating anything, so a refused key, value or operation changes
/// nothing on either side. The answer's clauses are built from the tree BEFORE and the tree
/// the adapter re-read, never from what was asked.
/// </para>
/// </summary>
internal sealed class ItemDataWriteCommands(
	IKernelItemFacts items,
	ISessionControl session,
	ILocalItemAccess localItems,
	IItemControl itemControl,
	IItemStateLanguage language,
	ILogger log,
	CheatVerbGate cheatVerbs,
	ItemDataTargets targets)
{
	private readonly IKernelItemFacts _items = items;
	private readonly ISessionControl _session = session;
	private readonly ILocalItemAccess _localItems = localItems;
	private readonly IItemControl _itemControl = itemControl;
	private readonly IItemStateLanguage _language = language;
	private readonly ILogger _log = log;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;
	private readonly ItemDataTargets _targets = targets;

	internal ConsoleAnswer Merge(IReadOnlyList<string> tokens)
	{
		if (!ItemDataVerbParser.TryParseMerge(tokens, out var request, out var error))
		{
			return ConsoleAnswer.Refused(error!);
		}

		var compound = request.State;
		return Write(ItemDataVerbParser.MergeOperation, request.Target, before =>
		{
			if (!SnbtTree.TryMerge(before, NbtPath.Root, compound, out var merged, out var mergeError))
			{
				return Edit.Failed(mergeError!);
			}

			var edits = new List<StateEdit>(compound.Entries.Count);
			foreach (var entry in compound.Entries)
			{
				edits.Add(new PathEdit(new NbtPath([new NbtPathKey(entry.Key)])));
			}

			return Edit.Applied(merged, edits);
		});
	}

	internal ConsoleAnswer Modify(IReadOnlyList<string> tokens)
	{
		if (!ItemDataVerbParser.TryParseModify(tokens, out var request, out var error))
		{
			return ConsoleAnswer.Refused(error!);
		}

		if (request.Path.IsRoot)
		{
			return ConsoleAnswer.Refused($"modify needs a path inside the state — the whole state is not replaced; merge its keys with /data merge. Usage: {ItemDataVerbParser.Usage}");
		}

		return Write(ItemDataVerbParser.ModifyOperation, request.Target, before =>
			ApplyModify(before, request.Path, request.Operation, request.Index, request.Value));
	}

	internal ConsoleAnswer Remove(IReadOnlyList<string> tokens)
	{
		if (!ItemDataVerbParser.TryParseRemove(tokens, out var request, out var error))
		{
			return ConsoleAnswer.Refused(error!);
		}

		if (request.Path.IsRoot)
		{
			return ConsoleAnswer.Refused($"remove needs a path inside the state — the whole state is not removable. Usage: {ItemDataVerbParser.Usage}");
		}

		return Write(ItemDataVerbParser.RemoveOperation, request.Target, before =>
			SnbtTree.TryRemove(before, request.Path, out var updated, out var removed, out var removeError)
				? Edit.Applied(updated, [new RemoveEdit(request.Path, removed)])
				: Edit.Failed(removeError!));
	}

	private static Edit ApplyModify(SnbtValue before, NbtPath path, string operation, int index, SnbtValue value)
	{
		switch (operation)
		{
			case ItemDataVerbParser.SetOperation:
				return SnbtTree.TrySet(before, path, value, out var replaced, out var setError)
					? Edit.Applied(replaced, [new PathEdit(path)])
					: Edit.Failed(setError!);

			case ItemDataVerbParser.MergeOperation:
				if (value is not SnbtCompound compound)
				{
					return Edit.Failed($"modify merge takes a compound — got {value.Kind}.");
				}

				if (!SnbtTree.TryMerge(before, path, compound, out var merged, out var mergeError))
				{
					return Edit.Failed(mergeError!);
				}

				var edits = new List<StateEdit>(compound.Entries.Count);
				foreach (var entry in compound.Entries)
				{
					edits.Add(new PathEdit(new NbtPath([.. path.Segments, new NbtPathKey(entry.Key)])));
				}

				return Edit.Applied(merged, edits);

			case ItemDataVerbParser.AppendOperation or ItemDataVerbParser.PrependOperation:
				var appendIndex = operation == ItemDataVerbParser.AppendOperation
					&& SnbtTree.TryResolve(before, path, out var list, out _)
					&& list is SnbtList items
						? items.Items.Count
						: 0;
				return SnbtTree.TryInsert(before, path, appendIndex, value, out var appended, out var appendError)
					? Edit.Applied(appended, [new InsertEdit(path, appendIndex, value)])
					: Edit.Failed(appendError!);

			default:
				return SnbtTree.TryInsert(before, path, index, value, out var inserted, out var insertError)
					? Edit.Applied(inserted, [new InsertEdit(path, index, value)])
					: Edit.Failed(insertError!);
		}
	}

	/// <summary>
	/// The write path every write verb shares: the cheat family's front-end check, the ONE
	/// item the command names, the local capture, the edit on the document, ONE bind-and-write
	/// through the local scene, and the report of what the adapter re-read.
	/// </summary>
	private ConsoleAnswer Write(string verb, ItemDataQuery target, Func<SnbtValue, Edit> edit)
	{
		if (_cheatVerbs.TryRefuse("data " + verb, out var cheatRefusal))
		{
			return cheatRefusal;
		}

		var all = new List<ItemState>(_items.QueryItems().Values);
		if (!_targets.TryResolveWrite(target, all, out var instanceId, out var refusal))
		{
			_log.LogWarning("[Command] /data {Verb} refused: {Reason}", verb, refusal);
			return ConsoleAnswer.Refused(refusal!);
		}

		var local = _localItems.ReadLocalItem(instanceId);
		if (local is null)
		{
			return ConsoleAnswer.Refused(RefuseNotHeld(instanceId));
		}

		if (!_language.TryDescribe(local, out var before))
		{
			return ConsoleAnswer.Refused(ItemDataCommands.NoLanguage);
		}

		var change = edit(before);
		if (!change.Ok)
		{
			_log.LogWarning("[Command] /data {Verb} on #{InstanceId} refused: {Reason}", verb, instanceId, change.Error);
			return ConsoleAnswer.Refused(change.Error!);
		}

		var outcome = _localItems.WriteLocalItem(instanceId, change.Document);
		if (!outcome.Applied || outcome.Written is null)
		{
			_log.LogWarning("[Command] /data {Verb} on #{InstanceId} refused by the local scene: {Result}: {Refusal}.",
				verb, instanceId, outcome.Result, outcome.Refusal);
			return ConsoleAnswer.Refused(RefuseWrite(outcome, instanceId));
		}

		var written = outcome.Written;
		if (!_language.TryDescribe(written, out var after))
		{
			return ConsoleAnswer.Refused(ItemDataCommands.NoLanguage);
		}

		var changes = DescribeChanges(change.Edits, before, after);
		if (_session.Role == SessionRole.Guest)
		{
			// A MEMBER's write is the same act with one difference: the host judges it as a
			// console write under its own rule, so the report travels under its own kind and
			// the answer is provisional. A refusal still arrives later, puts the recorded
			// state back and prints its own line (ItemStateSettlement, ItemDataWriteRefusal).
			_itemControl.SendItemDataWrite(instanceId, written);
			_log.LogInformation("[Command] /data {Verb} wrote #{InstanceId} ({Definition}): {Changes}; reported as a console write for the host's rule to judge.",
				verb, instanceId, written.ItemId, string.Join("; ", changes));
			return ConsoleAnswer.Ok(ItemDataRenderer.Wrote(written, changes, awaitingHost: true, outcome.Note));
		}

		Report(instanceId, written);
		_log.LogInformation("[Command] /data {Verb} wrote #{InstanceId} ({Definition}): {Changes}; reported as this client's own item state.",
			verb, instanceId, written.ItemId, string.Join("; ", changes));
		return ConsoleAnswer.Ok(ItemDataRenderer.Wrote(written, changes, awaitingHost: false, outcome.Note));
	}

	/// <summary>
	/// The answer's clauses, built from the tree BEFORE and the tree the adapter re-read: a
	/// value that did not really change (a zero-amount stack the bind dropped) therefore reads
	/// as what happened, not as what was asked.
	/// </summary>
	private static List<string> DescribeChanges(IReadOnlyList<StateEdit> edits, SnbtValue before, SnbtValue after)
	{
		var changes = new List<string>(edits.Count);
		foreach (var edit in edits)
		{
			switch (edit)
			{
				case PathEdit path:
					changes.Add(PathClause(before, after, path.Path));
					break;
				case InsertEdit insert:
					changes.Add($"{insert.Path.Text}[{insert.Index}] inserted {ItemDataRenderer.Document(insert.Value)}");
					break;
				case RemoveEdit remove:
					changes.Add($"{remove.Path.Text} {ItemDataRenderer.Document(remove.Removed)} -> removed");
					break;
			}
		}

		return changes;
	}

	/// <summary>
	/// One path's clause. A path INSIDE a list whose length changed (the bind dropped a
	/// zero-amount stack) is answered by the list itself: the index now names a different
	/// element, so an index-keyed before/after would claim a change that never happened.
	/// </summary>
	private static string PathClause(SnbtValue before, SnbtValue after, NbtPath path)
	{
		if (path.Segments.Count > 1)
		{
			var parent = new NbtPath([.. path.Segments.Take(path.Segments.Count - 1)]);
			if (SnbtTree.TryResolve(before, parent, out var beforeValue, out _)
				&& SnbtTree.TryResolve(after, parent, out var afterValue, out _)
				&& beforeValue is SnbtList beforeList
				&& afterValue is SnbtList afterList
				&& beforeList.Items.Count != afterList.Items.Count)
			{
				return $"{parent.Text} {ItemDataRenderer.Document(beforeList)} -> {ItemDataRenderer.Document(afterList)}";
			}
		}

		return $"{path.Text} {ValueAt(before, path)} -> {ValueAt(after, path)}";
	}

	private static string ValueAt(SnbtValue document, NbtPath path) =>
		SnbtTree.TryResolve(document, path, out var value, out _)
			? ItemDataRenderer.Document(value)
			: "(absent)";

	/// <summary>
	/// The record names an item the local scene does not hold: another player's carried item
	/// (whose local form here is a render proxy), or an id this client never loaded.
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
	private static string RefuseWrite(LocalItemWriteOutcome outcome, ulong instanceId) => outcome.Result switch
	{
		LocalItemWriteResult.Refused => outcome.Refusal ?? $"#{instanceId}'s item cannot carry that state.",
		LocalItemWriteResult.NotHeld => $"This client does not hold #{instanceId}.",
		LocalItemWriteResult.Unavailable => $"This build cannot express an item state, so #{instanceId} cannot be written here.",
		_ => $"#{instanceId} is not this client's fact to write (a render proxy, or another member's item incarnated here).",
	};

	/// <summary>
	/// The report route a native change of this item would take, chosen the way the native path
	/// chooses it: by whether the item's location chain is rooted in the WORLD — the kernel's
	/// own container walk (<see cref="ItemLocationChain"/>). World: the local copy is the fact
	/// and every other member's copy adopts it (the #194 correction). Anything else: this
	/// client's carried fact, the call <c>ItemUseSync</c>'s host branch makes for a native use.
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

	/// <summary>One applied edit: the new document plus the clauses that describe what it touched.</summary>
	private sealed record Edit(bool Ok, SnbtValue Document, IReadOnlyList<StateEdit> Edits, string? Error)
	{
		internal static Edit Applied(SnbtValue document, IReadOnlyList<StateEdit> edits) => new(true, document, edits, null);

		internal static Edit Failed(string error) => new(false, SnbtCompound.Empty, [], error);
	}

	private abstract record StateEdit;

	private sealed record PathEdit(NbtPath Path) : StateEdit;

	private sealed record InsertEdit(NbtPath Path, int Index, SnbtValue Value) : StateEdit;

	private sealed record RemoveEdit(NbtPath Path, SnbtValue Removed) : StateEdit;
}
