namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The one answer to "does this build know this item definition": the game's item table,
/// the prefab it resolves to and the <see cref="Item"/> component a capture can read.
///
/// <para>
/// Two seams ask it — the give verb's creation port and the state language's definition
/// schema — and they must not drift about what "known" means: an id in the table whose
/// prefab cannot be loaded is refused by both, not discovered later by a receiving
/// client's failed instantiation.
/// </para>
/// </summary>
internal static class ItemDefinitionTable
{
	/// <summary>
	/// Resolves <paramref name="definitionId"/> to its prefab's <see cref="Item"/>; false
	/// with the operator's refusal when this build cannot. The refusal is the give verb's
	/// sentence: a caller that only wants a silent completion answer maps false to null.
	/// </summary>
	internal static bool TryResolve(string definitionId, out Item? template, out string? refusal)
	{
		template = null;
		refusal = null;

		if (string.IsNullOrWhiteSpace(definitionId))
		{
			refusal = "No item id was given — name the game's own item id, e.g. bandage.";
			return false;
		}

		if (Item.GlobalItems is not { } known || !known.ContainsKey(definitionId))
		{
			refusal = $"No item definition '{definitionId}' is known to this game — /give names a definition from the game's own item table.";
			return false;
		}

		var prefab = ItemPrefabResolver.Load(definitionId);
		if (prefab == null) // Unity object — ==
		{
			refusal = $"Item definition '{definitionId}' has no prefab this build can load, so it cannot be created.";
			return false;
		}

		template = prefab.GetComponent<Item>();
		if (template == null) // Unity object — ==
		{
			refusal = $"Item definition '{definitionId}' has no Item component on its prefab, so it cannot be created.";
			return false;
		}

		return true;
	}
}
