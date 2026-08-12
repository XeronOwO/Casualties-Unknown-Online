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

	/// <summary>The reflection host must resolve every assembly the adapter
	/// binds to — the game DLLs (references/), Harmony, and the adapter's own
	/// output. Any missing file fails the load (with the copy instructions),
	/// never silently skips.</summary>
	private static readonly string[] RequiredDlls =
	[
		"Assembly-CSharp.dll", "UnityEngine.dll", "UnityEngine.CoreModule.dll",
		"UnityEngine.SharedInternalsModule.dll", "UnityEngine.SubsystemsModule.dll",
		"UnityEngine.UI.dll", "UnityEngine.InputLegacyModule.dll",
		"UnityEngine.AudioModule.dll", "UnityEngine.IMGUIModule.dll",
		"UnityEngine.Physics2DModule.dll", "UnityEngine.AnimationModule.dll",
		"UnityEngine.TilemapModule.dll", "UnityEngine.ParticleSystemModule.dll",
		"0Harmony.dll", "CasualtiesUnknownOnline.GameAdapter.dll",
	];

	private static Assembly? _game;

	private static Assembly? _adapter;

	private static void EnsureLoaded()
	{
		if (_adapter != null)
		{
			return;
		}

		var missing = RequiredDlls.Where(f => !File.Exists(Path.Combine(BaseDir, f))).ToList();
		if (missing.Count > 0)
		{
			Assert.True(false, "Contract-test prerequisites missing — copy them per references/README.md (from the game's Managed/BepInEx folders into references/, then rebuild): "
				+ string.Join(", ", missing));
		}

		AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
		{
			var name = new AssemblyName(args.Name).Name;
			var path = Path.Combine(BaseDir, name + ".dll");
			return File.Exists(path) ? Assembly.LoadFrom(path) : null;
		};

		_game = Assembly.LoadFrom(Path.Combine(BaseDir, "Assembly-CSharp.dll"));
		_adapter = Assembly.LoadFrom(Path.Combine(BaseDir, "CasualtiesUnknownOnline.GameAdapter.dll"));
	}

	/// <summary>The adapter's own contract extraction — the single source of
	/// facts the runtime verification and these tests share.</summary>
	private static List<PatchContract> BuildContracts()
	{
		EnsureLoaded();
		var inventory = _adapter!.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found in the adapter assembly.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (List<PatchContract>)build.Invoke(null, null)!;
	}

	/// <summary>The test-side resolver, mirroring the runtime's AccessTools
	/// semantics: exact argument types first, name-only fallback.</summary>
	private static MethodInfo? Resolve(PatchContract contract)
	{
		var type = _game!.GetType(contract.TargetType);
		if (type == null)
		{
			return null;
		}

		if (contract.ParameterTypes.Count > 0)
		{
			var types = contract.ParameterTypes.Select(ResolveType).ToArray();
			var exact = type.GetMethod(contract.MethodName,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
				null, types, null);
			if (exact != null)
			{
				return exact;
			}
		}

		return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
			.FirstOrDefault(m => m.Name == contract.MethodName);
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

		return _game!.GetType(name, throwOnError: false)
			?? throw new InvalidOperationException($"Contract parameter type '{name}' not found.");
	}

	[Fact]
	public void Contracts_CoverEveryAttributedPatchClass_PlusTheDynamicOnes()
	{
		var contracts = BuildContracts();
		// Harmony declares the attribute as `class HarmonyPatch : Attribute` —
		// the CLR name has NO "Attribute" suffix.
		var attributed = _adapter!.GetTypes().Count(t =>
			t.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"));

		Assert.True(contracts.Count == attributed + 3,
			$"the contract inventory must cover every [HarmonyPatch] class ({attributed}) plus the 3 dynamic patches (InstallDynamicPatches) — got {contracts.Count}");
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

	private static class Fixtures
	{
		internal static void Target(int alpha, string beta)
		{
		}

		internal static void TargetNoParams()
		{
		}
	}
}
