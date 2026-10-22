using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The cut's ONE-placement rule for a member's carried items.
///
/// <para>
/// An archive states one item's placement in two carriers: the kernel checkpoint's item rows say WHO
/// holds it and what contains it, and <c>characters/&lt;playerKey&gt;.json</c> says which body slot it
/// occupies. The two are written at different moments — a mid-run cut's host character is a live
/// capture while a member's file is its last 1 Hz report, and a native pickup/drop reports the ITEM
/// fact immediately without firing the immediate character re-report. A cut taken inside that window
/// would store one id in two places, and the restore applies both halves independently: a carried row
/// the body does not carry becomes a phantom (nothing ever materializes it), and a body item the
/// kernel placed in the world becomes a second object under an id that already exists. Neither is a
/// choice a restore should have to make, so the CUT resolves it, while it holds both carriers at one
/// instant: the kernel's placement is the arbitrated fact, the file's slot stays the body's own fact,
/// and an id the kernel has never judged is left exactly where the report put it (the report is then
/// the id's only carrier, the same rule the restored-id seed follows).
/// </para>
///
/// <para>
/// An UNBOUND id (<c>0</c>) is never an identity: the game's starting supplies can carry several
/// before the registration edge stamps them, so every one of them is kept, in place, and none takes
/// part in the placement sets.
/// </para>
/// </summary>
internal static class CutCharacterPlacement
{
	/// <summary>The format's "the body's own slot rules place it" marker — the -1 a world/carried item with no body slot states.</summary>
	internal const int UnknownSlot = -1;

	/// <summary>What the cut writes after the reconcile: how many ids it added and removed (the NET set difference, one per id, never per action), and the account lines.</summary>
	internal readonly record struct Outcome(
		IReadOnlyList<SavedCharacter> Characters,
		int Added,
		int Removed,
		IReadOnlyList<string> Notes);

	/// <summary>
	/// Reconcile every collected character file against the checkpoint's item rows. The characters are
	/// COPIED first: the binder collected the live table's objects, and a cut is a read (the next report
	/// and the live clone rendering own those objects, not the archive).
	/// </summary>
	internal static Outcome Reconcile(
		IReadOnlyList<SavedCharacter> characters,
		IReadOnlyDictionary<string, ulong> ownerByKey,
		IReadOnlyList<ItemState> items,
		ILogger log)
	{
		if (characters.Count == 0)
		{
			return new Outcome(characters, 0, 0, []);
		}

		var byId = new Dictionary<ulong, ItemState>(items.Count);
		foreach (var item in items)
		{
			byId[item.Identity.InstanceId] = item;
		}

		var heldByOwner = new Dictionary<ulong, List<ItemState>>();
		foreach (var item in items)
		{
			var holder = ItemLocationChain.HolderOf(item, id => byId.TryGetValue(id, out var row) ? row : null);
			if (holder == 0)
			{
				continue;
			}

			if (!heldByOwner.TryGetValue(holder, out var held))
			{
				heldByOwner[holder] = held = [];
			}

			held.Add(item);
		}

		var reconciled = new List<SavedCharacter>(characters.Count);
		var notes = new List<string>();
		var added = 0;
		var removed = 0;
		foreach (var character in characters)
		{
			if (!ownerByKey.TryGetValue(character.PlayerKey, out var owner))
			{
				// The cut's own collected set always carries an owner. A caller that cannot name one
				// gets the report untouched and the gap named, never a guess at a body.
				log.LogWarning(
					"Cut character {PlayerKey}: the cut did not collect an owner for it, so its item placement was not reconciled against the kernel.",
					character.PlayerKey);
				reconciled.Add(character);
				continue;
			}

			var held = heldByOwner.TryGetValue(owner, out var rows) ? rows : [];
			var outcome = ReconcileOne(character, held, byId);
			reconciled.Add(outcome.Character);
			added += outcome.Added;
			removed += outcome.Removed;
			notes.AddRange(outcome.Notes);
		}

		return new Outcome(reconciled, added, removed, notes);
	}

	private readonly record struct One(SavedCharacter Character, int Added, int Removed, IReadOnlyList<string> Notes);

	private static One ReconcileOne(SavedCharacter saved, IReadOnlyList<ItemState> held, IReadOnlyDictionary<ulong, ItemState> byId)
	{
		var character = CharacterDataClone.Of(saved.Character);
		var heldById = new Dictionary<ulong, ItemState>(held.Count);
		foreach (var row in held)
		{
			heldById[row.Identity.InstanceId] = row;
		}

		var childrenByParent = new Dictionary<ulong, List<ItemState>>();
		foreach (var row in held)
		{
			if (row.Location.Kind != ItemLocationKind.Contained)
			{
				continue;
			}

			if (!childrenByParent.TryGetValue(row.Location.ParentItemId, out var children))
			{
				childrenByParent[row.Location.ParentItemId] = children = [];
			}

			children.Add(row);
		}

		var placed = new Dictionary<ulong, CharacterItemMsg>();
		var fileNodes = new Dictionary<ulong, CharacterItemMsg>(character.Items.Count);
		IndexFileNodes(character.Items, fileNodes);
		var notes = new List<string>();
		var state = new State(placed, byId, heldById, childrenByParent, fileNodes, notes);

		// Top level, in the file's own order first: a node the kernel still carries keeps its place
		// and its slot; an id the kernel has never judged stays exactly as the report put it.
		var top = new List<CharacterItemMsg>(character.Items.Count);
		foreach (var node in character.Items)
		{
			if (node.InstanceId == 0)
			{
				Keep(node, top, state);
				continue;
			}

			if (!heldById.TryGetValue(node.InstanceId, out var row))
			{
				if (byId.ContainsKey(node.InstanceId))
				{
					// The kernel places this id somewhere else (world, another owner, a limb, terminal):
					// the arbitrated placement wins and the stale body copy goes. Unknown descendants
					// the kernel never heard of are lifted instead of disappearing with it.
					NoteRemoval(node, byId[node.InstanceId].Location, state, notes);
					LiftUnknown(node, state, top, notes);
					continue;
				}

				Keep(node, top, state);
				continue;
			}

			if (row.Location.Kind == ItemLocationKind.Contained)
			{
				// The kernel says it lives inside a container this body carries: it is rebuilt under
				// that parent (the parent's contents append it, since nothing placed it here).
				continue;
			}

			Keep(node, top, state);
		}

		// Then the ids the kernel holds on this body that the file never carried (the report was lost
		// or is still in flight): the archive must not drop them on the floor. An id the file states
		// SOMEWHERE (a take-out the report still shows inside its container) keeps the file's state and
		// only moves to the top, with no body slot; one the file states nowhere is built from the
		// kernel's own row.
		foreach (var row in held)
		{
			if (row.Location.Kind != ItemLocationKind.Carried || placed.ContainsKey(row.Identity.InstanceId))
			{
				continue;
			}

			if (fileNodes.TryGetValue(row.Identity.InstanceId, out var stated))
			{
				stated.SlotIndex = UnknownSlot;
				Keep(stated, top, state);
				notes.Add($"moved {Describe(row)} to the top level (no body slot; the body places it)");
				continue;
			}

			var node = FromKernel(row, state);
			top.Add(node);
			notes.Add($"added {Describe(row)} at {(node.SlotIndex == UnknownSlot ? "no body slot (the body places it)" : $"slot {Slot(node.SlotIndex)}")}");
		}

		character.Items = top;
		var before = Ids(saved.Character.Items);
		var after = Ids(top);
		return new One(
			saved with { Character = character },
			after.Count(id => !before.Contains(id)),
			before.Count(id => !after.Contains(id)),
			notes);
	}

	/// <summary>The reconcile's per-character working set, so the tree walk stays a walk instead of an argument list.</summary>
	private sealed class State(
		Dictionary<ulong, CharacterItemMsg> placed,
		IReadOnlyDictionary<ulong, ItemState> byId,
		IReadOnlyDictionary<ulong, ItemState> heldById,
		IReadOnlyDictionary<ulong, List<ItemState>> childrenByParent,
		IReadOnlyDictionary<ulong, CharacterItemMsg> fileNodes,
		List<string> notes)
	{
		internal Dictionary<ulong, CharacterItemMsg> Placed { get; } = placed;

		internal IReadOnlyDictionary<ulong, ItemState> ById { get; } = byId;

		internal IReadOnlyDictionary<ulong, ItemState> HeldById { get; } = heldById;

		internal IReadOnlyDictionary<ulong, List<ItemState>> ChildrenByParent { get; } = childrenByParent;

		/// <summary>Every node the report stated, indexed by id wherever it sat in the tree — the state a moved node keeps.</summary>
		internal IReadOnlyDictionary<ulong, CharacterItemMsg> FileNodes { get; } = fileNodes;

		internal List<string> Notes { get; } = notes;
	}

	/// <summary>Index the report's whole item tree by id (a duplicate id keeps its first row; unbound id 0 is not an identity and is never indexed).</summary>
	private static void IndexFileNodes(IReadOnlyList<CharacterItemMsg> items, Dictionary<ulong, CharacterItemMsg> nodes)
	{
		foreach (var item in items)
		{
			if (item.InstanceId != 0 && !nodes.ContainsKey(item.InstanceId))
			{
				nodes[item.InstanceId] = item;
			}

			IndexFileNodes(item.Contents, nodes);
		}
	}

	/// <summary>Every bound id in one item tree (containers included); unbound rows carry no identity to compare.</summary>
	private static HashSet<ulong> Ids(IReadOnlyList<CharacterItemMsg> items)
	{
		var ids = new HashSet<ulong>();
		CollectIds(items, ids);
		return ids;
	}

	private static void CollectIds(IReadOnlyList<CharacterItemMsg> items, HashSet<ulong> ids)
	{
		foreach (var item in items)
		{
			if (item.InstanceId != 0)
			{
				ids.Add(item.InstanceId);
			}

			CollectIds(item.Contents, ids);
		}
	}

	/// <summary>Keep one file node at the top level, with its contents rebuilt from the kernel's placement.</summary>
	private static void Keep(CharacterItemMsg node, List<CharacterItemMsg> top, State state)
	{
		if (node.InstanceId != 0 && state.Placed.ContainsKey(node.InstanceId))
		{
			// One bound id twice in one file: the first placement stands, the duplicate is named.
			state.Notes.Add($"dropped a second row for item {Id(node.InstanceId)} (the file already carried it once)");
			return;
		}

		if (node.InstanceId != 0)
		{
			state.Placed[node.InstanceId] = node;
		}

		node.Contents = RebuildContents(node, state);
		top.Add(node);
	}

	/// <summary>
	/// The node's contents: the file's own children the kernel still places inside it (or has never
	/// judged), then the kernel's contained rows it was missing. A child the kernel moved to the TOP
	/// (a take-out) is not rebuilt here — the caller's second pass appends it there, with no body slot.
	/// </summary>
	private static List<CharacterItemMsg> RebuildContents(CharacterItemMsg node, State state)
	{
		var contents = new List<CharacterItemMsg>(node.Contents.Count);
		foreach (var child in node.Contents)
		{
			if (child.InstanceId == 0)
			{
				child.SlotIndex = node.SlotIndex;
				child.Contents = RebuildContents(child, state);
				contents.Add(child);
				continue;
			}

			if (!state.HeldById.TryGetValue(child.InstanceId, out var row))
			{
				if (state.ById.ContainsKey(child.InstanceId))
				{
					NoteRemoval(child, state.ById[child.InstanceId].Location, state, state.Notes);
					continue;
				}

				// Unknown to the kernel: the report is this id's only carrier — keep it where it is.
				if (state.Placed.ContainsKey(child.InstanceId))
				{
					continue;
				}

				state.Placed[child.InstanceId] = child;
				child.SlotIndex = node.SlotIndex;
				child.Contents = RebuildContents(child, state);
				contents.Add(child);
				continue;
			}

			if (row.Location.Kind != ItemLocationKind.Contained || row.Location.ParentItemId != node.InstanceId)
			{
				// The kernel says a different parent (or no container at all): that parent appends it.
				continue;
			}

			if (state.Placed.ContainsKey(child.InstanceId))
			{
				continue;
			}

			state.Placed[child.InstanceId] = child;
			child.SlotIndex = node.SlotIndex;
			child.Contents = RebuildContents(child, state);
			contents.Add(child);
		}

		if (state.ChildrenByParent.TryGetValue(node.InstanceId, out var kernelChildren))
		{
			foreach (var row in kernelChildren)
			{
				if (state.Placed.ContainsKey(row.Identity.InstanceId))
				{
					continue;
				}

				var child = state.FileNodes.TryGetValue(row.Identity.InstanceId, out var stated)
					? stated
					: FromKernel(row, state);
				state.Placed[row.Identity.InstanceId] = child;
				child.SlotIndex = node.SlotIndex;
				child.Contents = RebuildContents(child, state);
				contents.Add(child);
				state.Notes.Add($"moved {Describe(row)} inside item {Id(node.InstanceId)}");
			}
		}

		return contents;
	}

	/// <summary>Build a node from the kernel's own row: its state, its slot (a pickup row does not state one) and its contents.</summary>
	private static CharacterItemMsg FromKernel(ItemState row, State state)
	{
		var node = ItemKernelCodec.ToCharacterItem(row);
		state.Placed[node.InstanceId] = node;
		if (!state.ChildrenByParent.TryGetValue(row.Identity.InstanceId, out var kernelChildren))
		{
			return node;
		}

		var contents = new List<CharacterItemMsg>(kernelChildren.Count);
		foreach (var child in kernelChildren)
		{
			if (state.Placed.ContainsKey(child.Identity.InstanceId))
			{
				continue;
			}

			var childNode = FromKernel(child, state);
			childNode.SlotIndex = node.SlotIndex;
			contents.Add(childNode);
		}

		node.Contents = contents;
		return node;
	}

	/// <summary>
	/// An item the file carried that the kernel places elsewhere is REMOVED; an unknown descendant of
	/// it is lifted to the body instead of disappearing with it, and its contents are rebuilt so a
	/// descendant the kernel holds on this body cannot be serialized twice (inside the lifted node and
	/// again at the top).
	/// </summary>
	private static void LiftUnknown(CharacterItemMsg removedNode, State state, List<CharacterItemMsg> top, List<string> notes)
	{
		foreach (var child in removedNode.Contents)
		{
			var knownToKernel = state.ById.ContainsKey(child.InstanceId) || state.HeldById.ContainsKey(child.InstanceId);
			if (child.InstanceId != 0 && knownToKernel)
			{
				// The kernel describes this id too, so it is handled on its own placement; descend only
				// to find the unknown descendants it may still contain.
				LiftUnknown(child, state, top, notes);
				continue;
			}

			if (child.InstanceId != 0)
			{
				if (state.Placed.ContainsKey(child.InstanceId))
				{
					continue;
				}

				state.Placed[child.InstanceId] = child;
			}

			child.SlotIndex = UnknownSlot;
			child.Contents = RebuildContents(child, state);
			top.Add(child);
			notes.Add($"lifted {(child.InstanceId == 0 ? "an unbound item" : $"item {Id(child.InstanceId)}")} out of a container the kernel no longer places on this body (no body slot; the body places it)");
		}
	}

	/// <summary>Name a removed node and its removed descendants. A descendant the kernel holds on this body (moved) or has never judged (lifted) is NOT a removal and is not counted here.</summary>
	private static void NoteRemoval(CharacterItemMsg node, ItemLocation location, State state, List<string> notes, bool isRoot = true)
	{
		notes.Add(isRoot
			? $"removed {Describe(node)} ({Where(location)})"
			: $"removed {Describe(node)} (it was inside a container the kernel places elsewhere)");
		foreach (var child in node.Contents)
		{
			if (child.InstanceId == 0 || state.HeldById.ContainsKey(child.InstanceId) || !state.ById.ContainsKey(child.InstanceId))
			{
				continue;
			}

			NoteRemoval(child, location, state, notes, isRoot: false);
		}
	}

	private static string Describe(ItemState row) =>
		$"item {Id(row.Identity.InstanceId)} ({row.Identity.DefinitionId})";

	private static string Describe(CharacterItemMsg node) =>
		node.InstanceId == 0
			? "an unbound item"
			: $"item {Id(node.InstanceId)} ({(string.IsNullOrEmpty(node.ItemId) ? "no definition" : node.ItemId)})";

	private static string Where(ItemLocation location) => location.Kind switch
	{
		ItemLocationKind.World => "the kernel holds it in the world",
		ItemLocationKind.Carried => $"the kernel holds it carried by {location.Owner.Value.ToString(CultureInfo.InvariantCulture)}",
		ItemLocationKind.Contained => "the kernel holds it inside another container",
		ItemLocationKind.Applied => "the kernel holds it installed on a limb",
		ItemLocationKind.Terminal => "the kernel holds it terminal",
		_ => "the kernel places it elsewhere",
	};

	private static string Slot(int slotIndex) => slotIndex.ToString(CultureInfo.InvariantCulture);

	private static string Id(ulong instanceId) => instanceId.ToString(CultureInfo.InvariantCulture);
}
