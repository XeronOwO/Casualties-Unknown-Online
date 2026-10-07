using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The content-kind coverage gate (ticket
/// <c>docs/backlog/review/mod-content-kind-with-no-provider.md</c>): the
/// vocabulary <c>ModContentKind</c> publishes, the kinds the content-binding
/// providers declare and the providers a DI registration statement names under
/// <c>src/</c> are ONE list.
///
/// <para>
/// Why this is a gate rather than a unit test: the three facts live in three
/// places nothing ties together — a constants class in Abstractions, one class
/// per provider in the Game Adapter, and their DI registration statements. A
/// kind is a string the binder looks up in a map built from those registrations,
/// so a constant without a provider is a kind a mod can name and register that
/// never materializes (the defect this gate closes), and a provider no
/// registration names binds nothing at all because the binder never sees it. No
/// runtime test can enumerate the adapter's providers (the test host composes
/// the Runtime without the Game Adapter), so the census is read from SOURCE, in
/// the style of the other composition-root pins.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the vocabulary census reads the public
/// string fields of the type named <c>ModContentKind</c> in its own file; the
/// provider census reads every type under <c>src/</c> whose base list names
/// <c>IContentBindingProvider</c> — under any <c>using</c> alias the file
/// declares for that interface — together with the <c>Kind</c> member the type
/// declares; the registration census reads every DI registration call under
/// <c>src/</c> whose generic type argument list names that interface (alias
/// included) and resolves the provider type from its argument through the same
/// alias map. The registration census proves that a registration STATEMENT
/// names the provider, not that the running composition executes it: a
/// registration inside an extension method nothing calls satisfies it, and which
/// statements a container composes is not a syntax fact — in this tree the only
/// such registrations are the composition root's nine. A provider that inherits
/// the interface through another type without naming it is out of the
/// declaration census, and the registration census then fails the set comparison
/// rather than passing quietly. A registration written in a shape the census
/// cannot resolve, a <c>Kind</c> member it cannot read (including one that names
/// the vocabulary through an alias of <c>ModContentKind</c>, which is reported
/// rather than resolved) and a public string field whose value it cannot read
/// are all reported as failures instead of skipped. What a provider does with a
/// definition is not this gate's reach: that is each provider's own tests.
/// </para>
/// </summary>
public class ContentKindProviderCoverageGateTests
{
	/// <summary>The one file that publishes the vocabulary.</summary>
	internal const string VocabularyPath = "src/CasualtiesUnknownOnline.Abstractions/ModContentKind.cs";

	/// <summary>The interface a content provider implements and the binder collects.</summary>
	internal const string InterfaceName = "IContentBindingProvider";

	/// <summary>The type that publishes the vocabulary, as a <c>Kind</c> expression names it.</summary>
	internal const string VocabularyTypeName = "ModContentKind";

	/// <summary>The tree the two provider censuses read, derived from the repository layout rather than listed.</summary>
	internal const string SourceRoot = "src";

	/// <summary>The DI method names a registration can be spelled with: the composition root's own <c>AddSingleton</c> plus the shapes a later refactor would plausibly reach for, so a re-spelled registration is still found and an unresolvable one is reported.</summary>
	private static readonly string[] RegistrationMethods =
		["AddSingleton", "AddScoped", "AddTransient", "TryAddSingleton", "TryAddEnumerable", "Singleton"];

	/// <summary>Census floors: the measured counts at 2026-10-07 (nine kinds, nine providers, nine registrations). A pin emptied alongside its source must fail here rather than pass by checking nothing.</summary>
	internal const int KindFloor = 9;

	internal const int ProviderFloor = 9;

	internal const int RegistrationFloor = 9;

	[Fact]
	public void TheVocabulary_NamesExactlyTheKindsTheProvidersDeclare()
	{
		var failures = new List<string>();
		var vocabulary = VocabularyConstants(RepositoryPaths.ReadText(VocabularyPath));
		if (vocabulary.Count < KindFloor)
		{
			failures.Add($"{VocabularyPath} yielded {vocabulary.Count} public string field(s); the measured vocabulary is {KindFloor}");
		}

		var values = new HashSet<string>(StringComparer.Ordinal);
		foreach (var constant in vocabulary)
		{
			if (constant.Value is null)
			{
				failures.Add($"{VocabularyPath}: '{constant.Name}' is a public string field this gate cannot read; the vocabulary is a list of literal constants");
				continue;
			}

			values.Add(constant.Value);
		}

		foreach (var group in vocabulary.Where(constant => constant.Value is not null).GroupBy(constant => constant.Value!, StringComparer.Ordinal).Where(group => group.Count() > 1))
		{
			failures.Add(
				$"{VocabularyPath}: {string.Join(", ", group.Select(constant => $"'{constant.Name}'"))} all declare the value '{group.Key}'; "
				+ "a kind has one name, so a second name for the same kind is the drift this list exists to prevent");
		}

		var byName = vocabulary
			.Where(constant => constant.Value is not null)
			.ToDictionary(constant => constant.Name, constant => constant.Value!, StringComparer.Ordinal);

		var providerKinds = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var provider in Scan().Providers)
		{
			var kind = KindValue(provider.Kind, byName);
			if (kind is null)
			{
				failures.Add(
					$"{provider.File}: {provider.Type} declares Kind as '{provider.Kind?.ToString() ?? "(nothing readable)"}', "
					+ $"which this gate cannot read as a {VocabularyPath} constant or a string literal");
				continue;
			}

			if (providerKinds.TryGetValue(kind, out var owner))
			{
				failures.Add($"{provider.File}: {provider.Type} binds kind '{kind}', which {owner} already binds; the binder's kind map holds one provider per kind");
				continue;
			}

			providerKinds.Add(kind, provider.Type);
		}

		if (providerKinds.Count < ProviderFloor)
		{
			failures.Add($"the provider census read {providerKinds.Count} kind(s); the measured providers declare {ProviderFloor}");
		}

		foreach (var constant in vocabulary.Where(constant => constant.Value is not null && !providerKinds.ContainsKey(constant.Value)))
		{
			failures.Add(
				$"{VocabularyPath}: '{constant.Name} = \"{constant.Value}\"' is declared but no provider binds that kind; "
				+ "a kind without a provider is one a mod can name, register and never see materialized");
		}

		foreach (var pair in providerKinds.Where(pair => !values.Contains(pair.Key)))
		{
			failures.Add($"{pair.Value} binds kind '{pair.Key}', which {VocabularyPath} does not declare; every kind the framework binds belongs to the vocabulary");
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EveryProvider_IsNamedByARegistrationStatement()
	{
		var scan = Scan();
		var failures = new List<string>();
		var declared = scan.Providers.Select(provider => provider.Type).ToHashSet(StringComparer.Ordinal);
		if (declared.Count < ProviderFloor)
		{
			failures.Add($"the provider census read {declared.Count} type(s) declaring {InterfaceName}; the measured providers are {ProviderFloor}");
		}

		if (scan.Registrations.Count < RegistrationFloor)
		{
			failures.Add($"the registration census read {scan.Registrations.Count} {InterfaceName} registration(s); the measured registrations are {RegistrationFloor}");
		}

		var registered = new HashSet<string>(StringComparer.Ordinal);
		foreach (var registration in scan.Registrations)
		{
			if (registration.Type is null)
			{
				failures.Add(
					$"{registration.File}: this gate cannot resolve which provider '{registration.Call}' registers; "
					+ "a registration it cannot read would hide a provider from the binder's map");
				continue;
			}

			registered.Add(registration.Type);
			if (!declared.Contains(registration.Type))
			{
				failures.Add($"{registration.File}: '{registration.Call}' registers {registration.Type}, which does not declare {InterfaceName}");
			}
		}

		foreach (var type in declared.Where(type => !registered.Contains(type)).OrderBy(type => type, StringComparer.Ordinal))
		{
			failures.Add(
				$"'{type}' declares {InterfaceName} but no registration reaches it; the binder's map is built from the DI registrations, "
				+ "so a provider nothing registers binds nothing");
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Theory]
	[InlineData("public static class ModContentKind { public const string Item = \"item\"; }", "Item=item")]
	[InlineData("public static class ModContentKind { public const string A = \"a\", B = \"b\"; }", "A=a,B=b")]
	[InlineData("public static class ModContentKind { private const string Hidden = \"hidden\"; public const int Count = 3; internal static string Scoped = \"x\"; }", "")]
	[InlineData("public static class ModContentKind { public static string Mutable = \"mutable\"; }", "Mutable=mutable")]
	[InlineData("public static class Other { public const string Item = \"item\"; }", "")]
	[InlineData("public static class ModContentKind { public static readonly string Computed = Make(); }", "Computed=(unreadable)")]
	public void TheVocabularyCensus_ReadsThePublicStringFieldsOfTheVocabularyType(string source, string expected) =>
		Assert.Equal(
			expected,
			string.Join(",", VocabularyConstants(source).Select(constant => $"{constant.Name}={constant.Value ?? "(unreadable)"}")));

	[Theory]
	[InlineData("internal sealed class P : IContentBindingProvider { public string Kind => ModContentKind.Item; }", "P|ModContentKind.Item")]
	[InlineData("internal sealed class P : IContentBindingProvider, ICuoService { public string Kind => \"item\"; }", "P|\"item\"")]
	[InlineData("internal sealed class P : IContentBindingProvider { public string Kind { get; } = ModContentKind.Item; }", "P|ModContentKind.Item")]
	[InlineData("using Cbp = CasualtiesUnknownOnline.Runtime.Session.Mods.IContentBindingProvider;\ninternal sealed class P : Cbp { public string Kind => ModContentKind.Item; }", "P|ModContentKind.Item")]
	[InlineData("internal sealed class P : IContentBindingProvider { public string Kind => Compute(); }", "P|Compute()")]
	[InlineData("internal sealed class P : IContentBindingProvider { public string Kind { get; } }", "P|(none)")]
	[InlineData("internal sealed class NotAProvider { public string Kind => ModContentKind.Item; }", "")]
	public void TheProviderCensus_ReadsTheKindMemberOfEveryBindingProvider(string source, string expected) =>
		Assert.Equal(
			expected,
			string.Join(",", ProviderDeclarations("sample.cs", source).Select(provider => $"{provider.Type}|{provider.Kind?.ToString() ?? "(none)"}")));

	[Theory]
	[InlineData("ModContentKind.Item", "item")]
	[InlineData("\"item\"", "item")]
	[InlineData("ModContentKind.Archaeology", "(unresolved)")]
	[InlineData("Other.Item", "(unresolved)")]
	[InlineData("Compute()", "(unresolved)")]
	[InlineData("null", "(unresolved)")]
	public void TheKindResolution_ReadsAVocabularyConstantOrAStringLiteral(string expression, string expected)
	{
		var vocabulary = new Dictionary<string, string>(StringComparer.Ordinal) { ["Item"] = "item" };

		var value = KindValue(Expression(expression), vocabulary);

		Assert.Equal(expected, value ?? "(unresolved)");
	}

	[Theory]
	[InlineData("", "services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<Foo>());", "Foo")]
	[InlineData("", "services.AddSingleton<IContentBindingProvider, Foo>();", "Foo")]
	[InlineData("using Cbp = CasualtiesUnknownOnline.Runtime.Session.Mods.IContentBindingProvider;", "services.AddSingleton<Cbp, Foo>();", "Foo")]
	[InlineData("using Cbp = CasualtiesUnknownOnline.Runtime.Session.Mods.IContentBindingProvider;\nusing Bar = Sample.Foo;", "services.AddSingleton<Cbp, Bar>();", "Foo")]
	[InlineData("", "services.Replace(ServiceDescriptor.Singleton<IContentBindingProvider>(p => p.GetRequiredService<Foo>()));", "Foo")]
	[InlineData("", "services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<Foo>()); // IContentBindingProvider", "Foo")]
	[InlineData("", "services.AddSingleton<ICuoService>(p => p.GetRequiredService<Foo>());", "")]
	[InlineData("", "services.AddSingleton<Foo>();", "")]
	[InlineData("", "services.AddSingleton<IContentBindingProvider>(sp => Make());", "(unresolved)")]
	public void TheRegistrationCensus_ReadsTheProviderTypeOfEveryBindingRegistration(string usings, string body, string expected) =>
		Assert.Equal(
			expected,
			string.Join(
				",",
				RegistrationCalls("sample.cs", $"{usings}\ninternal static class Composition {{ public static void Add(IServiceCollection services) {{ {body} }} }}")
					.Select(registration => registration.Type ?? "(unresolved)")));

	/// <summary>Both censuses over <c>src/</c> in one pass: a file that names neither the interface nor an alias of it can contribute to neither.</summary>
	private static ScanResult Scan()
	{
		var providers = new List<ProviderDeclaration>();
		var registrations = new List<Registration>();
		foreach (var path in SourceScan.SourceFiles(RepositoryPaths.File(SourceRoot)))
		{
			var source = File.ReadAllText(path);
			if (!source.Contains(InterfaceName, StringComparison.Ordinal))
			{
				continue;
			}

			var file = Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
			providers.AddRange(ProviderDeclarations(file, source));
			registrations.AddRange(RegistrationCalls(file, source));
		}

		return new ScanResult(providers, registrations);
	}

	/// <summary>The vocabulary's public string fields, in declaration order. A field whose value is not a literal is returned with a null value rather than skipped, so it is reported instead of silently shrinking the vocabulary.</summary>
	internal static List<VocabularyConstant> VocabularyConstants(string source)
	{
		var declaration = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(type => string.Equals(type.Identifier.ValueText, VocabularyTypeName, StringComparison.Ordinal));

		if (declaration is null)
		{
			return [];
		}

		var constants = new List<VocabularyConstant>();
		foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>())
		{
			// A const field carries no 'static' token, so both spellings are the vocabulary.
			if (!field.Modifiers.Any(SyntaxKind.PublicKeyword)
				|| !(field.Modifiers.Any(SyntaxKind.ConstKeyword) || field.Modifiers.Any(SyntaxKind.StaticKeyword))
				|| !string.Equals(field.Declaration.Type.ToString(), "string", StringComparison.Ordinal))
			{
				continue;
			}

			foreach (var variable in field.Declaration.Variables)
			{
				constants.Add(new VocabularyConstant(
					variable.Identifier.ValueText,
					variable.Initializer?.Value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
						? literal.Token.ValueText
						: null));
			}
		}

		return constants;
	}

	/// <summary>Every type in one source file whose base list names the binding interface — directly or through the file's own alias for it — with its <c>Kind</c> member's expression.</summary>
	internal static List<ProviderDeclaration> ProviderDeclarations(string file, string source)
	{
		var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
		var spellings = InterfaceSpellings(root);
		var declarations = new List<ProviderDeclaration>();
		foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
		{
			if (type.BaseList is null
				|| !type.BaseList.Types.Any(b => string.Equals(Resolve(SimpleName(b.Type), spellings), InterfaceName, StringComparison.Ordinal)))
			{
				continue;
			}

			declarations.Add(new ProviderDeclaration(type.Identifier.ValueText, file, KindExpression(type)));
		}

		return declarations;
	}

	/// <summary>Every DI registration in one source file whose generic type argument list names the binding interface (directly or through the file's alias), with the provider type its argument resolves (null when this gate cannot read it).</summary>
	internal static List<Registration> RegistrationCalls(string file, string source)
	{
		var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
		var spellings = InterfaceSpellings(root);
		var registrations = new List<Registration>();
		foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			if (invocation.Expression is not MemberAccessExpressionSyntax access
				|| access.Name is not GenericNameSyntax generic
				|| !RegistrationMethods.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal))
			{
				continue;
			}

			var names = generic.TypeArgumentList.Arguments.Select(SimpleName).ToList();
			if (!names.Any(name => string.Equals(Resolve(name, spellings), InterfaceName, StringComparison.Ordinal)))
			{
				continue;
			}

			var type = names.Count == 1
				? ProviderFromRegistration(invocation, spellings)
				: Resolve(names.First(name => !string.Equals(Resolve(name, spellings), InterfaceName, StringComparison.Ordinal)), spellings);

			registrations.Add(new Registration(type, file, invocation.ToString()));
		}

		return registrations;
	}

	/// <summary>The kind a provider's <c>Kind</c> expression names: a vocabulary constant's value, or a string literal. Null when the expression is neither — the caller reports that rather than dropping the provider.</summary>
	internal static string? KindValue(ExpressionSyntax? expression, IReadOnlyDictionary<string, string> vocabulary)
	{
		if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			return literal.Token.ValueText;
		}

		if (expression is MemberAccessExpressionSyntax access
			&& access.Expression.ToString().EndsWith(VocabularyTypeName, StringComparison.Ordinal)
			&& access.Name is IdentifierNameSyntax name
			&& vocabulary.TryGetValue(name.Identifier.ValueText, out var value))
		{
			return value;
		}

		return null;
	}

	/// <summary>
	/// How one file spells the binding interface: the interface's own name plus every
	/// <c>using</c> alias whose target is it. The repository's own convention prefers aliases over
	/// qualified names, so a file that aliases the interface still names it — and without this map
	/// such a provider and its registration would be invisible to BOTH censuses, which is a silent
	/// hole rather than the loud failure an unreadable shape gets.
	/// </summary>
	private static Dictionary<string, string> InterfaceSpellings(SyntaxNode root)
	{
		var spellings = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			[InterfaceName] = InterfaceName
		};

		foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
		{
			if (directive.Alias is null || directive.Name is null)
			{
				continue;
			}

			spellings[directive.Alias.Name.Identifier.ValueText] = SimpleName(directive.Name);
		}

		return spellings;
	}

	private static string Resolve(string name, IReadOnlyDictionary<string, string> spellings) =>
		spellings.TryGetValue(name, out var target) ? target : name;

	/// <summary>The <c>Kind</c> member a type declares: its expression body or its initializer, null when it declares none this gate can read.</summary>
	private static ExpressionSyntax? KindExpression(TypeDeclarationSyntax type)
	{
		foreach (var property in type.Members.OfType<PropertyDeclarationSyntax>())
		{
			if (!string.Equals(property.Identifier.ValueText, "Kind", StringComparison.Ordinal))
			{
				continue;
			}

			return property.ExpressionBody?.Expression ?? property.Initializer?.Value;
		}

		return null;
	}

	/// <summary>The provider type a registration's argument resolves: the composition root's <c>GetRequiredService&lt;T&gt;()</c>, or a <c>typeof(T)</c>.</summary>
	private static string? ProviderFromRegistration(InvocationExpressionSyntax registration, IReadOnlyDictionary<string, string> spellings)
	{
		var argument = registration.ArgumentList.Arguments.FirstOrDefault()?.Expression;
		if (argument is null)
		{
			return null;
		}

		var resolved = argument.DescendantNodesAndSelf()
			.OfType<InvocationExpressionSyntax>()
			.Select(call => ServiceTypeOf(call.Expression))
			.FirstOrDefault(type => type is not null);
		if (resolved is not null)
		{
			return Resolve(resolved, spellings);
		}

		var typed = argument.DescendantNodesAndSelf().OfType<TypeOfExpressionSyntax>().FirstOrDefault();
		return typed is null ? null : Resolve(SimpleName(typed.Type), spellings);
	}

	/// <summary>The <c>T</c> of a <c>GetRequiredService&lt;T&gt;()</c> call — spelled on the method itself or on a receiver — and null for every other call.</summary>
	private static string? ServiceTypeOf(ExpressionSyntax expression) => expression switch
	{
		GenericNameSyntax generic when string.Equals(generic.Identifier.ValueText, "GetRequiredService", StringComparison.Ordinal) =>
			SimpleName(generic.TypeArgumentList.Arguments[0]),
		MemberAccessExpressionSyntax access when access.Name is GenericNameSyntax name
			&& string.Equals(name.Identifier.ValueText, "GetRequiredService", StringComparison.Ordinal) =>
			SimpleName(name.TypeArgumentList.Arguments[0]),
		_ => null,
	};

	/// <summary>One expression's syntax, for the matcher samples: parsed out of a sample property, so the samples exercise the same reading the census does.</summary>
	private static ExpressionSyntax? Expression(string expression) =>
		CSharpSyntaxTree.ParseText($"internal sealed class Sample {{ internal string Kind => {expression}; }}", new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<PropertyDeclarationSyntax>()
			.FirstOrDefault()?.ExpressionBody?.Expression;

	private static string SimpleName(TypeSyntax? type) => type switch
	{
		GenericNameSyntax generic => generic.Identifier.ValueText,
		QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		_ => type?.ToString() ?? string.Empty,
	};

	/// <summary>One content provider declaration: the type name, the file it came from, and the expression its <c>Kind</c> member returns.</summary>
	internal sealed record ProviderDeclaration(string Type, string File, ExpressionSyntax? Kind);

	/// <summary>One DI registration of the binding interface: the provider type it resolves (null when unreadable), the file it was written in and the call as written.</summary>
	internal sealed record Registration(string? Type, string File, string Call);

	/// <summary>One public string field of the vocabulary: its name and its literal value, or null when the value is not a literal.</summary>
	internal sealed record VocabularyConstant(string Name, string? Value);

	private sealed record ScanResult(List<ProviderDeclaration> Providers, List<Registration> Registrations);
}
