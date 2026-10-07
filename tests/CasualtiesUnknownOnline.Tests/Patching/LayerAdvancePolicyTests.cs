using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The end-of-layer choice's adapter half: which side reports the choice, whether
/// an admitted request may drive the host's advance, and the shape of the seam
/// that reports it. The adapter is compile-excluded (it binds game/Unity
/// assemblies), so all three are exercised reflectively through the shared
/// <see cref="GameAssemblyHost"/>.
/// <para>
/// <c>DecideDrive</c> is the native entry's own guard
/// (<c>WorldGeneration.ContinueRun</c>) minus its local-body clause, so the cases
/// below are the entry's clauses: a world that exists, no regeneration running,
/// not already generating.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(GameAssemblyCollection.Name)] // the descent-sink case writes the marker patch's process-global flag
public class LayerAdvancePolicyTests
{
	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.World.LayerAdvancePolicy",
		throwOnError: true)!;

	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.WorldGenerationContinueRunPatch",
		throwOnError: true)!;

	private static readonly Type SinkPatch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.WorldGenerationRegenerateWorldPatch",
		throwOnError: true)!;

	private static bool ShouldDelegateLocalAdvance(SessionRole role, bool sessionActive) =>
		(bool)Policy.GetMethod("ShouldDelegateLocalAdvance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [role, sessionActive])!;

	private static string DecideDrive(SessionRole role, bool sessionActive, bool worldExists, bool generating, bool regenerating) =>
		Policy.GetMethod("DecideDrive", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [role, sessionActive, worldExists, generating, regenerating])!
			.ToString()!;

	private static bool ShouldSuppressLocalDescent(bool fromPanelEntry, bool delegated) =>
		(bool)Policy.GetMethod("ShouldSuppressLocalDescent", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [fromPanelEntry, delegated])!;

	[Theory]
	[InlineData(true, true, true)]   // the panel's click, and the port took the delegation: the member does not descend
	[InlineData(true, false, false)] // the panel's click the port refused (nothing to report): the game descends
	[InlineData(false, true, false)] // the pod's or the console's own descent: the game descends
	[InlineData(false, false, false)]
	public void ShouldSuppressLocalDescent_NeedsBothThePanelEntryAndAnAcceptedDelegation(bool fromPanelEntry, bool delegated, bool expected) =>
		Assert.Equal(expected, ShouldSuppressLocalDescent(fromPanelEntry, delegated));

	[Theory]
	[InlineData(SessionRole.Guest, true, true)]   // the member at the layer's bottom hands the descent to the host
	[InlineData(SessionRole.Host, true, false)]   // the host's own choice IS the session's advance
	[InlineData(SessionRole.None, true, false)]   // solo
	[InlineData(SessionRole.Guest, false, false)] // a guest with no live session (the session ended)
	public void ShouldDelegateLocalAdvance_OnlyForAGuestInALiveSession(SessionRole role, bool sessionActive, bool expected) =>
		Assert.Equal(expected, ShouldDelegateLocalAdvance(role, sessionActive));

	[Theory]
	[InlineData(true, false, false, "Drive")]                 // the host's world is idle and generated
	[InlineData(true, false, true, "AlreadyAdvancing")]        // a regeneration is running (the fade before the capture)
	[InlineData(true, true, false, "AlreadyAdvancing")]        // the generation itself is running
	[InlineData(false, false, false, "NoWorld")]               // a host that has not entered a world
	public void DecideDrive_UsesTheNativeEntrysOwnClauses(bool worldExists, bool generating, bool regenerating, string expected) =>
		Assert.Equal(expected, DecideDrive(SessionRole.Host, sessionActive: true, worldExists, generating, regenerating));

	[Theory]
	[InlineData(SessionRole.Guest, true)]
	[InlineData(SessionRole.None, true)]
	[InlineData(SessionRole.Host, false)]
	public void DecideDrive_RefusesASideThatIsNotThisHost(SessionRole role, bool sessionActive) =>
		Assert.Equal("NotThisSides", DecideDrive(role, sessionActive, worldExists: true, generating: false, regenerating: false));

	/// <summary>
	/// The seam that suppresses the member's local descent is the SINK, not the
	/// entry: the game's own entry body always runs (its guard, the panel close,
	/// the walk release and the deepest-layer record are native), and the sink's
	/// prefix replaces only the returned enumerator — with a valid, empty one, because
	/// the entry hands that object to `StartCoroutine`.
	/// </summary>
	[Fact]
	public void TheDescentSink_ReplacesOnlyTheReturnedEnumerator()
	{
		var prefix = SinkPatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("WorldGenerationRegenerateWorldPatch.Prefix not found.");

		Assert.True(prefix.ReturnType == typeof(bool), $"the sink prefix must be able to skip the original, got {prefix.ReturnType}");

		var parameters = prefix.GetParameters();
		Assert.True(
			parameters.Length == 1 && parameters[0].Name == "__result" && parameters[0].ParameterType == typeof(IEnumerator).MakeByRefType(),
			$"the sink prefix must take ref IEnumerator __result, got {string.Join(", ", parameters.Select(parameter => parameter.ToString()))}");
	}

	/// <summary>The marker's own shape: a void prefix and a void postfix (it marks a window; it never blocks the entry), and the marker field the sink reads.</summary>
	[Fact]
	public void TheChoiceMarker_MarksWithoutBlocking()
	{
		var prefix = Patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("WorldGenerationContinueRunPatch.Prefix not found.");
		var postfix = Patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("WorldGenerationContinueRunPatch.Postfix not found.");

		Assert.True(prefix.ReturnType == typeof(void), $"the marker prefix must be void, got {prefix.ReturnType}");
		Assert.True(postfix.ReturnType == typeof(void), $"the marker postfix must be void, got {postfix.ReturnType}");
		Assert.NotNull(Marker());
	}

	/// <summary>No bridge (no session, in this test host) lets every caller keep the game's own descent — the pod's and the console's included.</summary>
	[Fact]
	public void TheDescentSink_WithoutASession_LetsTheGameDescend()
	{
		var prefix = SinkPatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("WorldGenerationRegenerateWorldPatch.Prefix not found.");

		// The marker off (the pod's and the console's own calls) is the first refusal.
		Marker().SetValue(null, false);
		Assert.True((bool)prefix.Invoke(null, [null])!, "a descent that is not the panel's click must keep the game's own path");

		// The marker on but no bound bridge (no GameAdapter in this process): the same answer.
		Marker().SetValue(null, true);
		Assert.True((bool)prefix.Invoke(null, [null])!, "with no session the game must descend as it always did");
		Marker().SetValue(null, false);
	}

	private static FieldInfo Marker() =>
		Patch.GetField("InContinueRun", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorldGenerationContinueRunPatch.InContinueRun not found.");

	[Fact]
	public void PatchInventory_ContainsTheContinueRunContract()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (IEnumerable)build.Invoke(null, null)!;
		var found = contracts.Cast<object>().Any(contract =>
		{
			var type = contract.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(contract) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(contract) as string;
			return target == "WorldGeneration" && method == "ContinueRun";
		});

		Assert.True(found, "PatchInventory must declare the WorldGeneration.ContinueRun patch contract.");
	}
}
