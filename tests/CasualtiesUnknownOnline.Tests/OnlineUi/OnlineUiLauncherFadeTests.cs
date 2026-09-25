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
/// The rule is pure (Runtime, no Unity dependency) and its matrix is tested here. The IMGUI draw has
/// no runtime probe in this tree, so the path from the rule to the pixels is pinned against the
/// source — as a MECHANISM, and in BOTH halves, because the first cut of this cycle showed how easily
/// the two drift apart: it delivered the alpha through the ambient <c>GUI.color</c> tint, which the
/// explicit-colour <c>GUI.DrawTexture</c> overload never consumes, so the panel (the launcher's whole
/// visible surface) stayed opaque while the suite was green. The window pin therefore requires the
/// alpha at the frame draw and at the label's style, and the theme pin requires that frame draw to
/// fold the alpha AND blend it. Every pin carries negative samples — the pre-change bodies among them
/// — so neither can pass vacuously.
/// </summary>
public sealed class OnlineUiLauncherFadeTests
{
	[Fact]
	public void TheLauncherDrawFoldsTheIdleAlphaIntoTheFrameAndTheLabel()
	{
		var body = ReadFlattenedLauncherDrawBody();

		Assert.True(PinsTheIdleFade(body), $"the launcher draw does not deliver the idle-fade alpha: {body}");
	}

	[Fact]
	public void TheThemeFoldsTheAlphaIntoABlendedFrame()
	{
		var theme = ReadThemeSource();

		Assert.True(PinsTheBlendedFrame(theme), "the theme does not fold the launcher's alpha into an alpha-blended frame");
	}

	/// <summary>The body exactly as it stood at HEAD (671e63c1): an opaque frame drawn every pass, no
	/// pointer fact and no rule. This is the cycle's red, made reproducible.</summary>
	[Fact]
	public void ThePinRejectsThePreChangeDraw() => Assert.False(PinsTheIdleFade(Flatten(PreChangeBody)));

	/// <summary>A draw that asks the rule and then discards the answer satisfies a substring-only pin,
	/// so the pin requires the value at the call sites.</summary>
	[Fact]
	public void ThePinRejectsAFadeThatNeverReachesTheControl() => Assert.False(PinsTheIdleFade(Flatten(DiscardedFadeBody)));

	/// <summary>A draw that asks the rule and then overwrites the answer keeps every call site intact,
	/// so the pin also requires the alpha to be assigned exactly once — from the rule.</summary>
	[Fact]
	public void ThePinRejectsAnAlphaThatIsOverwritten() => Assert.False(PinsTheIdleFade(Flatten(OverwrittenAlphaBody)));

	/// <summary>
	/// Four mutations of the REAL theme source, each a defect no call-site pin can see because the call
	/// sites stay byte-identical: an unfolded call site, an unfolded helper, a label that keeps the
	/// palette's opacity, and a frame draw whose blending flag is hard-coded. The NotEqual guards keep a
	/// mutation from silently becoming a no-op when the theme's text drifts.
	/// </summary>
	[Fact]
	public void TheThemePinRejectsAnUnfoldedCallSite()
	{
		var theme = ReadThemeSource();
		var broken = theme.Replace(
			"DrawFrame(rect, WithAlpha(Panel, alpha), WithAlpha(Border, alpha), alphaBlend: true)",
			"DrawFrame(rect, Panel, Border, alphaBlend: true)");

		Assert.NotEqual(theme, broken);
		Assert.False(PinsTheBlendedFrame(broken));
	}

	[Fact]
	public void TheThemePinRejectsAnUnfoldedWithAlphaHelper()
	{
		var theme = ReadThemeSource();
		var broken = theme.Replace(
			"new(color.r, color.g, color.b, color.a * alpha)",
			"color");

		Assert.NotEqual(theme, broken);
		Assert.False(PinsTheBlendedFrame(broken));
	}

	[Fact]
	public void TheThemePinRejectsALabelThatKeepsFullOpacity()
	{
		var theme = ReadThemeSource();
		var broken = theme.Replace(
			"style.normal.textColor = WithAlpha(Accent, alpha);",
			"style.normal.textColor = Accent;");

		Assert.NotEqual(theme, broken);
		Assert.False(PinsTheBlendedFrame(broken));
	}

	[Fact]
	public void TheThemePinRejectsAHardCodedBlendFlag()
	{
		var theme = ReadThemeSource();
		var broken = theme.Replace(
			"ScaleMode.StretchToFill, alphaBlend, 0f, panel",
			"ScaleMode.StretchToFill, false, 0f, panel");

		Assert.NotEqual(theme, broken);
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

		// the idle window starts at the first draw pass, so every case below has to
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
	public void RepeatedEvaluationsOnTheSamePassDoNotAdvanceTheIdleTime()
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

		// the next real pass continues the same ramp rather than restarting it
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
	/// The launcher's draw mechanism: the pointer fact comes from the current IMGUI event, in the same
	/// GUI space as the button rect; the rule is asked once per pass with the runtime clock
	/// (<c>Environment.TickCount</c>, which <c>Time.timeScale</c> does not move) and its answer is
	/// assigned exactly once; the alpha reaches BOTH visible halves; it does not travel through the
	/// ambient GUI tint, which the explicit-colour <c>DrawTexture</c> overload ignores; the pass builds
	/// no label string; and the rect and the click keep their meaning.
	/// </summary>
	private static bool PinsTheIdleFade(string body) =>
		body.Contains("rect.Contains(Event.current.mousePosition)", StringComparison.Ordinal)
		&& body.Contains("var alpha = _state.LauncherFade.Evaluate(ctx.Time.NowMs, hovered);", StringComparison.Ordinal)
		&& CountOf(body, "alpha") == 3
		&& body.Contains("OnlineUiTheme.DrawBackground(rect, alpha);", StringComparison.Ordinal)
		&& body.Contains("OnlineUiTheme.Launcher(alpha)", StringComparison.Ordinal)
		&& !body.Contains("GUI.color", StringComparison.Ordinal)
		&& !body.Contains("ctx.T(\"launcher\") + ", StringComparison.Ordinal)
		&& body.Contains("new Rect(Screen.width - 170f, 12f, 158f, 34f);", StringComparison.Ordinal)
		&& body.Contains("_state.Visible = !_state.Visible;", StringComparison.Ordinal);

	/// <summary>
	/// The theme half: the alpha-taking overload must hand the theme's own colours, scaled, to the
	/// shared frame draw AND ask for blending; the parameterless overload must keep the modal window's
	/// current behaviour; and the shared frame draw must pass its blending argument into all five draws
	/// rather than hard-coding one.
	/// </summary>
	private static bool PinsTheBlendedFrame(string themeSource)
	{
		var alphaOverload = Flatten(ExtractMember(themeSource, "internal static void DrawBackground(Rect rect, float alpha)"));
		var sharedOverload = Flatten(ExtractMember(themeSource, "internal static void DrawBackground(Rect rect)"));
		var frame = Flatten(ExtractMember(themeSource, "private static void DrawFrame("));
		var withAlpha = Flatten(ExtractMember(themeSource, "private static Color WithAlpha("));
		var launcher = Flatten(ExtractMember(themeSource, "internal static GUIStyle Launcher("));

		return alphaOverload.Contains("DrawFrame(rect, WithAlpha(Panel, alpha), WithAlpha(Border, alpha), alphaBlend: true)", StringComparison.Ordinal)
			&& sharedOverload.Contains("DrawFrame(rect, Panel, Border, alphaBlend: false)", StringComparison.Ordinal)
			&& CountOf(frame, "ScaleMode.StretchToFill, alphaBlend, 0f, panel") == 1
			&& CountOf(frame, "ScaleMode.StretchToFill, alphaBlend, 0f, border") == 4
			&& !frame.Contains("StretchToFill, false,", StringComparison.Ordinal)
			// …and the census is a CEILING too: a sixth draw added later (say the panel again, under
			// another name) would repaint the launcher opaque while the required draws stay intact
			&& CountOf(frame, "GUI.DrawTexture(") == 5
			// The fold itself and the label half of it: a helper that returns its colour unchanged, or
			// a style that keeps the palette's full opacity, leaves the control opaque while every call
			// site above still reads correctly.
			&& withAlpha.Contains("new(color.r, color.g, color.b, color.a * alpha)", StringComparison.Ordinal)
			&& launcher.Contains("style.normal.textColor = WithAlpha(Accent, alpha);", StringComparison.Ordinal)
			&& launcher.Contains("style.hover.textColor = WithAlpha(Text, alpha);", StringComparison.Ordinal)
			&& launcher.Contains("style.active.textColor = WithAlpha(Accent, alpha);", StringComparison.Ordinal)
			// the folds must also be the LAST word on those colours: a further assignment afterwards
			// would put the label back to full opacity with the folds still in place
			&& CountOf(launcher, "textColor =") == 3;
	}

	private const string PreChangeBody = """
		private void DrawLauncherButton(OnlineUiContext ctx)
		{
			var rect = new Rect(Screen.width - 170f, 12f, 158f, 34f);
			OnlineUiTheme.DrawBackground(rect);
			var label = ctx.T("launcher") + (_state.Visible ? " ▲" : " ▼");
			if (GUI.Button(rect, label, OnlineUiTheme.Launcher()))
			{
				_state.Visible = !_state.Visible;
				if (_state.Visible && _state.Page == OnlineUiPage.Home && ctx.Session.Role != SessionRole.None)
				{
					_state.Page = OnlineUiPage.Players;
				}
			}
		}
		""";

	private const string DiscardedFadeBody = """
		private void DrawLauncherButton(OnlineUiContext ctx)
		{
			var rect = new Rect(Screen.width - 170f, 12f, 158f, 34f);
			var hovered = Event.current != null && rect.Contains(Event.current.mousePosition);
			var alpha = _state.LauncherFade.Evaluate(ctx.Time.NowMs, hovered);
			OnlineUiTheme.DrawBackground(rect);
			if (GUI.Button(rect, _state.LauncherLabel(ctx.T("launcher")), OnlineUiTheme.Launcher()))
			{
				_state.Visible = !_state.Visible;
			}
		}
		""";

	private const string OverwrittenAlphaBody = """
		private void DrawLauncherButton(OnlineUiContext ctx)
		{
			var rect = new Rect(Screen.width - 170f, 12f, 158f, 34f);
			var hovered = Event.current != null && rect.Contains(Event.current.mousePosition);
			var alpha = _state.LauncherFade.Evaluate(ctx.Time.NowMs, hovered);
			alpha = 1f;
			OnlineUiTheme.DrawBackground(rect, alpha);
			if (GUI.Button(rect, _state.LauncherLabel(ctx.T("launcher")), OnlineUiTheme.Launcher(alpha)))
			{
				_state.Visible = !_state.Visible;
			}
		}
		""";

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

	private static string ReadFlattenedLauncherDrawBody() =>
		Flatten(ExtractMember(ReadSource("OnlineUiWindow.cs"), "private void DrawLauncherButton"));

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
