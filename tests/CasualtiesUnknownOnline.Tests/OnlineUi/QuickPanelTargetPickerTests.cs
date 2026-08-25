using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// Pure target selection for the standalone player-interaction quick panel.
/// The panel must keep a user-chosen target while it remains an in-world
/// remote and fall back to the deterministic nearest remote otherwise.
/// </summary>
public class QuickPanelTargetPickerTests
{
	[Fact]
	public void EmptyCandidates_ReturnsNull() => Assert.Null(QuickPanelTargetPicker.Resolve(null, 0f, 0f, []));

	[Fact]
	public void CurrentTargetStillPresent_IsKept()
	{
		var candidates = new List<QuickPanelTargetCandidate>
		{
			new(10, 100f, 100f),
			new(20, 10f, 10f),
		};

		var selected = QuickPanelTargetPicker.Resolve(10, 0f, 0f, candidates);

		Assert.Equal((ulong)10, selected);
	}

	[Fact]
	public void NoCurrentTarget_PicksNearest()
	{
		var candidates = new List<QuickPanelTargetCandidate>
		{
			new(10, 100f, 100f),
			new(20, 3f, 4f),
			new(30, -5f, 0f),
		};

		var selected = QuickPanelTargetPicker.Resolve(null, 0f, 0f, candidates);

		// 20 is 5 units away (3,4); 30 is 5 units away too — tie breaks to the
		// lower SteamId after distance.
		Assert.Equal((ulong)20, selected);
	}

	[Fact]
	public void CurrentTargetGone_PicksNearest()
	{
		var candidates = new List<QuickPanelTargetCandidate>
		{
			new(20, 3f, 4f),
			new(30, 100f, 0f),
		};

		var selected = QuickPanelTargetPicker.Resolve(10, 0f, 0f, candidates);

		Assert.Equal((ulong)20, selected);
	}

	[Fact]
	public void EqualDistance_TieBreaksByLowerSteamId()
	{
		var candidates = new List<QuickPanelTargetCandidate>
		{
			new(30, -5f, 0f),
			new(20, 3f, 4f),
		};

		var selected = QuickPanelTargetPicker.Resolve(null, 0f, 0f, candidates);

		Assert.Equal((ulong)20, selected);
	}
}
