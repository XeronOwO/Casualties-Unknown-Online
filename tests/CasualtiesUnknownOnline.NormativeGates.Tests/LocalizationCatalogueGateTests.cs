using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The catalogue declares exactly the keys the product source reads. The census counts READS — a key
/// named only in a comment, a log literal or prose is not a read — and it follows the
/// tree builds: an interpolated key space (<c>ctx.T($"prefs.color.{name}")</c>), a declared
/// key-producing helper, and a key handed to a helper whose <c>labelKey</c> parameter the helper
/// resolves through an accessor of its own. A new family of orphan keys, a dangling reference and a
/// table that drifted out of step therefore fail here instead of accumulating silently.
/// </summary>
public sealed class LocalizationCatalogueGateTests
{
	private const string CatalogueRelativePath = "src/CasualtiesUnknownOnline.Runtime/Localization/LocalizationCatalog.cs";
	private const string EnglishAnchor = "IReadOnlyDictionary<string, string> English = new Dictionary<string, string>";
	private const string ChineseAnchor = "IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>";

	/// <summary>The measured size when the gate landed, well below it: a broken parser must fail
	/// loudly instead of reporting an empty or one-sided census.</summary>
	private const int DeclaredKeyFloor = 150;
	private const int ReadKeyFloor = 120;

	/// <summary>The accessor call sites the text scan cannot resolve (pass-throughs, echoed parameters
	/// and unrelated `Format` methods): a diagnostic bound, so a broken accessor shape fails loudly
	/// instead of reporting a census that quietly stopped seeing call sites.</summary>
	private const int UnresolvedCeiling = 60;

	private static readonly string[] Accessors = ["T", "F", "Format"];

	/// <summary>
	/// The name a helper parameter carries when the helper is handed a catalogue KEY rather than a
	/// translated label: the helper resolves it through an accessor of its own, so a key-shaped literal
	/// passed at that position is a read. The convention is derived, never listed; the fact below
	/// asserts that every parameter of this name is really resolved through an accessor, and a helper
	/// that carries a key under another name keeps its keys out of the census, where they fail loudly
	/// as orphans instead of passing as reads.
	/// </summary>
	private const string KeyParameterName = "labelKey";

	private static readonly Regex KeyParameterPattern = new(@"([A-Za-z_]\w*)\([^()]*\blabelKey\b[^()]*\)", RegexOptions.Compiled);

	private static readonly Regex DeclaredKeyPattern = new(@"\[""([^""]+)""\]\s*=", RegexOptions.Compiled);

	/// <summary>
	/// Key-producing helpers the accessor scan cannot see through: the helper returns a literal key
	/// that a call site then hands to an accessor. Each entry is a declaration, not a convenience —
	/// the fact below fails when one disappears or returns a key outside the catalogue, and an
	/// UNDECLARED helper's literals are not reads, so a new one fails loudly instead of hiding an
	/// orphan behind a shape the census cannot follow.
	/// </summary>
	private static readonly (string Method, string Reason)[] KeyProducingHelpers =
	[
		("KindKeyOf", "OnlineUiWorldsDrawer's world-backup kind switch, whose literals reach ctx.T through ctx.T(KindKeyOf(backup.Kind)) at the only call site"),
	];

	[Fact]
	public void EveryDeclaredKey_IsReadByProductSource()
	{
		var unread = UnreadKeys(CatalogueText(), ProductSourceText());
		Assert.True(
			unread.Length == 0,
			$"{unread.Length} catalogue key(s) have no reader in src/: {string.Join(", ", unread)}");
	}

	[Fact]
	public void EveryKeyTheSourceReads_IsDeclared()
	{
		var dangling = DanglingKeys(CatalogueText(), ProductSourceText());
		Assert.True(
			dangling.Length == 0,
			$"{dangling.Length} key(s) are read by src/ but the catalogue does not declare: {string.Join(", ", dangling)}");
	}

	[Fact]
	public void BothLanguageTables_DeclareTheSameKeySet()
	{
		var (english, chinese) = DeclaredKeys(CatalogueText());
		var onlyEnglish = english.Except(chinese, StringComparer.Ordinal).ToArray();
		var onlyChinese = chinese.Except(english, StringComparer.Ordinal).ToArray();
		Assert.True(
			onlyEnglish.Length == 0 && onlyChinese.Length == 0,
			$"the two tables drifted — only English: [{string.Join(", ", onlyEnglish)}]; only Chinese: [{string.Join(", ", onlyChinese)}]");
	}

	[Fact]
	public void TheCensus_MeetsItsFloor()
	{
		var (english, chinese) = DeclaredKeys(CatalogueText());
		var census = Census(ProductSourceText());
		Assert.True(
			english.Length >= DeclaredKeyFloor,
			$"the English table declares {english.Length} keys, below the census floor {DeclaredKeyFloor}: the parser or the file's shape moved");
		Assert.True(
			chinese.Length >= DeclaredKeyFloor,
			$"the Chinese table declares {chinese.Length} keys, below the census floor {DeclaredKeyFloor}: the parser or the file's shape moved");
		Assert.True(
			census.Keys.Count >= ReadKeyFloor,
			$"the scan found {census.Keys.Count} read keys, below the census floor {ReadKeyFloor}: the scanner or the accessor shape moved");
		Assert.True(
			census.Unresolved <= UnresolvedCeiling,
			$"the scan could not resolve {census.Unresolved} accessor call sites, above the ceiling {UnresolvedCeiling}: the accessor shape moved");
	}

	[Fact]
	public void EveryKeyProducingHelper_StillReturnsDeclaredKeys()
	{
		var flattened = SourceScan.Flatten(ProductSourceText());
		var declared = DeclaredKeySet(CatalogueText());
		foreach (var helper in KeyProducingHelpers)
		{
			var definitions = CountOf(flattened, $"string {helper.Method}(");
			Assert.True(
				definitions == 1,
				$"the declared key-producing helper {helper.Method} has {definitions} definitions in src/, expected exactly one — {helper.Reason}");
			var keys = HelperLiterals(flattened, helper.Method);
			Assert.True(
				keys.Length > 0,
				$"the declared key-producing helper {helper.Method} returns no literal key any more — {helper.Reason}");
			foreach (var key in keys)
			{
				Assert.True(
					declared.Contains(key),
					$"{helper.Method} returns {key}, which the catalogue does not declare — {helper.Reason}");
			}
		}
	}

	[Fact]
	public void EveryKeyCarryingHelper_ResolvesItsKeyParameter()
	{
		var flattened = SourceScan.Flatten(ProductSourceText());
		var methods = KeyCarryingMethods(flattened);
		Assert.True(
			methods.Length > 0,
			$"no method declares a `{KeyParameterName}` parameter any more: the convention or the parser moved");
		foreach (var method in methods)
		{
			var body = MethodBody(flattened, method);
			Assert.True(body.Length > 0, $"the key-carrying helper {method} has no body in src/");
			Assert.True(
				Accessors.Any(accessor => body.Contains($".{accessor}({KeyParameterName}", StringComparison.Ordinal)),
				$"{method} declares a `{KeyParameterName}` parameter but never resolves it through an accessor, so its call sites' literals are not reads");
		}
	}

	// --- the matcher's own contract: each sample feeds synthetic text to the same pure functions the
	// facts use, so a scanner that stops seeing a read shape, or starts counting a mention, fails ---

	[Fact]
	public void TheCensus_ReadsEveryLiteralOfOneCallSite()
	{
		var source = """var a = ctx.T("alpha.beta"); var b = ctx.T(flag ? "alpha.gamma" : "alpha.delta"); var c = ctx.F("alpha.epsilon", 1);""";
		AssertSame(
			["alpha.beta", "alpha.gamma", "alpha.delta", "alpha.epsilon"],
			Census(source).Keys,
			"every literal of an accessor call site is a read, ternary branches included");
	}

	[Fact]
	public void TheCensus_ReadsAnInterpolatedKeySpace()
	{
		var source = """var label = ctx.T($"alpha.color.{name}");""";
		var census = Census(source);
		AssertSame(
			["alpha.color."],
			census.Prefixes,
			"an interpolated argument names a key space, not one key");
		AssertSame([], census.Keys, "an interpolated argument names no key of its own");
	}

	[Fact]
	public void TheCensus_ReadsAKeyReturnedByADeclaredHelper()
	{
		var source = "private static string KindKeyOf(Kind kind) => kind switch\n{\n\tKind.A => \"alpha.helper_a\",\n\t_ => \"alpha.helper_b\",\n\t};";
		AssertSame(
			["alpha.helper_a", "alpha.helper_b"],
			Census(source).Keys,
			"a DECLARED key-producing helper's literals are reads");
	}

	[Fact]
	public void TheCensus_ReadsAKeyHandedToAKeyCarryingHelper()
	{
		var source = "private static void DrawThing(OnlineUiContext ctx, string labelKey) => GUILayout.Label(ctx.T(labelKey));\nvar a = DrawThing(ctx, \"alpha.beta\");";
		AssertSame(["alpha.beta"], Census(source).Keys, "a key handed to a helper that resolves its labelKey is a read");
	}

	[Fact]
	public void TheCensus_IgnoresAKeyHandedToALabelHelper()
	{
		var source = "private static void DrawThing(OnlineUiContext ctx, string label) => GUILayout.Label(label);\nvar a = DrawThing(ctx, \"alpha.beta\");";
		AssertSame([], Census(source).Keys, "a helper that takes a rendered label does not make its argument a read");
	}

	[Fact]
	public void TheCensus_IgnoresAnInteriorHoleInterpolation()
	{
		var source = "var title = ctx.T($\"alpha.{kind}.title\");";
		var census = Census(source);
		AssertSame([], census.Prefixes, "a hole that does not end the literal names no key space");
		AssertSame([], census.Keys, "and it names no key of its own");
	}

	[Fact]
	public void TheCensus_IgnoresAKeyShapedArgumentThatIsNotTheKey()
	{
		var source = "var label = ctx.F(\"alpha.beta\", \"alpha.gamma\");";
		AssertSame(["alpha.beta"], Census(source).Keys, "an accessor's key is its FIRST argument; a literal in a later one is a message parameter");
	}

	[Fact]
	public void TheCensus_IgnoresAKeyInALineComment()
	{
		var source = "// ctx.T(\"alpha.beta\") was deleted here\nvar a = 1;";
		AssertSame([], Census(source).Keys, "a commented-out read is not a read");
	}

	[Fact]
	public void TheCensus_IgnoresAKeyInABlockComment()
	{
		var source = "/* ctx.T(\"alpha.beta\") */\nvar a = 1;";
		AssertSame([], Census(source).Keys, "a read hidden in a block comment is not a read");
	}

	[Fact]
	public void TheCensus_IgnoresAKeyInALogLiteral()
	{
		var source = "Logger.Info(\"alpha.beta\");";
		AssertSame([], Census(source).Keys, "a log literal naming a key is not a read");
	}

	[Fact]
	public void TheCensus_IgnoresAKeyReturnedByAnUndeclaredHelper()
	{
		var source = "private static string OtherKeyOf(Kind kind) => \"alpha.beta\";";
		AssertSame([], Census(source).Keys, "only a declared key-producing helper's literals count as reads");
	}

	[Fact]
	public void TheGate_FlagsADeclaredKeyWithNoReader()
	{
		var catalogue = SyntheticCatalogue(["alpha.read", "alpha.orphan"]);
		var source = """var a = ctx.T("alpha.read");""";
		AssertSame(["alpha.orphan"], UnreadKeys(catalogue, source), "a declared key nothing reads is an orphan");
	}

	[Fact]
	public void TheGate_FlagsAReadKeyThatIsNotDeclared()
	{
		var catalogue = SyntheticCatalogue(["alpha.read"]);
		var source = """var a = ctx.T("alpha.read"); var b = ctx.T("alpha.ghost");""";
		AssertSame(["alpha.ghost"], DanglingKeys(catalogue, source), "a read key the catalogue does not declare is a dangling reference");
	}

	private static string CatalogueText() => File.ReadAllText(Path.Combine(RepositoryPaths.Root, CatalogueRelativePath));

	private static string ProductSourceText() =>
		SourceScan.ReadAll(
			SourceScan.SourceFiles(Path.Combine(RepositoryPaths.Root, "src"))
				.Where(path => !path.EndsWith("LocalizationCatalog.cs", StringComparison.Ordinal)));

	private static string[] UnreadKeys(string catalogueText, string sourceText)
	{
		var declared = DeclaredKeySet(catalogueText);
		var census = Census(sourceText);
		return [.. declared.Where(key => !IsRead(key, census)).OrderBy(key => key, StringComparer.Ordinal)];
	}

	private static string[] DanglingKeys(string catalogueText, string sourceText)
	{
		var declared = DeclaredKeySet(catalogueText);
		return [.. Census(sourceText).Keys.Where(key => !declared.Contains(key)).OrderBy(key => key, StringComparer.Ordinal)];
	}

	private static bool IsRead(string key, (HashSet<string> Keys, HashSet<string> Prefixes, int Unresolved) census) =>
		census.Keys.Contains(key) || census.Prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal));

	private static HashSet<string> DeclaredKeySet(string catalogueText)
	{
		var (english, chinese) = DeclaredKeys(catalogueText);
		return new HashSet<string>(english.Concat(chinese), StringComparer.Ordinal);
	}

	/// <summary>The keys each table declares, in ordinal order. The anchors are asserted: a renamed
	/// table must fail here rather than silently census an empty set.</summary>
	private static (string[] English, string[] Chinese) DeclaredKeys(string catalogueText) =>
		(KeysAfter(catalogueText, EnglishAnchor), KeysAfter(catalogueText, ChineseAnchor));

	private static string[] KeysAfter(string catalogueText, string anchor)
	{
		var start = catalogueText.IndexOf(anchor, StringComparison.Ordinal);
		Assert.True(start >= 0, $"the catalogue no longer declares a table through `{anchor}`");
		var end = catalogueText.IndexOf("\n\t};", start, StringComparison.Ordinal);
		Assert.True(end > start, $"the table declared through `{anchor}` has no closing brace at one tab");
		return
		[
			.. DeclaredKeyPattern.Matches(catalogueText.Substring(start, end - start))
				.Cast<Match>()
				.Select(match => match.Groups[1].Value)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(key => key, StringComparer.Ordinal),
		];
	}

	/// <summary>
	/// Every read the product source performs: the literals inside an accessor call's arguments, the
	/// key spaces an interpolated argument opens, the literals a declared key-producing helper returns,
	/// and the literals handed to a helper that resolves its own <c>labelKey</c> parameter.
	/// <c>Unresolved</c> counts the call sites the text scan cannot resolve to a literal —
	/// localization pass-throughs such as <c>OnlineUiContext.T</c> and unrelated <c>Format</c> methods
	/// that share the accessor's name. They are echoes of a key read elsewhere and remain a recorded
	/// limit rather than a silent pass: a key only such a site can produce is reported as unread, which
	/// is the loud direction.
	/// </summary>
	private static (HashSet<string> Keys, HashSet<string> Prefixes, int Unresolved) Census(string sourceText)
	{
		var flattened = SourceScan.Flatten(sourceText);
		var keys = new HashSet<string>(StringComparer.Ordinal);
		var prefixes = new HashSet<string>(StringComparer.Ordinal);
		var unresolved = 0;
		foreach (var argument in SourceScan.MemberCallArguments(flattened, Accessors))
		{
			var keyArgument = FirstArgument(argument);
			var literals = PlainKeyLiterals(keyArgument);
			var spaces = KeyPrefixes(keyArgument);
			foreach (var key in literals)
			{
				keys.Add(key);
			}

			foreach (var prefix in spaces)
			{
				prefixes.Add(prefix);
			}

			if (literals.Length == 0 && spaces.Length == 0)
			{
				unresolved++;
			}
		}

		foreach (var helper in KeyProducingHelpers)
		{
			foreach (var key in HelperLiterals(flattened, helper.Method))
			{
				keys.Add(key);
			}
		}

		foreach (var method in KeyCarryingMethods(flattened))
		{
			foreach (var argument in SourceScan.MemberCallArguments(flattened, method))
			{
				foreach (var key in PlainKeyLiterals(argument))
				{
					keys.Add(key);
				}
			}
		}

		return (keys, prefixes, unresolved);
	}

	/// <summary>Every plain string literal in <paramref name="text"/> whose content is key-shaped.</summary>
	private static string[] PlainKeyLiterals(string text)
	{
		var keys = new List<string>();
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] != '"' || (i > 0 && text[i - 1] == '$'))
			{
				continue;
			}

			var end = SourceScan.LiteralEnd(text, i);
			var body = text.Substring(i + 1, Math.Max(0, end - i - 2));
			if (SourceScan.KeyShape.IsMatch(body))
			{
				keys.Add(body);
			}

			i = end - 1;
		}

		return [.. keys];
	}

	/// <summary>The accessor's first argument, cut at its first top-level comma: the key itself. A
	/// key-shaped literal in a later argument is a message parameter, not a key.</summary>
	private static string FirstArgument(string argument)
	{
		var depth = 0;
		for (var i = 0; i < argument.Length; i++)
		{
			var current = argument[i];
			if (current == '"' || current == '"')
			{
				i = SourceScan.LiteralEnd(argument, i) - 1;
				continue;
			}

			if (current == '(' || current == '[' || current == '{')
			{
				depth++;
				continue;
			}

			if (current == ')' || current == ']' || current == '}')
			{
				depth--;
				continue;
			}

			if (current == ',' && depth == 0)
			{
				return argument.Substring(0, i);
			}
		}

		return argument;
	}

	/// <summary>The key spaces the interpolated literals in <paramref name="text"/> open: the literal
	/// text before the first <c>{</c>, when that fragment is itself a dotted key fragment.</summary>
	private static string[] KeyPrefixes(string text)
	{
		var prefixes = new List<string>();
		for (var i = 1; i < text.Length; i++)
		{
			if (text[i] != '"' || text[i - 1] != '$')
			{
				continue;
			}

			var end = SourceScan.LiteralEnd(text, i);
			var body = text.Substring(i + 1, Math.Max(0, end - i - 2));
			var brace = body.IndexOf('{');
			var close = brace >= 0 ? body.IndexOf('}', brace) : -1;
			if (close != body.Length - 1)
			{
				// Only a hole that ENDS the literal names a key space. An interior hole would
				// otherwise open a prefix that keeps every declared key under it alive with no
				// reader, which is the silent direction.
				continue;
			}

			var prefix = body.Substring(0, brace);
			if (SourceScan.KeyPrefixShape.IsMatch(prefix))
			{
				prefixes.Add(prefix);
			}

			i = end - 1;
		}

		return [.. prefixes];
	}

	/// <summary>The key-shaped literals a helper method returns: its declaration through the closing
	/// brace at one tab.</summary>
	private static string[] HelperLiterals(string flattened, string method)
	{
		var anchor = $"string {method}(";
		var start = flattened.IndexOf(anchor, StringComparison.Ordinal);
		if (start < 0)
		{
			return [];
		}

		var end = flattened.IndexOf("\n\t}", start, StringComparison.Ordinal);
		var body = end > start ? flattened.Substring(start, end - start) : flattened.Substring(start);
		return PlainKeyLiterals(body);
	}

	/// <summary>The helpers that declare a <c>labelKey</c> parameter, in ordinal order. An accessor
	/// itself is not one of them: its own call sites are read sites already.</summary>
	private static string[] KeyCarryingMethods(string flattened) =>
	[
		.. KeyParameterPattern.Matches(flattened)
			.Cast<Match>()
			.Select(match => match.Groups[1].Value)
			.Where(method => !Accessors.Contains(method, StringComparer.Ordinal))
			.Distinct(StringComparer.Ordinal)
			.OrderBy(method => method, StringComparer.Ordinal),
	];

	/// <summary>A method's declaration through the closing brace at one tab.</summary>
	private static string MethodBody(string flattened, string method)
	{
		var start = flattened.IndexOf($" {method}(", StringComparison.Ordinal);
		if (start < 0)
		{
			return string.Empty;
		}

		var end = flattened.IndexOf("\n\t}", start, StringComparison.Ordinal);
		return end > start ? flattened.Substring(start, end - start) : flattened.Substring(start);
	}

	private static string SyntheticCatalogue(string[] keys)
	{
		var entries = string.Join("\n", keys.Select(key => $"\t\t[\"{key}\"] = \"x\","));
		return $"{EnglishAnchor}\n\t{{\n{entries}\n\t}};\n{ChineseAnchor}\n\t{{\n{entries}\n\t}};";
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

	private static void AssertSame(IEnumerable<string> expected, IEnumerable<string> actual, string because)
	{
		var expectedList = expected.OrderBy(item => item, StringComparer.Ordinal).ToArray();
		var actualList = actual.OrderBy(item => item, StringComparer.Ordinal).ToArray();
		Assert.True(
			expectedList.SequenceEqual(actualList, StringComparer.Ordinal),
			$"{because}: expected [{string.Join(", ", expectedList)}], got [{string.Join(", ", actualList)}]");
	}
}
