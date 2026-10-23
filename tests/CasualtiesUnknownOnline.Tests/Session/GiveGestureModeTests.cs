using CasualtiesUnknownOnline.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behaviour family: the give gesture's context (user ruling 2026-10-23). A drag inside the player's OWN UI means
/// the local body and nothing else; a player's detection range is read only in the world, and only a DISPLAYED
/// backpack offers the give (a slot) or the use/wear (its centre). The matrix is pure, so it is pinned here; the
/// live read from the game's own state is pinned by shape in <c>GiveGestureGateTests</c>.
/// </summary>
public class GiveGestureModeTests
{
	[Theory]
	[InlineData(true, false, "OwnUi")] // the player's own ring or panel is open
	[InlineData(true, true, "OwnUi")] // an own panel opening wins over a displayed backpack
	[InlineData(false, true, "DisplayedBackpack")] // the other player's backpack is displayed
	[InlineData(false, false, "World")] // nothing of the player's own is open
	public void TheContext_IsOwnUiThenTheDisplayedBackpackThenTheWorld(bool ownUiOpen, bool remoteViewOpen, string expected) =>
		Assert.Equal(expected, GiveGestureModePolicy.Decide(ownUiOpen, remoteViewOpen).ToString());
}
