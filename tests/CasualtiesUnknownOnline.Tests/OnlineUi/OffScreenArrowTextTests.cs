using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The mark an off-screen marker draws (ticket online-ui-art-and-controls-overhaul, S6). The rule is the
/// Runtime's because both IMGUI surfaces that used to draw these markers — the nameplate/arrow overlay and
/// the location pings — carried their own copy of the same four-way switch, and a migration that leaves
/// two copies behind would drift the moment one of them changed.
///
/// <para>
/// Only the mapping is tested here: where a marker lands is <c>OffScreenArrowGeometry</c>'s own matrix, and
/// whether the answer reaches the label is pinned against the real source in
/// <c>OnlineUiWorldOverlayPinTests</c>.
/// </para>
/// </summary>
public sealed class OffScreenArrowTextTests
{
	[Theory]
	[InlineData(OffScreenArrowDirection.Up, OffScreenArrowText.Up)]
	[InlineData(OffScreenArrowDirection.Down, OffScreenArrowText.Down)]
	[InlineData(OffScreenArrowDirection.Left, OffScreenArrowText.Left)]
	[InlineData(OffScreenArrowDirection.Right, OffScreenArrowText.Right)]
	public void EachDirectionHasItsOwnMark(OffScreenArrowDirection direction, string expected) =>
		Assert.Equal(expected, OffScreenArrowText.Glyph(direction));

	/// <summary>A marker the geometry found on screen has no direction to point in, and a dot is what it
	/// draws as — the case the two removed switches spelled as their default arm.</summary>
	[Fact]
	public void AMarkerWithNoDirectionDrawsADot() =>
		Assert.Equal(OffScreenArrowText.OnScreen, OffScreenArrowText.Glyph(OffScreenArrowDirection.None));

	/// <summary>Four arrows and a dot: a copy-paste that gave two directions the same mark would leave every
	/// other case in this file green.</summary>
	[Fact]
	public void TheFiveMarksAreDistinct()
	{
		var marks = new[]
		{
			OffScreenArrowText.Up,
			OffScreenArrowText.Down,
			OffScreenArrowText.Left,
			OffScreenArrowText.Right,
			OffScreenArrowText.OnScreen,
		};

		for (var left = 0; left < marks.Length; left++)
		{
			for (var right = left + 1; right < marks.Length; right++)
			{
				Assert.True(
					!string.Equals(marks[left], marks[right], StringComparison.Ordinal),
					$"marks {left} and {right} are the same glyph `{marks[left]}`");
			}
		}
	}

	/// <summary>An undefined value is not a direction: it must fall back to the on-screen mark rather than
	/// throw, because a marker is drawn from a struct the geometry owns.</summary>
	[Fact]
	public void AnUndefinedDirectionFallsBackToTheDot() =>
		Assert.Equal(OffScreenArrowText.OnScreen, OffScreenArrowText.Glyph((OffScreenArrowDirection)99));
}
