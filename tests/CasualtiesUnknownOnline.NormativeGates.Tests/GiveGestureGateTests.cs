using System;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The give gesture's release-order pin (user ruling 2026-10-22): a release on the displayed backpack is a
/// GIVE even when the cursor's world point is also inside the player's detection range, where the same
/// release would be a USE. The order lives in one <c>if</c> that a later edit can silently swap, and a scene
/// test is the only other thing that could see it, so it is pinned here.
///
/// <para>
/// The second check is the refusal half: the close rule keeps the drag-opened backpack only for a give that
/// actually went out. The seam's answer is what proves it — a refused give (view-only pack, an item with no
/// authoritative id) must close the view, so the flag must come from the call's answer and the finalizer must
/// read it.
/// </para>
/// </summary>
public class GiveGestureGateTests
{
	private const string ReleasePatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/PlayerCameraDragUsePatch.cs";

	private const string CueTrackerFile = "src/CasualtiesUnknownOnline.GameAdapter/DragUseCueTracker.cs";

	[Fact]
	public void ADisplayedBackpackRelease_IsResolvedBeforeTheInRangeUse()
	{
		var source = RepositoryPaths.ReadText(ReleasePatchFile);

		Assert.True(
			OrderHolds(source, "DragGiveTargetResolver.Resolve", "TryHandleDraggedItemUseOnRemote"),
			"the give resolution must run before the in-range use: a release on the displayed backpack is a GIVE even when the cursor is also inside the player's detection range");
	}

	[Fact]
	public void TheGiveFlag_IsTheSeamsAnswer_AndTheCloseRuleReadsIt()
	{
		var source = RepositoryPaths.ReadText(ReleasePatchFile);

		Assert.Contains("__state.Give = giveBridge.ReportLocalReleaseOntoProxy(", source, StringComparison.Ordinal);
		Assert.Contains("__state.AutoOpened && !__state.Give", source, StringComparison.Ordinal);
	}

	[Fact]
	public void TheCueAndTheRelease_ReadTheSameGiveResolution()
	{
		Assert.Contains("DragGiveTargetResolver.Resolve", RepositoryPaths.ReadText(ReleasePatchFile), StringComparison.Ordinal);
		Assert.Contains("DragGiveTargetResolver.Resolve", RepositoryPaths.ReadText(CueTrackerFile), StringComparison.Ordinal);
	}

	[Fact]
	public void TheOrderMatcher_SeesBothShapes()
	{
		Assert.True(OrderHolds("first(); second();", "first", "second"));
		Assert.False(OrderHolds("second(); first();", "first", "second"));
		Assert.False(OrderHolds("only one();", "first", "second"));
	}

	private static bool OrderHolds(string source, string before, string after)
	{
		var beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
		var afterIndex = source.IndexOf(after, StringComparison.Ordinal);
		return beforeIndex >= 0 && afterIndex >= 0 && beforeIndex < afterIndex;
	}
}
