using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.PinyinSearch.Core;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Patching;
using Microsoft.Extensions.Logging.Abstractions;
using Mono.Cecil;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.PinyinSearch;

/// <summary>
/// What the satellite mod's packaging and its Tier-2 declaration claim, as facts
/// about the SHIPPED assemblies (ticket stage 2/3 acceptance):
/// <list type="number">
/// <item>the game-bound plug-in assembly references NO CUO framework assembly, so
/// a CUO-less install cannot fail to load the half that extends the crafting
/// search box;</item>
/// <item>in the Core assembly only the two CUO-facing types name an Abstractions
/// type, so even with CUO installed the crafting path resolves nothing out of
/// it — the property the "works with CUO uninstalled" claim rests on;</item>
/// <item>the declaration CUO reports (<c>[CuoMod]</c> <c>NativeBinding</c> and the
/// local network mode) is present, because a mod that patches the game owes it
/// (<c>docs/api/advanced-modification-policy.md</c> §1.1);</item>
/// <item>the game-side contract the crafting patch binds still resolves — both
/// Harmony targets and the private field the item-filter guard reads. This is the
/// game-update churn the mod took over from CUO, so it is checked where the mod
/// lives.</item>
/// </list>
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class PinyinSearchContractTests
{
	private const string PluginAssemblyFile = "CasualtiesUnknownOnline.PinyinSearch.dll";
	private const string CoreAssemblyFile = "CasualtiesUnknownOnline.PinyinSearch.Core.dll";
	private const string CuoNamespace = "CasualtiesUnknownOnline.Abstractions";

	/// <summary>The CUO framework assemblies — the ones a CUO-less install does not have.</summary>
	private static readonly string[] FrameworkAssemblies =
	[
		"CasualtiesUnknownOnline.Abstractions",
		"CasualtiesUnknownOnline.Runtime",
		"CasualtiesUnknownOnline.GameAdapter",
		"CasualtiesUnknownOnline.Protocol",
		"CasualtiesUnknownOnline.GameState",
	];

	[Fact]
	public void PluginAssembly_ReferencesNoCuoFrameworkAssembly()
	{
		using var assembly = AssemblyDefinition.ReadAssembly(OutputPath(PluginAssemblyFile));

		var references = assembly.MainModule.AssemblyReferences.Select(reference => reference.Name).ToList();
		var cuoReferences = FrameworkAssemblies.Where(references.Contains).ToList();

		Assert.True(
			cuoReferences.Count == 0,
			$"the game-bound plug-in assembly must not reference CUO: {string.Join(", ", cuoReferences)}");
	}

	[Fact]
	public void CoreAssembly_OnlyTheCuoFacingTypesNameAnAbstractionsType()
	{
		using var assembly = AssemblyDefinition.ReadAssembly(OutputPath(CoreAssemblyFile));

		var touching = CuoTouchingTypeNames(assembly).OrderBy(name => name, StringComparer.Ordinal).ToList();

		Assert.Equal(
			[
				"CasualtiesUnknownOnline.PinyinSearch.Core.PinyinSearchMod",
				"CasualtiesUnknownOnline.PinyinSearch.Core.PinyinSearchStage",
			],
			touching);
	}

	[Fact]
	public void ModEntryPoint_IsAcceptedByCuoDiscovery()
	{
		var discovered = new ModRegistry(NullLogger<ModRegistry>.Instance)
			.Discover([typeof(PinyinSearchMod).Assembly]);

		var mod = Assert.Single(discovered);
		Assert.Equal("cuo.pinyinsearch", mod.Manifest.Id);
		Assert.Equal("0.1.0", mod.Manifest.Version);
		Assert.Equal(NetworkMode.ClientOnly, mod.Manifest.NetworkMode);
		Assert.Equal(ModPermission.None, mod.Manifest.Permissions);
		Assert.Equal("PlayerCamera.RefreshRecipeList + Recipe.simpleName getter", mod.Manifest.NativeBinding);
		Assert.Equal(typeof(PinyinSearchMod), mod.Type);
	}

	[Fact]
	public void ModDeclaration_DeclaresTheNativeBindingAndALocalNetworkMode()
	{
		var declaration = typeof(PinyinSearchMod).GetCustomAttribute<CuoModAttribute>();

		Assert.NotNull(declaration);
		Assert.Equal(NetworkMode.ClientOnly, declaration!.NetworkMode);
		Assert.Contains("PlayerCamera.RefreshRecipeList", declaration.NativeBinding, StringComparison.Ordinal);
		Assert.Contains("Recipe.simpleName", declaration.NativeBinding, StringComparison.Ordinal);
	}

	[Fact]
	public void CraftingPatchTargets_StillResolveInTheGameAssembly()
	{
		var camera = GameAssemblyHost.ResolveType("PlayerCamera");
		var recipe = GameAssemblyHost.ResolveType("Recipe");
		const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		Assert.NotNull(camera);
		Assert.NotNull(recipe);
		Assert.NotNull(camera!.GetMethod("RefreshRecipeList", Any));
		Assert.NotNull(recipe!.GetProperty("simpleName", Any));
		Assert.True(
			camera.GetField("recipeItemFilter", Any) is not null || camera.GetProperty("recipeItemFilter", Any) is not null,
			"PlayerCamera.recipeItemFilter is gone — the item-filter guard can no longer be evaluated");
	}

	private static string OutputPath(string fileName) =>
		Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

	/// <summary>
	/// The types whose metadata names a CUO.Abstractions type — interfaces, field
	/// and method signatures, and the members a method body calls. A type that
	/// names none of them cannot require the Abstractions assembly to load, which
	/// is what keeps the crafting path CUO-independent with CUO uninstalled.
	/// </summary>
	private static IEnumerable<string> CuoTouchingTypeNames(AssemblyDefinition assembly) =>
		assembly.MainModule.Types.SelectMany(Flatten).Where(NamesCuo).Select(type => type.FullName);

	private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type) =>
		type.NestedTypes.SelectMany(Flatten).Prepend(type);

	private static bool NamesCuo(TypeDefinition type) =>
		type.Interfaces.Any(@interface => IsCuo(@interface.InterfaceType))
		|| type.Fields.Any(field => IsCuo(field.FieldType))
		|| type.Methods.Any(NamesCuo);

	private static bool NamesCuo(MethodDefinition method) =>
		IsCuo(method.ReturnType)
		|| method.Parameters.Any(parameter => IsCuo(parameter.ParameterType))
		|| (method.HasBody && method.Body.Instructions.Any(instruction => ReferencesCuo(instruction.Operand)));

	private static bool ReferencesCuo(object? operand) => operand switch
	{
		TypeReference reference => IsCuo(reference),
		MemberReference reference => IsCuo(reference.DeclaringType),
		_ => false,
	};

	private static bool IsCuo(TypeReference? reference) =>
		reference is not null
		&& (string.Equals(reference.Namespace, CuoNamespace, StringComparison.Ordinal) || IsCuo(reference.DeclaringType));
}
