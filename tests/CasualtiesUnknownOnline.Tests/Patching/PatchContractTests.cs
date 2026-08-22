using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Phase-3 contract tests — the game-update guard. Every patch contract the
/// adapter declares (PatchInventory.BuildContracts — the SAME facts the
/// runtime verification consumes) must resolve against the game assembly with
/// the exact signature. The game assemblies are loaded REFLECTIVELY from the
/// test output (copied from references/ — see the csproj and
/// references/README.md): a game update means re-copying the updated DLLs
/// into references/ and running `dotnet test` — every broken contract is
/// named BEFORE the game launches. MISSING references are a FAILURE, never a
/// skip: a silently-skipped contract test is no guard at all.
/// </summary>
public class PatchContractTests
{
	private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

	/// <summary>The adapter's own contract extraction — the single source of
	/// facts the runtime verification and these tests share. The reflection
	/// host is the shared GameAssemblyHost (the game DLLs beside the test
	/// output; missing references are a FAILURE with the copy instructions,
	/// never a silent skip).</summary>
	private static List<PatchContract> BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found in the adapter assembly.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (List<PatchContract>)build.Invoke(null, null)!;
	}

	/// <summary>The test-side resolver, mirroring the runtime's AccessTools
	/// semantics: exact argument types first, then a deterministic single
	/// name-only match. A same-name overload pair can no longer be silently
	/// resolved to an arbitrary method: a constrained contract must resolve by
	/// its exact parameter types (no name-only fallback), and an unconstrained
	/// contract against a multi-overload target is ambiguous and fails loudly
	/// with instructions to add the argument types. The target type may live
	/// OUTSIDE the game assembly (a UnityEngine type — the SceneManager
	/// scene-load patch): any loaded assembly first, then the module DLL beside
	/// the test output (the Unity modules are split assemblies; the game's
	/// references load them on demand).</summary>
	private static MethodInfo? Resolve(PatchContract contract)
	{
		var type = GameAssemblyHost.Game.GetType(contract.TargetType) ?? ResolveExternalType(contract.TargetType);
		if (type == null)
		{
			return null;
		}

		if (contract.ParameterTypes.Count > 0)
		{
			var types = contract.ParameterTypes.Select(ResolveType).ToArray();
			return type.GetMethod(contract.MethodName,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
				null, types, null);
		}

		var sameName = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
			.Where(m => m.Name == contract.MethodName)
			.ToArray();
		return sameName.Length switch
		{
			0 => null,
			1 => sameName[0],
			_ => throw new InvalidOperationException(
				$"ambiguous patch contract: {contract.TargetType}.{contract.MethodName} has {sameName.Length} overloads; " +
				"add the [HarmonyPatch] argumentTypes (or the parameter types in a hand-declared dynamic contract) to disambiguate."),
		};
	}

	/// <summary>A contract target type that lives outside the game assembly
	/// (a UnityEngine type — the SceneManager patch): every loaded assembly
	/// first (the game's own references load the Unity modules on demand),
	/// then the module DLLs beside the test output (UnityEngine*.dll — the
	/// type's module may be any of the split assemblies).</summary>
	private static Type? ResolveExternalType(string name)
	{
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var found = assembly.GetType(name, throwOnError: false);
			if (found != null)
			{
				return found;
			}
		}

		foreach (var file in Directory.GetFiles(BaseDir, name.Split('.')[0] + "*.dll"))
		{
			var found = Assembly.LoadFrom(file).GetType(name, throwOnError: false);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	/// <summary>Resolve a contract parameter type: system types via the global
	/// binder (mscorlib/System), then every loaded assembly (a UnityEngine
	/// module loads on demand), then the game assembly itself — the type may
	/// live in any of them ("System.String", "Item", "UnityEngine.Vector2"…).</summary>
	private static Type ResolveType(string name)
	{
		var found = Type.GetType(name);
		if (found != null)
		{
			return found;
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			found = assembly.GetType(name, throwOnError: false);
			if (found != null)
			{
				return found;
			}
		}

		// The type's assembly is not loaded yet — load it by the first name
		// segment (UnityEngine.Vector2 → UnityEngine.dll beside the test output).
		var assemblyName = name.Split('.')[0];
		var path = Path.Combine(BaseDir, assemblyName + ".dll");
		if (File.Exists(path))
		{
			found = Assembly.LoadFrom(path).GetType(name, throwOnError: false);
			if (found != null)
			{
				return found;
			}
		}

		return GameAssemblyHost.Game.GetType(name, throwOnError: false)
			?? throw new InvalidOperationException($"Contract parameter type '{name}' not found.");
	}

	[Fact]
	public void Contracts_CoverEveryAttributedPatchClass_PlusTheDynamicOnes()
	{
		var contracts = BuildContracts();
		// Harmony declares the attribute as `class HarmonyPatch : Attribute` —
		// the CLR name has NO "Attribute" suffix.
		var attributed = GameAssemblyHost.Adapter.GetTypes().Count(t =>
			t.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"));

		Assert.True(contracts.Count == attributed + 8,
			$"the contract inventory must cover every [HarmonyPatch] class ({attributed}) plus the 8 dynamic patches (InstallDynamicPatches) — got {contracts.Count}");
	}

	[Fact]
	public void EveryContract_ResolvesWithExactSignature()
	{
		var violations = new List<string>();
		foreach (var contract in BuildContracts())
		{
			violations.AddRange(PatchContractChecker.Check(contract, Resolve(contract)));
		}

		Assert.True(violations.Count == 0,
			$"broken patch contracts against the game assembly ({violations.Count}):\n" + string.Join("\n", violations));
	}

	/// <summary>
	/// The CrystalMimic patch surface: the two public CrystalBehaviour
	/// dispatchers whose false→true activated edge the mimic event reports on.
	/// A regression that deletes one hook fails here before the game launches.
	/// </summary>
	[Fact]
	public void CrystalMimicPatchSet_IsComplete()
	{
		var contracts = BuildContracts();
		var expected = new[]
		{
			("CrystalBehaviour", "OnCollisionEnter2D"),
			("CrystalBehaviour", "BuildingHit"),
		};
		var missing = new List<string>();
		foreach (var (type, method) in expected)
		{
			if (!contracts.Any(c => c.TargetType == type && c.MethodName == method))
			{
				missing.Add($"{type}.{method}");
			}
		}

		Assert.True(missing.Count == 0,
			$"CrystalMimic patch surface is incomplete ({missing.Count}):" + Environment.NewLine + string.Join(Environment.NewLine, missing));
	}

	/// <summary>
	/// The enemy-combat patch surface is the replacement for dual-open manual
	/// acceptance of the hook layer: every patch the host-ordered attack chain
	/// depends on must exist, and SpiderHandler.Update must carry BOTH the
	/// guest freeze prefix and the host target-guidance prefix/postfix. A
	/// regression that deletes one hook fails here before the game launches.
	/// </summary>
	[Fact]
	public void EnemyCombatPatchSet_IsComplete()
	{
		var contracts = BuildContracts();
		var expected = new[]
		{
			("SpiderHandler", "DamageLimb"),
			("SpiderHandlerTBE", "DamageLimb"),
			("SpiderHandler", "FixedUpdate"),
			("SpiderHandler", "OnCollisionStay2D"),
			("SpiderHandler", "OnCollisionEnter2D"),
			("CrystalEnemy", "Update"),
			("CrystalEnemy", "FixedUpdate"),
			("CrystalEnemy", "get_body"),
			("CrystalEnemy", "Lunge"),
		};
		var missing = new List<string>();
		foreach (var (type, method) in expected)
		{
			var count = contracts.Count(c => c.TargetType == type && c.MethodName == method);
			if (count == 0)
			{
				missing.Add($"{type}.{method}");
			}
		}

		var updateCount = contracts.Count(c => c.TargetType == "SpiderHandler" && c.MethodName == "Update");
		if (updateCount < 2)
		{
			missing.Add($"SpiderHandler.Update (freeze + target guidance) — got {updateCount} patch class(es)");
		}

		var itemHitCount = contracts.Count(c =>
			c.TargetType == "SpiderHandler" && c.MethodName == "OnCollisionEnter2D");
		if (itemHitCount < 2)
		{
			missing.Add($"SpiderHandler.OnCollisionEnter2D (freeze + item-hit) — got {itemHitCount} patch class(es)");
		}

		Assert.True(missing.Count == 0,
			$"enemy-combat patch surface is incomplete ({missing.Count}):\n" + string.Join("\n", missing));
	}

	/// <summary>
	/// The enemy-proximity + host-local-lunge patch surface: every hook the
	/// dedicated EnemyEffect / host-local EnemyLunge chains depend on must
	/// exist. A regression that deletes one hook fails here before the game
	/// launches.
	/// </summary>
	[Fact]
	public void EnemyProximityPatchSet_IsComplete()
	{
		var contracts = BuildContracts();
		var expected = new[]
		{
			("ElderThornbackBehaviour", "Update"),
			("ElderThornbackBehaviour", "OnDestroy"),
			("XalorisScript", "OnWillRenderObject"),
			("GrabberPlant", "Update"),
			("CrystalEnemy", "Lunge"),
		};
		var missing = new List<string>();
		foreach (var (type, method) in expected)
		{
			var count = contracts.Count(c => c.TargetType == type && c.MethodName == method);
			if (count == 0)
			{
				missing.Add($"{type}.{method}");
			}
		}

		Assert.True(missing.Count == 0,
			$"enemy-proximity patch surface is incomplete ({missing.Count}):\n" + string.Join("\n", missing));
	}

	/// <summary>
	/// The unstable-crystal tick surface: the two dynamic hooks that drive the
	/// 5 s pre-explosion ticking sync — StartTimer (the timerStarted false→true
	/// edge, reported as CrystalUnstableTicked) and Update (the timer>5 explosion
	/// edge, CrystalUnstableExploded). A regression that deletes one hook leaves
	/// half the chain silent — fails here before the game launches.
	/// </summary>
	[Fact]
	public void CrystalUnstableTickingPatchSet_IsComplete()
	{
		var contracts = BuildContracts();
		var expected = new[]
		{
			("CrystalUnstable", "StartTimer"),
			("CrystalUnstable", "Update"),
		};
		var missing = new List<string>();
		foreach (var (type, method) in expected)
		{
			if (!contracts.Any(c => c.TargetType == type && c.MethodName == method))
			{
				missing.Add($"{type}.{method}");
			}
		}

		Assert.True(missing.Count == 0,
			$"CrystalUnstable ticking patch surface is incomplete ({missing.Count}):" + Environment.NewLine + string.Join(Environment.NewLine, missing));
	}

	// ---- PatchContractChecker verdict unit tests	}

	// ---- PatchContractChecker verdict unit tests (against this assembly's own methods) ----

	private static MethodInfo FixtureMethod(string name) =>
		typeof(Fixtures).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

	[Fact]
	public void Check_MatchingContract_Passes()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Target", ["System.Int32", "System.String"], ["alpha", "beta"]);

		Assert.Empty(PatchContractChecker.Check(contract, FixtureMethod("Target")));
	}

	[Fact]
	public void Check_UnconstrainedContract_Passes()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "TargetNoParams", [], []);

		Assert.Empty(PatchContractChecker.Check(contract, FixtureMethod("TargetNoParams")));
	}

	[Fact]
	public void Check_MissingTarget_Reports()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Missing", [], []);

		var violations = PatchContractChecker.Check(contract, null);
		Assert.True(violations.Count == 1 && violations[0].Contains("not found"),
			violations.Count > 0 ? violations[0] : "no violation");
	}

	[Fact]
	public void Check_ParameterCountMismatch_Reports()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Target", ["System.Int32"], []);

		var violations = PatchContractChecker.Check(contract, FixtureMethod("Target"));
		Assert.True(violations.Count == 1 && violations[0].Contains("expects 1 parameter(s), game has 2"),
			violations.Count > 0 ? violations[0] : "no violation");
	}

	[Fact]
	public void Check_ParameterTypeMismatch_Reports()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Target", ["System.Int32", "System.Boolean"], []);

		var violations = PatchContractChecker.Check(contract, FixtureMethod("Target"));
		Assert.True(violations.Count == 1 && violations[0].Contains("parameter[1] type mismatch"),
			violations.Count > 0 ? violations[0] : "no violation");
	}

	[Fact]
	public void Check_ParameterRenamed_Reports()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Target", [], ["gamma"]);

		var violations = PatchContractChecker.Check(contract, FixtureMethod("Target"));
		Assert.True(violations.Count == 1 && violations[0].Contains("patch parameter 'gamma' missing"),
			violations.Count > 0 ? violations[0] : "no violation");
	}

	[Fact]
	public void Resolve_ConstrainedContract_SelectsTheExactOverload()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Overloaded", ["System.Int32"], []);

		var resolved = Resolve(contract);

		Assert.NotNull(resolved);
		Assert.Equal(typeof(int), resolved!.GetParameters()[0].ParameterType);
		Assert.Empty(PatchContractChecker.Check(contract, resolved));
	}

	[Fact]
	public void Resolve_UnconstrainedContractAgainstOverloads_ThrowsAmbiguous()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Overloaded", [], []);

		var ex = Assert.Throws<InvalidOperationException>(() => Resolve(contract));

		Assert.Contains("ambiguous", ex.Message);
		Assert.Contains("argumentTypes", ex.Message);
	}

	[Fact]
	public void Resolve_ExactTypeMismatch_DoesNotFallBackToNameOnly()
	{
		var contract = new PatchContract("t", typeof(Fixtures).FullName!, "Overloaded", ["System.Single"], []);

		Assert.Null(Resolve(contract));
	}

	private static class Fixtures
	{
		internal static void Target(int alpha, string beta)
		{
		}

		internal static void TargetNoParams()
		{
		}

		internal static void Overloaded(int value)
		{
		}

		internal static void Overloaded(string value)
		{
		}
	}
}
