using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The pointer census (ticket online-ui-art-and-controls-overhaul, S4 — the retirement pass, closed out by
/// S5): which CUO surface the pointer is over, and which world input that forbids. It is the one rule the
/// world middle-click ping and the world right-click menu both ask — the launcher's rectangle used to be on
/// neither path's list (S2a's recorded limit), which is exactly the drift one shared rule removes.
///
/// <para>
/// The rule is pure, so the cases here are the whole answer: the four surface facts, and the deliberate
/// asymmetry between the two questions — a modal surface owns the SCREEN, so no ping becomes a world ping
/// anywhere, while a right-click outside every CUO surface still targets a player. The geometry the
/// rectangles used to carry is gone: the quick panel and the context menu are controls of CUO's on-canvas
/// surface now, so each surface reports one boolean fact and the rule needs no point at all.
/// </para>
/// </summary>
public sealed class OnlineUiPointerCensusTests
{
	[Fact]
	public void AnIdleScreenBlocksNoWorldInput()
	{
		var census = new OnlineUiPointerCensus();

		Assert.False(census.BlocksWorldPing());
		Assert.False(census.BlocksWorldMenu());
	}

	/// <summary>Every CUO surface the pointer can be over blocks both world inputs while it is the one
	/// under the pointer: the click belongs to the surface.</summary>
	[Fact]
	public void EachSurfaceFactBlocksBothWorldInputs()
	{
		var cases = new (string Fact, Action<OnlineUiPointerCensus> Set)[]
		{
			("the launcher", census => census.OverLauncher = true),
			("the window", census => census.OverWindow = true),
			("the quick panel", census => census.OverQuickPanel = true),
			("the context menu", census => census.OverContextMenu = true),
		};

		foreach (var (fact, set) in cases)
		{
			var census = new OnlineUiPointerCensus();
			set(census);

			Assert.True(census.BlocksWorldPing(), $"{fact} must block a world ping");
			Assert.True(census.BlocksWorldMenu(), $"{fact} must block the world menu");
		}
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

		Assert.True(census.BlocksWorldPing(), "a modal CUO surface owns the whole screen");
		Assert.False(census.BlocksWorldMenu(), "the modal surface owns the screen, not the world menu");
	}

	/// <summary>The facts are pushed in every frame, so leaving a surface must unblock immediately — a
	/// sticky answer would keep the world unclickable after the pointer left.</summary>
	[Fact]
	public void LeavingASurfaceUnblocksTheWorldInputs()
	{
		var census = new OnlineUiPointerCensus { OverLauncher = true };
		Assert.True(census.BlocksWorldMenu());

		census.OverLauncher = false;
		Assert.False(census.BlocksWorldPing());
		Assert.False(census.BlocksWorldMenu());

		census.OverQuickPanel = true;
		Assert.True(census.BlocksWorldMenu());

		census.OverQuickPanel = false;
		census.ModalSurfaceOpen = true;
		Assert.True(census.BlocksWorldPing());
		Assert.False(census.BlocksWorldMenu());

		census.ModalSurfaceOpen = false;
		Assert.False(census.BlocksWorldPing());
		Assert.False(census.BlocksWorldMenu());
	}

	/// <summary>One surface's fact is not another's: the census answers from the facts it was given, never
	/// from a surface the plugin happens to have open.</summary>
	[Fact]
	public void OneSurfaceFactDoesNotStandInForAnother()
	{
		var census = new OnlineUiPointerCensus { OverContextMenu = true };

		Assert.True(census.BlocksWorldMenu());
		census.OverContextMenu = false;
		census.OverWindow = true;
		Assert.True(census.BlocksWorldMenu());
		census.OverWindow = false;
		Assert.False(census.BlocksWorldMenu());
	}
}
