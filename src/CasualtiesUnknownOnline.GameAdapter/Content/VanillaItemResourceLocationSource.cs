using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The vanilla game-content half of the resource vocabulary: every entry in the
/// game's own item table (<c>Item.GlobalItems</c>) becomes <c>cu:&lt;item id&gt;</c>
/// with the game-localised <c>ItemInfo.fullName</c> as display name (evidence:
/// <c>Item.cs:7096</c> assigns <c>Locale.GetItem(id)</c>, so a Chinese client
/// sees and completes by the Chinese name). Mod items actually injected into the
/// same table are excluded: they are addressable only through their owning mod's
/// namespace, which the Runtime mod-content source provides.
///
/// The table → entry mapping is a pure static method so the game-assembly
/// contract tests can lock its branches without a running Unity instance.
/// </summary>
public sealed class VanillaItemResourceLocationSource(
	GameAdapterItemContentProvider itemContent,
	ILogger<VanillaItemResourceLocationSource> log) : IResourceLocationSource
{
	/// <inheritdoc />
	public IReadOnlyList<ResourceLocationEntry> Entries
	{
		get
		{
			var items = Item.GlobalItems;
			if (items is null) // static game table not ready yet — no game entries to offer
			{
				return [];
			}

			var pairs = new List<KeyValuePair<string, string?>>(items.Count);
			foreach (var pair in items)
			{
				pairs.Add(new KeyValuePair<string, string?>(pair.Key, pair.Value?.fullName));
			}

			return BuildEntries(pairs, itemContent.InjectedItemIds, log);
		}
	}

	/// <summary>
	/// Pure table → resource-entry mapping: skip injected mod items and ids that
	/// cannot form a canonical path, and fall back to the bare id when the game
	/// table has no display name.
	/// </summary>
	internal static IReadOnlyList<ResourceLocationEntry> BuildEntries(
		IReadOnlyList<KeyValuePair<string, string?>> items,
		IReadOnlyCollection<string> injectedItemIds,
		ILogger log)
	{
		var modItemIds = new HashSet<string>(injectedItemIds, StringComparer.Ordinal);
		var entries = new List<ResourceLocationEntry>(items.Count);
		foreach (var pair in items)
		{
			if (modItemIds.Contains(pair.Key))
			{
				continue;
			}

			if (!ContentId.TryCreate(ContentId.BuiltInNamespace, pair.Key, out var id))
			{
				log.LogDebug("[ContentId] vanilla item id '{Id}' is not a valid content path — skipped.", pair.Key);
				continue;
			}

			entries.Add(new ResourceLocationEntry(
				id,
				ModContentKind.Item,
				string.IsNullOrWhiteSpace(pair.Value) ? pair.Key : pair.Value!));
		}

		return entries;
	}
}
