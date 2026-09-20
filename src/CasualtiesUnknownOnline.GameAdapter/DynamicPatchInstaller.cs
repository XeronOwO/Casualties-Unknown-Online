using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Patching;
using HarmonyLib;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The dynamic patches: targets whose types are INTERNAL to the game assembly
/// (no compile-time reference possible) — reflect the type and patch the
/// method directly. The patch methods live in the Patches namespace
/// (TrapCrystalPatch / CrystalDrippingPatch). Split out of GameAdapter at the
/// 600-line gate — "install the dynamic patches" is one responsibility.
///
/// The targets are declared ONCE, in <see cref="Targets"/>: the installer binds
/// exactly that table, and <c>PatchInventory.BuildContracts</c> derives the
/// hand-declared contract rows from the SAME table, so the rows the contract
/// tests and the capability catalog guard cannot describe a target the installer
/// never binds (a silent hook gap is how sync bugs hide).
///
/// A target that no longer exists is NOT a silent no-op: the error line is what
/// it always was, and the failure is returned as a fact so the capability report
/// can name the trap capability and its reason. Stage 1 keeps the existing
/// install verdict — the dynamic rows do not block the install (only the
/// attributed contracts do), which is why their failures carry
/// <c>BlocksInstall: false</c>.
/// </summary>
internal static class DynamicPatchInstaller
{
	/// <summary>
	/// Every reflected target, in the order the installer binds them. Consumed by
	/// the installer AND by <c>PatchInventory.BuildContracts</c>; the two abort
	/// rows keep the original early-return flow, whose unattempted remainder is
	/// recorded as a failure so the report cannot imply it was checked.
	/// </summary>
	internal static IReadOnlyList<DynamicPatchTarget> Targets { get; } =
	[
		new("CrystalFragile", "Touched", false, nameof(TrapCrystalPatch), null, nameof(TrapCrystalPatch.Postfix),
			DynamicPatchTarget.MissingMessageShape.ByResolution, AbortsRemaining: true, "the fragile-crystal break sync is off", []),
		new("CrystalElectric", "Shock", false, nameof(TrapCrystalPatch), null, nameof(TrapCrystalPatch.ElectricShockPostfix),
			DynamicPatchTarget.MissingMessageShape.ByResolution, AbortsRemaining: true, "the electric-crystal shock sync is off", []),
		new("CrystalTeleport", "Touched", false, nameof(TrapCrystalPatch), nameof(TrapCrystalPatch.TeleportTouchedPrefix), nameof(TrapCrystalPatch.TeleportTouchedPostfix),
			DynamicPatchTarget.MissingMessageShape.TypeAndMethod, AbortsRemaining: false, "the crystal sync is off", ["touched"]),
		new("CrystalDripping", "Update", false, nameof(CrystalDrippingPatch), nameof(CrystalDrippingPatch.Prefix), null,
			DynamicPatchTarget.MissingMessageShape.TypeOnly, AbortsRemaining: false, "the guest-side drip suppression is off", []),
		new("CrystalUnstable", "Update", false, nameof(TrapCrystalPatch), nameof(TrapCrystalPatch.UnstableUpdatePrefix), nameof(TrapCrystalPatch.UnstableUpdatePostfix),
			DynamicPatchTarget.MissingMessageShape.TypeAndMethod, AbortsRemaining: false, "the crystal sync is off", []),
		new("CrystalMetamorphic", "Touched", false, nameof(TrapCrystalPatch), nameof(TrapCrystalPatch.MetamorphicTouchedPrefix), nameof(TrapCrystalPatch.MetamorphicTouchedPostfix),
			DynamicPatchTarget.MissingMessageShape.TypeAndMethod, AbortsRemaining: false, "the crystal sync is off", []),
		new("CrystalShy", "Touched", false, nameof(TrapCrystalPatch), nameof(TrapCrystalPatch.ShyTouchedPrefix), nameof(TrapCrystalPatch.ShyTouchedPostfix),
			DynamicPatchTarget.MissingMessageShape.TypeAndMethod, AbortsRemaining: false, "the crystal sync is off", []),
		new("CrystalEMP", "TryEMP", false, nameof(TrapCrystalPatch), nameof(TrapCrystalPatch.EmpTryEMPPrefix), nameof(TrapCrystalPatch.EmpTryEMPPostfix),
			DynamicPatchTarget.MissingMessageShape.TypeAndMethod, AbortsRemaining: false, "the crystal sync is off", []),
		new("CrystalUnstable", "StartTimer", true, nameof(TrapCrystalPatch), nameof(TrapCrystalPatch.UnstableTimerStartPrefix), nameof(TrapCrystalPatch.UnstableTimerStartPostfix),
			DynamicPatchTarget.MissingMessageShape.MethodOnly, AbortsRemaining: false, "the crystal ticking visual sync is off", []),
	];

	internal static List<PatchVerificationFailure> Install(Harmony harmony, ILogger log)
	{
		var failures = new List<PatchVerificationFailure>();
		foreach (var target in Targets)
		{
			var flags = (target.NonPublic ? BindingFlags.NonPublic : BindingFlags.Public) | BindingFlags.Instance;
			var type = typeof(CrystalEffect).Assembly.GetType(target.TypeName);
			var method = type?.GetMethod(target.MethodName, flags);
			if (method == null)
			{
				var typeMissing = type == null;
				log.LogError("{Message}", MissingMessage(target, typeMissing));
				failures.Add(Failure(target, $"{Subject(target, typeMissing)} not found — {target.Reason}"));

				// The original flow stopped installing the remaining targets here, so the
				// report must not leave a reader believing they were checked.
				if (target.AbortsRemaining)
				{
					failures.Add(Failure(target, $"the remaining dynamic targets were not attempted (this installer aborts after {target.TypeName})"));
					return failures;
				}

				continue;
			}

			harmony.Patch(
				method,
				prefix: target.Prefix is { } prefix ? new HarmonyMethod(PatchMethod(target.PatchClass, prefix)) : null,
				postfix: target.Postfix is { } postfix ? new HarmonyMethod(PatchMethod(target.PatchClass, postfix)) : null);
		}

		return failures;
	}

	/// <summary>
	/// The miss line a target has always logged, rendered from its declared shape
	/// (the tests pin the wording against the strings this installer used before
	/// the targets became a table).
	/// </summary>
	internal static string MissingMessage(DynamicPatchTarget target, bool typeMissing) =>
		$"Dynamic patch {Kind(target, typeMissing)} {Subject(target, typeMissing)} not found — {target.Reason}.";

	/// <summary>The patch method by name out of its declaring patch class (they are private static).</summary>
	private static MethodInfo PatchMethod(string patchClass, string methodName)
	{
		var patchNamespace = typeof(TrapCrystalPatch).Namespace;
		var owner = typeof(TrapCrystalPatch).Assembly.GetType($"{patchNamespace}.{patchClass}")
			?? throw new InvalidOperationException($"dynamic patch class {patchClass} not found");
		return owner.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"dynamic patch method {patchClass}.{methodName} not found");
	}

	/// <summary>The word the miss message uses — "target" or "method", per the target's declared shape.</summary>
	private static string Kind(DynamicPatchTarget target, bool typeMissing) =>
		target.MissingMessage == DynamicPatchTarget.MissingMessageShape.MethodOnly
		|| (target.MissingMessage == DynamicPatchTarget.MissingMessageShape.ByResolution && !typeMissing)
			? "method"
			: "target";

	/// <summary>What the miss message names — the type, or type.method.</summary>
	private static string Subject(DynamicPatchTarget target, bool typeMissing) =>
		target.MissingMessage switch
		{
			DynamicPatchTarget.MissingMessageShape.TypeOnly => target.TypeName,
			DynamicPatchTarget.MissingMessageShape.MethodOnly => $"{target.TypeName}.{target.MethodName}",
			DynamicPatchTarget.MissingMessageShape.TypeAndMethod => $"{target.TypeName}.{target.MethodName}",
			_ => typeMissing ? target.TypeName : $"{target.TypeName}.{target.MethodName}",
		};

	/// <summary>A dynamic-row failure: non-blocking, keyed by the same pseudo
	/// patch-class name the hand-declared contract rows carry.</summary>
	private static PatchVerificationFailure Failure(DynamicPatchTarget target, string detail)
	{
		var name = target.PatchClass + PatchInventory.DynamicSuffix;
		return new PatchVerificationFailure(name, name, detail, blocksInstall: false);
	}
}
