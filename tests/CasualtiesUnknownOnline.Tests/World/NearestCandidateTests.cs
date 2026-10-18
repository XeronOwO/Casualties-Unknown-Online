using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The position-keyed identity rule the entity-event appliers share
/// (<see cref="NearestCandidate"/>): the entity NEAREST the event point inside the tolerance,
/// never the scene's first hit. The regression is named in the first fact — a first hit applies
/// an event to a sibling and judges a destructive event's drops against the WRONG prefab's
/// table, which is the runtime-entity geyser finding this rule replaces.
/// </summary>
public class NearestCandidateTests
{
	private static readonly (float X, float Y)[] TwoCandidates = [(0.5f, 0f), (2.9f, 0f)];

	/// <summary>BOTH candidates are inside the 3-unit tolerance; the first one the scene returns stands 2.3 units from the event point while the second stands 0.1. The old first hit answered 0 here, which is the defect: the applier would apply to (and read the table of) the wrong entity.</summary>
	[Fact]
	public void FindIndex_ReturnsTheNearest_NotTheFirstOneInsideTheTolerance() =>
		Assert.Equal(1, NearestCandidate.FindIndex(2.8f, 0f, TwoCandidates, 3f));

	[Fact]
	public void FindIndex_NoneInsideTheTolerance_ReturnsMinusOne() =>
		Assert.Equal(-1, NearestCandidate.FindIndex(10f, 0f, TwoCandidates, 3f));

	/// <summary>The appliers have always compared with a strict `&lt;`: a candidate at exactly the tolerance is NOT the entity at the point.</summary>
	[Fact]
	public void FindIndex_TheToleranceBoundaryIsExcluded()
	{
		Assert.Equal(-1, NearestCandidate.FindIndex(0f, 0f, [(3f, 0f)], 3f));
		Assert.Equal(0, NearestCandidate.FindIndex(0f, 0f, [(2.999f, 0f)], 3f));
	}

	/// <summary>Equal distances must answer the same index twice for one scene; the earlier one wins.</summary>
	[Fact]
	public void FindIndex_ATie_KeepsTheEarlierCandidate() =>
		Assert.Equal(0, NearestCandidate.FindIndex(0f, 0f, [(1f, 0f), (-1f, 0f)], 3f));

	[Fact]
	public void FindIndex_Empty_ReturnsMinusOne() =>
		Assert.Equal(-1, NearestCandidate.FindIndex(0f, 0f, [], 3f));

	[Fact]
	public void FindIndex_MeasuresBothAxes() =>
		Assert.Equal(1, NearestCandidate.FindIndex(1.9f, 0.1f, new List<(float X, float Y)> { (0f, 2.5f), (2f, 0f) }, 3f));
}
