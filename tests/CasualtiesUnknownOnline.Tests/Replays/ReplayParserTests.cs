using System;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The replay-file format's structural contract: comments and blank lines are
/// skipped, steps carry their source line, equal timestamps run in file order,
/// and every malformed shape (missing '@', bad timestamp, backwards timeline,
/// unknown action, wrong argument count, unknown node) fails with the
/// file:line of the offending line — a replay file that cannot be understood
/// is a test failure, never a silent skip.
/// </summary>
public class ReplayParserTests
{
	[Fact]
	public void SkipsCommentsAndBlankLines()
	{
		var steps = ReplayParser.Parse(
			"""
			# provenance: op=3 item=42 origin=OnItemDropped result=Committed(1) events=[Drop, Throw]

			@0 spawn g1 42 test_item 1.0
			# another comment
			@33 pickup g1 42 test_item 1.0
			""", "sample.replay");

		Assert.Equal(2, steps.Length);
		Assert.Equal(3, steps[0].Line); // the first step's real line (1-2 are the comment and the blank line)
		Assert.Equal(5, steps[1].Line);
	}

	[Fact]
	public void EqualTimestampsKeepFileOrder()
	{
		var steps = ReplayParser.Parse(
			"""
			@33 pickup g1 42 test_item 1.0
			@33 pickup g2 42 test_item 1.0
			""", "same-frame.replay");

		Assert.Equal(33, steps[0].Ms);
		Assert.Equal(33, steps[1].Ms);
		Assert.Equal("pickup", steps[0].Action);
		Assert.Equal("g2", steps[1].Args[0]);
	}

	[Theory]
	[InlineData("no-at-sign\n", ":1: expected '@<ms> <action>'")]
	[InlineData("@abc spawn g1 42 t 1.0\n", ":1: invalid timestamp")]
	[InlineData("@66 pickup g1 42 t 1.0\n@33 pickup g2 42 t 1.0\n", ":2: timestamp 33 precedes")]
	[InlineData("@0 nuke g1 42\n", ":1: unknown action 'nuke'")]
	[InlineData("@0 spawn g1 42\n", ":1: 'spawn' needs at least 4 argument")]
	[InlineData("@0 spawn g2x 42 t 1.0\n", ":1: 'spawn' — unknown node 'g2x'")]
	[InlineData("@0 event g1 MineExploded 10\n", ":1: 'event' needs at least 4 argument")]
	[InlineData("@0 snapshot g2x\n", ":1: 'snapshot' — unknown node 'g2x'")]
	[InlineData("@0 fluid g2x 0 0\n", ":1: 'fluid' — unknown node 'g2x'")]
	[InlineData("", "no replay steps")]
	public void MalformedShapesFailWithSourceLine(string text, string expectedFragment)
	{
		var e = Assert.Throws<FormatException>(() => ReplayParser.Parse(text, "bad.replay"));
		Assert.Contains(expectedFragment, e.Message);
		Assert.StartsWith("bad.replay", e.Message);
	}

	[Fact]
	public void EmptyReplayFileFails()
	{
		var e = Assert.Throws<FormatException>(() => ReplayParser.Parse("# only comments\n", "empty.replay"));
		Assert.Contains("no replay steps", e.Message);
	}
}
