using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The default <see cref="IItemContainerFit"/>: no container is known to be able to hold anything, so the
/// give ladder's container rungs are skipped. A build without the Game Adapter (a test host, a headless one)
/// registers this and the ladder still delivers — a body slot first, a drop last — instead of throwing.
/// </summary>
public sealed class UnavailableItemContainerFit : IItemContainerFit
{
	/// <inheritdoc />
	public bool TryFindContainer(IReadOnlyList<CharacterItemMsg> candidates, CharacterItemMsg item, out ulong containerId)
	{
		containerId = 0;
		return false;
	}
}
