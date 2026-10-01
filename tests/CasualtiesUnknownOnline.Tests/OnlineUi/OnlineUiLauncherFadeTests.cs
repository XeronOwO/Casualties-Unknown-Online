using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI launcher's idle fade. The reported case: the top-right CUO launcher drew an opaque
/// panel over the play area on every frame, with no idle state and no way to see through it
/// (<c>docs/backlog/done/cuo-launcher-button-obscures-the-view.md</c>).
///
/// The rule is pure (Runtime, no Unity dependency) and its matrix is tested here. How the rule reaches
/// the pixels is pinned in <c>OnlineUiSurfacePinTests</c> since S2a, when the launcher moved onto the
/// game's own control: the Runtime half is the plugin's frame push (the rule asked with the runtime
/// clock, its answer handed to the surface) and the Unity half is the surface itself (the alpha landing
/// on the launcher's CanvasGroup) — neither is visible from this file any more.
///
/// What stays here besides the matrix is the IMGUI theme's own contract, which S5 reduced to its last
/// surface: every themed surface still drawn in IMGUI must draw through the blended frame — the command
/// console overlay is the only one left, because the quick panel and the player context menu are controls
/// of the game's own canvas now — and the theme's draw census is a CEILING of exactly one rectangle, so a
/// second IMGUI panel (or a launcher quietly regrown in IMGUI) cannot appear unnoticed.
/// </summary>
public sealed class OnlineUiLauncherFadeTests
{
	/// <summary>
	/// Every surface that still draws through the IMGUI theme, and the draw it must make. One row is left:
	/// S2a moved the launcher onto the game's own control, S2b the modal window family, and S5 the quick
	/// panel and the player context menu — so the command console overlay, a developer surface with a text
	/// input, is the only themed IMGUI draw there is. A surface that hand-rolls an unblended rectangle of
	/// its own, or a new surface that appears without joining this census, is the same defect one level up.
	/// </summary>
	[Fact]
	public void EveryRemainingThemedSurface_DrawsThroughTheBlendedFrame()
	{
		const string overlay = "CommandConsoleOverlay.cs";
		const string call = "OnlineUiTheme.DrawOverlayBackground(rect);";

		Assert.True(
			ReadSource(overlay).Contains(call, StringComparison.Ordinal),
			$"{overlay} no longer draws its frame through `{call}` — a themed surface that stops using the blended frame is the defect this census exists for");

		// The overlay is drawn from FOUR methods (the history panel, the closed-console notifications,
		// the suggestion list and the tooltip), so the census counts them: losing one of the four would
		// leave the representative row above intact.
		Assert.Equal(4, CountOf(ReadSource(overlay), call));

		// The flag's spelling, not one spelling of it: `false` with any spacing is the defect.
		Assert.DoesNotMatch(@"StretchToFill,\s*false", ReadThemeSource());
	}

	/// <summary>
	/// The theme's own half: the ONE rectangle it still paints is the console overlay's, and it is blended.
	/// The two mutation cases below both assert the same helper is FALSE on a broken theme, so this positive
	/// case is what keeps them from passing on a theme that lost the draw altogether.
	/// </summary>
	[Fact]
	public void TheThemeKeepsItsLastFrameBlended()
	{
		Assert.True(
			PinsTheBlendedOverlay(ReadThemeSource()),
			"the one surface still drawn in IMGUI must keep drawing a blended frame — the palette's alpha is dead on a draw that does not ask for blending");
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
		Assert.False(PinsTheBlendedOverlay(broken));
	}

	/// <summary>The census is a ceiling as well as a count: a second rectangle painted by this theme means a
	/// surface that should have moved onto the game's own controls did not (the panels did, in S5).</summary>
	[Fact]
	public void TheThemePinRejectsASecondRectangle()
	{
		var theme = ReadThemeSource();
		const string call = "GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, OverlayPanel, 0f, 0f);";
		var broken = theme.Replace(call, call + "\n\t\t" + call);

		Assert.True(
			theme != broken,
			"the mutation's anchor text is no longer in the theme — re-anchor this mutation before trusting it");
		Assert.False(PinsTheBlendedOverlay(broken));
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
	/// The theme half: the ONE rectangle this theme still paints — the console overlay's background — must
	/// hand its own palette colour to the draw and ASK FOR BLENDING, because the palette's alpha
	/// (OverlayPanel 0.58) is dead on a draw that does not: the explicit-colour <c>DrawTexture</c> overload
	/// hands the flag to the native draw verbatim. The draw count is a CEILING as well as a census: one is
	/// every rectangle this theme paints, so a second draw (a panel or a launcher regrown in IMGUI, under
	/// any name) cannot appear unnoticed.
	/// </summary>
	private static bool PinsTheBlendedOverlay(string themeSource)
	{
		var overlay = Flatten(ExtractMember(themeSource, "internal static void DrawOverlayBackground("));
		var theme = Flatten(themeSource);

		return overlay.Contains("ScaleMode.StretchToFill, true, 0f, OverlayPanel", StringComparison.Ordinal)
			&& !overlay.Contains("StretchToFill, false,", StringComparison.Ordinal)
			&& CountOf(theme, "GUI.DrawTexture(") == 1
			&& !theme.Contains("DrawFrame(", StringComparison.Ordinal);
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
