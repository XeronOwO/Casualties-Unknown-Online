using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Patching;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Capabilities;

/// <summary>
/// The capability-catalog gate (ticket stage 1). The catalog is the one place
/// that says which gameplay system a patch belongs to, and the report it feeds is
/// only as honest as its coverage — so this gate enumerates the adapter
/// assembly's <c>[HarmonyPatch]</c> classes itself (an expansion independent of
/// the production one) and demands that the catalog claims every one of them
/// exactly once, that every declared owner still contributes patch classes, and
/// that the hand-declared dynamic rows are claimed by pseudo name. The adapter is
/// loaded reflectively (the test project never compile-references it), which is
/// why the catalog is reached through reflection.
/// </summary>
[Trait("Category", "Integration")]
public class AdapterCapabilityCatalogTests
{
	private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
	private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
	private const string CatalogTypeName = "CasualtiesUnknownOnline.GameAdapter.Capabilities.AdapterCapabilityCatalog";
	private const string ProbeTypeName = "CasualtiesUnknownOnline.GameAdapter.Capabilities.AdapterCapabilityProbe";
	private const string DynamicSuffix = " (dynamic)";
	private const string ProbeLine = "PlayerCamera/Body/PreRunScript/WorldGeneration: OK";

	[Fact]
	public void EveryPatchClass_IsClaimedByExactlyOneCapability()
	{
		var declared = new List<Type>();
		var claimedTwice = new List<string>();
		foreach (var definition in Declared())
		{
			foreach (var patchClass in Expand(OwnersOf(definition)))
			{
				if (declared.Contains(patchClass))
				{
					claimedTwice.Add($"{patchClass.FullName} (again in '{IdOf(definition)}')");
				}

				declared.Add(patchClass);
			}
		}

		var unclaimed = PatchClasses().Where(patchClass => !declared.Contains(patchClass))
			.Select(patchClass => patchClass.FullName ?? patchClass.Name)
			.ToList();

		Assert.True(unclaimed.Count == 0, "patch classes no capability claims: " + string.Join(", ", unclaimed));
		Assert.True(claimedTwice.Count == 0, "patch classes claimed by more than one capability: " + string.Join(", ", claimedTwice));
		Assert.Equal(PatchClasses().Count, declared.Count);
	}

	[Fact]
	public void EveryDeclaredOwner_ContributesPatchClasses()
	{
		var barren = new List<string>();
		foreach (var definition in Declared())
		{
			var owners = OwnersOf(definition);
			Assert.True(owners.Count > 0, $"capability '{IdOf(definition)}' declares no patch owner at all");
			foreach (var owner in owners)
			{
				if (!Expand([owner]).Any())
				{
					barren.Add($"{owner.FullName} ('{IdOf(definition)}')");
				}
			}
		}

		Assert.True(barren.Count == 0, "declared owners that carry no [HarmonyPatch] class: " + string.Join(", ", barren));
	}

	[Fact]
	public void CapabilityIds_AreUniqueStableAndClassified()
	{
		var ids = new List<string>();
		foreach (var definition in Declared())
		{
			var id = IdOf(definition);
			Assert.False(string.IsNullOrWhiteSpace(id), "a capability declares an empty id");
			Assert.Matches("^[a-z][a-z0-9-]*$", id);
			Assert.False(string.IsNullOrWhiteSpace(TitleOf(definition)), $"capability '{id}' has no title");
			Assert.True(Enum.IsDefined(typeof(AdapterCapabilityKind), KindOf(definition)), $"capability '{id}' has no Required/Optional class");
			ids.Add(id);
		}

		Assert.True(ids.Count >= 10, $"the catalog declares only {ids.Count} capabilities — too coarse to name a broken gameplay system");
		Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
	}

	[Fact]
	public void ContractRows_AllJoinToACapability()
	{
		var contracts = BuildContracts();
		var attributed = contracts.Count(contract => !contract.PatchClass.EndsWith(DynamicSuffix, StringComparison.Ordinal));
		var dynamic = contracts.Count(contract => contract.PatchClass.EndsWith(DynamicSuffix, StringComparison.Ordinal));

		Assert.Equal(CountTargets(), attributed);
		Assert.Equal(9, dynamic);
		Assert.Equal(CountTargets(), PatchClasses().Count);

		var orphans = contracts
			.Where(contract => OwnerOf(contract) is null)
			.Select(contract => $"{contract.PatchClass} → {contract.TargetType}.{contract.MethodName}")
			.ToList();
		Assert.True(orphans.Count == 0, "contract rows no capability claims: " + string.Join(", ", orphans));
	}

	[Fact]
	public void DynamicRows_AreDeclaredExactlyOnce()
	{
		var rows = BuildContracts()
			.Where(contract => contract.PatchClass.EndsWith(DynamicSuffix, StringComparison.Ordinal))
			.Select(contract => contract.PatchClass)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToList();

		var declared = Declared().SelectMany(DynamicOf).ToList();
		Assert.Equal(rows, declared.OrderBy(name => name, StringComparer.Ordinal).ToList());
		Assert.Equal(declared.Count, declared.Distinct(StringComparer.Ordinal).Count());
	}

	[Fact]
	public void SessionCapability_CarriesTheGameProbeTypes()
	{
		var types = (IReadOnlyList<Type>)Catalog().GetProperty("GameProbeTypes", AnyStatic)!.GetValue(null)!;
		string[] expected = ["PlayerCamera", "Body", "PreRunScript", "WorldGeneration"];
		Assert.Equal(expected, types.Select(type => type.Name).ToArray());
	}

	/// <summary>
	/// The behaviour-preservation pin for the probe: stage 1 moved the four
	/// <c>typeof</c> reads into the patch life cycle so the catalog could declare
	/// them, and the verdict and the report text the game has always logged must
	/// come out unchanged.
	/// </summary>
	[Fact]
	public void ProbeGame_KeepsItsVerdictAndReportText()
	{
		var lifecycle = Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInstallLifecycle")
			?? throw new InvalidOperationException("PatchInstallLifecycle not found in the adapter assembly.");
		var probe = lifecycle.GetMethod("ProbeGame", AnyStatic)
			?? throw new InvalidOperationException("PatchInstallLifecycle.ProbeGame not found.");
		var arguments = new object?[] { null };

		var ok = (bool)probe.Invoke(null, arguments)!;

		Assert.True(ok);
		Assert.Equal("PlayerCamera/Body/PreRunScript/WorldGeneration: OK", (string)arguments[0]!);
	}

	[Fact]
	public void Probe_AccountsForEveryContractRow()
	{
		var report = RunProbe([], ProbeLine);

		Assert.Equal(BuildContracts().Count, report.ContractCount);
		Assert.Equal(CountTargets(), report.PatchContracts);
		Assert.Equal(9, report.DynamicContracts);
		Assert.Empty(report.UnmappedFailures);
		Assert.False(report.RefusesSession);
		Assert.All(report.Statuses, status => Assert.False(status.Failed, $"{status.Id} reports a failure on an intact tree"));
	}

	[Fact]
	public void Probe_NamesTheBrokenCapabilityAndItsReason()
	{
		var traps = Declared().Single(definition => IdOf(definition) == "traps");
		var patchClass = Expand(OwnersOf(traps)).First();
		var failure = new PatchVerificationFailure(
			patchClass.Name,
			patchClass.FullName ?? patchClass.Name,
			"SyntheticTarget.Missing not applied",
			blocksInstall: true);

		var report = RunProbe([failure], ProbeLine);
		var text = report.Render();

		Assert.Contains("[Required] traps", text, StringComparison.Ordinal);
		Assert.Contains("SyntheticTarget.Missing not applied", text, StringComparison.Ordinal);
		Assert.True(report.Statuses.Single(status => status.Id == "traps").Failed);
		Assert.True(report.RefusesSession);
	}

	[Fact]
	public void Probe_DynamicFailure_IsReportedWithoutRefusingInStageOne()
	{
		var traps = Declared().Single(definition => IdOf(definition) == "traps");
		var dynamicPatchClass = DynamicOf(traps).First();
		var failure = new PatchVerificationFailure(
			dynamicPatchClass,
			dynamicPatchClass,
			"CrystalFragile.Touched not found — the fragile-crystal break sync is off",
			blocksInstall: false);

		var report = RunProbe([failure], ProbeLine);
		var text = report.Render();

		Assert.True(report.Statuses.Single(status => status.Id == "traps").Failed);
		Assert.Contains("CrystalFragile.Touched not found", text, StringComparison.Ordinal);
		Assert.Contains("reported only", text, StringComparison.Ordinal);
		Assert.False(report.RefusesSession);
	}

	/// <summary>
	/// The dynamic installer's target table is the same declaration the dynamic
	/// contract rows are DERIVED from, so the drift to prove gone is a row with no
	/// binding. What remains is resolution: these targets live on game types with
	/// no compile-time reference (<c>internal</c>), so only reflection can say they
	/// are still there — and a missing one must fail here, before the game runs.
	/// </summary>
	[Fact]
	public void EveryDeclaredDynamicTarget_ResolvesAgainstTheGameAssembly()
	{
		var targets = DynamicTargets();

		Assert.Equal(9, targets.Count);
		foreach (var target in targets)
		{
			var typeName = Property<string>(target, "TypeName");
			var methodName = Property<string>(target, "MethodName");
			var flags = (Property<bool>(target, "NonPublic") ? BindingFlags.NonPublic : BindingFlags.Public) | BindingFlags.Instance;
			var type = GameAssemblyHost.Game.GetType(typeName, throwOnError: false);

			Assert.True(type is not null, $"dynamic target type {typeName} is gone from the game assembly");
			Assert.True(
				type!.GetMethod(methodName, flags) is not null,
				$"dynamic target {typeName}.{methodName} is gone from the game assembly");
		}
	}

	private static Assembly Adapter => GameAssemblyHost.Adapter;

	/// <summary>
	/// The wording the dynamic installer logged target by target before the
	/// targets became one table — a game-update reader matches these lines against
	/// logs, so the table must render them unchanged (the four shapes: name the
	/// type or type.method, as a target or as a method).
	/// </summary>
	[Theory]
	[InlineData(0, true, "Dynamic patch target CrystalFragile not found — the fragile-crystal break sync is off.")]
	[InlineData(0, false, "Dynamic patch method CrystalFragile.Touched not found — the fragile-crystal break sync is off.")]
	[InlineData(1, true, "Dynamic patch target CrystalElectric not found — the electric-crystal shock sync is off.")]
	[InlineData(2, true, "Dynamic patch target CrystalTeleport.Touched not found — the crystal sync is off.")]
	[InlineData(3, true, "Dynamic patch target CrystalDripping not found — the guest-side drip suppression is off.")]
	[InlineData(3, false, "Dynamic patch target CrystalDripping not found — the guest-side drip suppression is off.")]
	[InlineData(8, false, "Dynamic patch method CrystalUnstable.StartTimer not found — the crystal ticking visual sync is off.")]
	public void DynamicMissMessages_KeepTheirOriginalWording(int index, bool typeMissing, string expected)
	{
		var installer = Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.DynamicPatchInstaller")
			?? throw new InvalidOperationException("DynamicPatchInstaller not found in the adapter assembly.");
		var render = installer.GetMethod("MissingMessage", AnyStatic)
			?? throw new InvalidOperationException("DynamicPatchInstaller.MissingMessage not found.");

		Assert.Equal(expected, (string)render.Invoke(null, [DynamicTargets()[index], typeMissing])!);
	}

	private static IReadOnlyList<object> DynamicTargets()
	{
		var installer = Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.DynamicPatchInstaller")
			?? throw new InvalidOperationException("DynamicPatchInstaller not found in the adapter assembly.");
		var targets = installer.GetProperty("Targets", AnyStatic)?.GetValue(null) as IEnumerable
			?? throw new InvalidOperationException("DynamicPatchInstaller.Targets not found or not enumerable.");
		return targets.Cast<object>().ToList();
	}

	private static Type Catalog() =>
		Adapter.GetType(CatalogTypeName) ?? throw new InvalidOperationException($"{CatalogTypeName} not found in the adapter assembly.");

	private static IEnumerable<object> Declared()
	{
		var all = Catalog().GetProperty("All", AnyStatic)?.GetValue(null) as IEnumerable
			?? throw new InvalidOperationException("AdapterCapabilityCatalog.All not found or not enumerable.");
		return all.Cast<object>().ToList();
	}

	private static string IdOf(object definition) => Property<string>(definition, "Id");

	private static string TitleOf(object definition) => Property<string>(definition, "Title");

	private static AdapterCapabilityKind KindOf(object definition) => Property<AdapterCapabilityKind>(definition, "Kind");

	private static IReadOnlyList<Type> OwnersOf(object definition) => Property<IReadOnlyList<Type>>(definition, "PatchOwners");

	private static IReadOnlyList<string> DynamicOf(object definition) => Property<IReadOnlyList<string>>(definition, "DynamicPatchClasses");

	private static T Property<T>(object instance, string name) =>
		(T)(instance.GetType().GetProperty(name, AnyInstance)?.GetValue(instance)
			?? throw new InvalidOperationException($"{instance.GetType().Name}.{name} not found."));

	/// <summary>The test's own expansion: a type is a patch class when it carries the <c>[HarmonyPatch]</c> attribute (matched by name — the test project sees HarmonyLib only under an extern alias), and a container contributes its nested classes.</summary>
	private static IEnumerable<Type> Expand(IEnumerable<Type> owners)
	{
		foreach (var owner in owners)
		{
			if (IsPatchClass(owner))
			{
				yield return owner;
			}

			foreach (var patchClass in Expand(owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)))
			{
				yield return patchClass;
			}
		}
	}

	private static bool IsPatchClass(Type type) =>
		type.GetCustomAttributes(inherit: false).Any(attribute => attribute.GetType().FullName == "HarmonyLib.HarmonyPatch");

	private static List<Type> PatchClasses() => Adapter.GetTypes().Where(IsPatchClass).ToList();

	private static string? OwnerOf(PatchContract contract)
	{
		var catalog = Catalog();
		var byPatchClass = catalog.GetMethod("OwnerOfPatchClass", AnyStatic)
			?? throw new InvalidOperationException("AdapterCapabilityCatalog.OwnerOfPatchClass not found.");
		var byDynamicRow = catalog.GetMethod("OwnerOfDynamicPatchClass", AnyStatic)
			?? throw new InvalidOperationException("AdapterCapabilityCatalog.OwnerOfDynamicPatchClass not found.");

		return (string?)(byPatchClass.Invoke(null, [contract.PatchClassType])
			?? byDynamicRow.Invoke(null, [contract.PatchClass]));
	}

	private static AdapterCapabilityReport RunProbe(
		IReadOnlyList<PatchVerificationFailure> failures,
		string gameProbe)
	{
		var probe = Adapter.GetType(ProbeTypeName) ?? throw new InvalidOperationException($"{ProbeTypeName} not found in the adapter assembly.");
		var build = probe.GetMethod("Build", AnyStatic) ?? throw new InvalidOperationException("AdapterCapabilityProbe.Build not found.");
		return (AdapterCapabilityReport)build.Invoke(null, [failures, gameProbe])!;
	}

	private static List<PatchContract> BuildContracts()
	{
		var inventory = Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found in the adapter assembly.");
		var build = inventory.GetMethod("BuildContracts", AnyStatic)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (List<PatchContract>)build.Invoke(null, null)!;
	}

	private static int CountTargets()
	{
		var inventory = Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found in the adapter assembly.");
		var count = inventory.GetMethod("CountTargets", AnyStatic)
			?? throw new InvalidOperationException("PatchInventory.CountTargets not found.");
		return (int)count.Invoke(null, null)!;
	}
}
