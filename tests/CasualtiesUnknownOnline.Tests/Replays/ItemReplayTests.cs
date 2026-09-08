using System.Collections.Generic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// Phase-4 replay regression, item domain: every *.replay file whose exclusive
/// actions belong to the item world is one automated scenario — a real bug's
/// operation sequence fossilized as data (with its OperationTrace provenance in
/// the file's comments), driven over the full simulated stack (TestNode +
/// FakeNetwork). The shared stateless runner (<see cref="ReplayHarness"/>) owns
/// the assertions; splitting the folder by domain keeps xUnit v2 from
/// serializing every replay in one class collection.
/// </summary>
public class ItemReplayTests
{
	public static IEnumerable<object[]> Files => ReplayHarness.FilesOfDomain("item");

	[Theory]
	[MemberData(nameof(Files))]
	public void Replay(string fileName) => ReplayHarness.Run(fileName);
}
