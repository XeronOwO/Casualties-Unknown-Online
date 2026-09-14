using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Source-shape contract for the Harmony patch whose verdict SKIPS the game's own
/// action. A bool prefix answers "run the original?" — true runs it — so an
/// inverted expression skips the action whenever it should not (observed: the
/// menu-exit interception's first cut returned the verdict inverted, which
/// silently swallowed the player's leave). The verdict itself is the unit-tested
/// <c>MenuExitInterception</c> + <c>RunMenuReturnPolicy.WouldLeaveWorld</c>; what
/// this test locks is that the one-line adapter still asks that question, still
/// negates it, and still checks the world half here instead of re-deriving it.
/// </summary>
public class MenuExitPatchShapeTests
{
	private static readonly Regex PrefixExpression = new(
		@"private static bool Prefix\(\)\s*=>\s*(?<body>[^;]+);",
		RegexOptions.Singleline | RegexOptions.CultureInvariant);

	private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

	private static readonly string SourcePath = Path.GetFullPath(Path.Combine(
		AppContext.BaseDirectory,
		"..", "..", "..", "..", "..",
		"src", "CasualtiesUnknownOnline.GameAdapter", "Patches", "PlayerCameraMenuExitPatches.cs"));

	[Fact]
	public void Prefix_SkipsTheOriginalOnlyWhenTheDeferralWasRecorded()
	{
		Assert.True(File.Exists(SourcePath), $"menu-exit patch source not found at {SourcePath}");

		var match = PrefixExpression.Match(File.ReadAllText(SourcePath));
		Assert.True(match.Success, "the menu-exit patch has no expression-bodied bool Prefix()");

		var body = Whitespace.Replace(match.Groups["body"].Value, string.Empty);

		Assert.StartsWith("!(", body, StringComparison.Ordinal);
		Assert.Contains("HarmonyTraverse.HasLiveWorld", body, StringComparison.Ordinal);
		Assert.Contains("TryDeferMenuReturn(hasLiveWorld:true)", body, StringComparison.Ordinal);
	}
}
