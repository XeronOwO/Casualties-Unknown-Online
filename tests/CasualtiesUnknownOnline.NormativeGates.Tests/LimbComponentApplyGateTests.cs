using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The limb-component apply has TWO halves that must never be swapped, and this gate is what says so
/// without a running game.
/// <para>
/// The AUTHORITATIVE half writes the body that owns the limb: there the message's component list is the
/// truth, so a type it omits is a REMOVAL — without it the authority's own splint removal never reached
/// the target, the target's next report carried the component straight back, and the same limb could be
/// "removed" again for a second splint (batch <c>20261011-b</c>). The DISPLAY half writes a body CUO only
/// renders, where instantiating the game's own component runs the game's own <c>Start</c> — a wound-view
/// icon whose removal predicate dereferences the limb, and a per-frame <c>NullReferenceException</c>
/// storm once the body under the icon is replaced.
/// </para>
/// <para>
/// Behavioural tests cannot reach either half: they live in the Game Adapter, which the behavioural
/// project references with <c>ExcludeAssets="compile"</c>, and the code they drive is Unity's own
/// <c>AddComponent</c>/<c>Destroy</c>. What this gate pins is therefore the SOURCE shape — which mode each
/// caller names, that the removal covers exactly the types the capture writes, that the display half
/// refuses a body CUO does not own and disables every instance it creates, and that the display icons are
/// owned by both of their lifetimes. The halves' BEHAVIOUR is proven at runtime instead: the acceptance
/// probe applies a component-less limb row through <c>CharacterDataSync.ApplyHealState</c> and reads the
/// limb back (batch <c>20261011-c</c>).
/// </para>
/// <para>
/// Every scan below reads code with the comments stripped: these files explain themselves by quoting the
/// game's own source (<c>[Saveable]</c> teardown, the icon predicates), and a text pin that counted its
/// own explanation would fail on a documentation edit and pass on a real regression.
/// </para>
/// </summary>
public class LimbComponentApplyGateTests
{
	private const string CodecPath = "src/CasualtiesUnknownOnline.GameAdapter/Character/LimbComponentStateCodec.cs";
	private const string CoordinatorPath = "src/CasualtiesUnknownOnline.GameAdapter/RemoteMedicalCoordinator.cs";
	private const string ViewPath = "src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteMedicalView.cs";
	private const string IconsPath = "src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteMedicalDisplayIcons.cs";
	private const string BodySyncPath = "src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs";
	private const string RestorePath = "src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterRestoreApplier.cs";

	/// <summary>The component types the codec owns — the same three on both halves.</summary>
	private const int KnownComponentTypes = 3;

	/// <summary>
	/// Which files may name which apply mode. The authoritative half belongs to the bodies that own their
	/// limbs (the local body's heal apply and the reconnect restore); the display half belongs to the one
	/// coordinator that builds a display copy. A call anywhere else is a call whose mode nobody chose.
	/// </summary>
	private static readonly (string Call, string[] Owners)[] Modes =
	[
		("ApplyAuthoritative(", [BodySyncPath, RestorePath]),
		("ApplyDisplay(", [CoordinatorPath]),
	];

	[Fact]
	public void EveryLimbComponentApply_NamesItsModeFromAnOwningFile()
	{
		var sources = SourceScan.SourceFiles(RepositoryPaths.File("src"))
			.Select(path => (
				Path: Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/'),
				Text: File.ReadAllText(path)))
			.ToArray();

		var failures = ModeCallSiteProblems(sources).ToList();
		var observed = ObservedCallSites(sources).ToList();

		// The floor: the two named modes are called at all (three call sites today). A scan that sees fewer
		// is a broken scan, not a clean tree.
		if (observed.Count < 3)
		{
			failures.Add($"the scan found {observed.Count} named apply call(s); the tree calls the two modes from three sites");
		}

		foreach (var owner in Modes.SelectMany(mode => mode.Owners))
		{
			if (!observed.Any(site => site.StartsWith(owner + ":", StringComparison.Ordinal)))
			{
				failures.Add($"{owner} no longer calls any apply mode — the gate no longer reaches its own subject");
			}
		}

		Assert.True(
			failures.Count == 0,
			"a limb-component apply does not name its mode, or is called from a file that does not own it." + Environment.NewLine
			+ string.Join(Environment.NewLine, failures));
	}

	/// <summary>
	/// The matcher is pinned with both answers, so the gate above cannot pass by matching nothing: a stray
	/// caller is reported, an owner is not, and a call that names no mode at all is reported.
	/// </summary>
	[Fact]
	public void TheCallSiteMatcher_ReadsTheOwnerAndFlagsEverythingElse()
	{
		(string Path, string Text)[] strayCaller = [("src/other/Somewhere.cs", "LimbComponentStateCodec.ApplyDisplay(limb, states);")];
		(string Path, string Text)[] strayAuthoritative = [("src/other/Somewhere.cs", "LimbComponentStateCodec.ApplyAuthoritative(limb, states);")];
		(string Path, string Text)[] unnamedCaller = [("src/other/Somewhere.cs", "LimbComponentStateCodec.Apply(limb, states);")];

		Assert.Contains(ModeCallSiteProblems(strayCaller), problem => problem.Contains("ApplyDisplay", StringComparison.Ordinal));
		Assert.Contains(ModeCallSiteProblems(strayAuthoritative), problem => problem.Contains("ApplyAuthoritative", StringComparison.Ordinal));
		Assert.Contains(ModeCallSiteProblems(unnamedCaller), problem => problem.Contains("names no mode", StringComparison.Ordinal));
		Assert.Empty(ModeCallSiteProblems([(CoordinatorPath, "LimbComponentStateCodec.ApplyDisplay(limb, states);")]));
		Assert.Empty(ModeCallSiteProblems([(BodySyncPath, "LimbComponentStateCodec.ApplyAuthoritative(limb, states);")]));
		Assert.Empty(ModeCallSiteProblems([(RestorePath, "LimbComponentStateCodec.ApplyAuthoritative(limb, states);")]));
	}

	[Fact]
	public void TheAuthoritativeApply_RemovesExactlyWhatItCaptures_AndNeverRunsTheGamesTeardown()
	{
		var codec = SourceOnly(RepositoryPaths.ReadText(CodecPath));
		var captured = Matches(codec, @"nameof\((?<type>[A-Za-z_][A-Za-z0-9_]*)\)").ToHashSet(StringComparer.Ordinal);
		var removed = Matches(codec, @"DestroyIfPresent<(?<type>[A-Za-z_][A-Za-z0-9_]*)>\(limb\)").ToHashSet(StringComparer.Ordinal);

		Assert.True(
			captured.Count == KnownComponentTypes,
			$"the codec names {captured.Count} component type(s), not the pinned {KnownComponentTypes}: {string.Join(", ", captured.Order(StringComparer.Ordinal))}");
		Assert.Equal(
			captured.Order(StringComparer.Ordinal),
			removed.Order(StringComparer.Ordinal));

		// The game's own teardown hands the removed item to the LIMB'S OWNER (`AutoPickUpItem`); the design
		// gives it to the operator instead, and the target must never receive a second splint.
		Assert.DoesNotContain("TakeOff", codec, StringComparison.Ordinal);

		// The removal has to be visible in the frame it happens: the receiver re-reports its character
		// snapshot in the same call that applies the message, so a frame-deferred Destroy would report the
		// component that is on its way out straight back to the authority.
		Assert.Contains("DestroyImmediate", codec, StringComparison.Ordinal);
	}

	[Fact]
	public void TheDisplayApply_RefusesABodyCuoDoesNotOwn_AndKeepsEveryInstanceInert()
	{
		var codec = SourceOnly(RepositoryPaths.ReadText(CodecPath));
		var coordinator = SourceOnly(RepositoryPaths.ReadText(CoordinatorPath));

		Assert.Contains("RemoteMedicalDisplayBody.IsDisplayCopy(limb)", codec, StringComparison.Ordinal);
		Assert.Contains("AddComponent<RemoteMedicalDisplayBody>()", coordinator, StringComparison.Ordinal);

		// Every component the display half can create is created disabled. The load-bearing guard against a
		// LIVE body is `IsDisplayCopy` above (that is the shape the storm came from); the flag is defence in
		// depth on top of the parked hierarchy, and a disabled behaviour receives no Start/Update/OnEnable.
		// `tools/acceptance/recipes/limb-component-apply.cs` reproduces the pair on a live client
		// (`mode=add-disabled` + `mode=status` reads Start unrun; the same component left enabled reads it
		// run).
		var inert = Regex.Matches(codec, @"enabled = false;").Count;
		Assert.Equal(KnownComponentTypes, inert);
	}

	[Fact]
	public void TheDisplayIcons_AreOwnedByBothLifetimes()
	{
		var icons = SourceOnly(RepositoryPaths.ReadText(IconsPath));
		var view = SourceOnly(RepositoryPaths.ReadText(ViewPath));

		// Lifetime 1: the focus teardown — the one place that destroys the display copy — drops every icon
		// this class owns, and the state going away removes the icon it belongs to.
		Assert.Contains("RemoteMedicalDisplayIcons.Clear();", view, StringComparison.Ordinal);
		Assert.Contains("Entries.Remove(entry);", icons, StringComparison.Ordinal);

		// Lifetime 2: the icon's own removal predicate may only ask whether its limb is still alive. The
		// game's own predicates dereference the limb, which is what turned a replaced display body into a
		// per-frame exception storm; this one cannot.
		Assert.Contains("(Limb x) => x == null", icons, StringComparison.Ordinal);
		Assert.DoesNotContain("x.GetComponent", icons, StringComparison.Ordinal);
	}

	/// <summary>The files that call an apply mode with a name the gate does not own, or with no mode name at all.</summary>
	private static IEnumerable<string> ModeCallSiteProblems(IEnumerable<(string Path, string Text)> sources)
	{
		foreach (var (path, text) in sources)
		{
			var code = SourceOnly(text);
			if (Regex.IsMatch(code, @"LimbComponentStateCodec\.Apply\s*\("))
			{
				yield return $"{path}: `LimbComponentStateCodec.Apply(...)` names no mode — the authoritative and display halves are different operations";
			}

			foreach (var (call, owners) in Modes)
			{
				if (code.Contains("LimbComponentStateCodec." + call, StringComparison.Ordinal) && !owners.Contains(path, StringComparer.Ordinal))
				{
					yield return $"{path}: `{call}` is not this file's mode (owners: {string.Join(", ", owners)})";
				}
			}
		}
	}

	private static IEnumerable<string> ObservedCallSites(IEnumerable<(string Path, string Text)> sources) =>
		from source in sources
		from mode in Modes
		where SourceOnly(source.Text).Contains("LimbComponentStateCodec." + mode.Call, StringComparison.Ordinal)
		select $"{source.Path}:{mode.Call}";

	private static IEnumerable<string> Matches(string text, string pattern) =>
		Regex.Matches(text, pattern).Select(match => match.Groups["type"].Value).Distinct(StringComparer.Ordinal);

	/// <summary>The text with its comments removed: these files quote the game's own source in their documentation, and a pin that read its own explanation would fail on a doc edit and pass on a regression.</summary>
	private static string SourceOnly(string text) =>
		Regex.Replace(Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"//[^\r\n]*", string.Empty);
}
