using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The container half of the give ladder as a test's own fixture: the ladder's ORDER is the Runtime's
/// claim, so the fixture records every candidate it was offered (in the order the ladder asked) and
/// answers the one id the case says can hold the item. The capacity arithmetic behind a real answer is
/// the Game Adapter's own rule and has no test host; the acceptance batch reads it on a real client.
/// </summary>
internal sealed class FakeItemContainerFit : IItemContainerFit
{
	/// <summary>Every candidate the ladder offered, in order, by instance id.</summary>
	internal List<ulong> Offered { get; } = [];

	/// <summary>The one container this fixture says can hold the item; 0 means none can, so the ladder falls to its drop rung.</summary>
	internal ulong Fits { get; set; }

	/// <inheritdoc />
	public bool TryFindContainer(IReadOnlyList<CharacterItemMsg> candidates, CharacterItemMsg item, out ulong containerId)
	{
		containerId = 0;
		foreach (var candidate in candidates)
		{
			Offered.Add(candidate.InstanceId);
			if (Fits != 0 && candidate.InstanceId == Fits)
			{
				containerId = Fits;
				return true;
			}
		}

		return false;
	}
}
