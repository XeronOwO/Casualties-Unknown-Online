using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// The item state language's tree operations: resolve, replace, merge, insert and remove,
/// over immutable values. Every edit returns the new root and leaves the old tree alone,
/// which is what lets a command apply several changes and bind them once.
///
/// <para>
/// The path must EXIST for every operation, as it does in Minecraft: <c>set</c> replaces
/// a value, it does not create a key (a merge is how keys are added), and <c>remove</c>
/// deletes a node. The only path that may be empty is the document root — <c>get</c>
/// prints it and <c>merge</c> merges into it — while a replace or remove of the root is
/// refused: the item's state is the document, so there is nothing to replace it with.
/// </para>
/// </summary>
public static class SnbtTree
{
	/// <summary>Resolves one path to the value it addresses.</summary>
	public static bool TryResolve(SnbtValue root, NbtPath path, out SnbtValue value, out string? error)
	{
		value = SnbtCompound.Empty;
		error = null;
		var node = root;
		for (var at = 0; at < path.Segments.Count; at++)
		{
			if (!TryStep(node, path, at, out node, out error))
			{
				return false;
			}
		}

		value = node;
		return true;
	}

	/// <summary>Replaces the value at an existing path.</summary>
	public static bool TrySet(SnbtValue root, NbtPath path, SnbtValue replacement, out SnbtValue updated, out string? error)
	{
		if (path.IsRoot)
		{
			updated = replacement;
			error = null;
			return true;
		}

		return TryReplace(root, path, 0, replacement, out updated, out error);
	}

	/// <summary>
	/// Merges a compound into the compound at an existing path. The merge is SHALLOW, as
	/// Minecraft's is: a key it names is replaced whole, and a nested field is changed
	/// with <c>modify — merge</c> at that field's own path.
	/// </summary>
	public static bool TryMerge(SnbtValue root, NbtPath path, SnbtCompound merge, out SnbtValue updated, out string? error)
	{
		updated = root;
		error = null;
		if (!TryResolve(root, path, out var target, out error))
		{
			return false;
		}

		if (target is not SnbtCompound compound)
		{
			error = $"'{path.Text}' is {target.Kind}, not a compound — merge needs a compound to merge into";
			return false;
		}

		var merged = compound;
		foreach (var entry in merge.Entries)
		{
			merged = merged.With(entry.Key, entry.Value);
		}

		return TrySet(root, path, merged, out updated, out error);
	}

	/// <summary>
	/// Inserts <paramref name="item"/> into the list at an existing path, at
	/// <paramref name="index"/> (0..count). Append and prepend are this operation with the
	/// index the verb chose, so one rule decides what an insert means.
	/// </summary>
	public static bool TryInsert(SnbtValue root, NbtPath path, int index, SnbtValue item, out SnbtValue updated, out string? error)
	{
		updated = root;
		error = null;
		if (!TryResolve(root, path, out var target, out error))
		{
			return false;
		}

		if (target is not SnbtList list)
		{
			error = $"'{path.Text}' is {target.Kind}, not a list — append, prepend and insert add to a list";
			return false;
		}

		if (index < 0 || index > list.Items.Count)
		{
			error = $"'{path.Text}' has {list.Items.Count} element(s), so index {index} is out of range — insert takes 0..{list.Items.Count}";
			return false;
		}

		var items = new List<SnbtValue>(list.Items.Count + 1);
		for (var i = 0; i < list.Items.Count; i++)
		{
			if (i == index)
			{
				items.Add(item);
			}

			items.Add(list.Items[i]);
		}

		if (index == list.Items.Count)
		{
			items.Add(item);
		}

		return TrySet(root, path, new SnbtList(items), out updated, out error);
	}

	/// <summary>Removes the node at an existing path and reports it, so an answer can print what went.</summary>
	public static bool TryRemove(SnbtValue root, NbtPath path, out SnbtValue updated, out SnbtValue removed, out string? error)
	{
		updated = root;
		removed = SnbtCompound.Empty;
		error = null;
		if (path.IsRoot)
		{
			error = "the whole state cannot be removed — remove a key or a list element, e.g. <list>[0]";
			return false;
		}

		return TryRemoveAt(root, path, 0, out updated, out removed, out error);
	}

	/// <summary>
	/// The give verb's state overlay: compounds overlay recursively, everything else
	/// replaces. Different from <see cref="TryMerge"/> on purpose — a give has no document
	/// to start from, so the operator names only what should differ from the prefab's
	/// capture, while a merge edits a document they just read.
	/// </summary>
	public static SnbtValue Overlay(SnbtValue target, SnbtValue overlay)
	{
		if (target is not SnbtCompound compound || overlay is not SnbtCompound patch)
		{
			return overlay;
		}

		var result = compound;
		foreach (var entry in patch.Entries)
		{
			result = result.TryGet(entry.Key, out var existing)
				? result.With(entry.Key, Overlay(existing, entry.Value))
				: result.With(entry.Key, entry.Value);
		}

		return result;
	}

	private static bool TryStep(SnbtValue node, NbtPath path, int at, out SnbtValue child, out string? error)
	{
		child = SnbtCompound.Empty;
		error = null;
		var prefix = Parent(path, at);
		switch (path.Segments[at])
		{
			case NbtPathKey key:
				if (node is not SnbtCompound compound)
				{
					error = $"{prefix} is {node.Kind}, not a compound — '{key.Key}' needs a compound";
					return false;
				}

				if (!compound.TryGet(key.Key, out child))
				{
					error = $"{prefix} has no '{key.Key}' — it carries: {Keys(compound)}";
					return false;
				}

				return true;

			case NbtPathIndex index:
				if (node is not SnbtList list)
				{
					error = $"{prefix} is {node.Kind}, not a list — '[{index.Index}]' needs a list";
					return false;
				}

				if (!TryResolveIndex(list, index.Index, out _, out var resolvedIndex))
				{
					error = $"{prefix} has {list.Items.Count} element(s), so [{index.Index}] is out of range";
					return false;
				}

				child = list.Items[resolvedIndex];
				return true;

			default:
				error = "unsupported path segment";
				return false;
		}
	}

	private static bool TryReplace(SnbtValue node, NbtPath path, int at, SnbtValue replacement, out SnbtValue updated, out string? error)
	{
		updated = node;
		error = null;
		if (at == path.Segments.Count)
		{
			updated = replacement;
			return true;
		}

		var prefix = Parent(path, at);
		switch (path.Segments[at])
		{
			case NbtPathKey key:
				if (node is not SnbtCompound compound)
				{
					error = $"{prefix} is {node.Kind}, not a compound — '{key.Key}' needs a compound";
					return false;
				}

				if (!compound.TryGet(key.Key, out var existing))
				{
					error = $"{prefix} has no '{key.Key}' — it carries: {Keys(compound)}. Set replaces a value; merge is how a compound gains a key";
					return false;
				}

				if (!TryReplace(existing, path, at + 1, replacement, out var child, out error))
				{
					return false;
				}

				updated = compound.With(key.Key, child);
				return true;

			case NbtPathIndex index:
				if (node is not SnbtList list)
				{
					error = $"{prefix} is {node.Kind}, not a list — '[{index.Index}]' needs a list";
					return false;
				}

				if (!TryResolveIndex(list, index.Index, out _, out var resolvedIndex))
				{
					error = $"{prefix} has {list.Items.Count} element(s), so [{index.Index}] is out of range";
					return false;
				}

				if (!TryReplace(list.Items[resolvedIndex], path, at + 1, replacement, out var element, out error))
				{
					return false;
				}

				List<SnbtValue> items = [.. list.Items];
				items[resolvedIndex] = element;
				updated = new SnbtList(items);
				return true;

			default:
				error = "unsupported path segment";
				return false;
		}
	}

	private static bool TryRemoveAt(SnbtValue node, NbtPath path, int at, out SnbtValue updated, out SnbtValue removed, out string? error)
	{
		updated = node;
		removed = SnbtCompound.Empty;
		error = null;
		var last = at == path.Segments.Count - 1;
		var prefix = Parent(path, at);
		switch (path.Segments[at])
		{
			case NbtPathKey key:
				if (node is not SnbtCompound compound)
				{
					error = $"{prefix} is {node.Kind}, not a compound — '{key.Key}' needs a compound";
					return false;
				}

				if (!compound.TryGet(key.Key, out var existing))
				{
					error = $"{prefix} has no '{key.Key}' — it carries: {Keys(compound)}";
					return false;
				}

				if (last)
				{
					removed = existing;
					updated = compound.Without(key.Key);
					return true;
				}

				if (!TryRemoveAt(existing, path, at + 1, out var child, out removed, out error))
				{
					return false;
				}

				updated = compound.With(key.Key, child);
				return true;

			case NbtPathIndex index:
				if (node is not SnbtList list)
				{
					error = $"{prefix} is {node.Kind}, not a list — '[{index.Index}]' needs a list";
					return false;
				}

				if (!TryResolveIndex(list, index.Index, out _, out var resolvedIndex))
				{
					error = $"{prefix} has {list.Items.Count} element(s), so [{index.Index}] is out of range";
					return false;
				}

				if (last)
				{
					removed = list.Items[resolvedIndex];
					var items = new List<SnbtValue>(list.Items.Count - 1);
					for (var i = 0; i < list.Items.Count; i++)
					{
						if (i != resolvedIndex)
						{
							items.Add(list.Items[i]);
						}
					}

					updated = new SnbtList(items);
					return true;
				}

				if (!TryRemoveAt(list.Items[resolvedIndex], path, at + 1, out var element, out removed, out error))
				{
					return false;
				}

				List<SnbtValue> rebuilt = [.. list.Items];
				rebuilt[resolvedIndex] = element;
				updated = new SnbtList(rebuilt);
				return true;

			default:
				error = "unsupported path segment";
				return false;
		}
	}

	private static bool TryResolveIndex(SnbtList list, int index, out SnbtValue value, out int resolvedIndex)
	{
		value = SnbtCompound.Empty;
		resolvedIndex = index < 0 ? list.Items.Count + index : index;
		if (resolvedIndex < 0 || resolvedIndex >= list.Items.Count)
		{
			return false;
		}

		value = list.Items[resolvedIndex];
		return true;
	}

	/// <summary>The name of the node whose segment is <paramref name="at"/> — the path up to its PARENT, quoted for a sentence; "the state" at the root.</summary>
	private static string Parent(NbtPath path, int at) =>
		at == 0 ? "the state" : $"'{new NbtPath([.. path.Segments.Take(at)]).Text}'";

	/// <summary>The keys a compound carries, for a refusal that names what the path could have addressed.</summary>
	private static string Keys(SnbtCompound compound) =>
		compound.Entries.Count == 0
			? "no key"
			: string.Join(", ", compound.Entries.Select(entry => entry.Key));
}
