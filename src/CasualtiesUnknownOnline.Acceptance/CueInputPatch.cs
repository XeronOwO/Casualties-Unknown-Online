using System;
using System.Globalization;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// The drag-use cue tracker's own key read, replaced at its call site.
///
/// <para>
/// Why it is a file of its own: <see cref="InputQueryPatches"/>'s replacement reaches the call sites INSIDE
/// <c>PlayerCamera.HandleInput</c>, the minigame's hand read and <c>PlayerCamera.HandleDragging</c>; the cue tracker
/// is none of them. <c>DragUseCueTracker.Update</c> reads
/// <c>Input.GetKey(KeyBinds.GetBind("iteminteract"))</c> directly, and the policy hides the cue whenever that read is
/// false — so a staged press can never make the cue name a target, however the drag was staged (measured 2026-11-08:
/// the display and pointer halves read true and the cue still read <c>showing false</c>).
/// </para>
///
/// <para>
/// It is the same shape as <see cref="DragInputPatch"/>: a narrow method that patches ONE method and reports what it
/// did, so <see cref="VirtualInput"/>'s install report names every patched site in one place.
/// </para>
/// </summary>
internal static class CueInputPatch
{
	/// <summary>
	/// Patches <c>DragUseCueTracker.Update</c> with the staged-input replacement and returns the line the install
	/// report carries. Never throws: a failure is a sentence in that report, because a batch must be able to see WHY a
	/// cue row cannot be staged.
	/// </summary>
	internal static string Install(Harmony harmony)
	{
		var trackerType = AccessTools.TypeByName("CasualtiesUnknownOnline.GameAdapter.DragUseCueTracker");
		if (trackerType == null)
		{
			return "DragUseCueTracker: no type on this build";
		}

		var update = AccessTools.Method(trackerType, "Update");
		if (update == null)
		{
			return "DragUseCueTracker.Update: no method on this build";
		}

		try
		{
			InputQueryPatches.ResetReplacementCount();
			harmony.Patch(update, transpiler: new HarmonyMethod(typeof(InputQueryPatches), nameof(InputQueryPatches.TranspileKeyReads)));
			return "DragUseCueTracker.Update: patched, "
				+ InputQueryPatches.TakeReplacementCount().ToString(CultureInfo.InvariantCulture)
				+ " call(s) replaced";
		}
		catch (Exception exception)
		{
			return "DragUseCueTracker.Update: FAILED " + exception.GetType().Name + ": " + exception.Message;
		}
	}
}
