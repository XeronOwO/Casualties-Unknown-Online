using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI launcher's idle fade. The reported case: the top-right CUO launcher drew an opaque
/// panel over the play area on every frame, with no idle state and no way to see through it
/// (<c>docs/backlog/review/cuo-launcher-button-obscures-the-view.md</c>).
///
/// The rule is pure (Runtime, no Unity dependency) and its matrix is tested here. How the rule reaches
/// the pixels is pinned in <c>OnlineUiSurfacePinTests</c> since S2a, when the launcher moved onto the
/// game's own control: the Runtime half is the plugin's frame push (the rule asked with the runtime
/// clock, its answer handed to the surface) and the Unity half is the surface itself (the alpha landing
/// on the launcher's CanvasGroup) — neither is visible from this file any more.
///
/// What stays here besides the matrix is the IMGUI theme's own contract, which neither S2a nor S2b ends:
/// the surfaces the theme still draws must keep drawing blended frames, and its draw census is a CEILING,
/// so a new unblended draw path — or a launcher quietly regrown in IMGUI — cannot appear unnoticed.
/// </summary>
public sealed class OnlineUiLauncherFadeTests
{
	/// <summary>
	/// Every surface that still draws through the IMGUI theme, and the draw it must make. Three of the
	/// five rows the panel-blending ticket enumerated are left: S2a moved the launcher onto the game's own
	/// control, and S2b moved the modal window family with it, so the quick panel, the context menu and
	/// the console overlay are the remaining ones. A surface that hand-rolls an unblended rectangle of its
	/// own, or a new surface that appears without joining this census, is the same defect one level up.
	/// </summary>
	[Fact]
	public void EveryRemainingThemedSurface_DrawsThroughTheBlendedFrame()
	{
		var surfaces = new (string File, string Call)[]
		{
			("OnlineUiQuickPanel.cs", "OnlineUiTheme.DrawBackground(rect);"),
			("OnlineUiPlayerContextMenu.cs", "OnlineUiTheme.DrawBackground(rect);"),
			("CommandConsoleOverlay.cs", "OnlineUiTheme.DrawOverlayBackground(rect);"),
		};

		foreach (var (file, call) in surfaces)
		{
			Assert.True(
				ReadSource(file).Contains(call, StringComparison.Ordinal),
				$"{file} no longer draws its frame through `{call}` — a themed surface that stops using the blended frame is the defect this census exists for");
		}

		// The overlay is drawn from FOUR methods (the history panel, the closed-console notifications,
		// the suggestion list and the tooltip), so the census counts them: losing one of the four would
		// leave the representative row above intact.
		Assert.Equal(4, CountOf(ReadSource("CommandConsoleOverlay.cs"), "OnlineUiTheme.DrawOverlayBackground(rect);"));

		// The flag's spelling, not one spelling of it: `false` with any spacing is the defect.
		Assert.DoesNotMatch(@"StretchToFill,\s*false", ReadThemeSource());
	}

	/// <summary>
	/// The theme's own half: every frame it still draws is blended, and its draw census is a ceiling. The
	/// three mutation cases below all assert the same helper is FALSE on a broken theme, so this positive
	/// case is what keeps them from passing on a theme that lost the frames altogether.
	/// </summary>
	[Fact]
	public void TheThemeKeepsEveryRemainingFrameBlended()
	{
		Assert.True(
			PinsTheBlendedFrame(ReadThemeSource()),
			"the IMGUI surfaces that are still themed must keep drawing blended frames — the palette's alphas are dead on a draw that does not ask for blending");
	}

	/// <summary>The overlay half of the same fact: the console's background is drawn by its own member,
	/// so a blend flag flipped there alone would leave every other pin green.</summary>
	[Fact]
	public void TheThemePinRejectsAnUnblendedOverlay()
	{
		var theme = ReadThemeSource();
		var broken = theme.Replace(
			"ScaleMode.StretchToFill, true, 0f, OverlayPanel",
			"ScaleMode.StretchToFill, false, 0f, OverlayPanel");

		Assert.True(
			theme != broken,
			"the mutation's anchor text is no longer in the theme — re-anchor this mutation before trusting it");
		Assert.False(PinsTheBlendedFrame(broken));
	}

	/// <summary>A frame draw that stops asking for blending leaves the palette's alphas with nothing to
	/// apply them, while every call site above still reads correctly.</summary>
	[Fact]
	public void TheThemePinRejectsAHardCodedBlendFlag()
	{
		var theme = ReadThemeSource();
		var broken = theme.Replace(
			"ScaleMode.StretchToFill, true, 0f, panel",
			"ScaleMode.StretchToFill, false, 0f, panel");

		Assert.True(
			theme != broken,
			"the mutation's anchor text is no longer in the theme — re-anchor this mutation before trusting it");
		Assert.False(PinsTheBlendedFrame(broken));
	}

	/// <summary>The shared overload must keep handing BOTH palette colours to the frame draw: a frame
	/// drawn with the panel colour twice loses the border and still contains the call.</summary>
	[Fact]
	public void TheThemePinRejectsAFrameThatLosesItsBorder()
	{
		var theme = ReadThemeSource();
		var broken = theme.Replace(
			"DrawFrame(rect, Panel, Border)",
			"DrawFrame(rect, Panel, Panel)");

		Assert.True(
			theme != broken,
			"the mutation's anchor text is no longer in the theme — re-anchor this mutation before trusting it");
		Assert.False(PinsTheBlendedFrame(broken));
	}

	[Fact]
	public void AnUntouchedLauncherIsOpaqueThroughTheIdleWindowAndSettlesAtTheFloor()
	{
		var fade = new OnlineUiLauncherFade();
		const long start = 1_000_000;

		AssertAlpha(1f, fade.Evaluate(start, hovered: false));
		AssertAlpha(1f, fade.Evaluate(start + OnlineUiLauncherFade.IdleDelayMs, hovered: false));
		AssertAlpha(
			OnlineUiLauncherFade.IdleAlpha,
			fade.Evaluate(start + OnlineUiLauncherFade.IdleDelayMs + OnlineUiLauncherFade.FadeMs, hovered: false));
		AssertAlpha(
			OnlineUiLauncherFade.IdleAlpha,
			fade.Evaluate(start + (OnlineUiLauncherFade.IdleDelayMs * 10), hovered: false));
	}

	[Fact]
	public void TheRampIsMonotoneBetweenTheFloorAndFullOpacity()
	{
		var fade = new OnlineUiLauncherFade();
		const long start = 1_000_000;
		var rampStart = start + OnlineUiLauncherFade.IdleDelayMs;

		// the idle window starts at the first evaluation, so every case below has to
		// establish that baseline before it advances the clock
		AssertAlpha(1f, fade.Evaluate(start, hovered: false));

		var quarter = fade.Evaluate(rampStart + (OnlineUiLauncherFade.FadeMs / 4), hovered: false);
		var half = fade.Evaluate(rampStart + (OnlineUiLauncherFade.FadeMs / 2), hovered: false);
		var threeQuarters = fade.Evaluate(rampStart + ((OnlineUiLauncherFade.FadeMs * 3) / 4), hovered: false);

		Assert.True(quarter < 1f && quarter > half, $"the ramp must start below full opacity and fall: {quarter} then {half}");
		Assert.True(
			half > threeQuarters && threeQuarters > OnlineUiLauncherFade.IdleAlpha,
			$"the ramp must fall toward the floor without passing it: {half} then {threeQuarters}");
	}

	[Fact]
	public void AHoverRestoresFullOpacityImmediately()
	{
		var fade = new OnlineUiLauncherFade();
		const long start = 1_000_000;
		var rampEnd = start + OnlineUiLauncherFade.IdleDelayMs + OnlineUiLauncherFade.FadeMs;

		AssertAlpha(1f, fade.Evaluate(start, hovered: false));
		AssertAlpha(OnlineUiLauncherFade.IdleAlpha, fade.Evaluate(rampEnd, hovered: false));
		AssertAlpha(1f, fade.Evaluate(rampEnd + 1, hovered: true));
	}

	[Fact]
	public void AHoverRestartsTheIdleWindow()
	{
		var fade = new OnlineUiLauncherFade();
		const long start = 1_000_000;
		var hoveredAt = start + (OnlineUiLauncherFade.IdleDelayMs * 3);

		AssertAlpha(1f, fade.Evaluate(start, hovered: false));
		AssertAlpha(OnlineUiLauncherFade.IdleAlpha, fade.Evaluate(hoveredAt, hovered: false));
		AssertAlpha(1f, fade.Evaluate(hoveredAt, hovered: true));
		// the pointer left: a whole idle window later the launcher is still opaque,
		// and only then does the ramp start
		AssertAlpha(1f, fade.Evaluate(hoveredAt + OnlineUiLauncherFade.IdleDelayMs, hovered: false));
		AssertAlpha(
			OnlineUiLauncherFade.IdleAlpha,
			fade.Evaluate(hoveredAt + OnlineUiLauncherFade.IdleDelayMs + OnlineUiLauncherFade.FadeMs, hovered: false));
	}

	[Fact]
	public void RepeatedEvaluationsOnTheSameUpdateDoNotAdvanceTheIdleTime()
	{
		var fade = new OnlineUiLauncherFade();
		const long start = 1_000_000;
		var midRamp = start + OnlineUiLauncherFade.IdleDelayMs + (OnlineUiLauncherFade.FadeMs / 2);

		AssertAlpha(1f, fade.Evaluate(start, hovered: false));
		var first = fade.Evaluate(midRamp, hovered: false);
		for (var pass = 0; pass < 10; pass++)
		{
			AssertAlpha(first, fade.Evaluate(midRamp, hovered: false));
		}

		// the next real frame continues the same ramp rather than restarting it
		AssertAlpha(
			OnlineUiLauncherFade.IdleAlpha,
			fade.Evaluate(start + OnlineUiLauncherFade.IdleDelayMs + OnlineUiLauncherFade.FadeMs, hovered: false));
	}

	[Fact]
	public void AClockThatWentBackwardsRestartsTheIdleWindowInsteadOfFading()
	{
		var fade = new OnlineUiLauncherFade();
		const long start = 1_000_000;

		AssertAlpha(1f, fade.Evaluate(start, hovered: false));
		AssertAlpha(
			OnlineUiLauncherFade.IdleAlpha,
			fade.Evaluate(start + (OnlineUiLauncherFade.IdleDelayMs * 4), hovered: false));
		// the runtime clock wraps (Environment.TickCount): an earlier stamp must not
		// read as an enormous idle age
		AssertAlpha(1f, fade.Evaluate(start, hovered: false));
		AssertAlpha(1f, fade.Evaluate(start + OnlineUiLauncherFade.IdleDelayMs, hovered: false));
		AssertAlpha(
			OnlineUiLauncherFade.IdleAlpha,
			fade.Evaluate(start + OnlineUiLauncherFade.IdleDelayMs + OnlineUiLauncherFade.FadeMs, hovered: false));
	}

	[Fact]
	public void TheAlphaNeverLeavesTheFloorToFullOpacityRange()
	{
		var fade = new OnlineUiLauncherFade();
		const long start = 1_000_000;

		for (var step = 0; step <= 40; step++)
		{
			var alpha = fade.Evaluate(start + (step * 200), hovered: false);
			Assert.True(
				alpha >= OnlineUiLauncherFade.IdleAlpha && alpha <= 1f,
				$"alpha {alpha} left [{OnlineUiLauncherFade.IdleAlpha}, 1] at step {step}");
		}
	}

	/// <summary>
	/// The ticket delegates the exact alpha, delay and easing to this cycle, so the values themselves are
	/// free — but not their intent: the floor has to be clearly translucent and the idle window on the
	/// order of a few seconds. Without this fact a floor of 0.99 would keep every other case green while
	/// the launcher stayed effectively opaque.
	/// </summary>
	[Fact]
	public void TheConstantsMatchTheTicketsIntent()
	{
		Assert.True(
			OnlineUiLauncherFade.IdleAlpha <= 0.6f,
			$"the idle floor must be clearly translucent, not {OnlineUiLauncherFade.IdleAlpha}");
		Assert.True(
			OnlineUiLauncherFade.IdleDelayMs >= 1_000 && OnlineUiLauncherFade.IdleDelayMs <= 10_000,
			$"the idle window must be on the order of a few seconds, not {OnlineUiLauncherFade.IdleDelayMs} ms");
		Assert.True(
			OnlineUiLauncherFade.FadeMs > 0 && OnlineUiLauncherFade.FadeMs <= 2_000,
			$"the ramp must be short enough to read as a fade, not {OnlineUiLauncherFade.FadeMs} ms");
	}

	/// <summary>
	/// The theme half: the shared panel overload must hand the theme's own colours to the shared frame
	/// draw, and that draw must ASK FOR BLENDING — the palette's alphas (Panel 0.96, Border 0.9,
	/// OverlayPanel 0.58) are dead on a draw that does not, because the explicit-colour
	/// <c>DrawTexture</c> overload hands the flag to the native draw verbatim. The console overlay is the
	/// same fact one member over. The draw counts are a CEILING as well as a census: the five frame draws
	/// plus the overlay's own are every rectangle this theme paints, so a sixth draw (the launcher
	/// regrown in IMGUI, under any name) cannot appear unnoticed.
	/// </summary>
	private static bool PinsTheBlendedFrame(string themeSource)
	{
		var sharedOverload = Flatten(ExtractMember(themeSource, "internal static void DrawBackground(Rect rect)"));
		var frame = Flatten(ExtractMember(themeSource, "private static void DrawFrame("));
		var overlay = Flatten(ExtractMember(themeSource, "internal static void DrawOverlayBackground("));

		return sharedOverload.Contains("DrawFrame(rect, Panel, Border)", StringComparison.Ordinal)
			&& CountOf(frame, "ScaleMode.StretchToFill, true, 0f, panel") == 1
			&& CountOf(frame, "ScaleMode.StretchToFill, true, 0f, border") == 4
			&& !frame.Contains("StretchToFill, false,", StringComparison.Ordinal)
			&& overlay.Contains("ScaleMode.StretchToFill, true, 0f, OverlayPanel", StringComparison.Ordinal)
			&& !overlay.Contains("StretchToFill, false,", StringComparison.Ordinal)
			&& CountOf(frame, "GUI.DrawTexture(") == 5
			&& CountOf(Flatten(themeSource), "GUI.DrawTexture(") == 6;
	}

	private static void AssertAlpha(float expected, float actual) =>
		Assert.True(Math.Abs(expected - actual) < 1e-4f, $"expected alpha {expected}, got {actual}");

	private static int CountOf(string text, string needle)
	{
		var count = 0;
		for (var index = text.IndexOf(needle, StringComparison.Ordinal);
			index >= 0;
			index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	private static string ReadThemeSource() => ReadSource("OnlineUiTheme.cs");

	private static string ReadSource(string fileName) =>
		File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.Plugin", fileName));

	/// <summary>The member's declaration line and its body, up to the next member (a line that starts at
	/// one tab with a declaration or with its doc comment).</summary>
	private static string ExtractMember(string source, string marker)
	{
		var start = source.IndexOf(marker, StringComparison.Ordinal);
		Assert.True(start >= 0, $"{marker} not found");
		var lines = source.Substring(start).Split('\n');
		var kept = new List<string> { lines[0] };
		for (var i = 1; i < lines.Length; i++)
		{
			var line = lines[i];
			if (line.StartsWith("\tprivate ", StringComparison.Ordinal)
				|| line.StartsWith("\tinternal ", StringComparison.Ordinal)
				|| line.StartsWith("\t/// ", StringComparison.Ordinal))
			{
				break;
			}

			kept.Add(line);
		}

		return string.Join("\n", kept);
	}

	/// <summary>
	/// Flattens a body to one whitespace-normalised line with its comments removed, so a comment that
	/// merely names an API can neither satisfy a pin nor break one. A trailing comment is cut only
	/// outside a string literal, so a path or a URL inside one survives.
	/// </summary>
	private static string Flatten(string text)
	{
		var kept = new List<string>();
		foreach (var line in text.Split('\n'))
		{
			kept.Add(StripComment(line));
		}

		return string.Join(" ", string.Join(" ", kept).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}

	private static string StripComment(string line)
	{
		var inString = false;
		for (var i = 0; i < line.Length - 1; i++)
		{
			if (inString && line[i] == '\\')
			{
				i++;
				continue;
			}

			if (line[i] == '"')
			{
				inString = !inString;
			}
			else if (!inString && line[i] == '/' && line[i + 1] == '/')
			{
				return line.Substring(0, i);
			}
		}

		return line;
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
