using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The end-of-layer panel's ownership (ticket `layer-end-save-and-exit-native-write.md`, the user's 2026-10-24
/// ruling): only the HOST presses "Continue" or "Save and exit"; on a member both buttons are hidden. The rule is
/// pure so the three standings are pinned without a scene.
/// </summary>
[Trait("Category", "Unit")]
public class LayerPanelChoicePolicyTests
{
	[Fact]
	public void AMemberInAnActiveSession_DoesNotOwnTheChoices() =>
		Assert.True(LayerPanelChoicePolicy.HidesMemberChoices(SessionRole.Guest, sessionActive: true));

	[Fact]
	public void TheHost_KeepsBothChoices() =>
		Assert.False(LayerPanelChoicePolicy.HidesMemberChoices(SessionRole.Host, sessionActive: true));

	[Fact]
	public void SoloPlay_KeepsBothChoices()
	{
		// No session at all (the offline run): the player owns the panel the way the game intends.
		Assert.False(LayerPanelChoicePolicy.HidesMemberChoices(SessionRole.Host, sessionActive: false));
		Assert.False(LayerPanelChoicePolicy.HidesMemberChoices(SessionRole.Guest, sessionActive: false));
	}
}
