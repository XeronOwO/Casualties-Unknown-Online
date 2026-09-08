using System.Collections.Generic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// Phase-4 replay regression, trade domain: every *.replay file whose exclusive
/// actions belong to the trade world runs over <c>TradeReplayWorld</c> (the
/// delayed-sequence, full-sequence and rejected-purchase fossils). The shared
/// stateless runner (<see cref="ReplayHarness"/>) owns the assertions;
/// splitting the folder by domain keeps xUnit v2 from serializing every replay
/// in one class collection.
/// </summary>
[Trait("Category", "Integration")]
public class TradeReplayTests
{
	public static IEnumerable<object[]> Files => ReplayHarness.FilesOfDomain("trade");

	[Theory]
	[MemberData(nameof(Files))]
	public void Replay(string fileName) => ReplayHarness.Run(fileName);
}
