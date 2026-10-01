using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The rule that decides how wide one control of a page is, and how much of it is left for the content inside
/// it (ticket online-ui-layout-and-input-detail-pass, reworked by its own acceptance pass).
///
/// <para>
/// It is pure, so the rule the acceptance pass of batch `20261001-k` proved is settleable without a Unity
/// runtime: the width the player sees is a DECLARATION — the model's floor under whatever the control's own
/// caption measures — and never a measurement of content that stretches inside the box. The negative samples
/// below are the two shapes that failed in the live run: a width that ignores the model's floor, and a box
/// narrower than the room its own content keeps.
/// </para>
/// </summary>
public sealed class OnlineUiControlBoxTests
{
	private const float Padding = OnlineUiWindowLayout.ControlInnerPadding;

	[Fact]
	public void TheModelFloorIsNeverUndercut()
	{
		Assert.Equal(150f, OnlineUiControlBox.EffectiveWidth(150f, 0f));
		Assert.Equal(150f, OnlineUiControlBox.EffectiveWidth(150f, 90f));
	}

	[Fact]
	public void ContentWiderThanTheFloorWidensTheControl() =>
		// The user's own finding of 2026-10-01: an English caption must not be cut inside a box sized for the
		// Chinese one, so a caption wider than the hint IS the width.
		Assert.Equal(169.2f, OnlineUiControlBox.EffectiveWidth(112f, 169.2f));

	[Fact]
	public void AWidthThatIsNotThereIsZeroAndNotNegative()
	{
		// A negative width is a rect the player cannot see; the box the row divides must never be one.
		Assert.Equal(0f, OnlineUiControlBox.EffectiveWidth(-40f, 0f));
		Assert.Equal(0f, OnlineUiControlBox.EffectiveWidth(0f, -1f));
	}

	[Fact]
	public void TheInteriorLeavesTheRowsRoomOnBothSides() => Assert.Equal(150f - (2f * Padding), OnlineUiControlBox.InteriorWidth(150f, Padding));

	[Fact]
	public void AnInteriorNarrowerThanItsOwnRoomIsZero()
	{
		// The kind of control the acceptance pass found empty: a box 6 units wide cannot hold content that
		// keeps 10 units of room at each edge, and a stretched child with a negative size is invisible.
		Assert.Equal(0f, OnlineUiControlBox.InteriorWidth(6f, Padding));
		Assert.Equal(0f, OnlineUiControlBox.InteriorWidth(20f, Padding));
	}

	[Fact]
	public void TheRoomIsTheOneTheLayoutDeclares()
	{
		Assert.True(OnlineUiWindowLayout.ControlInnerPadding > 0f, "a control's content keeps room from its own edge");
		Assert.True(
			OnlineUiWindowLayout.MinimumControlWidth > 2f * OnlineUiWindowLayout.ControlInnerPadding,
			"the floor a field falls back to must hold its own content: a minimum width that cannot fit the room the content keeps is a box that renders nothing");
	}
}
