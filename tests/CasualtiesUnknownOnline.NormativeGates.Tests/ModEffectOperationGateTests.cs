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
/// The named-operation gate. A mod-authored effect or item action writes the game's
/// body and limb state through an ENUM-KEYED operation surface
/// (<c>ModBodyVital</c> / <c>ModLimbVital</c>), and the whole point of that shape is
/// that the set is enumerable: every member names a game field a census found (the
/// liquid census in <c>docs/backlog/review/mod-authored-effects.md</c>, the item
/// census in <c>docs/backlog/review/mod-item-authored-actions.md</c>), and the one
/// mapping table in the Game Adapter answers for every one of them.
///
/// <para>
/// Two holes are possible and neither is a compile error, which is why this is a
/// gate. A member declared with no arm silently does nothing when a mod writes it —
/// the arm table's own default logs and refuses, so the failure is observable, but
/// it is still a declared operation that cannot work. And an arm that reads one game
/// field while writing another is a get/add/set triple that drifted, which is exactly
/// the shape one shared table exists to prevent.
/// </para>
///
/// <para>
/// The same rule reaches the permission vocabulary beside it: a bit declared outside
/// <c>ModPermission.All</c> is rejected by the discovery policy, so a mod could
/// declare it and never be granted it.
/// </para>
///
/// <para>
/// The scan reads named source files rather than a directory, because the rule is
/// about two specific pairs; the matchers are pinned by synthetic cases in both
/// directions, and the member census floor makes a scan that read nothing fail.
/// </para>
/// </summary>
public class ModEffectOperationGateTests
{
	/// <summary>Each enum-keyed surface: the enum, the file that declares it, and the file that owns its mapping table.</summary>
	private static readonly (string Enum, string Declaration, string Table)[] Surfaces =
	[
		(
			"ModBodyVital",
			"src/CasualtiesUnknownOnline.Abstractions/ModBodyVital.cs",
			"src/CasualtiesUnknownOnline.GameAdapter/ModEffects/ModEffectBody.cs"),
		(
			"ModLimbVital",
			"src/CasualtiesUnknownOnline.Abstractions/ModLimbVital.cs",
			"src/CasualtiesUnknownOnline.GameAdapter/ModEffects/ModEffectLimb.cs")
	];

	/// <summary>The two surfaces hold 33 and 9 members today; a scan that read fewer has broken.</summary>
	private const int MemberFloor = 39;

	[Fact]
	public void EveryDeclaredVital_HasExactlyOneMappingArm_ThatReadsAndWritesTheSameField()
	{
		var problems = Surfaces.SelectMany(surface =>
		{
			var tablePath = RepositoryPaths.File(surface.Table);
			Assert.True(File.Exists(tablePath), $"{surface.Table} was not found — the gate's own surface moved");
			var table = File.ReadAllText(tablePath);

			var declared = EnumMembers(RepositoryPaths.ReadText(RepositoryPaths.File(surface.Declaration)), surface.Enum);
			Assert.True(declared.Count > 0, $"{surface.Declaration} declares no {surface.Enum} member — the matcher read nothing");

			var arms = MappingArms(table, surface.Enum);
			Assert.True(arms.Count > 0, $"{surface.Table} declares no {surface.Enum} mapping arm — the matcher read nothing");

			var handled = arms.Select(arm => arm.Member).ToHashSet(StringComparer.Ordinal);
			return declared
				.Where(member => !handled.Contains(member))
				.Select(member => $"{surface.Enum}.{member} is declared but no arm in {surface.Table} answers for it")
				.Concat(arms
					.Where(arm => !declared.Contains(arm.Member))
					.Select(arm => $"{surface.Table}: the arm {surface.Enum}.{arm.Member} names no declared member"))
				.Concat(arms
					.Where(arm => arm.ReadField is null || !string.Equals(arm.ReadField, arm.WriteField, StringComparison.Ordinal))
					.Select(arm => $"{surface.Table}: the arm {surface.Enum}.{arm.Member} does not read and write the same game field ({arm.ReadField ?? "none"} / {arm.WriteField ?? "none"})"));
		}).ToArray();

		var total = Surfaces.Sum(surface => EnumMembers(
			RepositoryPaths.ReadText(RepositoryPaths.File(surface.Declaration)), surface.Enum).Count);
		Assert.True(total >= MemberFloor, $"the scan read {total} declared vital member(s) — the scan surface broke");

		Assert.Empty(problems);
	}

	[Fact]
	public void EveryDeclaredPermissionBit_IsInsideTheDefinedMask()
	{
		var source = RepositoryPaths.ReadText(RepositoryPaths.File("src/CasualtiesUnknownOnline.Abstractions/ModPermission.cs"));
		var declared = EnumMembers(source, "ModPermission");
		Assert.True(declared.Count > 2, $"the scan read {declared.Count} permission member(s) — the scan surface broke");

		var allBits = EnumInitializer(source, "ModPermission", "All");
		Assert.True(allBits is not null, "ModPermission.All carries no initializer — the mask cannot be checked");

		Assert.Empty(declared
			.Where(member => member is not "All" and not "None")
			.Where(member => !allBits!.Contains(member, StringComparison.Ordinal))
			.Select(member => $"{member} is declared but is not inside ModPermission.All"));
	}

	[Theory]
	[InlineData("enum ModBodyVital { Thirst, Hunger }", "Thirst,Hunger")]
	[InlineData("enum ModBodyVital { Thirst = 0, Hunger = 1 }", "Thirst,Hunger")]
	[InlineData("enum ModLimbVital { Pain }", "Pain")]
	[InlineData("enum Other { Thirst }", "")]
	[InlineData("class C { }", "")]
	public void TheEnumMatcher_ReadsTheMembersOfTheNamedEnumOnly(string source, string expected) =>
		Assert.Equal(
			expected,
			string.Join(",", new[] { "ModBodyVital", "ModLimbVital" }.SelectMany(name => EnumMembers(source, name))));

	[Theory]
	[InlineData("class C { object A(ModBodyVital v) => v switch { ModBodyVital.Thirst => (() => _body.thirst, x => _body.thirst = x), _ => null }; }", "Thirst:thirst:thirst")]
	[InlineData("class C { object A(ModBodyVital v) => v switch { ModBodyVital.Thirst => (() => _body.thirst, x => _body.hunger = x), _ => null }; }", "Thirst:thirst:hunger")]
	[InlineData("class C { object A(ModLimbVital v) => v switch { ModLimbVital.Pain => (() => _limb.pain, x => _limb.pain = x), _ => null }; }", "Pain:pain:pain")]
	[InlineData("class C { object A(ModBodyVital v) => v switch { _ => null }; }", "")]
	[InlineData("class C { object A(ModBodyVital v) => v switch { ModBodyVital.Thirst => null, _ => null }; }", "Thirst::")]
	[InlineData("class C { object A(Other v) => v switch { Other.Thirst => (() => _body.thirst, x => _body.thirst = x), _ => null }; }", "")]
	public void TheArmMatcher_ReadsTheMemberAndBothFieldNames(string source, string expected) =>
		Assert.Equal(
			expected,
			string.Join(",", new[] { "ModBodyVital", "ModLimbVital" }
				.SelectMany(name => MappingArms(source, name))
				.Select(arm => $"{arm.Member}:{arm.ReadField}:{arm.WriteField}")));

	[Theory]
	[InlineData("enum ModPermission { None = 0, A = 1 << 0, All = A }", "A")]
	[InlineData("enum ModPermission { None = 0, A = 1 << 0, All = None }", "None")]
	[InlineData("enum ModPermission { None = 0, A = 1 << 0 }", "")]
	[InlineData("enum Other { None = 0, A = 1 << 0, All = A }", "")]
	public void TheMaskMatcher_ReadsTheAllInitializer(string source, string expected) =>
		Assert.Equal(expected, EnumInitializer(source, "ModPermission", "All") ?? "");

	/// <summary>The members one enum declaration declares, in declaration order.</summary>
	internal static IReadOnlyList<string> EnumMembers(string source, string enumName) =>
		[.. CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.Where(declaration => string.Equals(declaration.Identifier.ValueText, enumName, StringComparison.Ordinal))
			.SelectMany(declaration => declaration.Members)
			.Select(member => member.Identifier.ValueText)];

	/// <summary>The text of one enum member's initializer, or null when it carries none.</summary>
	internal static string? EnumInitializer(string source, string enumName, string memberName) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.Where(declaration => string.Equals(declaration.Identifier.ValueText, enumName, StringComparison.Ordinal))
			.SelectMany(declaration => declaration.Members)
			.Where(member => string.Equals(member.Identifier.ValueText, memberName, StringComparison.Ordinal))
			.Select(member => member.EqualsValue?.Value.ToString())
			.FirstOrDefault();

	/// <summary>
	/// Every <c>&lt;enumName&gt;.&lt;Member&gt; =&gt; …</c> arm of a switch expression under
	/// <paramref name="source"/>, with the game field its read lambda and its write lambda each name.
	/// The discard arm (<c>_ =&gt; null</c>) is not a member and is skipped.
	/// </summary>
	internal static IReadOnlyList<MappingArm> MappingArms(string source, string enumName) =>
		[.. CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<SwitchExpressionArmSyntax>()
			.Where(arm => arm.Pattern is ConstantPatternSyntax pattern
				&& pattern.Expression is MemberAccessExpressionSyntax access
				&& string.Equals(access.Expression.ToString(), enumName, StringComparison.Ordinal))
			.Select(arm => new MappingArm(
				((MemberAccessExpressionSyntax)((ConstantPatternSyntax)arm.Pattern).Expression).Name.Identifier.ValueText,
				Field(arm.Expression, 0),
				Field(arm.Expression, 1)))];

	/// <summary>
	/// The game field the <paramref name="index"/>-th lambda of a mapping arm touches:
	/// a tuple arm holds the read lambda first and the write lambda second. Null when
	/// the arm is not a two-lambda tuple, which the caller reports as a mismatch.
	/// </summary>
	private static string? Field(SyntaxNode armExpression, int index)
	{
		if (armExpression is not TupleExpressionSyntax tuple || tuple.Arguments.Count != 2)
		{
			return null;
		}

		return tuple.Arguments[index].Expression
			.DescendantNodes()
			.OfType<MemberAccessExpressionSyntax>()
			.Select(access => access.Name.Identifier.ValueText)
			.FirstOrDefault();
	}

	/// <summary>One mapping arm: the enum member it answers for and the fields its two lambdas name.</summary>
	internal sealed record MappingArm(string Member, string? ReadField, string? WriteField);
}
