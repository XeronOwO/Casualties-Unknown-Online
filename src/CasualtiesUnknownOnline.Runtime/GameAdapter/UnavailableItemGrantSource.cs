using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The default <see cref="IItemGrantSource"/> of a build with no Game Adapter: the
/// item definition table and the prefab resolution are the game's own, so this
/// build cannot create a definition at all. The verb answers with that fact rather
/// than with "no such item", which would blame the operator for the build's gap —
/// the same shape <see cref="UnavailableLocalItemAccess"/> takes for the write verb.
/// </summary>
public sealed class UnavailableItemGrantSource : IItemGrantSource
{
	public bool TryCreate(string definitionId, SnbtValue? state, out CharacterItemMsg? item, out GrantRefusal? refusal)
	{
		item = null;
		refusal = new GrantRefusal($"This build cannot create items: no Game Adapter is loaded, so '{definitionId}' cannot be resolved to a prefab or given an instance id.", "item.answer.refused_no_adapter", definitionId);
		return false;
	}
}
