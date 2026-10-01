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
/// Three rules about the acceptance tools, gated rather than trusted: the in-process template and every
/// scenario recipe must stay inside the language the evaluator compiles (Mono.CSharp, C# 7.x — a newer
/// construct fails at run time, in the one place the run cannot recompile around); every recipe must
/// declare exactly the arguments it uses, so the run's -RecipeArg is never guessed; and nothing under
/// tools/acceptance/ may reach for OS-level input (the user's boundary: the agent drives from inside the
/// process, never the mouse and keyboard). Each check pins its own matcher with positive and negative
/// samples, so a rule that stopped matching fails here instead of passing by checking nothing.
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
		"[System.Windows.Forms.Cursor]::Position = new System.Drawing.Point(0, 0);",
		"var point = System.Windows.Forms.Cursor.Position;",
		"var text = System.Windows.Forms.Clipboard.GetText();"
	];

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

		var failures = files
			.SelectMany(file => OsInputApis
				.Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
				.Select(token => $"{Path.GetRelativePath(RepositoryPaths.Root, file)}: '{token}' is an OS-level input API; an acceptance run drives in process only"))
			.ToArray();
		Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));

		// The matcher's own teeth: every input family must be covered, a real call must be caught, and the
		// rule's own sentence must stay clean.
		foreach (var sample in OsInputSamples)
		{
			Assert.True(OsInputApis.Any(token => sample.Contains(token, StringComparison.Ordinal)), $"the banned list no longer covers an OS-input family: {sample}");
		}

		Assert.True(OsInputApis.Any(token => "using System.Windows.Forms; SendKeys.SendWait(\"x\");".Contains(token, StringComparison.Ordinal)), "the banned list must catch a real OS-input call");
		Assert.False(OsInputApis.Any(token => "the driver acts in process and takes no OS input".Contains(token, StringComparison.Ordinal)), "the banned list must not fire on the rule's own sentence");
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
