using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The launcher's caption. The open/closed marker is what the launcher has always used to say whether the
/// Online UI window is open, and since S2a this string is the only thing that travels from the Runtime to
/// the game's own button — so it is pinned here instead of being left to a rewrite of the launcher.
/// </summary>
public sealed class OnlineUiLauncherTextTests
{
	[Fact]
	public void AClosedWindowCarriesTheClosedMarker() =>
		Assert.Equal("CUO ONLINE ▼", OnlineUiLauncherText.Label("CUO ONLINE", open: false));

	[Fact]
	public void AnOpenWindowCarriesTheOpenMarker() =>
		Assert.Equal("CUO ONLINE ▲", OnlineUiLauncherText.Label("CUO ONLINE", open: true));

	[Fact]
	public void TheMarkersAreTheLaunchersOwn() =>
		Assert.NotEqual(OnlineUiLauncherText.OpenMarker, OnlineUiLauncherText.ClosedMarker);

	[Fact]
	public void AnEmptyCaptionStillCarriesTheMarker() =>
		Assert.Equal(OnlineUiLauncherText.ClosedMarker, OnlineUiLauncherText.Label(string.Empty, open: false));
}
