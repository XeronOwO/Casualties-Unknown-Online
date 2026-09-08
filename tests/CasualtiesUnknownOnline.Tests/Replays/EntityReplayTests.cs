using System.Collections.Generic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// Phase-4 replay regression, entity/fluid domain: every *.replay file whose
/// exclusive actions belong to the entity-event world (the event/checkpoint/
/// fluid actions and the replayed/executed/fluid assertions — the replay
/// files' entity/fluid bug fossils) runs over the three-node
/// <c>EntityEventSimWorld</c>. The shared stateless runner
/// (<see cref="ReplayHarness"/>) owns the assertions; splitting the folder by
/// domain keeps xUnit v2 from serializing every replay in one class collection.
/// </summary>
public class EntityReplayTests
{
	public static IEnumerable<object[]> Files => ReplayHarness.FilesOfDomain("entity");

	[Theory]
	[MemberData(nameof(Files))]
	public void Replay(string fileName) => ReplayHarness.Run(fileName);
}
