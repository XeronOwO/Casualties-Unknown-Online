using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Unit-test gate for AGENTS.md Engineering Convention #10: prefer using
/// directives/aliases over fully qualified type names. The test covers the
/// clear violation, the accepted forms, and the documented ambiguity exception.
/// It also runs the same Roslyn gate over the repository's C# sources so the
/// convention is enforced in the ordinary <c>dotnet test</c> loop.
/// </summary>
public class FullyQualifiedNameGateTests
{
	[Fact]
	public void ClearViolation_IsReported()
	{
		const string source = """
			using System;

			namespace Demo;

			class Sample
			{
				string Value;
				String Value2;
				System.String Value3;
			}
			""";

		var violations = FullyQualifiedNameGate.FindInFile("Sample.cs", source);

		Assert.Contains(violations, v => v.Text == "System.String");
	}

	[Fact]
	public void AcceptedForms_AreNotReported()
	{
		const string source = """
			using System;
			using Str = System.String;

			namespace Demo;

			class Sample
			{
				string Value;
				String Value2;
				Str Value3;
				object Value4 = "System.String";
			}
			""";

		var violations = FullyQualifiedNameGate.FindInFile("Accepted.cs", source);

		Assert.Empty(violations);
	}

	[Fact]
	public void StaticMemberAccess_SystemStringComparison_IsReported()
	{
		const string source = """
			using System;

			namespace Demo;

			class Sample
			{
				bool IsSlash(string value) => value.StartsWith("/", System.StringComparison.Ordinal);
			}
			""";

		var violations = FullyQualifiedNameGate.FindInFile("MemberAccess.cs", source);

		Assert.Contains(violations, v => v.Text == "System.StringComparison.Ordinal");
	}

	[Fact]
	public void NamespaceDeclarationsAndUsingDirectives_AreNotReported()
	{
		const string source = """
			using System.Collections.Generic;

			namespace System.Runtime.CompilerServices;

			class Sample
			{
				List<string> Values;
			}
			""";

		var violations = FullyQualifiedNameGate.FindInFile("Namespaces.cs", source);

		Assert.Empty(violations);
	}

	[Fact]
	public void AmbiguityCollision_WithMemberNamedPath_IsAllowed()
	{
		const string source = """
			using System.IO;

			class TempFiles
			{
				public string Path(string name) => name;

				public static string Create()
				{
					return System.IO.Path.Combine("a", "b");
				}
			}
			""";

		var violations = FullyQualifiedNameGate.FindInFile("Collision.cs", source);

		Assert.Empty(violations);
	}

	[Fact]
	public void Repository_HasNoUnnecessaryFullyQualifiedTypeNames()
	{
		var root = FindRepositoryRoot();
		var violations = new List<FullyQualifiedNameViolation>();
		var parseFailures = new List<string>();

		foreach (var directory in new[] { Path.Combine(root, "src"), Path.Combine(root, "tests") })
		{
			foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
			{
				if (IsBuildOutput(file))
				{
					continue;
				}

				var relative = file.StartsWith(root, StringComparison.Ordinal)
					? file.Substring(root.Length + 1)
					: file;
				try
				{
					violations.AddRange(FullyQualifiedNameGate.FindInFile(relative, File.ReadAllText(file)));
				}
				catch (Exception ex)
				{
					parseFailures.Add($"{relative}: {ex.GetType().Name}: {ex.Message}");
				}
			}
		}

		Assert.True(parseFailures.Count == 0,
			"Normative gate could not parse source files" + Environment.NewLine + string.Join(Environment.NewLine, parseFailures));
		Assert.True(violations.Count == 0,
			"Repository contains unnecessary fully qualified type names (AGENTS.md #10):\n"
			+ string.Join("\n", violations.Select(v => $"{v.FilePath}:{v.Line}: {v.Text}")));
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		Assert.True(directory is not null, "could not locate repository root (CasualtiesUnknownOnline.slnx)");
		return directory!.FullName;
	}

	private static bool IsBuildOutput(string path)
	{
		return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
			|| path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
	}
}
