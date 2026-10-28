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
	private const string CatalogueRelativePath = "src/CasualtiesUnknownOnline.Runtime/Localization/en.json";
	private const string ChineseCatalogueRelativePath = "src/CasualtiesUnknownOnline.Runtime/Localization/zh.json";

	/// <summary>The measured size when the gate landed, well below it: a broken parser must fail
	/// loudly instead of reporting an empty or one-sided census.</summary>
	private const int DeclaredKeyFloor = 150;
	private const int ReadKeyFloor = 120;

	/// <summary>The accessor call sites the text scan cannot resolve (pass-throughs, echoed parameters
	/// and unrelated `Format` methods): a diagnostic bound, so a broken accessor shape fails loudly
	/// instead of reporting a census that quietly stopped seeing call sites. Raised from 60 to 63 on
	/// 2026-10-27 for the three `AddKeyedLine` sites that pass a key held in a variable (`answer.Key`,
	/// a report part's key) — the writer became an accessor that day, and those three sites are its
	/// pass-throughs, not a broken shape.</summary>
	private const int UnresolvedCeiling = 63;

	/// <summary>
	/// Every call shape that reads a catalogue key. `lookup` is the console resolver's own parameter: a console line is rendered by
	/// calling the localization service's lookup it is handed, which is the same read one indirection later, so a key reached only
	/// through it is still read by product source.
	/// </summary>
	private static readonly string[] Accessors = ["T", "F", "Format", "lookup", "AddKeyedLine"];

	/// <summary>
	/// A console answer that names its catalogue key: <c>ConsoleAnswer.Refused(sentence, "key", args…)</c> and its <c>Ok</c> sibling. The
	/// console renders that key in the reader's language, so the literal is a read of the catalogue even though it never reaches an
	/// accessor directly. Two patterns because the answer's sentence may interpolate a call of its own
	/// (<c>{string.Join(", ", Names)}</c>) and may wrap across lines: the first captures the call's whole argument text with one level of
	/// nested parentheses, the second takes the first dotted-lowercase literal inside it (a sentence is never one).
	/// </summary>
	private static readonly Regex AnswerCall = new(
		@"ConsoleAnswer\.(?:Refused|Ok)\((?:[^()]|\([^()]*\))*\)",
		RegexOptions.Compiled);

	/// <summary>
	/// A KEYED REFUSAL built where its reason is known: <c>new GrantRefusal(sentence, "key", args…)</c>. The item-creation port answers this shape
	/// (the definition table's grounds), and the console renders the key, so a literal there is a read of the catalogue like any other keyed answer.
	/// </summary>
	private static readonly Regex KeyedRefusalCall = new(
		@"new GrantRefusal\((?:[^()]|\([^()]*\))*\)",
		RegexOptions.Compiled);

	private static readonly Regex AnswerKeyLiteral = new(
		@"""([a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+)""",
		RegexOptions.Compiled);

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

	private static readonly Regex JsonKeyPattern = new(@"""([^""]+)""\s*:", RegexOptions.Compiled);

	/// <summary>One catalogue entry: the key and the sentence it holds. A value's own escapes are kept as written, because the scan
	/// below judges the text a reader would see and an escape is not a word boundary.</summary>
	private static readonly Regex JsonEntryPattern = new(@"""([^""]+)""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

	/// <summary>Characters no English sentence carries: CJK punctuation, kana, ideographs and the fullwidth forms.</summary>
	private static readonly Regex ChineseText = new(@"[\u3000-\u303F\u3040-\u30FF\u3400-\u4DBF\u4E00-\u9FFF\uFF00-\uFFEF]", RegexOptions.Compiled);

	/// <summary>
	/// A run of three or more ASCII words: an English sentence. One or two ASCII tokens inside a Chinese sentence are a command
	/// (<c>/heal</c>), an identifier (<c>AllowMemberCheatVerbs</c>) or a product name (<c>Steam</c>), which the game's own Chinese UI
	/// writes that way; three in a row is prose that was never translated.
	/// </summary>
	private static readonly Regex EnglishProse = new(@"[A-Za-z][A-Za-z'-]{1,}(?: [A-Za-z][A-Za-z'-]{1,}){2,}", RegexOptions.Compiled);

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
		("WaitLineKey", "SessionWaitText's wait-kind switch: the loading screen's line is a key the local catalogue renders, handed to the coordinator's renderer by SessionWaitText.Line"),
		("LineKey", "StartingSupplyGrantReport's per-disposition key switch: the report's line is printed by the console through AddKeyedLine, so a Chinese client reads the account in Chinese (2026-10-25)"),
		("WaitNobodyKey", "SessionWaitText's word for an empty name list, rendered by the same catalogue so a Chinese sentence never carries an English clause"),
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

	/// <summary>
	/// The two tables hold TWO LANGUAGES, and each holds only its own: an English sentence never carries a Chinese character, and a
	/// Chinese sentence never carries a run of English words. This is the user's 2026-10-25 rule ("涉及到多语言的都要改") made
	/// mechanical at the catalogue, which is where every player-facing sentence now lives: a key that was filled by pasting the host's
	/// English into the Chinese table — the exact defect that produced `使用被拒绝：injectable medicine goes through the medical view。`
	/// — fails here instead of on a player's screen.
	/// </summary>
	[Fact]
	public void TheEnglishTable_HoldsNoChineseText()
	{
		var offenders = Entries(CatalogueText())
			.Where(entry => ChineseText.IsMatch(entry.Value))
			.Select(entry => $"{entry.Key} = '{entry.Value}'")
			.ToArray();

		Assert.True(
			offenders.Length == 0,
			$"{offenders.Length} English entr(ies) carry Chinese text: {string.Join("; ", offenders)}");
	}

	/// <summary>
	/// The mirror fact, and the one that catches a pasted host sentence: a Chinese entry may name a command (`/heal`), an identifier
	/// (`AllowMemberCheatVerbs`) or a product (`Steam`) — one token — but a run of three ASCII words is an English sentence inside a
	/// Chinese frame, which is the defect regardless of how it got there.
	/// </summary>
	[Fact]
	public void TheChineseTable_HoldsNoEnglishProse()
	{
		var offenders = Entries(ChineseCatalogueText())
			.Where(entry => EnglishProse.IsMatch(entry.Value))
			.Select(entry => $"{entry.Key} = '{entry.Value}'")
			.ToArray();

		Assert.True(
			offenders.Length == 0,
			$"{offenders.Length} Chinese entr(ies) carry a run of English words: {string.Join("; ", offenders)}");
	}

	/// <summary>
	/// The two scans above are only worth their failures if they can FAIL: this pins the matchers with the case the user reported
	/// (a host sentence pasted into the Chinese table) and with the case a careless scan would reject (a legitimate command name and
	/// identifier inside a Chinese sentence), so a matcher that stopped matching — or one that started matching everything — is red.
	/// </summary>
	[Fact]
	public void TheLanguageScan_RejectsAPastedHostSentenceAndAcceptsAToken()
	{
		const string Pasted = "{ \"hud.use_refused\": \"使用被拒绝：injectable medicine goes through the medical view。\" }";
		const string Legitimate = "{ \"hud.use_refused\": \"使用被拒绝：这条命令（AllowMemberCheatVerbs）不接受 /heal。\" }";
		const string ChineseInEnglish = "{ \"hud.ok\": \"已治疗 {0}。\" }";

		Assert.Contains(Entries(Pasted), entry => EnglishProse.IsMatch(entry.Value));
		Assert.DoesNotContain(Entries(Legitimate), entry => EnglishProse.IsMatch(entry.Value));
		Assert.Contains(Entries(ChineseInEnglish), entry => ChineseText.IsMatch(entry.Value));
		// The English scan must NOT reject a Chinese table: it is the English one that may hold no Chinese, and the sample above is
		// the Chinese sentence a command name and an identifier legitimately sit inside.
		Assert.Contains(Entries(Legitimate), entry => ChineseText.IsMatch(entry.Value));
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
			["alpha.color.|"],
			census.Spaces.Select(space => space.Prefix + "|" + space.Suffix),
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
	public void TheCensus_ReadsAMiddleHoleAsAPrefixAndSuffixSpace()
	{
		var source = "var title = ctx.T($\"alpha.{kind}.title\");";
		var census = Census(source);
		AssertSame(
			["alpha.|.title"],
			census.Spaces.Select(space => space.Prefix + "|" + space.Suffix),
			"a hole in the MIDDLE names a space with a suffix, which is what a per-name entry family looks like");
		AssertSame([], census.Keys, "and it names no key of its own");
	}

	[Fact]
	public void TheCensus_ReadsNoSpaceFromAHoleThatSplicesLetters()
	{
		var source = "var title = ctx.T($\"alpha{kind}title\");";
		var census = Census(source);
		AssertSame([], census.Spaces.Select(space => space.Prefix + "|" + space.Suffix), "a hole between letters names no space");
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
		AssertSame(["alpha.orphan"], UnreadKeys(catalogue, source, catalogue), "a declared key nothing reads is an orphan");
	}

	[Fact]
	public void TheGate_FlagsAReadKeyThatIsNotDeclared()
	{
		var catalogue = SyntheticCatalogue(["alpha.read"]);
		var source = """var a = ctx.T("alpha.read"); var b = ctx.T("alpha.ghost");""";
		AssertSame(["alpha.ghost"], DanglingKeys(catalogue, source, catalogue), "a read key the catalogue does not declare is a dangling reference");
	}

	private static string CatalogueText() => File.ReadAllText(Path.Combine(RepositoryPaths.Root, CatalogueRelativePath));

	private static string ChineseCatalogueText() => File.ReadAllText(Path.Combine(RepositoryPaths.Root, ChineseCatalogueRelativePath));

	private static string ProductSourceText() =>
		SourceScan.ReadAll(
			SourceScan.SourceFiles(Path.Combine(RepositoryPaths.Root, "src"))
				.Where(path => !path.EndsWith("LocalizationCatalog.cs", StringComparison.Ordinal)));

	private static string[] UnreadKeys(string catalogueText, string sourceText, string? chineseText = null)
	{
		var declared = DeclaredKeySet(catalogueText, chineseText);
		var census = Census(sourceText);
		return [.. declared.Where(key => !IsRead(key, census)).OrderBy(key => key, StringComparer.Ordinal)];
	}

	private static string[] DanglingKeys(string catalogueText, string sourceText, string? chineseText = null)
	{
		var declared = DeclaredKeySet(catalogueText, chineseText);
		return [.. Census(sourceText).Keys.Where(key => !declared.Contains(key)).OrderBy(key => key, StringComparer.Ordinal)];
	}

	/// <summary>
	/// Whether a declared key is read: named outright, or covered by a key SPACE — the prefix matches and the suffix (when the space has one)
	/// ends the key. A space with a prefix only is the older shape (<c>$"prefs.color.{name}"</c>); the suffix form is the one a per-name entry
	/// family needs (<c>console.cmd.&lt;name&gt;.desc</c>).
	/// </summary>
	private static bool IsRead(string key, (HashSet<string> Keys, List<(string Prefix, string Suffix)> Spaces, int Unresolved) census) =>
		census.Keys.Contains(key)
		|| census.Spaces.Any(space => key.StartsWith(space.Prefix, StringComparison.Ordinal)
			&& (space.Suffix.Length == 0 || key.EndsWith(space.Suffix, StringComparison.Ordinal)));

	private static HashSet<string> DeclaredKeySet(string catalogueText, string? chineseText = null)
	{
		var (english, chinese) = DeclaredKeys(catalogueText, chineseText);
		return new HashSet<string>(english.Concat(chinese), StringComparer.Ordinal);
	}

	/// <summary>The keys each table declares, in ordinal order. Both texts are passed in so a self-test can drive the same
	/// parser with a synthetic pair; the real pair comes from the two catalogue files.</summary>
	private static (string[] English, string[] Chinese) DeclaredKeys(string englishJson, string? chineseJson = null) =>
		(KeysIn(englishJson), KeysIn(chineseJson ?? ChineseCatalogueText()));

	/// <summary>Every key of one flat catalogue file: a quoted property name followed by a colon.</summary>
	private static string[] KeysIn(string json) =>
	[
		.. JsonKeyPattern.Matches(json)
			.Cast<Match>()
			.Select(match => match.Groups[1].Value)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(key => key, StringComparer.Ordinal),
	];

	/// <summary>Every key of one flat catalogue file with the sentence it holds: the language scan judges the sentence, and reports
	/// the key so a failure names the line a translator has to fix.</summary>
	private static (string Key, string Value)[] Entries(string json) =>
	[
		.. JsonEntryPattern.Matches(json)
			.Cast<Match>()
			.Select(match => (Key: match.Groups[1].Value, Value: match.Groups[2].Value))
			.OrderBy(entry => entry.Key, StringComparer.Ordinal),
	];

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
	private static (HashSet<string> Keys, List<(string Prefix, string Suffix)> Spaces, int Unresolved) Census(string sourceText)
	{
		var flattened = SourceScan.Flatten(sourceText);
		var keys = new HashSet<string>(StringComparer.Ordinal);
		var spaces = new List<(string Prefix, string Suffix)>();
		var unresolved = 0;
		foreach (var argument in SourceScan.MemberCallArguments(flattened, Accessors))
		{
			var keyArgument = FirstArgument(argument);
			var literals = PlainKeyLiterals(keyArgument);
			var keySpaces = KeySpaces(keyArgument);
			foreach (var key in literals)
			{
				keys.Add(key);
			}

			spaces.AddRange(keySpaces);

			if (literals.Length == 0 && keySpaces.Length == 0 && !CarriesADeclaredKey(keyArgument))
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

		// A console answer carries its key as the SECOND argument when its sentence comes from a closed set
		// (`ConsoleAnswer.Refused(sentence, "key", args…)`): the first argument stays the sentence for the log and the fallback,
		// and the console renders the key in the reader's language. Without this pass every converted answer would read as an
		// unread key, which is the opposite of the truth.
		// A console answer carries its key when its sentence comes from a closed set: `ConsoleAnswer.Refused(sentence, "key", args…)`. The
		// sentence stays for the log and the fallback, and the console renders the key in the reader's language. Read as a pattern over the
		// source rather than through the argument splitter, because a sentence may interpolate a string of its own
		// (`{string.Join(", ", Names)}`), which makes the balanced split see a stray comma.
		foreach (Match answer in AnswerCall.Matches(flattened))
		{
			var key = AnswerKeyLiteral.Match(answer.Value);
			if (key.Success)
			{
				keys.Add(key.Groups[1].Value);
			}
		}

		// The same read one layer earlier: a KEYED REFUSAL built where its reason is known (`new GrantRefusal(sentence, "key", args…)`), which the
		// item-creation port answers and the console renders.
		foreach (Match refusal in KeyedRefusalCall.Matches(flattened))
		{
			var key = AnswerKeyLiteral.Match(refusal.Value);
			if (key.Success)
			{
				keys.Add(key.Groups[1].Value);
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

		return (keys, spaces, unresolved);
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

	/// <summary>
	/// True when the accessor's key argument is one the census already counted at its PRODUCER: a stored refusal's
	/// <c>.Key</c> (a key that travelled as data and was declared where it was made) or the <c>labelKey</c> parameter of a
	/// key-carrying helper (whose call sites' literals are counted). Without this, carrying keys as values would read as an
	/// unresolvable call site, which is the opposite of what the census is for.
	/// </summary>
	private static bool CarriesADeclaredKey(string keyArgument)
	{
		var trimmed = keyArgument.Trim();
		return trimmed.EndsWith(".Key", StringComparison.Ordinal)
			|| string.Equals(trimmed, KeyParameterName, StringComparison.Ordinal);
	}

	/// <summary>The accessor's first argument, cut at its first top-level comma: the key itself. A
	/// key-shaped literal in a later argument is a message parameter, not a key.</summary>
	private static string FirstArgument(string argument)
	{
		var depth = 0; for (var i = 0; i < argument.Length; i++)
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

	/// <summary>
	/// One top-level argument of a call's argument text, counting from zero: the same balanced split <see cref="FirstArgument"/>
	/// performs, continued past the commas before the wanted one. Returns empty when the call has fewer arguments.
	/// </summary>
	private static string ArgumentAt(string argument, int index)
	{
		var depth = 0;
		var seen = 0;
		var start = 0;
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

			if (current != ',' || depth != 0)
			{
				continue;
			}

			if (seen == index)
			{
				return argument.Substring(start, i - start);
			}

			seen++;
			start = i + 1;
		}

		return seen == index ? argument.Substring(start) : "";
	}

	/// <summary>
	/// The key spaces the interpolated literals in <paramref name="text"/> open — the literal text around its first hole, when both halves are
	/// dotted key fragments. A hole at the END names a space by its PREFIX (<c>$"prefs.color.{name}"</c>); a hole in the MIDDLE names the same kind
	/// of space with a SUFFIX (<c>$"console.cmd.{name}.desc"</c>), which is what a rule about a key family looks like when the family is a
	/// per-name entry rather than a per-name prefix. Both are a family with one reader; a hole that neither starts nor ends a dotted fragment is
	/// opaque and opens nothing.
	/// </summary>
	private static (string Prefix, string Suffix)[] KeySpaces(string text)
	{
		var spaces = new List<(string Prefix, string Suffix)>();
		for (var i = 1; i < text.Length; i++)
		{
			if (text[i] != '"' || text[i - 1] != '$')
			{
				continue;
			}

			var end = SourceScan.LiteralEnd(text, i);
			var body = text.Substring(i + 1, Math.Max(0, end - i - 2));
			var brace = body.IndexOf('{');
			if (brace < 0)
			{
				i = end - 1;
				continue;
			}

			var close = body.IndexOf('}', brace);
			var prefix = body.Substring(0, brace);
			var suffix = close >= 0 && close < body.Length - 1 ? body.Substring(close + 1) : "";
			var closed = close >= 0;

			// The suffix must begin at a dot, so the two halves cannot be spliced into a key of a different family.
			if (closed && SourceScan.KeyPrefixShape.IsMatch(prefix) && (suffix.Length == 0 || suffix.StartsWith(".", StringComparison.Ordinal)))
			{
				spaces.Add((prefix, suffix));
			}

			i = end - 1;
		}

		return [.. spaces];
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
		// The catalogue is JSON now: a synthetic table is one flat object, so the parser pins are exercised against the same
		// shape a translator edits.
		var entries = string.Join(",\n", keys.Select(key => $"  \"{key}\": \"x\""));
		return $"{{\n{entries}\n}}";
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
