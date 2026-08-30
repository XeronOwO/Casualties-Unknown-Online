using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 contract for the enemy target-resolution extraction: the Game Adapter's
/// target resolver remains a top-level type with the expected methods so a
/// future kernelization cannot silently merge the responsibility back into
/// <c>EnemyCombatDirector</c>.
/// </summary>
public class EnemyTargetResolverContractTests
{
	private static readonly Type Resolver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.EnemyTargetResolver",
		throwOnError: true)!;

	private static readonly Type Target = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.EnemyTarget",
		throwOnError: true)!;

	[Fact]
	public void Resolver_ExposesTheExpectedTargetMethods()
	{
		var methods = Resolver.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

		Assert.Contains(methods, m => m.Name == "BuildCandidates");
		Assert.Contains(methods, m => m.Name == "Find");
		Assert.Contains(methods, m => m.Name == "Facts");
		Assert.Contains(methods, m => m.Name == "SelectLimbIndex");
		Assert.Contains(methods, m => m.Name == "LocalBody");
	}

	[Fact]
	public void Target_IsATopLevelDataCarrierWithToFact()
	{
		var methods = Target.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.Contains(methods, m => m.Name == "ToFact");
	}
}
