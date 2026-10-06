using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The rules about the acceptance tools, gated rather than trusted: the in-process template and every
/// scenario recipe must stay inside the language the evaluator compiles (Mono.CSharp, C# 7.x — a newer
/// construct fails at run time, in the one place the run cannot recompile around); every recipe must
/// declare exactly the arguments it uses, so the run's -RecipeArg is never guessed; nothing under
/// tools/acceptance/ may reach for OS-level input (the user's boundary: the agent drives from inside the
/// process, never the mouse and keyboard), with the single exception of the eval declarations under
/// driver/eval-declarations/ — a key held for the game's own gesture is queued on the client's OWN window,
/// and those four read/post names are excused nowhere else; and those declarations must carry the name
/// the driver probes for and stay compilable at the evaluator's language version. Each check pins its own
/// matcher with positive and negative samples, so a rule that stopped matching fails here instead of
/// passing by checking nothing.
/// </summary>
public class AcceptanceDriverGateTests
{
	private const string TemplatePath = "tools/acceptance/driver/InProcessDriver.cs";
	private const string DriverPath = "tools/acceptance/drive-in-process.ps1";

	/// <summary>The OS-input families the ban must cover: actuation, cursor and key state, focus and hook
	/// manipulation, window messages, and the managed clipboard/cursor wrappers. The scan is capability
	/// shaped on purpose — a shortlist of famous API names is what let the first cut through.</summary>
	private static readonly string[] OsInputApis =
	[
		"SendKeys",
		"SendInput",
		"keybd_event",
		"mouse_event",
		"SetCursorPos",
		"GetCursorPos",
		"GetAsyncKeyState",
		"GetKeyboardState",
		"BlockInput",
		"AttachThreadInput",
		"SetForegroundWindow",
		"SetFocus",
		"SetWindowsHookEx",
		"SendMessage",
		"PostMessage",
		"PostThreadMessage",
		"SendNotifyMessage",
		"Clipboard",
		"Cursor]::Position",
		"Cursor.Position"
	];

	/// <summary>Every actuation family a future helper could reach for; each sample must be caught by at
	/// least one token, so a ban list that shrank back to a shortlist fails here.</summary>
	private static readonly string[] OsInputSamples =
	[
		"using System.Windows.Forms; SendKeys.SendWait(\"x\");",
		"[DllImport(\"user32.dll\")] static extern uint SendInput(uint count, INPUT[] inputs, int size);",
		"[DllImport(\"user32.dll\")] static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);",
		"[DllImport(\"user32.dll\")] static extern void mouse_event(uint flags, uint x, uint y, uint data, int extra);",
		"[DllImport(\"user32.dll\")] static extern bool SetCursorPos(int x, int y);",
		"[DllImport(\"user32.dll\")] static extern bool GetCursorPos(out POINT point);",
		"[DllImport(\"user32.dll\")] static extern short GetAsyncKeyState(int key);",
		"[DllImport(\"user32.dll\")] static extern bool BlockInput(bool block);",
		"[DllImport(\"user32.dll\")] static extern bool AttachThreadInput(uint a, uint b, bool attach);",
		"[DllImport(\"user32.dll\")] static extern bool SetForegroundWindow(IntPtr window);",
		"[DllImport(\"user32.dll\")] static extern bool SetWindowsHookEx(int id, IntPtr proc, IntPtr module, uint thread);",
		"SendMessage(hWnd, 0x0100, 0, 0);",
		"PostMessage(hWnd, 0x0201, 0, 0);",
		"PostThreadMessage(threadId, 0x0100, 0, 0);",
		"SendNotifyMessage(hWnd, 0x0100, 0, 0);",
		"[System.Windows.Forms.Cursor]::Position = new System.Drawing.Point(0, 0);",
		"var point = System.Windows.Forms.Cursor.Position;",
		"var text = System.Windows.Forms.Clipboard.GetText();"
	];

	/// <summary>The one directory whose files may name an OS-input API, and the exact names they may carry
	/// there. A run holds a key the game's own gesture needs by queueing a message on the client's OWN
	/// window and reading the OS key state back to show the hold never left the client — so the window
	/// message and the key-state read, in that one directory, are the whole exception. Every other family
	/// — actuation, cursor, focus-setting, hooks, clipboard, `SendMessage` — stays banned everywhere, that
	/// directory included; a name the ban does not list needs no exception at all.</summary>
	private const string DeclarationDirectory = "tools/acceptance/driver/eval-declarations";

	private static readonly string[] DeclarationOnlyApis = ["PostMessage", "GetAsyncKeyState"];

	/// <summary>The CUO and Unity references an offline compile cannot resolve: the shared diagnostics are
	/// pinned by census, so a typo, a wrong argument count or a duplicate declaration — each of which
	/// changes this number — fails here instead of passing as "both versions agree". Update it deliberately
	/// when the template legitimately gains a game or plugin reference (19 since the save batch's
	/// continue/leave/console verbs added two `CommandConsoleService` references).</summary>
	private const int UnresolvedReferenceCensus = 19;

	private static readonly string[] UnresolvedReferenceIds = ["CS0246", "CS0103", "CS0234"];

	/// <summary>The BCL set a snippet is compiled against. CUO and Unity types stay out on purpose:
	/// every snippet carries the same unknown-type errors at both language versions, so the version
	/// comparison cancels them and only a language-version difference survives.</summary>
	private static readonly IReadOnlyList<MetadataReference> PlatformReferences = BuildPlatformReferences();

	[Fact]
	public void TheInProcessTemplateIsCsharp7()
	{
		var template = RepositoryPaths.ReadText(TemplatePath);
		Assert.True(template.Length > 2000, $"the template is only {template.Length} characters; the check would pass by checking nothing");
		Assert.Contains("{{COMMAND}}", template, StringComparison.Ordinal);
		Assert.Contains("{{ARGUMENT}}", template, StringComparison.Ordinal);
		Assert.Contains("{{TEXT}}", template, StringComparison.Ordinal);

		var substituted = template
			.Replace("\"{{COMMAND}}\"", "\"state\"", StringComparison.Ordinal)
			.Replace("\"{{ARGUMENT}}\"", "\"\"", StringComparison.Ordinal)
			.Replace("\"{{TEXT}}\"", "\"\"", StringComparison.Ordinal);
		Assert.False(substituted.Contains("{{", StringComparison.Ordinal), "a placeholder is missing its quoted substitution site");

		// The evaluator wraps the snippet in a method body, so parsing it the same way is the honest
		// check: a bare expression is not a legal compilation unit at all.
		var source = "public static class __Probe { public static object Run() { var result = " + substituted + ";\nreturn result; } }";

		var syntax = SyntaxErrors(source);
		Assert.True(syntax.Length == 0, "the in-process template is not valid C#:" + Environment.NewLine + string.Join(Environment.NewLine, syntax.Select(diagnostic => diagnostic.ToString())));

		// The errors both versions share are the CUO/Unity references this offline compile cannot resolve.
		// Their census is pinned, so a template that picked up a typo, a wrong argument count or a duplicate
		// declaration is refused here instead of hiding behind the version comparison.
		var shared = SharedErrors(source);
		Assert.True(shared.Length == UnresolvedReferenceCensus, $"the offline compile reported {shared.Length} shared error(s), not the pinned {UnresolvedReferenceCensus}; an ordinary template error changes this census — fix the template, or update the census deliberately if the template gained a new game/plugin reference");
		var unexpected = shared.Where(diagnostic => !UnresolvedReferenceIds.Contains(diagnostic.Id, StringComparer.Ordinal)).ToArray();
		Assert.True(unexpected.Length == 0, "the shared errors must all be unresolved CUO/Unity references:" + Environment.NewLine + string.Join(Environment.NewLine, unexpected.Select(diagnostic => diagnostic.ToString())));

		var versionOnly = VersionOnlyErrors(source);
		Assert.True(versionOnly.Length == 0, "the in-process template uses a construct the evaluator's C# 7 language version cannot take:" + Environment.NewLine + string.Join(Environment.NewLine, versionOnly.Select(diagnostic => diagnostic.ToString())));

		// The version check's own teeth: a construct the newer compilers accept must appear as a
		// 7.3-only error, or the comparison above would pass by comparing nothing.
		Assert.NotEmpty(VersionOnlyErrors("public static class __Probe { public static int Run() { var value = 1; return value switch { 1 => 2, _ => 3 }; } }"));

		// The census check's own teeth: an ordinary error in the template's own code changes it.
		var broken = source.Replace("finish(fields)", "finish(fields, escape)", StringComparison.Ordinal);
		Assert.NotEqual(UnresolvedReferenceCensus, SharedErrors(broken).Length);
	}

	[Fact]
	public void TheAcceptanceToolsNeverUseOsLevelInput()
	{
		var directory = RepositoryPaths.File("tools/acceptance");
		var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToArray();

		// A census floor: the scan must see the script and the template it sends.
		Assert.True(files.Length >= 2, $"tools/acceptance/ held only {files.Length} file(s); the check would pass by scanning nothing");
		Assert.Contains(files, file => file.EndsWith("drive-in-process.ps1", StringComparison.Ordinal));
		Assert.Contains(files, file => file.EndsWith("InProcessDriver.cs", StringComparison.Ordinal));

		var failures = new List<string>();
		var declarations = 0;
		foreach (var file in files)
		{
			var relative = Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/');
			if (IsDeclarationDirectory(relative)) { declarations++; }
			var text = File.ReadAllText(file);
			foreach (var token in OsInputApis)
			{
				if (!text.Contains(token, StringComparison.Ordinal)) { continue; }
				if (IsDeclarationException(relative, token)) { continue; }
				failures.Add($"{relative}: '{token}' is an OS-level input API; an acceptance run drives in process only");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
		// The exception must have files to cover: the probe and at least one declaration live there, and a
		// directory that shrank to nothing fails here instead of widening what the scan skips.
		Assert.True(declarations >= 2, $"the declaration exception covers {declarations} file(s) under {DeclarationDirectory}; the probe and every declaration live there");

		// The matcher's own teeth: every input family must be covered, a real call must be caught, and the
		// rule's own sentence must stay clean.
		foreach (var sample in OsInputSamples)
		{
			Assert.True(OsInputApis.Any(token => sample.Contains(token, StringComparison.Ordinal)), $"the banned list no longer covers an OS-input family: {sample}");
		}

		Assert.True(OsInputApis.Any(token => "using System.Windows.Forms; SendKeys.SendWait(\"x\");".Contains(token, StringComparison.Ordinal)), "the banned list must catch a real OS-input call");
		Assert.False(OsInputApis.Any(token => "the driver acts in process and takes no OS input".Contains(token, StringComparison.Ordinal)), "the banned list must not fire on the rule's own sentence");

		// The exception's own teeth, on the same predicate the scan uses: each excused name passes inside
		// the declaration directory and nowhere else, a name the ban does not list is never excused, and a
		// message or focus family outside the two is refused even there.
		foreach (var name in DeclarationOnlyApis)
		{
			Assert.True(OsInputApis.Contains(name, StringComparer.Ordinal), $"the exception may only excuse a name the ban itself lists: {name}");
			Assert.True(IsDeclarationException(DeclarationDirectory + "/window-key.cs", name), $"the exception must cover '{name}' in a declaration file");
			Assert.False(IsDeclarationException("tools/acceptance/recipes/key-hold.cs", name), $"the exception must not cover '{name}' outside the declaration directory");
		}

		Assert.False(IsDeclarationException(DeclarationDirectory + "/window-key.cs", "SendMessage"), "a declaration may not reach for a message API the exception does not name");
		Assert.False(IsDeclarationException(DeclarationDirectory + "/window-key.cs", "SetForegroundWindow"), "a declaration may read the foreground window but never set it");
		Assert.False(IsDeclarationException("tools/acceptance/drive-in-process.ps1", "PostMessage"), "the driver script is not a declaration file");
	}

	[Fact]
	public void TheEvalDeclarationsCarryTheirNameAndCompileAtTheEvaluatorsLanguage()
	{
		var directory = RepositoryPaths.File(DeclarationDirectory);
		var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).OrderBy(file => file, StringComparer.Ordinal).ToArray();

		// The probe plus at least one declaration: the census floor keeps a gutted directory from passing.
		Assert.True(files.Length >= 2, $"{DeclarationDirectory} held only {files.Length} file(s); the probe and every declaration live there");

		var declaredFiles = new List<string>();
		foreach (var file in files)
		{
			var stem = Path.GetFileNameWithoutExtension(file);
			var relative = Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/');
			var text = File.ReadAllText(file);
			if (stem == "check-declared")
			{
				// The probe is the one committed expression the driver substitutes a type name into, and it
				// has to stay a single trailing expression at the evaluator's own language version.
				Assert.Single(Regex.Matches(text, Regex.Escape("{{s:type}}")));
				var substituted = text.Replace("{{s:type}}", "\"x\"", StringComparison.Ordinal);
				var probeProblems = ExpressionProblems(substituted);
				Assert.True(probeProblems.Length == 0, $"{relative}:{Environment.NewLine}{string.Join(Environment.NewLine, probeProblems)}");
				continue;
			}

			// A declaration is a whole input of its own: the driver's presence probe asks for the type its
			// `// declares:` line names, so a declaration without one has nothing to ask about.
			var declares = Regex.Match(text, @"^// declares: (?<name>[A-Za-z_][A-Za-z0-9_.]*)[ \t]*\r?$", RegexOptions.Multiline);
			Assert.True(declares.Success, $"{relative} carries no '// declares: <type>' line; the driver's presence probe would have nothing to ask about");
			var declared = declares.Groups["name"].Value;
			Assert.Contains("class " + declared, text, StringComparison.Ordinal);
			var unitProblems = UnitProblems(text);
			Assert.True(unitProblems.Length == 0, $"{relative}:{Environment.NewLine}{string.Join(Environment.NewLine, unitProblems)}");
			declaredFiles.Add(stem);
		}

		Assert.NotEmpty(declaredFiles);

		// A recipe that names a declaration must name one that exists: a typo would otherwise surface only
		// in a live client, as an unresolved type inside the evaluator. The matcher is pinned and the
		// recipes that name one are counted, so a `// requires:` line that stopped being seen fails here
		// instead of turning the loop into a no-op.
		Assert.Equal("window-key", RequiredDeclaration("// recipe: sample\n// requires: window-key - the declaration it calls\ncode"));
		Assert.Equal("window-key", RequiredDeclaration("// recipe: sample\r\n// requires: window-key\r\ncode"));
		Assert.Null(RequiredDeclaration("// recipe: sample\n// args: none\ncode"));
		Assert.Null(RequiredDeclaration("// recipe: sample\n//   requires: window-key\ncode"));
		Assert.Null(RequiredDeclaration("// recipe: sample\n// Requires: window-key\ncode"));

		var recipes = Directory.EnumerateFiles(RepositoryPaths.File("tools/acceptance/recipes"), "*.cs", SearchOption.TopDirectoryOnly).OrderBy(file => file, StringComparer.Ordinal).ToArray();
		var requiring = recipes.Where(recipe => RequiredDeclaration(File.ReadAllText(recipe)) is not null).ToArray();
		Assert.NotEmpty(requiring);
		foreach (var recipe in requiring)
		{
			var name = RequiredDeclaration(File.ReadAllText(recipe))!;
			Assert.True(declaredFiles.Contains(name, StringComparer.Ordinal), $"{Path.GetFileName(recipe)} requires the '{name}' eval declaration, which does not exist under {DeclarationDirectory} (declared: {string.Join(", ", declaredFiles)})");
		}
	}

	[Fact]
	public void TheDriverActsThroughTheOnlineUiControls()
	{
		var script = RepositoryPaths.ReadText(DriverPath);
		var template = RepositoryPaths.ReadText(TemplatePath);

		// The scenario vocabulary is the Online UI's own control ids and the native start entry: the
		// driver must not fall back to calling Steam directly, which is the policy bypass this whole
		// capability exists to avoid.
		foreach (var control in new[] { "home.create_lobby", "home.lobby_id", "home.join", "tab.home" })
		{
			Assert.Contains(control, script, StringComparison.Ordinal);
		}

		Assert.Contains("Runtime.OnlineUi.OnlineUiIntent", template, StringComparison.Ordinal);
		Assert.Contains("ControlInvoked", template, StringComparison.Ordinal);
		Assert.Contains("ControlEdited", template, StringComparison.Ordinal);
		Assert.Contains("PreRunScript", template, StringComparison.Ordinal);
		Assert.False(template.Contains("SteamService.CreateLobby", StringComparison.Ordinal), "the driver must not create a lobby behind the Online UI's policy");
		Assert.False(template.Contains("SteamService.JoinLobby", StringComparison.Ordinal), "the driver must not join a lobby behind the Online UI's policy");
	}

	[Fact]
	public void TheRecipesStayInTheEvaluatorsLanguageAndDeclareTheirArguments()
	{
		var directory = RepositoryPaths.File("tools/acceptance/recipes");
		var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
			.OrderBy(file => file, StringComparer.Ordinal)
			.ToArray();

		Assert.True(files.Length >= 6, $"tools/acceptance/recipes/ held only {files.Length} recipe(s); the census floor would let a gutted directory pass");

		foreach (var file in files)
		{
			var name = Path.GetFileNameWithoutExtension(file);
			var problems = RecipeProblems(name, File.ReadAllText(file));
			Assert.True(problems.Length == 0, $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
		}

		// The matcher's own teeth: an undeclared placeholder, a declared argument that is never used, a
		// brace form that is not {{s:key}}/{{n:key}} and a construct newer than the evaluator's language
		// version each have to fail, or the scan above passes by checking nothing.
		Assert.NotEmpty(RecipeProblems("sample", "// recipe: sample\n// args: a=s\n((System.Func<string>)(() => {{s:b}}))()"));
		Assert.NotEmpty(RecipeProblems("sample", "// recipe: sample\n// args: a=s b=n\n((System.Func<string>)(() => {{s:a}}))()"));
		Assert.NotEmpty(RecipeProblems("sample", "// recipe: sample\n// args: none\n((System.Func<string>)(() => \"{{t:x}}\"))()"));
		Assert.NotEmpty(RecipeProblems("sample", "// recipe: sample\n// args: none\n((System.Func<int>)(() => 1 switch { 1 => 2, _ => 3 }))()"));
		Assert.Empty(RecipeProblems("sample", "// recipe: sample\n// args: a=s b=n\n((System.Func<string>)(() => {{s:a}} + {{n:b}}))()"));
		// A CRLF checkout reads the declaration the same as LF: the ending is not an argument, an
		// invalid kind must still be reported, and a `none` declaration must still be accepted.
		Assert.Empty(RecipeProblems("sample", "// recipe: sample\r\n// args: a=s b=n\r\n((System.Func<string>)(() => {{s:a}} + {{n:b}}))()\r\n"));
		Assert.Empty(RecipeProblems("sample", "// recipe: sample\r\n// args: a=s b=n\r\n((System.Func<string>)(() => {{s:a}} + {{n:b}}))()"));
		Assert.NotEmpty(RecipeProblems("sample", "// recipe: sample\r\n// args: a=x\r\n((System.Func<string>)(() => {{s:a}}))()\r\n"));
		Assert.Empty(RecipeProblems("sample", "// recipe: sample\r\n// args: none\r\n((System.Func<string>)(() => \"x\"))()\r\n"));
	}

	/// <summary>
	/// The problems a committed recipe would present to the evaluator, or none. Split out so the gate's
	/// own matcher is pinned with positive and negative samples instead of trusting the directory scan.
	/// </summary>
	private static string[] RecipeProblems(string name, string text)
	{
		var problems = new List<string>();
		if (!text.Contains($"// recipe: {name}", StringComparison.Ordinal))
		{
			problems.Add($"the recipe does not declare itself with '// recipe: {name}'");
		}

		var declared = new HashSet<string>(StringComparer.Ordinal);
		// The capture stops at the line terminator: `.gitattributes` declares CRLF checkouts, and on
		// one of those `$` matches before the `\n`, so `.*` would keep the `\r` inside the last
		// argument; a line ending is not part of the declaration.
		var argsLine = Regex.Match(text, @"^// args: (?<args>[^\r\n]*)", RegexOptions.Multiline);
		if (!argsLine.Success)
		{
			problems.Add("the recipe carries no '// args:' line");
		}
		else if (argsLine.Groups["args"].Value.Trim() != "none")
		{
			foreach (var part in argsLine.Groups["args"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				var split = part.Split('=');
				if (split.Length != 2 || (split[1] != "s" && split[1] != "n"))
				{
					problems.Add($"the declared argument '{part}' is not <key>=s or <key>=n");
				}
				else if (!declared.Add(split[0]))
				{
					problems.Add($"the argument '{split[0]}' is declared twice");
				}
			}
		}

		var used = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (Match match in Regex.Matches(text, @"\{\{(?<kind>[sn]):(?<key>[A-Za-z0-9-]+)\}\}"))
		{
			var key = match.Groups["key"].Value;
			used[key] = used.TryGetValue(key, out var count) ? count + 1 : 1;
			if (!declared.Contains(key))
			{
				problems.Add($"the placeholder '{key}' is not declared in '// args:'");
			}
		}

		foreach (var key in declared)
		{
			if (!used.TryGetValue(key, out var count))
			{
				problems.Add($"the declared argument '{key}' has no placeholder");
			}
			else if (count != 1)
			{
				problems.Add($"the placeholder '{key}' appears {count} times; one value is substituted once");
			}
		}

		var substituted = Regex.Replace(text, @"\{\{[sn]:[A-Za-z0-9-]+\}\}", match => match.Value.StartsWith("{{s:", StringComparison.Ordinal) ? "\"x\"" : "0");
		if (substituted.Contains("{{", StringComparison.Ordinal))
		{
			problems.Add("a brace form other than {{s:key}} / {{n:key}} survived");
		}

		// The recipes are expressions; the evaluator wraps them in a method body, so parsing the same way
		// is the honest check (a bare expression is not a compilation unit).
		var source = "public static class __Probe { public static object Run() { var result = " + substituted + ";\nreturn result; } }";
		var syntax = SyntaxErrors(source);
		if (syntax.Length != 0)
		{
			problems.Add("the recipe is not C# 7.x syntax: " + string.Join("; ", syntax.Select(diagnostic => diagnostic.ToString())));
		}

		var versionOnly = VersionOnlyErrors(source);
		if (versionOnly.Length != 0)
		{
			problems.Add("the recipe uses a construct the evaluator's language version cannot take: " + string.Join("; ", versionOnly.Select(diagnostic => diagnostic.ToString())));
		}

		var unexpected = SharedErrors(source).Where(diagnostic => !UnresolvedReferenceIds.Contains(diagnostic.Id, StringComparer.Ordinal)).ToArray();
		if (unexpected.Length != 0)
		{
			problems.Add("the recipe has errors beyond unresolved game/CUO types: " + string.Join("; ", unexpected.Select(diagnostic => diagnostic.ToString())));
		}

		return [.. problems];
	}

	/// <summary>The eval declaration a recipe says it needs, or null. A recipe names one on its own
	/// `// requires:` line at the start of the line, which is the name the run passes to
	/// `-Action declare`.</summary>
	private static string? RequiredDeclaration(string recipeText)
	{
		var match = Regex.Match(recipeText, @"^// requires: (?<name>[a-z0-9-]+)", RegexOptions.Multiline);
		return match.Success ? match.Groups["name"].Value : null;
	}

	/// <summary>True when a repository-relative path is inside the one directory the window-message
	/// exception covers.</summary>
	private static bool IsDeclarationDirectory(string relativePath) =>
		relativePath.StartsWith(DeclarationDirectory + "/", StringComparison.Ordinal);

	/// <summary>The exception itself: a file in that directory may name the four read/post APIs, and no
	/// other name on the ban list is ever excused, there or anywhere else.</summary>
	private static bool IsDeclarationException(string relativePath, string token) =>
		IsDeclarationDirectory(relativePath) && DeclarationOnlyApis.Contains(token, StringComparer.Ordinal);

	/// <summary>The problems a whole eval-declaration input would present to the evaluator, or none. It is
	/// a compilation unit of its own — the evaluator refuses a declaration with a trailing expression —
	/// so it is parsed and compiled the same way.</summary>
	private static string[] UnitProblems(string source)
	{
		var problems = new List<string>();
		var unexpected = CompilationErrors(source, LanguageVersion.CSharp7_3)
			.Where(diagnostic => !UnresolvedReferenceIds.Contains(diagnostic.Id, StringComparer.Ordinal))
			.ToArray();
		if (unexpected.Length != 0)
		{
			problems.Add("the declaration does not compile at the evaluator's language version: " + string.Join("; ", unexpected.Select(diagnostic => diagnostic.ToString())));
		}

		var versionOnly = VersionOnlyErrors(source);
		if (versionOnly.Length != 0)
		{
			problems.Add("the declaration uses a construct the evaluator's language version cannot take: " + string.Join("; ", versionOnly.Select(diagnostic => diagnostic.ToString())));
		}

		return [.. problems];
	}

	/// <summary>The problems the presence probe would present to the evaluator, or none — the same shape a
	/// recipe is checked in, because both are one trailing expression in a method body.</summary>
	private static string[] ExpressionProblems(string expression)
	{
		var source = "public static class __Probe { public static object Run() { var result = " + expression + ";\nreturn result; } }";
		var problems = new List<string>();
		var syntax = SyntaxErrors(source);
		if (syntax.Length != 0)
		{
			problems.Add("the probe is not C# 7.x syntax: " + string.Join("; ", syntax.Select(diagnostic => diagnostic.ToString())));
		}

		var versionOnly = VersionOnlyErrors(source);
		if (versionOnly.Length != 0)
		{
			problems.Add("the probe uses a construct the evaluator's language version cannot take: " + string.Join("; ", versionOnly.Select(diagnostic => diagnostic.ToString())));
		}

		var unexpected = CompilationErrors(source, LanguageVersion.CSharp7_3)
			.Where(diagnostic => !UnresolvedReferenceIds.Contains(diagnostic.Id, StringComparer.Ordinal))
			.ToArray();
		if (unexpected.Length != 0)
		{
			problems.Add("the probe has errors beyond unresolved game/CUO types: " + string.Join("; ", unexpected.Select(diagnostic => diagnostic.ToString())));
		}

		return [.. problems];
	}

	private static Diagnostic[] SyntaxErrors(string source) =>
		[.. CSharpSyntaxTree
			.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp7_3))
			.GetDiagnostics()
			.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

	/// <summary>Errors the C# 7.3 compilation reports that the latest language version does not: the language-version-only differences, with every error both versions share cancelled out.</summary>
	private static Diagnostic[] VersionOnlyErrors(string source)
	{
		var latest = CompilationErrors(source, LanguageVersion.Latest).Select(Key).ToHashSet(StringComparer.Ordinal);
		return [.. CompilationErrors(source, LanguageVersion.CSharp7_3).Where(diagnostic => !latest.Contains(Key(diagnostic)))];
	}

	/// <summary>The errors both language versions report for the same position: the CUO and Unity references an offline compile cannot resolve, and nothing else in a healthy template.</summary>
	private static Diagnostic[] SharedErrors(string source)
	{
		var latest = CompilationErrors(source, LanguageVersion.Latest).Select(Key).ToHashSet(StringComparer.Ordinal);
		return [.. CompilationErrors(source, LanguageVersion.CSharp7_3).Where(diagnostic => latest.Contains(Key(diagnostic)))];
	}

	private static Diagnostic[] CompilationErrors(string source, LanguageVersion version) =>
		[.. CSharpCompilation
			.Create(
				"__AcceptanceDriverProbe",
				[CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(version))],
				PlatformReferences,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
			.GetDiagnostics()
			.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

	private static string Key(Diagnostic diagnostic) =>
		diagnostic.Id + "|" + diagnostic.Location.GetLineSpan().StartLinePosition.Line;

	private static IReadOnlyList<MetadataReference> BuildPlatformReferences()
	{
		var paths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
			?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
			?? [.. AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
				.Select(assembly => assembly.Location)];
		return [.. paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
	}
}
