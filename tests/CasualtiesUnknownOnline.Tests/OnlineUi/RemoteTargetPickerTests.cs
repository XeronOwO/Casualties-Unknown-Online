using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

public sealed class RemoteTargetPickerTests
{
	[Fact]
	public void ReturnsAllCandidatesInsideRadiusOrderedByDistance()
	{
		var candidates = new List<RemoteScreenTarget>
		{
			new(200, 120f, 120f),
			new(100, 100f, 100f),
			new(300, 110f, 100f),
			new(400, 200f, 200f),
		};

		var matches = RemoteTargetPicker.Find(candidates, mouseX: 100f, mouseY: 100f, radius: 30f);

		Assert.Equal([100UL, 300UL, 200UL], matches.Select(m => m.SteamId).ToArray());
	}

	[Fact]
	public void Ties_AreBrokenBySteamId()
	{
		var candidates = new List<RemoteScreenTarget>
		{
			new(300, 110f, 100f),
			new(100, 100f, 110f),
		};

		var matches = RemoteTargetPicker.Find(candidates, mouseX: 100f, mouseY: 100f, radius: 20f);

		Assert.Equal([100UL, 300UL], matches.Select(m => m.SteamId).ToArray());
	}

	[Fact]
	public void EmptyInput_ReturnsEmpty()
	{
		var matches = RemoteTargetPicker.Find([], mouseX: 0f, mouseY: 0f, radius: 10f);

		Assert.Empty(matches);
	}

	[Fact]
	public void NegativeRadius_ReturnsEmpty()
	{
		var candidates = new List<RemoteScreenTarget> { new(1, 0f, 0f) };

		var matches = RemoteTargetPicker.Find(candidates, mouseX: 0f, mouseY: 0f, radius: -1f);

		Assert.Empty(matches);
	}

	[Fact]
	public void ExactMousePosition_IsIncludedAtZeroRadius()
	{
		var candidates = new List<RemoteScreenTarget> { new(1, 5f, 5f) };

		var matches = RemoteTargetPicker.Find(candidates, mouseX: 5f, mouseY: 5f, radius: 0f);

		Assert.Single(matches);
		Assert.Equal(1UL, matches[0].SteamId);
	}
}
