using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Shrapnel minigame gore capture. Pulling a piece free with a bare hand can
/// BREAK the grasp, and the native step plays the body's gore roll when it does
/// (<c>ShrapnelMinigame.BreakGrasp</c>, <c>ShrapnelMinigame.cs:61-71</c>, called
/// from its own <c>Update</c> at <c>:111</c>/<c>:116</c> → <c>Body.DoGoreSound</c>,
/// <c>Body.cs:2443-2446</c>). That step runs after the limb action that started
/// the minigame returned, so no peer heard it — neither for the local removal nor
/// for the remote operator's own session, which drives the same native minigame
/// over the displayed limb.
/// <para>
/// Only the bare-handed path can reach it: the two break conditions sit behind
/// <c>!this.hasTweezers</c> (<c>ShrapnelMinigame.cs:109</c>), so the tweezers
/// keep the silence the treatment census records for them. The scope is entered
/// per STEP around <c>Update</c> and never kept open across frames; it opens only
/// for a plain local action on this client. An OBSERVER copy of the shared
/// session is excluded on top of that guard: the operator already reported the
/// removal, so an observer's own copy must never report a second play. On the
/// remote path the window also moves the displayed body off its parked off-world
/// position onto the patient's own clone
/// (<see cref="RemoteMedicalDisplayCapture"/>), because the native call reports
/// the BODY's own transform.
/// </para>
/// </summary>
internal static class ShrapnelMinigameSoundPatch
{
	[HarmonyPatch(typeof(ShrapnelMinigame), "Update")]
	internal static class UpdateSoundPatch
	{
		private static void Prefix(ShrapnelMinigame __instance, out IDisposable? __state)
		{
			if (!CaptureScopeGuard.IsLocalAction()
				|| RemoteMedicalOperationHandler.IsObserverShrapnelMinigame(__instance))
			{
				__state = null;
				return;
			}

			var limb = Traverse.Create(__instance).Field("limb").GetValue<Limb>();
			var window = RemoteMedicalDisplayCapture.Enter(limb != null ? limb.body : null);
			__state = new CaptureScope(CallContext.Enter(CallContext.Origin.CharacterMedicalUse), window);
		}

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}

	/// <summary>The capture scope and the display-body window, closed together — the capture first, so nothing the restore does can be reported.</summary>
	private sealed class CaptureScope(IDisposable scope, IDisposable window) : IDisposable
	{
		private readonly IDisposable _scope = scope;
		private readonly IDisposable _window = window;

		public void Dispose()
		{
			_scope.Dispose();
			_window.Dispose();
		}
	}
}
