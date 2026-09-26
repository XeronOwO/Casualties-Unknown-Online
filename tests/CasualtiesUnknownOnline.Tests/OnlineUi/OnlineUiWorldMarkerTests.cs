using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The world-space markers the plugin builds for the game's own canvas (ticket
/// online-ui-art-and-controls-overhaul, S6). What a marker carries beyond its world point and its colour is
/// the TEXT of its two forms, and the two factories are where the IMGUI overlay's own composition rules
/// moved to: the name with the distance beside it when a nameplate is pinned to the screen's edge, and the
/// ping's mark beside the pinger's name.
///
/// <para>
/// The spacing is the point of testing it: the label a player reads is composed here now, and a lost
/// separator would be a label that reads as one word.
/// </para>
/// </summary>
public sealed class OnlineUiWorldMarkerTests
{
	private static readonly OnlineUiNativeRgba Color = new(0.2f, 0.4f, 0.6f, 0.8f);

	[Fact]
	public void ANameplateIsTheNameAloneWhileItIsOnScreen()
	{
		var marker = OnlineUiWorldMarker.Nameplate(12f, -3f, "Ana", "42 m", Color);

		Assert.Equal(OnlineUiWorldMarkerKind.Nameplate, marker.Kind);
		Assert.Equal(12f, marker.X);
		Assert.Equal(-3f, marker.Y);
		Assert.Equal("Ana", marker.OnScreenText);
	}

	[Fact]
	public void AnOffScreenNameplateCarriesTheDistanceBesideTheName()
	{
		var marker = OnlineUiWorldMarker.Nameplate(0f, 0f, "Ana", "42 m", Color);

		Assert.Equal("Ana  42 m", marker.OffScreenText);
	}

	/// <summary>A nameplate draws no mark of its own: the empty glyph is what says so, and the surface reads
	/// the KIND rather than guessing from the text.</summary>
	[Fact]
	public void ANameplateDrawsNoMarkAtThePoint()
	{
		var marker = OnlineUiWorldMarker.Nameplate(0f, 0f, "Ana", "42 m", Color);

		Assert.Equal("", marker.Glyph);
	}

	[Fact]
	public void APingDrawsItsOwnMarkAtThePoint()
	{
		var marker = OnlineUiWorldMarker.Ping(5f, 6f, "Bo", "!", Color);

		Assert.Equal(OnlineUiWorldMarkerKind.LocationPing, marker.Kind);
		Assert.Equal(5f, marker.X);
		Assert.Equal(6f, marker.Y);
		Assert.Equal("!", marker.Glyph);
		Assert.Equal("Bo", marker.OnScreenText);
	}

	[Fact]
	public void AnOffScreenPingCarriesTheMarkBesideTheName()
	{
		var marker = OnlineUiWorldMarker.Ping(0f, 0f, "Bo", "\u25CF", Color);

		Assert.Equal("Bo \u25CF", marker.OffScreenText);
	}

	[Fact]
	public void TheColourTravelsWithTheMarker()
	{
		Assert.Equal(Color, OnlineUiWorldMarker.Nameplate(0f, 0f, "Ana", "1 m", Color).Color);
		Assert.Equal(Color, OnlineUiWorldMarker.Ping(0f, 0f, "Bo", "!", Color).Color);
	}

	/// <summary>The state a session with nobody in the world is in, and the one the start gate pushes: a
	/// frame that carries it shows nothing, and its list is never null.</summary>
	[Fact]
	public void AnEmptyOverlayShowsNothing()
	{
		var none = OnlineUiWorldOverlay.None;

		Assert.True(none.Hud is null, "an empty overlay must carry no readout");
		Assert.NotNull(none.Markers);
		Assert.Empty(none.Markers);
	}
}
