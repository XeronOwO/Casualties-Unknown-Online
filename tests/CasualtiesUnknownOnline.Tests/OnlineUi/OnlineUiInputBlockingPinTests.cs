using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI's input blocking after the migration (ticket online-ui-art-and-controls-overhaul, S4 —
/// the retirement pass). Two facts hold the family together and neither is visible from a unit test:
/// CUO's own guard must never lay a blocker over CUO's own surface, because that surface is uGUI and
/// blocks its own pixels (a blocker above it swallows every click on the launcher and the window); and
/// both world input paths — the middle-click ping and the in-world right-click menu — must ask ONE
/// pointer census, which knows the launcher's rectangle as well as the window's (the launcher was on
/// neither path's list, the limit S2a recorded).
///
/// <para>
/// Every pin carries a mutation of the REAL source as its negative sample — the rows of
/// <see cref="Mutations"/> — so none of them can pass vacuously, and every anchor is matched after
/// comment-stripping and whitespace flattening, so indentation, line endings and a comment that merely
/// names an API cannot decide a result.
/// </para>
///
/// <para>
/// What these pins cannot see: whether a click really reaches the game's own controls behind the
/// surface, whether the pointer really lands on the launcher's pixels at the game's UI scale, and
/// whether the console's mutual exclusion reads right in play — those are run observations. The pins
/// hold the shape the adapter and the plugin were built to.
/// </para>
/// </summary>
public sealed class OnlineUiInputBlockingPinTests
{
	[Fact]
	public void TheGuardNeverBlocksCuosOwnSurface() =>
		Assert.True(
			GuardsItsOwnSurface(AdapterRoot("OnlineMenuInputGuard.cs")),
			"CUO's own guard must leave CUO's own surface alone in every sweep it makes: its blockers exist for surfaces the game's EventSystem cannot see, and one over CUO's own canvas makes the launcher and the window unclickable");

	[Fact]
	public void TheSurfaceMarksItsOwnCanvas() =>
		Assert.True(
			MarksItsOwnCanvas(Adapter("OnlineUiSurfaceHost.cs")),
			"the live surface must mark the root it builds, or the guard has no way to tell CUO's canvas from the game's");

	[Fact]
	public void TheLauncherEntersThePointerCensus() =>
		Assert.True(
			FeedsTheLauncherFact(Plugin("OnlineUiHost.cs")),
			"the launcher's pointer fact must reach the census: without it a middle-click over the launcher pings the world and a right-click there opens the in-world menu (S2a's recorded limit)");

	[Fact]
	public void BothWorldInputPathsAskTheCensus() =>
		Assert.True(
			BothPathsAskTheCensus(Plugin("OnlineUiOverlay.cs")),
			"the world middle-click and the world right-click must ask the same rule: two hand-kept lists of surfaces are how the launcher came to be on neither");

	[Fact]
	public void OneRectSourceFeedsTheBlockerAndTheCensus() =>
		Assert.True(
			OneRectSource(Plugin("OnlineUiOverlay.cs")),
			"the adapter's scoped blockers and the plugin's census must read the SAME rectangle list, or a click can be blocked on one path and leak on the other");

	[Fact]
	public void TheConsoleKeepsTheLauncherFromOpeningASecondSurface() =>
		Assert.True(
			TheConsoleOwnsTheInput(Plugin("OnlineUiOverlay.cs")),
			"the command console owns the input while it is open, so a launcher click must not open the window behind it — the blocker that used to swallow that click is gone by design");

	[Fact]
	public void ThePointerFactsDieWithTheSurface() =>
		Assert.True(
			RetractsThePointerFacts(Adapter("OnlineUiSurfaceHost.cs")),
			"the views report a FLIP and a rebuilt one starts un-hovered, so the facts must be retracted where the views die: a stale one is global and blocks every world ping and every in-world right-click");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(TheGuardNeverBlocksCuosOwnSurface), "adapter-root/OnlineMenuInputGuard.cs", "&& !OnlineUiSurfaceMarker.IsInside(canvas);", "&& true;", "a guard that blocks CUO's own canvas again" },
		{ nameof(TheGuardNeverBlocksCuosOwnSurface), "adapter-root/OnlineMenuInputGuard.cs", "private int CreateScopedBlockers()\n\t{\n\t\tvar created = 0;\n\t\tforeach (var canvas in Object.FindObjectsOfType<Canvas>())\n\t\t{\n\t\t\tif (!IsBlockable(canvas))", "private int CreateScopedBlockers()\n\t{\n\t\tvar created = 0;\n\t\tforeach (var canvas in Object.FindObjectsOfType<Canvas>())\n\t\t{\n\t\t\tif (canvas == null)", "one sweep that stops asking the ownership rule" },
		{ nameof(TheGuardNeverBlocksCuosOwnSurface), "adapter-root/OnlineMenuInputGuard.cs", "|| OnlineUiSurfaceMarker.IsInside(button)", "|| false", "a modal sweep that disables a control CUO's own surface put there" },
		{ nameof(TheSurfaceMarksItsOwnCanvas), "adapter/OnlineUiSurfaceHost.cs", "root.AddComponent<OnlineUiSurfaceMarker>();", "// (no marker)", "a surface the guard cannot recognise as CUO's own" },
		{ nameof(TheLauncherEntersThePointerCensus), "plugin/OnlineUiHost.cs", "_onlineUi.SetPointerOverLauncher(true);", "// the launcher's pointer fact is dropped", "a launcher hover that never reaches the census" },
		{ nameof(BothWorldInputPathsAskTheCensus), "plugin/OnlineUiOverlay.cs", "return _pointerCensus.BlocksWorldPing(gui.x, gui.y);", "return IsCommandConsoleOpen || IsWindowVisible;", "a ping path that only knows the modal flag again" },
		{ nameof(BothWorldInputPathsAskTheCensus), "plugin/OnlineUiOverlay.cs", "return _pointerCensus.BlocksWorldMenu(guiPoint.x, guiPoint.y);", "return _contextMenu.IsOpen && _contextMenu.Contains(guiPoint);", "a right-click path that keeps its own list of surfaces" },
		{ nameof(OneRectSourceFeedsTheBlockerAndTheCensus), "plugin/OnlineUiOverlay.cs", "inputBlocker?.SetOnlineUiScopedBlocks(_commandOverlay.IsOpen ? [] : CollectOverlayRects());", "inputBlocker?.SetOnlineUiScopedBlocks(_commandOverlay.IsOpen ? [] : []);", "a blocker list that no longer matches the census" },
		{ nameof(TheConsoleKeepsTheLauncherFromOpeningASecondSurface), "plugin/OnlineUiOverlay.cs", "Plugin.Logger.LogInfo(\"Online UI launcher click ignored: the command console owns the input.\");", "// the launcher opens the window behind the console", "a launcher that opens a second surface behind the console" },
		{ nameof(TheConsoleKeepsTheLauncherFromOpeningASecondSurface), "plugin/OnlineUiOverlay.cs", "if (_commandOverlay.IsOpen)\n\t\t{\n\t\t\tPlugin.Logger.LogInfo(\"Online UI launcher click ignored: the command console owns the input.\");\n\t\t\treturn;\n\t\t}\n\n\t\t_window.State.Visible = !_window.State.Visible;", "_window.State.Visible = !_window.State.Visible;\n\n\t\tif (_commandOverlay.IsOpen)\n\t\t{\n\t\t\tPlugin.Logger.LogInfo(\"Online UI launcher click ignored: the command console owns the input.\");\n\t\t\treturn;\n\t\t}", "a refusal that runs after the window has already opened" },
		{ nameof(ThePointerFactsDieWithTheSurface), "adapter/OnlineUiSurfaceHost.cs", "_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.LauncherHoverLeft));", "// the launcher's stale pointer fact is kept", "a surface that leaves the census holding a fact its view no longer reports" },
	};

	/// <summary>
	/// Every pin of this class, against a broken source: the anchor must be in the real file (so a text
	/// drift cannot turn the sample into a tautology), the replacement must change it, and the pin's own
	/// matcher must then be false.
	/// </summary>
	[Theory]
	[MemberData(nameof(Mutations))]
	public void EveryPinRejectsItsMutation(string pin, string file, string anchor, string replacement, string why)
	{
		var source = Read(file);
		Assert.True(
			source.Contains(anchor, StringComparison.Ordinal),
			$"{pin}: the mutation anchor `{anchor}` is not in {file} — re-anchor this mutation before trusting it");

		var broken = source.Replace(anchor, replacement);
		Assert.NotEqual(source, broken);
		Assert.False(Matcher(pin)(broken), $"{pin}: {why}");
	}

	private static Func<string, bool> Matcher(string pin) => pin switch
	{
		nameof(TheGuardNeverBlocksCuosOwnSurface) => GuardsItsOwnSurface,
		nameof(TheSurfaceMarksItsOwnCanvas) => MarksItsOwnCanvas,
		nameof(TheLauncherEntersThePointerCensus) => FeedsTheLauncherFact,
		nameof(BothWorldInputPathsAskTheCensus) => BothPathsAskTheCensus,
		nameof(OneRectSourceFeedsTheBlockerAndTheCensus) => OneRectSource,
		nameof(TheConsoleKeepsTheLauncherFromOpeningASecondSurface) => TheConsoleOwnsTheInput,
		nameof(ThePointerFactsDieWithTheSurface) => RetractsThePointerFacts,
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>
	/// The guard's ownership rule: one predicate that every screen-space sweep asks, and the modal
	/// sweep's own version of it for the game's custom buttons. The predicate is what the two blockers
	/// (full-screen and scoped) share, so the count of its call sites is the pin's census of sweeps.
	/// COVERAGE LIMIT, recorded rather than implied: the count sees the sweeps that exist — a THIRD sweep
	/// added later that never asks the predicate would not raise the count and would not be caught here.
	/// </summary>
	private static bool GuardsItsOwnSurface(string guardSource)
	{
		var flat = Flatten(guardSource);
		var blockable = Flatten(ExtractMember(guardSource, "private static bool IsBlockable("));

		return blockable.Contains("&& !OnlineUiSurfaceMarker.IsInside(canvas);", StringComparison.Ordinal)
			&& CountOf(flat, "if (!IsBlockable(canvas))") == 2
			&& flat.Contains("|| OnlineUiSurfaceMarker.IsInside(button)", StringComparison.Ordinal);
	}

	/// <summary>The marker is added to the root the surface builds, before anything is built under it.</summary>
	private static bool MarksItsOwnCanvas(string surfaceSource)
	{
		var flat = Flatten(surfaceSource);
		var marker = flat.IndexOf("root.AddComponent<OnlineUiSurfaceMarker>();", StringComparison.Ordinal);

		return marker >= 0
			&& marker < flat.IndexOf("OnlineUiLauncherView.TryCreate(root.transform", StringComparison.Ordinal)
			&& marker < flat.IndexOf("OnlineUiWindowView.Create(root.transform", StringComparison.Ordinal);
	}

	/// <summary>Both directions of the launcher's pointer flip reach the census, beside the fade's own
	/// input — one fact, two readers.</summary>
	private static bool FeedsTheLauncherFact(string hostSource)
	{
		var flat = Flatten(hostSource);

		return flat.Contains(
				"case OnlineUiIntentKind.LauncherHoverEntered: _launcherHovered = true; _onlineUi.SetPointerOverLauncher(true); break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.LauncherHoverLeft: _launcherHovered = false; _onlineUi.SetPointerOverLauncher(false); break;",
				StringComparison.Ordinal);
	}

	/// <summary>
	/// Both world paths ask the census and nothing else: the ping converts the pointer to GUI space and
	/// asks the Runtime's rule, the right-click asks it too — and the plugin keeps no second copy of the
	/// window's pointer fact.
	/// </summary>
	private static bool BothPathsAskTheCensus(string overlaySource)
	{
		var flat = Flatten(overlaySource);

		return flat.Contains("var gui = new Vector2(mousePosition.x, Screen.height - mousePosition.y);", StringComparison.Ordinal)
			&& flat.Contains("return _pointerCensus.BlocksWorldPing(gui.x, gui.y);", StringComparison.Ordinal)
			&& flat.Contains("if (BlocksWorldMenu(mouse))", StringComparison.Ordinal)
			&& flat.Contains("return _pointerCensus.BlocksWorldMenu(guiPoint.x, guiPoint.y);", StringComparison.Ordinal)
			&& !flat.Contains("_pointerOverWindow", StringComparison.Ordinal);
	}

	/// <summary>The scoped blockers the adapter builds and the rectangles the census reads are one list.</summary>
	private static bool OneRectSource(string overlaySource)
	{
		var flat = Flatten(overlaySource);

		return flat.Contains("_pointerCensus.OverlayRects = CollectOverlayRects();", StringComparison.Ordinal)
			&& flat.Contains(
				"inputBlocker?.SetOnlineUiScopedBlocks(_commandOverlay.IsOpen ? [] : CollectOverlayRects());",
				StringComparison.Ordinal);
	}

	/// <summary>
	/// The launcher's own opening rule refuses while the console owns the input, and says so at a level the
	/// plugin's default log configuration shows. The ORDER is part of the pin: a refusal that ran after the
	/// toggle would leave the window open behind the console with the line still in the file.
	/// </summary>
	private static bool TheConsoleOwnsTheInput(string overlaySource)
	{
		var body = Flatten(ExtractMember(overlaySource, "internal void ToggleWindow("));
		var refusal = body.IndexOf("if (_commandOverlay.IsOpen)", StringComparison.Ordinal);
		var toggle = body.IndexOf("_window.State.Visible = !_window.State.Visible;", StringComparison.Ordinal);

		return refusal >= 0
			&& toggle > refusal
			&& body.Contains("Plugin.Logger.LogInfo(", StringComparison.Ordinal);
	}

	/// <summary>
	/// Both pointer facts are retracted where the views that reported them die. The views report a FLIP
	/// only and a rebuilt one starts un-hovered, so a surface rebuilt while the pointer sat on the
	/// launcher would otherwise leave the census holding "the pointer is over CUO's UI" for good — and
	/// that fact is global, so it would block every world ping and every in-world right-click.
	/// </summary>
	private static bool RetractsThePointerFacts(string surfaceSource)
	{
		var body = Flatten(ExtractMember(surfaceSource, "private void DestroySurface()"));

		return body.Contains("_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.LauncherHoverLeft));", StringComparison.Ordinal)
			&& body.Contains("_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.WindowHoverLeft));", StringComparison.Ordinal);
	}

	private static string Read(string file)
	{
		var separator = file.IndexOf('/');
		Assert.True(separator > 0, $"the mutation's file `{file}` must be written as `<tree>/<name>.cs`");
		var tree = file.Substring(0, separator);
		var name = file.Substring(separator + 1);
		return tree switch
		{
			"plugin" => Plugin(name),
			"adapter" => Adapter(name),
			"adapter-root" => AdapterRoot(name),
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

	private static string Plugin(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

	private static string Adapter(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", fileName));

	/// <summary>The adapter's own root: the input guard and the scoped raycast filter are not part of the
	/// <c>OnlineUi/</c> view folder, so the mutation rows name them separately.</summary>
	private static string AdapterRoot(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, fileName));

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

	/// <summary>Comments cut and whitespace collapsed, so indentation and line endings cannot decide a
	/// pin; the file is read with its line endings normalised for the same reason (<c>dotnet format</c>
	/// rewrites the tree to CRLF).</summary>
	private static string Flatten(string source) =>
		string.Join(" ", StripComments(source).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	private static string StripComments(string source)
	{
		var kept = new List<string>();
		var inBlock = false;
		foreach (var line in source.Split('\n'))
		{
			var text = line;
			if (inBlock)
			{
				var end = text.IndexOf("*/", StringComparison.Ordinal);
				if (end < 0)
				{
					kept.Add(string.Empty);
					continue;
				}

				text = text.Substring(end + 2);
				inBlock = false;
			}

			var block = text.IndexOf("/*", StringComparison.Ordinal);
			var lineComment = text.IndexOf("//", StringComparison.Ordinal);
			if (block >= 0 && (lineComment < 0 || block < lineComment))
			{
				kept.Add(text.Substring(0, block));
				if (text.IndexOf("*/", block, StringComparison.Ordinal) < 0)
				{
					inBlock = true;
				}

				continue;
			}

			kept.Add(lineComment < 0 ? text : text.Substring(0, lineComment));
		}

		return string.Join("\n", kept);
	}

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

	private static string ReadNormalised(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

	private static string GameAdapterDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.GameAdapter");

	private static string PluginDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.Plugin");

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
