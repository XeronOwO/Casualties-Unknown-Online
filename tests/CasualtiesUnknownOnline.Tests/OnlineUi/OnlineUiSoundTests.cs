using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.OnlineUi;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The overlay's sound mapping: a button press is the game's own "click", a toggle or dropdown pick is its
/// small click (`SettingsMenu`'s pair), and nothing else makes a sound. The live call is
/// `PlayerCamera.PlayUISound` — the entry the game's own buttons use — so what is read here is the decision,
/// not the audio device.
/// </summary>
public sealed class OnlineUiSoundTests
{
	private static readonly OnlineUiIntentKind[] NotClicks =
	[
		OnlineUiIntentKind.ControlChanged,
		OnlineUiIntentKind.ControlEdited,
		OnlineUiIntentKind.ControlCommitted,
		OnlineUiIntentKind.WindowHoverEntered,
		OnlineUiIntentKind.WindowHoverLeft,
		OnlineUiIntentKind.LauncherToggled,
	];

	[Fact]
	public void AButtonPressPlaysTheGamesClick() =>
		Assert.Equal("click", OnlineUiSound.For(OnlineUiIntentKind.ControlInvoked));

	[Theory]
	[InlineData(OnlineUiIntentKind.ControlToggled)]
	[InlineData(OnlineUiIntentKind.ControlSelected)]
	public void AToggleOrDropdownPickPlaysTheSmallClick(OnlineUiIntentKind kind) =>
		Assert.Equal("miniClick", OnlineUiSound.For(kind));

	[Fact]
	public void TypingDraggingAndHoveringStaySilent()
	{
		var sounding = new List<OnlineUiIntentKind>();
		foreach (var kind in NotClicks)
		{
			if (OnlineUiSound.For(kind) != null)
			{
				sounding.Add(kind);
			}
		}

		Assert.Empty(sounding);
	}
}
