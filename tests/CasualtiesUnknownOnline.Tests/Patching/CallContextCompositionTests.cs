using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The call-identity composition contract behind ticket
/// <c>backlog/todo/unhooked-damage-block-callers.md</c> row 4. CallContext states
/// WHO mutates the scene through a scope stack, and the guards that keep a remote
/// application from being reported as the local player's action read it. The
/// stack nests (a remote apply runs the game's damage roll and the roll pushes
/// <c>DamageBlockOrigin</c>, an inventory load pushes <c>InternalReorder</c>, a
/// craft pushes <c>Craft</c>), and <c>Current</c> answers the INNERMOST origin —
/// so attribution must use the chain query: an outer RemoteApply has to stay
/// visible under any classification sub-scope. That masking is what leaked the
/// receiving side's presentation write back to the host (batch `20261002-c`,
/// row 4), and <c>Current</c> keeps its innermost answer for the classification
/// guards. The class invokes production code that mutates a process-global
/// static (the scope stack), so it joins the non-parallel GameAssembly
/// collection — the suite's own rule for that blind spot.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class CallContextCompositionTests
{
	private static readonly Type CallContext = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.CallContext",
		throwOnError: true)!;

	private static readonly Type Origin = CallContext.GetNestedType("Origin", BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CallContext.Origin not found.");

	private static readonly MethodInfo Enter = CallContext.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CallContext.Enter not found.");

	private static readonly PropertyInfo Current = CallContext.GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CallContext.Current not found.");

	/// <summary>Resolved per test so the missing contract fails the test with its own message rather than a type-initializer trace.</summary>
	private static MethodInfo RequireIsWithin() =>
		CallContext.GetMethod("IsWithin", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException(
			"CallContext must declare the compositional attribution query `internal static bool IsWithin(Origin origin)`: "
			+ "a nested classification sub-scope hides the caller's RemoteApply from Current (batch 20261002-c, row 4).");

	[Fact]
	public void Current_StillAnswersTheInnermostOrigin()
	{
		using (EnterScope("RemoteApply"))
		using (EnterScope("DamageBlockOrigin"))
		{
			Assert.Equal("DamageBlockOrigin", CurrentOriginName());
		}
	}

	[Fact]
	public void TheRemoteApplyAttribution_SurvivesANestedClassificationScope()
	{
		var isWithin = RequireIsWithin();

		using (EnterScope("RemoteApply"))
		using (EnterScope("DamageBlockOrigin"))
		{
			Assert.Equal("DamageBlockOrigin", CurrentOriginName());
			Assert.True(
				(bool)isWithin.Invoke(null, [OriginValue("RemoteApply")])!,
				"an outer RemoteApply must stay visible under a nested classification sub-scope");
			Assert.False(
				(bool)isWithin.Invoke(null, [OriginValue("Craft")])!,
				"an origin that is not in the chain must answer false");
		}
	}

	[Fact]
	public void TheAttributionQuery_IsFalseOutsideRemoteApply()
	{
		var isWithin = RequireIsWithin();

		using (EnterScope("DamageBlockOrigin"))
		{
			Assert.False(
				(bool)isWithin.Invoke(null, [OriginValue("RemoteApply")])!,
				"a local damage roll is not a remote application");
		}

		Assert.True(
			(bool)isWithin.Invoke(null, [OriginValue("LocalAction")])!,
			"with no scope open the implicit origin is LocalAction");
	}

	[Fact]
	public void Dispose_RestoresTheEnclosingScope()
	{
		var isWithin = RequireIsWithin();

		using (EnterScope("RemoteApply"))
		{
			using (EnterScope("DamageBlockOrigin"))
			{
			}

			Assert.Equal("RemoteApply", CurrentOriginName());
			Assert.True((bool)isWithin.Invoke(null, [OriginValue("RemoteApply")])!);
		}

		Assert.Equal("LocalAction", CurrentOriginName());
		Assert.False((bool)isWithin.Invoke(null, [OriginValue("RemoteApply")])!);
	}

	private static IDisposable EnterScope(string origin) =>
		(IDisposable)Enter.Invoke(null, [OriginValue(origin)])!;

	private static object OriginValue(string origin) => Enum.Parse(Origin, origin);

	private static string CurrentOriginName() => Current.GetValue(null)!.ToString()!;
}
