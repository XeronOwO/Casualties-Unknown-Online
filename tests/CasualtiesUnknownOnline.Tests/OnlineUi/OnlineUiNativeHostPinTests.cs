using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The native host's contract, pinned against the source (ticket online-ui-art-and-controls-overhaul,
/// S1). The host touches the game's live scene, and no unit test in this tree can instantiate Unity
/// objects, so the rules that must not regress are pinned as source facts instead: the canvas is a CHILD
/// of the game's own canvas (that is where the game's UI scale and sorting come from), the hierarchy is
/// inactive before anything of it exists, the only spawn surface is the game's two settings rows, and the
/// settings screen is never spawned — <c>Special/SettingsMenu</c>'s <c>Start</c> builds the whole screen
/// and claims the static <c>SettingsMenu.instance</c> the game's own <c>OpenMenu</c> returns early on,
/// and its <c>Close</c>/<c>ResetToDefault</c> write the settings file.
///
/// <para>
/// Every pin carries a negative sample — mostly a mutation of the real source — so none of them can pass
/// vacuously. The pins are deliberately shaped to catch the evasions a first cut misses: exactly ONE
/// parent call (a later detach fails), no <c>SetActive(true)</c>, exactly one <c>Resources.Load</c> and
/// one <c>Instantiate</c> (a third spawn through another overload fails), the destructive prefab matched
/// over the WHOLE adapter and plugin trees including its quoted bare name (a concatenated path fails),
/// and anchors flattened before matching, so indentation and line endings cannot decide a result.
/// </para>
/// </summary>
public sealed class OnlineUiNativeHostPinTests
{
	[Fact]
	public void TheCanvasIsParentedUnderTheGamesOwnCanvas()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");

		Assert.True(
			ParentsUnderTheGameCanvas(host),
			"the CUO canvas must be created as a child of the game's canvas — a detached root canvas loses the game's scale and sorting");
	}

	[Fact]
	public void ThePinRejectsADetachedRootCanvas() => Assert.False(ParentsUnderTheGameCanvas(DetachedRootBody));

	[Fact]
	public void ThePinRejectsALaterDetach()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");
		var broken = host.Replace(
			"root.transform.SetParent(parent, worldPositionStays: false);",
			"root.transform.SetParent(parent, worldPositionStays: false);\n\t\troot.transform.SetParent(null, worldPositionStays: false);");

		Assert.True(host != broken, "the detach mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(ParentsUnderTheGameCanvas(broken));
	}

	[Fact]
	public void ThePreRunCanvasIsTriedBeforeThePlayerCameras()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");

		Assert.True(
			PreRunCanvasComesFirst(host),
			"the pre-run/menu canvas exists before the player camera's, so it must be tried first");
	}

	[Fact]
	public void ThePinRejectsTheReversedCanvasOrder()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");
		var broken = host.Replace(
			"if (PreRunScript.instance != null && PreRunScript.instance.mainCanvas != null)",
			"if (PlayerCamera.main != null && PlayerCamera.main.mainCanvas != null)");

		Assert.True(host != broken, "the canvas-order mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(PreRunCanvasComesFirst(broken));
	}

	[Fact]
	public void TheProbeInstantiatesExactlyTheGamesTwoSettingRows()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");

		Assert.True(
			InstantiatesTheTwoSettingRows(host),
			"the probe may spawn the game's two settings rows and nothing else — the row list is the whole spawn surface");
	}

	[Fact]
	public void ThePinRejectsAThirdRowPrefab()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");
		var broken = host.Replace(
			"[\"Special/GameSettingDropdown\", \"Special/GameSettingInput\"]",
			"[\"Special/GameSettingDropdown\", \"Special/GameSettingInput\", \"Special/GameSettingBool\"]");

		Assert.True(host != broken, "the row-list mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(InstantiatesTheTwoSettingRows(broken));
	}

	[Fact]
	public void ThePinRejectsASecondSpawnThroughAnotherOverload()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");
		var broken = host.Replace(
			"\t\treturn host;",
			"\t\t_ = Object.Instantiate(Resources.Load(\"Special/GameSettingFloat\"), root.transform);\n\t\treturn host;");

		Assert.True(host != broken, "the second-spawn mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(InstantiatesTheTwoSettingRows(broken));
	}

	[Fact]
	public void NoSourceInTheAdapterOrThePluginLoadsTheSettingsMenuPrefab()
	{
		var offenders = AdapterAndPluginSources()
			.Where(file => LoadsTheSettingsMenu(file.Source))
			.Select(file => file.Path)
			.ToList();

		Assert.True(
			offenders.Count == 0,
			$"these files name `Special/SettingsMenu` outside a comment — instantiating that prefab builds the whole settings screen and claims the game's own singleton: {string.Join(", ", offenders)}");
	}

	[Fact]
	public void TheSettingsMenuMatcher_FlagsTheLoadAndIgnoresTheMention()
	{
		Assert.True(
			LoadsTheSettingsMenu("var prefab = Resources.Load<GameObject>(\"Special/SettingsMenu\");"),
			"the matcher must see a real load of the prefab");
		Assert.True(
			LoadsTheSettingsMenu("var path = \"Special/\" + \"SettingsMenu\";"),
			"a concatenated path and a bare quoted name are the same load");
		Assert.False(
			LoadsTheSettingsMenu("/// <c>Special/SettingsMenu</c> is never instantiated: its Start builds the whole screen."),
			"a doc-comment mention is not a load");
		Assert.False(
			LoadsTheSettingsMenu("/* Special/SettingsMenu is never spawned here */ var x = 1;"),
			"a block-comment mention is not a load");
		Assert.False(
			LoadsTheSettingsMenu("var path = \"Special/GameSettingDropdown\";"),
			"no other prefab path may trip the matcher");
	}

	[Fact]
	public void TheProbeHierarchyIsInactiveBeforeAnythingOfItExists()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");

		Assert.True(
			DeactivatesBeforeBuilding(host),
			"the root must be inactive before it is parented, given its Canvas or given a row — and nothing may ever re-activate it");
	}

	[Fact]
	public void ThePinRejectsRowsSpawnedIntoAnActiveCanvas()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");
		var broken = host
			.Replace("\t\troot.SetActive(false);\n", string.Empty)
			.Replace("\t\treturn host;", "\t\troot.SetActive(false);\n\t\treturn host;");

		Assert.True(host != broken, "the deactivation-order mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(DeactivatesBeforeBuilding(broken));
	}

	[Fact]
	public void ThePinRejectsAReActivatedProbe()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");
		var broken = host.Replace("\t\treturn host;", "\t\troot.SetActive(true);\n\t\treturn host;");

		Assert.True(host != broken, "the re-activation mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(DeactivatesBeforeBuilding(broken));
	}

	[Fact]
	public void TheProbeTearsItsOwnSceneObjectsDown()
	{
		var host = AdapterSource("OnlineUiNativeSurfaceHost.cs");
		var capture = Flatten(AdapterSource("OnlineUiNativeFactsCapture.cs"));
		var adapter = AdapterSource("..\\GameAdapter.cs");

		Assert.True(
			host.Contains("Object.Destroy(_root);", StringComparison.Ordinal),
			"the host must destroy its own canvas — a probe that leaves a canvas in the game's scene is not read-only");
		Assert.True(
			capture.Contains("if (reading.IsComplete) { _finished = true; Dispose(); }", StringComparison.Ordinal),
			"a complete reading must dispose the probe's scene objects in the same call that produced it");
		Assert.True(
			capture.Contains("if (_host != null && _host.RootTransform == null)", StringComparison.Ordinal),
			"a scene change destroys CUO's canvas with the game's, so the capture must rebuild a host whose root is gone instead of dereferencing destroyed objects");
		Assert.True(
			adapter.Contains("_onlineUiNativeFacts.Dispose();", StringComparison.Ordinal),
			"the adapter's own Dispose must release the host of a reading that never completed");
	}

	[Fact]
	public void ThePluginProbeReportsThroughTheRuntimePolicyAndReport()
	{
		var probe = PluginSource("OnlineUiNativeFactsProbe.cs");

		Assert.True(
			ReportsThroughTheRuntime(probe),
			"the plugin must ask the Runtime policy when to probe and print the Runtime report, not format the reading itself");
	}

	[Fact]
	public void ThePinRejectsAPluginThatFormatsTheReadingItself()
	{
		var probe = PluginSource("OnlineUiNativeFactsProbe.cs");
		var broken = probe.Replace(
			"OnlineUiNativeFactsReport.Describe(facts)",
			"new[] { \"canvas=\" + facts.CanvasPath }");

		Assert.True(probe != broken, "the report mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(ReportsThroughTheRuntime(broken));
	}

	/// <summary>The canvas is created under the transform the game's own canvas source returned, exactly once,
	/// so a later detach cannot leave it hanging off the scene root.</summary>
	private static bool ParentsUnderTheGameCanvas(string hostSource) =>
		hostSource.Contains("var parent = FindMainCanvasTransform();", StringComparison.Ordinal)
		&& CountOf(hostSource, ".SetParent(") == 1
		&& hostSource.Contains("root.transform.SetParent(parent, worldPositionStays: false);", StringComparison.Ordinal);

	/// <summary>The pre-run canvas is consulted first, the player camera's second.</summary>
	private static bool PreRunCanvasComesFirst(string hostSource)
	{
		var preRun = hostSource.IndexOf("PreRunScript.instance.mainCanvas", StringComparison.Ordinal);
		var camera = hostSource.IndexOf("PlayerCamera.main.mainCanvas", StringComparison.Ordinal);
		return preRun >= 0 && camera > preRun;
	}

	/// <summary>The spawn surface is one array of exactly the game's dropdown and keybind rows, walked once,
	/// through exactly one load and one instantiation.</summary>
	private static bool InstantiatesTheTwoSettingRows(string hostSource) =>
		hostSource.Contains(
			"internal static readonly string[] RowPrefabPaths = [\"Special/GameSettingDropdown\", \"Special/GameSettingInput\"];",
			StringComparison.Ordinal)
		&& hostSource.Contains("foreach (var path in RowPrefabPaths)", StringComparison.Ordinal)
		&& CountOf(hostSource, "Resources.Load") == 1
		&& CountOf(hostSource, "Instantiate(") == 1;

	/// <summary>The settings-menu prefab loaded or named as a path, in code (comments are cut first): the
	/// exact path, or the quoted bare name a concatenated path would use.</summary>
	private static bool LoadsTheSettingsMenu(string source)
	{
		var code = StripComments(source);
		return code.Contains("Special/SettingsMenu", StringComparison.Ordinal)
			|| code.Contains("\"SettingsMenu\"", StringComparison.Ordinal);
	}

	/// <summary>Inactive first, and never activated again: the root is deactivated before it is parented,
	/// before it is given its Canvas, and before the first row is loaded.</summary>
	private static bool DeactivatesBeforeBuilding(string hostSource)
	{
		var deactivate = hostSource.IndexOf("root.SetActive(false);", StringComparison.Ordinal);
		var parent = hostSource.IndexOf("root.transform.SetParent(", StringComparison.Ordinal);
		var canvas = hostSource.IndexOf("root.AddComponent<Canvas>();", StringComparison.Ordinal);
		var spawn = hostSource.IndexOf("Resources.Load", StringComparison.Ordinal);
		return deactivate >= 0
			&& deactivate < parent
			&& deactivate < canvas
			&& deactivate < spawn
			&& !hostSource.Contains("SetActive(true)", StringComparison.Ordinal);
	}

	/// <summary>The plugin asks the Runtime policy and prints the Runtime report.</summary>
	private static bool ReportsThroughTheRuntime(string probeSource) =>
		probeSource.Contains("if (_query is null || !_policy.ShouldAttempt(nowMs))", StringComparison.Ordinal)
		&& probeSource.Contains("_policy.NoteAttempt(facts, nowMs)", StringComparison.Ordinal)
		&& probeSource.Contains("OnlineUiNativeFactsReport.Describe(facts)", StringComparison.Ordinal);

	/// <summary>Every `.cs` file of the two game-facing projects, with comments cut — the scan surface the
	/// destructive-prefab rule is checked over.</summary>
	private static IReadOnlyList<(string Path, string Source)> AdapterAndPluginSources()
	{
		var roots = new[] { GameAdapterDirectory, PluginDirectory };
		return
		[
			.. roots
				.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
				.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
					&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => (Path: path, Source: StripComments(ReadNormalised(path)))),
		];
	}

	/// <summary>Cut line and block comments, so a comment that NAMES a prefab is not read as a load of it,
	/// while a path inside a string literal survives.</summary>
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

	/// <summary>Comments cut and whitespace collapsed, so indentation and line endings cannot decide a pin.</summary>
	private static string Flatten(string source) =>
		string.Join(" ", StripComments(source).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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

	private const string DetachedRootBody = """
		var root = new GameObject(RootName);
		var canvas = root.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		root.SetActive(false);
		""";

	/// <summary>
	/// A source file read with its line endings normalised to <c>\n</c>. The working tree is checked out
	/// with CRLF (<c>core.autocrlf=true</c>), so a pin that spelled a multi-line anchor with <c>\n</c>
	/// would pass on a fresh file and fail the moment <c>dotnet format</c> rewrote it — which is exactly
	/// how this pin set first broke.
	/// </summary>
	private static string ReadNormalised(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

	private static string AdapterSource(string fileName) => ReadNormalised(Path.Combine(AdapterOnlineUiDirectory, fileName));

	private static string PluginSource(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

	private static string AdapterOnlineUiDirectory => Path.Combine(GameAdapterDirectory, "OnlineUi");

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
