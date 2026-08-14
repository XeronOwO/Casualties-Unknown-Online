using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The shared reflection host for the game-assembly contract tests: loads
/// the game DLLs (copied from references/ to the test output — see the csproj
/// and references/README.md) plus the adapter's own output, with an
/// AssemblyResolve that pulls any module from beside the test output (the
/// Unity game assemblies reference the netstandard 2.1 facade and split
/// UnityEngine modules — a resolution miss fails the load loudly). MISSING
/// references are a FAILURE with the copy instructions, never a silent skip:
/// a silently-skipped contract test is no guard at all. Shared by the
/// patch-contract tests (PatchInventory.BuildContracts) and the game-field
/// contract tests (the Traverse-accessed field/property types).
/// </summary>
internal static class GameAssemblyHost
{
	private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

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

	private static readonly Lazy<Assembly> GameLazy = new(() => Load());
	private static readonly Lazy<Assembly> AdapterLazy = new(() => Load("CasualtiesUnknownOnline.GameAdapter.dll"));

	internal static Assembly Game => GameLazy.Value;

	internal static Assembly Adapter => AdapterLazy.Value;

	/// <summary>Resolve a type by name for the contract tables: the game assembly
	/// first, then every loaded assembly (the game's references load the Unity
	/// modules on demand), then the module DLLs beside the test output
	/// (UnityEngine*.dll — the type's module may be any of the split assemblies).
	/// Nested types use the "+" form ("WorldGeneration+OverrideSceneType").</summary>
	internal static Type? ResolveType(string name)
	{
		_ = Game; // ensure the host + the AssemblyResolve fallback are up
		var found = Game.GetType(name, throwOnError: false);
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

		foreach (var file in Directory.GetFiles(BaseDir, "UnityEngine*.dll"))
		{
			found = Assembly.LoadFrom(file).GetType(name, throwOnError: false);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static Assembly Load(string? specific = null)
	{
		var missing = RequiredDlls.Where(f => !File.Exists(Path.Combine(BaseDir, f))).ToList();
		if (missing.Count > 0)
		{
			throw new InvalidOperationException("Contract-test prerequisites missing — copy them per references/README.md (from the game's Managed/BepInEx folders into references/, then rebuild): "
				+ string.Join(", ", missing));
		}

		AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
		{
			var name = new AssemblyName(args.Name).Name;
			var path = Path.Combine(BaseDir, name + ".dll");
			return File.Exists(path) ? Assembly.LoadFrom(path) : null;
		};

		return Assembly.LoadFrom(Path.Combine(BaseDir, specific ?? "Assembly-CSharp.dll"));
	}
}
