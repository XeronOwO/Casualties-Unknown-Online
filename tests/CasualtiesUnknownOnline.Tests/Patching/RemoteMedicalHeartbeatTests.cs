using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 regression surface for the remote medical panel's heartbeat cue. The bug: the native
/// <c>Body.HandleCirculation</c> thump follows the VIEWER's own body and the viewer's own panel state, so a
/// viewer in a critical state heard the monitor alarm while the panel displayed a healthy patient. The
/// adapter is compile-excluded (it binds Unity/game assemblies), so these tests lock the Unity-free decision
/// and the patch target by reflection. They fail on the pre-fix adapter because the cue helper and its
/// <c>Sound.Play</c> suppression patch did not exist.
/// </summary>
[Trait("Category", "Integration")]
public class RemoteMedicalHeartbeatTests
{
	private const string HeartbeatTypeName = "CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalHeartbeat";

	private const string PatchTypeName =
		"CasualtiesUnknownOnline.GameAdapter.Patches.RemoteMedicalPatches+RemoteMedicalNativeHeartbeatPatch";

	private static readonly Type? Heartbeat = GameAssemblyHost.Adapter.GetType(HeartbeatTypeName, throwOnError: false);

	[Fact]
	public void HeartbeatHelper_Exists() => Assert.NotNull(Heartbeat);

	[Fact]
	public void Beat_PlaysOneAlarmPerCycleAndRearmsBelowTheThreshold()
	{
		Reset();

		// The native block thumps once, when heartProg passes 0.3 (Body.cs:907-909), and re-arms at the wrap.
		// The caller's AdvanceHeartProgress performs the native wrap before this rule sees the phase, so the
		// re-arm input here is the post-wrap value (0.2) — the native 888-906 branch already ran.
		Assert.Null(Call("Beat", 0.1f, true, true));
		Assert.Equal("heartthump-heavy-monitor", Call("Beat", 0.35f, true, true));
		Assert.Null(Call("Beat", 0.5f, true, true));
		Assert.Null(Call("Beat", 0.2f, true, true));
		Assert.Equal("heartthump-heavy-monitor", Call("Beat", 0.31f, true, true));
	}

	[Fact]
	public void Beat_HealthyDisplayedBodyIsSilent()
	{
		Reset();
		Assert.Null(Call("Beat", 0.1f, false, true));
		Assert.Null(Call("Beat", 0.35f, false, true));
		Assert.Null(Call("Beat", 0.9f, false, true));

		// The silent rule does not depend on the variant being available.
		Reset();
		Assert.Null(Call("Beat", 0.35f, false, false));
	}

	[Fact]
	public void Beat_UsesTheDisplayedBodysCriticalStateForTheVariant()
	{
		Reset();
		Assert.Equal("heartthump-heavy-monitor", Call("Beat", 0.4f, true, true));

		Reset();
		Assert.Equal("heartthump-heavy", Call("Beat", 0.4f, true, false));
	}

	[Fact]
	public void Beat_APatientThatBecomesCritical_AlarmsOnItsNextBeat()
	{
		Reset();

		// The cycle's beat crossed while the patient was healthy: it is consumed silently, so the alarm starts on
		// the next beat instead of on the partial cycle (the ECG spike and the thump stay on their own phase).
		Assert.Null(Call("Beat", 0.4f, false, true));
		Assert.Null(Call("Beat", 0.7f, true, true));
		Assert.Null(Call("Beat", 0.1f, true, true));
		Assert.Equal("heartthump-heavy-monitor", Call("Beat", 0.35f, true, true));
	}

	[Fact]
	public void Reset_ClearsTheLatchForTheNextFocus()
	{
		Reset();
		Assert.Equal("heartthump-heavy-monitor", Call("Beat", 0.4f, true, true));
		Assert.Null(Call("Beat", 0.4f, true, true));

		Reset();
		Assert.Equal("heartthump-heavy-monitor", Call("Beat", 0.4f, true, true));
	}

	[Fact]
	public void NativeHeartbeatClip_RecognisesOnlyTheGamesThreeClips()
	{
		Assert.Equal(true, Call("IsNativeHeartbeatClip", "heartthump"));
		Assert.Equal(true, Call("IsNativeHeartbeatClip", "heartthump-heavy"));
		Assert.Equal(true, Call("IsNativeHeartbeatClip", "heartthump-heavy-monitor"));
		Assert.Equal(false, Call("IsNativeHeartbeatClip", "heartthump-monitor"));
		Assert.Equal(false, Call("IsNativeHeartbeatClip", ""));
		Assert.Equal(false, Call("IsNativeHeartbeatClip", [null]));
	}

	[Fact]
	public void NativeHeartbeatPatch_TargetsTheStringSoundPlayOverload()
	{
		// The patch must bind the STRING overload by its exact argument types: the AudioClip overload is the
		// panel's own cue and must stay reachable. The contract inventory is the adapter's own extraction.
		var contract = Assert.Single(
			Contracts(),
			c => c.PatchClassType == PatchTypeName);
		Assert.Equal("Sound", contract.TargetType);
		Assert.Equal("Play", contract.MethodName);
		Assert.Equal(9, contract.ParameterTypes.Count);
		Assert.Equal("System.String", contract.ParameterTypes[0]);
		Assert.Contains("clip", contract.PatchParameters);

		var patch = GameAssemblyHost.Adapter.GetType(PatchTypeName, throwOnError: false);
		Assert.NotNull(patch);
		var prefix = patch!.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(prefix);
		Assert.Equal(typeof(bool), prefix!.ReturnType);
	}

	[Fact]
	public void DisplayBody_BleedSpeedRidesTheWire_NotALimbSum()
	{
		// The alarm's critical state comes from the display copy's isCriticallyDying, whose bleeding clause reads
		// totalBleedSpeed. The wire carries the owner's OWN native value (CharacterHealthMsg.TotalBleedSpeed,
		// captured from Body.totalBleedSpeed); a limb-only rebuild drops the native bleedingSpeedMultiplier
		// (~0.01-0.02x), the internal-bleeding term and the regen term, so it is orders of magnitude high and
		// flips the clause. The coordinator must therefore keep the mapped wire value.
		var source = AdapterSource("RemoteMedicalCoordinator.cs");
		Assert.DoesNotContain("ComputeTotalBleedSpeed", source);
		Assert.DoesNotContain("totalBleedSpeed =", source);
		Assert.Contains("averagePain = ComputeAveragePain", source);
	}

	[Fact]
	public void ClosePath_ResetsThePanelLatch_BeforeTheEarlyReturn()
	{
		// The latch is per focus: deleting this reset would leave a new focus inheriting the previous beat.
		var close = Between(
			AdapterSource(Path.Combine("Character", "RemoteMedicalView.cs")),
			"internal static void Close()",
			"if (!wasOpen)");
		Assert.Contains("RemoteMedicalHeartbeat.Reset();", close);
	}

	[Fact]
	public void Coordinator_PlaysTheAlarmFromThePhaseItJustAdvanced()
	{
		// The cue must read the SAME phase the redirected ECG draws: the call sits after AdvanceMedicalDisplay.
		var update = Between(
			AdapterSource("RemoteMedicalCoordinator.cs"),
			"internal void Update()",
			"/// <summary>");
		var advanced = update.IndexOf("AdvanceMedicalDisplay(", StringComparison.Ordinal);
		var played = update.IndexOf("RemoteMedicalHeartbeat.Play(display, _log);", StringComparison.Ordinal);
		Assert.True(advanced >= 0, "the coordinator's display pump no longer advances the display phase");
		Assert.True(played >= 0, "the coordinator no longer plays the panel's alarm");
		Assert.True(advanced < played, "the alarm must read the heart phase the ECG was just advanced to");
	}

	[Fact]
	public void SuppressionPrefix_RefusesOnlyWhileTheRemoteFocusIsOpen()
	{
		var prefix = Between(
			AdapterSource(Path.Combine("Patches", "RemoteMedicalPatches.cs")),
			"[HarmonyPatch(typeof(Sound), \"Play\",",
			"[HarmonyPatch(typeof(MoodleManager), \"UpdateMoodles\")]");
		Assert.Contains(
			"!RemoteMedicalView.IsOpen || !RemoteMedicalHeartbeat.IsNativeHeartbeatClip(clip)",
			prefix);
		Assert.Contains("ReportRemoteMedicalHeartbeatSuppressed(clip)", prefix);
		Assert.Contains("return false;", prefix);
	}

	private static void Reset() => Call("Reset");

	private static object? Call(string name, params object?[] args)
	{
		Assert.NotNull(Heartbeat);
		var method = Heartbeat!.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException($"{HeartbeatTypeName}.{name} not found.");
		Assert.True(method.IsStatic, $"{HeartbeatTypeName}.{name} must be static.");
		return method.Invoke(null, args);
	}

	private static List<PatchContract> Contracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory",
			throwOnError: true)!;
		var build = inventory.GetMethod(
			"BuildContracts",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (List<PatchContract>)build.Invoke(null, null)!;
	}

	/// <summary>One adapter source file, relative to the adapter project directory — read without the game.</summary>
	private static string AdapterSource(string relativePath)
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return File.ReadAllText(Path.Combine(
			directory.FullName,
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			relativePath));
	}

	private static string Between(string text, string startAnchor, string endAnchor)
	{
		var start = text.IndexOf(startAnchor, StringComparison.Ordinal);
		Assert.True(start >= 0, $"anchor not found in the adapter source: {startAnchor}");
		var end = text.IndexOf(endAnchor, start + startAnchor.Length, StringComparison.Ordinal);
		return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
	}
}
