using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The pointer census (ticket online-ui-art-and-controls-overhaul, S4): which CUO surface the pointer is
/// over, and which world input that forbids. It is the one rule the world middle-click ping and the
/// world right-click menu both ask — the launcher's rectangle used to be on neither path's list (S2a's
/// recorded limit), which is exactly the drift one shared rule removes.
///
/// <para>
/// The rule is pure, so the cases here are the whole answer: the facts, the rectangles and their edges,
/// and the deliberate asymmetry between the two questions — a modal surface owns the SCREEN, so no ping
/// becomes a world ping anywhere, while a right-click outside every CUO surface still targets a player.
/// </para>
/// </summary>
public sealed class OnlineUiPointerCensusTests
{
	[Fact]
	public void AnIdleScreenBlocksNoWorldInput()
	{
		var census = new OnlineUiPointerCensus();

		Assert.False(census.BlocksWorldPing(100f, 100f));
		Assert.False(census.BlocksWorldMenu(100f, 100f));
	}

	[Fact]
	public void TheLauncherBlocksBothWorldInputs()
	{
		var census = new OnlineUiPointerCensus { OverLauncher = true };

		Assert.True(census.BlocksWorldPing(100f, 100f));
		Assert.True(census.BlocksWorldMenu(100f, 100f));
	}

	[Fact]
	public void TheWindowBlocksBothWorldInputs()
	{
		var census = new OnlineUiPointerCensus { OverWindow = true };

		Assert.True(census.BlocksWorldPing(100f, 100f));
		Assert.True(census.BlocksWorldMenu(100f, 100f));
	}

	/// <summary>
	/// A modal CUO surface owns the screen: the console overlay or the window means no world ping
	/// anywhere. The world MENU keeps its own rule — the modal surface owns the screen, not the world
	/// menu, so a right-click outside the window's rectangle still targets a player, which is the
	/// behaviour the IMGUI window had.
	/// </summary>
	[Fact]
	public void AModalSurfaceBlocksEveryWorldPingButNotTheWorldMenu()
	{
		var census = new OnlineUiPointerCensus { ModalSurfaceOpen = true };

		Assert.True(census.BlocksWorldPing(1f, 1f));
		Assert.True(census.BlocksWorldPing(1920f, 1080f));
		Assert.False(census.BlocksWorldMenu(100f, 100f));
		Assert.False(census.BlocksWorldMenu(1920f, 1080f));
	}

	/// <summary>The two IMGUI panels are their own rectangles: inside them both world inputs are the
	/// panel's, outside them the world's (nothing else is set).</summary>
	[Fact]
	public void TheOverlayPanelsBlockOnlyTheirOwnRectangles()
	{
		var census = new OnlineUiPointerCensus
		{
			OverlayRects = [new OnlineUiBlockRect(1564f, 644f, 340f, 420f)],
		};

		Assert.True(census.BlocksWorldPing(1600f, 700f));
		Assert.True(census.BlocksWorldMenu(1600f, 700f));
		Assert.False(census.BlocksWorldPing(1200f, 700f));
		Assert.False(census.BlocksWorldMenu(1200f, 700f));
	}

	/// <summary>Every rectangle counts, not just the first: the quick panel and an open context menu are
	/// both in the list while both are up.</summary>
	[Fact]
	public void EveryOverlayRectangleIsConsidered()
	{
		var census = new OnlineUiPointerCensus
		{
			OverlayRects =
			[
				new OnlineUiBlockRect(10f, 10f, 100f, 100f),
				new OnlineUiBlockRect(500f, 300f, 120f, 80f),
			],
		};

		Assert.True(census.BlocksWorldMenu(50f, 50f));
		Assert.True(census.BlocksWorldMenu(560f, 340f));
		Assert.False(census.BlocksWorldMenu(400f, 340f));
	}

	/// <summary>The rectangles carry the same edge semantics as <see cref="OnlineUiBlockRect"/>: the
	/// boundary belongs to the surface, so a click on the panel's own edge is the panel's.</summary>
	[Fact]
	public void TheRectangleEdgesBelongToTheSurface()
	{
		var census = new OnlineUiPointerCensus
		{
			OverlayRects = [new OnlineUiBlockRect(100f, 200f, 50f, 40f)],
		};

		Assert.True(census.BlocksWorldMenu(100f, 200f));
		Assert.True(census.BlocksWorldMenu(150f, 240f));
		Assert.False(census.BlocksWorldMenu(99f, 200f));
		Assert.False(census.BlocksWorldMenu(150f, 240.5f));
	}

	/// <summary>The facts are pushed in every frame, so leaving a surface must unblock immediately — a
	/// sticky answer would keep the world unclickable after the pointer left.</summary>
	[Fact]
	public void LeavingASurfaceUnblocksTheWorldInputs()
	{
		var census = new OnlineUiPointerCensus { OverLauncher = true };
		Assert.True(census.BlocksWorldMenu(100f, 100f));

		census.OverLauncher = false;
		Assert.False(census.BlocksWorldPing(100f, 100f));
		Assert.False(census.BlocksWorldMenu(100f, 100f));

		census.ModalSurfaceOpen = true;
		Assert.True(census.BlocksWorldPing(100f, 100f));

		census.ModalSurfaceOpen = false;
		Assert.False(census.BlocksWorldPing(100f, 100f));
	}

	/// <summary>An empty rectangle list is the normal state (both panels closed) and must not read as
	/// "everything is covered" — the list is replaced, never appended to.</summary>
	[Fact]
	public void AnEmptyRectangleListBlocksNothing()
	{
		var census = new OnlineUiPointerCensus { OverlayRects = [] };

		Assert.False(census.BlocksWorldPing(0f, 0f));
		Assert.False(census.BlocksWorldMenu(0f, 0f));
	}

	/// <summary>The point is GUI space (origin top-left, Y down), the space the IMGUI panels and the
	/// plugin's conversion both use: a negative Y is above the screen and can only be the world's.</summary>
	[Fact]
	public void ThePointIsGuiSpaceWithTheOriginAtTheTopLeft()
	{
		var census = new OnlineUiPointerCensus
		{
			OverlayRects = [new OnlineUiBlockRect(0f, 0f, 200f, 100f)],
		};

		Assert.True(census.BlocksWorldPing(10f, 10f));
		Assert.False(census.BlocksWorldPing(10f, -10f));
	}
}
