using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The container half of <c>/give</c>'s destination ladder (the user's ruling of 2026-10-17): given the
/// recipient's carried items as the host's snapshot sees them, which one can take the created item by its
/// OWN rules — a container's weight capacity, the per-item limit and its tag restriction.
///
/// <para>
/// The split is the family's own: the Runtime owns the ladder's ORDER and the sentences the operator reads,
/// while "can this container hold this item" is a game rule the adapter alone can answer — it is the same
/// rule the receiving client's own <c>Container.CanHoldItem</c> applies when the item arrives. The Runtime
/// passes the candidates in ladder order (the body's carried containers, then the worn ones) and takes the
/// first id the port answers; a port that answers none means the item falls to the next rung.
/// </para>
///
/// <para>
/// A build with no Game Adapter registers <see cref="UnavailableItemContainerFit"/>, so the ladder skips its
/// container rungs and answers the drop rung rather than pretending a container can hold anything.
/// </para>
/// </summary>
public interface IItemContainerFit
{
	/// <summary>
	/// The instance id of the FIRST candidate whose own container rules can hold <paramref name="item"/>,
	/// or 0 when none can. A candidate that is not a container, or whose definition this build cannot read,
	/// is skipped rather than guessed at.
	/// </summary>
	bool TryFindContainer(IReadOnlyList<CharacterItemMsg> candidates, CharacterItemMsg item, out ulong containerId);
}
