using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// Phase-4 replay regression: every *.replay file under Replays/ is one
/// automated scenario — a real bug's operation sequence fossilized as data
/// (with its OperationTrace provenance in the file's comments), driven over
/// the full simulated stack (TestNode + FakeNetwork, same injection helpers
/// as the hand-written simulations). A replay that cannot be parsed, an
/// assertion that never converges or an expectation the world violates fails
/// this test with the file:line of the offending step. Hand-written scenarios
/// keep covering what replay files do not express (random sequences, world
/// setup variants); the replay archive covers "this exact bug, forever".
/// </summary>
public class ReplayTests
{
	public static IEnumerable<object[]> ReplayFiles() =>
		Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Replays"), "*.replay")
			.Select(path => new object[] { Path.GetFileName(path) })
			.OrderBy(args => (string)args[0]);

	[Theory]
	[MemberData(nameof(ReplayFiles))]
	public void Replay(string fileName)
	{
		var path = Path.Combine(AppContext.BaseDirectory, "Replays", fileName);
		var steps = ReplayParser.Parse(File.ReadAllText(path), fileName);
		using var world = ItemSimWorld.Create();
		ReplayRunner.Run(fileName, world, steps);
	}
}
