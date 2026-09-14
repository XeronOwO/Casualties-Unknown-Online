using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The starting-supplies decision (S4.3): who is a NEW player, what the run's setting
/// hands them, and the two ways the decision can be wrong — supplying a player the world
/// already has a character for, and supplying twice because the game's own first-layer
/// grant already ran.
///
/// Reached by reflection, like every Game Adapter unit here: the test project references
/// the adapter with <c>ExcludeAssets="compile"</c> (the adapter is the only project that
/// may reference the game assemblies, and compiling against it would drag them into every
/// test), so <c>GameAssemblyHost.Adapter</c> is the door. The DECISION is pure, which is
/// what makes these rows possible at all — a coordinator that touched a Unity type could
/// never be driven from this host.
/// </summary>
[Trait("Category", "Integration")]
public class StartingSupplyPolicyTests
{
	// ---- the setting's plan: the game's own items and slots ----

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 1)]
	[InlineData(2, 4)]
	public void PlanFor_MatchesTheGamesOwnSettingTable(int setting, int expectedCount) =>
		// RunSettings.cs:53 — the dropdown is none/light/full, and the game's grant
		// (WorldGeneration.cs:1899-1913) hands one item for light and four for full.
		Assert.Equal(expectedCount, Probe.PlanFor(setting).Count);

	[Fact]
	public void PlanFor_Light_IsTheEmergencyLightInTheGamesSlot()
	{
		var entry = Assert.Single(Probe.PlanFor(1));

		Assert.Equal("emergencylight", entry.ItemId);
		Assert.Equal(3, entry.Slot); // the game's own slot argument
	}

	[Fact]
	public void PlanFor_Full_IsTheGamesFourItemsInTheGamesSlots()
	{
		// The ids and slots are the game's own (WorldGeneration.cs:1904-1907), which is
		// what makes a CUO-supplied player and a natively-supplied player carry the same
		// inventory. A "better" ordering here would silently diverge the two paths.
		Assert.Equal(
			[
				("lantern", 3),
				("dogfood", 4),
				("waterbottle", 5),
				("trashbag", 1),
			],
			Probe.PlanFor(2).Select(entry => (entry.ItemId, entry.Slot)));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(3)]
	[InlineData(99)]
	public void PlanFor_AnUnknownValue_GrantsNothing(int setting) =>
		// The game's own grant has no else-branch: any value other than 1 or 2 hands out
		// nothing at all. Guessing "full" for an unknown value would hand out items the
		// run never configured.
		Assert.Empty(Probe.PlanFor(setting));

	[Theory]
	[InlineData(0, "none")]
	[InlineData(1, "light")]
	[InlineData(2, "full")]
	public void Describe_UsesTheGamesOwnWords(int setting, string expected) =>
		Assert.Equal(expected, Probe.Describe(setting));

	[Fact]
	public void Describe_AnUnknownValue_KeepsTheRawNumber() =>
		Assert.Equal("unknown (7)", Probe.Describe(7));

	// ---- reading the run's setting from the generation baseline ----

	[Fact]
	public void SettingOf_ReadsTheBaseline() =>
		Assert.Equal(2, Probe.SettingOf(Baseline(Setting(2))));

	[Fact]
	public void SettingOf_ARunWithoutSettings_IsNull() =>
		// The tutorial nulls runSettings itself (PreRunScript.cs:312), and a run whose
		// baseline was published without them must not be guessed at.
		Assert.Null(Probe.SettingOf(Baseline(null)));

	[Fact]
	public void SettingOf_ARunWithoutTheSetting_IsNull() =>
		Assert.Null(Probe.SettingOf(Baseline(new Dictionary<string, object> { ["other"] = 5 })));

	[Fact]
	public void SettingOf_NoBaselineAtAll_IsNull() => Assert.Null(Probe.SettingOf(null));

	// ---- the native grant's coverage ----

	[Fact]
	public void NativeGrantCovers_TheRunsFirstLayer_IsTrue() =>
		// WorldGeneration.cs:1891 verbatim: totalTraveled <= 0 && biomeOverride == None
		// && debugStartDepth == 0. On that layer the game itself hands the supplies out,
		// so CUO handing them out again is the duplicate this rule exists to prevent.
		Assert.True(Probe.NativeGrantCovers(Baseline(Full)));

	[Fact]
	public void NativeGrantCovers_ARestoredStartingLayer_IsStillTrue() =>
		// The S4.3 adversarial review's BLOCKER, pinned. A CUO continue lets the native
		// PreRunScript.LoadRun run (PreRunScriptLoadRunPatch), which loads the scene and walks
		// WorldGeneration.GenerateWorld → WorldPlacePlayer, while CUO blocks
		// SaveSystem.TryLoadGame — the only thing that ever wrote a non-zero totalTraveled on
		// a load. So a restored run frozen on its starting layer reaches the game's grant with
		// the archive's own totalTraveled == 0 and the game DOES hand the supplies out.
		//
		// The rule's first version carried an extra "unless the generation came from an
		// archive" clause, which flipped exactly this case to Granted: the native grant had
		// just filled the slots, Body.PickUpItem refused every one of them silently, and a
		// second set of items was left on the ground as world items. No Runtime fact may be
		// allowed back into this condition — it is the game's own.
		Assert.True(Probe.NativeGrantCovers(Baseline(Full)));

	[Fact]
	public void NativeGrantCovers_AfterTheFirstLayer_IsFalse() =>
		Assert.False(Probe.NativeGrantCovers(Baseline(Full, totalTraveled: 120)));

	[Fact]
	public void NativeGrantCovers_TheTutorial_IsFalse() =>
		// biomeOverride == Tutorial (1) fails the game's own test, so the game hands out
		// nothing — and neither does CUO (its decision reports Disabled for a run with no
		// settings, which the tutorial always is).
		Assert.False(Probe.NativeGrantCovers(Baseline(Full, biomeOverride: 1)));

	[Fact]
	public void NativeGrantCovers_ARunStartedAtADebugDepth_IsFalse() =>
		// The third clause of the game's own test (WorldGeneration.cs:1891): a run the player
		// started from the debug console is not the run's first layer, so the game hands out
		// nothing and CUO must supply rather than report AlreadyOwned.
		Assert.False(Probe.NativeGrantCovers(Baseline(Full, debugStartDepth: 2)));

	[Fact]
	public void NativeGrantCovers_AStoredRun_IsFalse() =>
		Assert.False(Probe.NativeGrantCovers(Baseline(Full, loadedRun: true)));

	[Fact]
	public void NativeGrantCovers_NoBaseline_IsFalse() =>
		Assert.False(Probe.NativeGrantCovers(null));

	// ---- the decision itself ----

	[Fact]
	public void Decide_AQueuedRestore_WinsOverEverything()
	{
		// The world HAS a character for this player: no grant, whatever the setting says
		// and whatever the layer is. This is the branch that stops the grant from being
		// created and then wiped by the restore's own first pass.
		var decision = Probe.Decide(
			restoredPending: true,
			Baseline(Full, totalTraveled: 500),
			setting: 2);

		Assert.Equal("CharacterRestored", decision.Reason);
		Assert.False(decision.ShouldGrant);
		Assert.Empty(decision.Plan);
	}

	[Fact]
	public void Decide_NoBaseline_IsNotAVerdict()
	{
		// NoBaseline is the one reason that must stay retryable: nothing describes this
		// generation yet, and the coordinator holds the body unjudged so a later frame can
		// decide it. Every other reason is final for the body.
		var decision = Probe.Decide(restoredPending: false, baseline: null, setting: 2);

		Assert.Equal("NoBaseline", decision.Reason);
		Assert.False(decision.ShouldGrant);
	}

	[Fact]
	public void Decide_AnOmittedPlayerInARestoredWorldPastTheFirstLayer_IsGranted()
	{
		// The acceptance case of S4.3: a player who was not in the package joins a restored
		// world. They are a new player (no restore queued for the body) and the game's own
		// grant does not cover this generation — the restored run is past its starting layer
		// — so the run's supplies are theirs.
		var decision = Probe.Decide(
			restoredPending: false,
			Baseline(Full, totalTraveled: 4200, biomeDepth: 3),
			setting: 2);

		Assert.Equal("Granted", decision.Reason);
		Assert.True(decision.ShouldGrant);
		Assert.Equal("full", decision.Setting);
		Assert.Equal(4, decision.Plan.Count);
	}

	[Fact]
	public void Decide_ARestoredStartingLayer_IsAlreadyOwned()
	{
		// The BLOCKER's other half: on a restored run's OWN starting layer the game's grant
		// runs (see NativeGrantCovers_ARestoredStartingLayer_IsStillTrue), so CUO must report
		// AlreadyOwned rather than hand out a second set into slots that are already full.
		// The first version of this rule returned Granted here, and the items ended up on the
		// ground at the player's feet.
		var decision = Probe.Decide(
			restoredPending: false,
			Baseline(Full),
			setting: 2);

		Assert.Equal("AlreadyOwned", decision.Reason);
		Assert.False(decision.ShouldGrant);
		Assert.Empty(decision.Plan);
	}

	[Fact]
	public void Decide_AMidRunJoin_SuppliesThePlayer()
	{
		// A player joining a running world (deep layer): the native grant's first-layer
		// test fails, so nothing has supplied them and they are a new player here.
		var decision = Probe.Decide(
			restoredPending: false,
			Baseline(Full, totalTraveled: 3000, biomeDepth: 4),
			setting: 1);

		Assert.Equal("Granted", decision.Reason);
		Assert.Equal("light", decision.Setting);
		Assert.Single(decision.Plan);
	}

	[Fact]
	public void Decide_AFreshRun_TheGameAlreadySuppliedEveryone()
	{
		// A fresh run's first layer: the game's own grant hands the supplies out inside
		// generation. CUO reports AlreadyOwned instead of granting a second set.
		var decision = Probe.Decide(
			restoredPending: false,
			Baseline(Full),
			setting: 2);

		Assert.Equal("AlreadyOwned", decision.Reason);
		Assert.False(decision.ShouldGrant);
		Assert.Empty(decision.Plan);
	}

	[Fact]
	public void Decide_RunWithoutSupplies_ReportsDisabledNotGranted()
	{
		// startingsupplies = none is a decision the run made, and the player must be able
		// to tell it apart from "the mod never ran".
		var decision = Probe.Decide(
			restoredPending: false,
			Baseline(Full, totalTraveled: 900),
			setting: 0);

		Assert.Equal("Disabled", decision.Reason);
		Assert.Equal("none", decision.Setting);
		Assert.Empty(decision.Plan);
	}

	[Fact]
	public void Decide_ARunWithoutRunSettings_ReportsDisabledUnset()
	{
		// The tutorial, and any baseline published without run settings. The planner is
		// handed what the reader returned, exactly as the coordinator does it.
		var baseline = Baseline(null);
		var decision = Probe.Decide(
			restoredPending: false,
			baseline,
			Probe.SettingOf(baseline));

		Assert.Equal("Disabled", decision.Reason);
		Assert.Equal("unset", decision.Setting);
	}

	[Fact]
	public void Decide_ADisabledRunOnTheFirstLayer_IsDisabledNotAlreadyOwned()
	{
		// Ordering matters for the account's words: the run's own "no supplies" answers
		// before the game's coverage does, so a player on a fresh desolate run reads that
		// the RUN gave nothing rather than that the game already gave it.
		var decision = Probe.Decide(
			restoredPending: false,
			Baseline(Full),
			setting: 0);

		Assert.Equal("Disabled", decision.Reason);
	}

	// ---- fixture ----

	private static Dictionary<string, object> Setting(int value) =>
		new() { [Probe.SettingName] = value };

	/// <summary>One generation baseline; every default is the run's first layer of a fresh run. The settings are always passed explicitly: "the run has no run settings" is a case this suite pins, and a nullable default would silently substitute the normal preset for it.</summary>
	private static WorldStartParams Baseline(
		Dictionary<string, object>? settings,
		int totalTraveled = 0,
		byte biomeOverride = 0,
		byte biomeDepth = 0,
		int debugStartDepth = 0,
		bool loadedRun = false) =>
		new()
		{
			RandomState = [1, 2, 3, 4],
			BiomeOverride = biomeOverride,
			BiomeDepth = biomeDepth,
			TotalTraveled = totalTraveled,
			DebugStartDepth = (byte)debugStartDepth,
			LoadedRun = loadedRun,
			RunSettings = settings,
		};

	/// <summary>The run's setting, as the baseline carries it in <c>startingsupplies = full</c>.</summary>
	private static Dictionary<string, object> Full => Setting(2);

	/// <summary>
	/// The reflection door to <c>CasualtiesUnknownOnline.GameAdapter.Character.StartingSupplyPolicy</c>
	/// (see the class remarks for why reflection is the only way in). Method and member
	/// names are resolved ONCE and throw on a miss, so a rename that outruns this file is
	/// a loud failure rather than a silently skipped row.
	/// </summary>
	private static class Probe
	{
		private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Character.StartingSupplyPolicy",
			throwOnError: true)!;

		private static readonly Type DecisionType = Policy.GetNestedType("Decision", BindingFlags.NonPublic)
			?? throw new InvalidOperationException("StartingSupplyPolicy.Decision not found.");

		private static readonly Type EntryType = Policy.GetNestedType("StartingSupplyEntry", BindingFlags.NonPublic)
			?? throw new InvalidOperationException("StartingSupplyPolicy.StartingSupplyEntry not found.");

		internal static string SettingName => Field("SettingName").GetValue(null)!.ToString()!;

		internal static IReadOnlyList<(string ItemId, int Slot)> PlanFor(int setting) =>
			((IEnumerable)Method("PlanFor").Invoke(null, [setting])!)
				.Cast<object>()
				.Select(entry => ((string)EntryType.GetProperty("ItemId")!.GetValue(entry)!, (int)EntryType.GetProperty("Slot")!.GetValue(entry)!))
				.ToList();

		internal static string Describe(int setting) => (string)Method("Describe").Invoke(null, [setting])!;

		internal static int? SettingOf(WorldStartParams? baseline) => (int?)Method("SettingOf").Invoke(null, [baseline]);

		internal static bool NativeGrantCovers(WorldStartParams? baseline) =>
			(bool)Method("NativeGrantCovers").Invoke(null, [baseline])!;

		internal static DecisionView Decide(bool restoredPending, WorldStartParams? baseline, int? setting)
		{
			var decision = Method("Decide").Invoke(null, [restoredPending, baseline, setting])!;
			var plan = ((IEnumerable)DecisionType.GetProperty("Plan")!.GetValue(decision)!)
				.Cast<object>()
				.Select(entry => ((string)EntryType.GetProperty("ItemId")!.GetValue(entry)!, (int)EntryType.GetProperty("Slot")!.GetValue(entry)!))
				.ToList();
			return new DecisionView(
				Value(DecisionType, "Reason", decision)!.ToString()!,
				(string)Value(DecisionType, "Setting", decision)!,
				(bool)Value(DecisionType, "ShouldGrant", decision)!,
				plan);
		}

		/// <summary>
		/// A property or backing field on a nested adapter type. Property first, then the
		/// field of the same name: the adapter is compiled without a debugger-only symbol
		/// table, so which of the two the compiler emitted is its own choice — and a name
		/// that matches NEITHER is a loud failure, never a silently skipped row.
		/// </summary>
		private static object? Value(Type type, string name, object instance) =>
			type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is { } property
				? property.GetValue(instance)
				: type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is { } field
					? field.GetValue(instance)
					: throw new InvalidOperationException($"{type.FullName}.{name} not found.");

		private static MethodInfo Method(string name) =>
			Policy.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"StartingSupplyPolicy.{name} not found.");

		private static FieldInfo Field(string name) =>
			Policy.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"StartingSupplyPolicy.{name} not found.");

		internal readonly record struct DecisionView(
			string Reason,
			string Setting,
			bool ShouldGrant,
			IReadOnlyList<(string ItemId, int Slot)> Plan);
	}
}
