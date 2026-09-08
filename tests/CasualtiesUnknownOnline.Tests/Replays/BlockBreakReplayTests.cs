using System.Collections.Generic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// Phase-4 replay regression, block-break domain: every *.replay file whose
/// exclusive actions belong to the block-break world runs over
/// <c>BlockBreakReplayWorld</c> (the first-writer-wins and unattributed-refusal
/// fossils). The shared stateless runner (<see cref="ReplayHarness"/>) owns the
/// assertions; splitting the folder by domain keeps xUnit v2 from serializing
/// every replay in one class collection.
/// </summary>
[Trait("Category", "Integration")]
public class BlockBreakReplayTests
{
	public static IEnumerable<object[]> Files => ReplayHarness.FilesOfDomain("block-break");

	[Theory]
	[MemberData(nameof(Files))]
	public void Replay(string fileName) => ReplayHarness.Run(fileName);
}
