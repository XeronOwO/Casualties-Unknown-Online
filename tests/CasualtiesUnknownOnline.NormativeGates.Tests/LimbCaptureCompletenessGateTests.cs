using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The limb rows are ONE capture. A month of snapshots fed two loops — the 1 Hz character report's
/// loop in <c>CharacterDataCapture</c> and a second loop in <c>CharacterDataSync</c> — and the second
/// one left <c>IsHead</c>/<c>IsVital</c>/<c>DistanceToHeart</c>/<c>Components</c> at their CLR
/// defaults. That was invisible while the rows were only reports; the heal reset APPLIES a captured
/// row back onto the live limb, so the defaults became false/false/0 written onto a real body, and
/// the reset's own re-report then published the corruption to the host and every peer.
///
/// <para>
/// The fix is a single complete helper, and this gate is what keeps it single and complete: the
/// assignments that are not discoverable from the mapper (index, topology, components, graph
/// translation) must be in the one helper, and no second mapper loop may exist beside it. The
/// matcher is pinned with a positive and a negative sample, so a gate that silently stopped
/// matching fails rather than passing by checking nothing.
/// </para>
/// </summary>
public class LimbCaptureCompletenessGateTests
{
	private const string CapturePath = "src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataCapture.cs";
	private const string SyncPath = "src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs";

	/// <summary>The helper's own body must carry every field the mapper cannot produce, plus the index the wire uses.</summary>
	private static readonly string[] RequiredAssignments =
	[
		"limbMsg.Index = i;",
		"limbMsg.IsHead = body.limbs[i].isHead;",
		"limbMsg.IsVital = body.limbs[i].isVital;",
		"limbMsg.Components = LimbComponentStateCodec.Capture(body.limbs[i]);",
		"limbMsg.ConnectedLimbIndices =",
		"limbMsg.DistanceToHeart = body.limbs[i].distanceToHeart;",
	];

	[Fact]
	public void TheCaptureHelper_CarriesEveryFieldTheMapperCannotProduce()
	{
		var source = File.ReadAllText(RepositoryPaths.File(CapturePath));
		var missing = MissingAssignments(source);

		Assert.True(missing.Count == 0,
			$"{CapturePath}: the limb capture helper is missing {string.Join(", ", missing)}; a row captured without them writes CLR defaults onto the live limb when it is applied");
		Assert.Equal(1, CountOccurrences(source, "mapper.Map<CharacterLimbMsg>"));
	}

	[Fact]
	public void TheCharacterSync_HasNoSecondLimbCaptureLoop()
	{
		var source = File.ReadAllText(RepositoryPaths.File(SyncPath));

		Assert.Contains("CharacterDataCapture.CaptureLimbs(_mapper, body)", source, StringComparison.Ordinal);
		Assert.DoesNotContain("_mapper.Map<CharacterLimbMsg>", source, StringComparison.Ordinal);
	}

	/// <summary>The matcher's contract: a complete sample passes, a sample missing one assignment names it.</summary>
	[Fact]
	public void MissingAssignments_PinsTheMatcher()
	{
		var complete = string.Join(Environment.NewLine, RequiredAssignments);

		Assert.Empty(MissingAssignments(complete));
		Assert.Equal(["limbMsg.IsVital = body.limbs[i].isVital;"], MissingAssignments(complete.Replace(RequiredAssignments[2], "", StringComparison.Ordinal)));
	}

	private static List<string> MissingAssignments(string source) =>
		[.. RequiredAssignments.Where(assignment => !source.Contains(assignment, StringComparison.Ordinal))];

	private static int CountOccurrences(string source, string text) =>
		source.Split([text], StringSplitOptions.None).Length - 1;
}
