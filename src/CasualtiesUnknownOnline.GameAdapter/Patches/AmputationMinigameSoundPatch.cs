using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Amputation minigame gore capture. The <c>gore</c> and <c>gore{N}</c> clips are
/// not played by a limb action: the limb's own <c>Dismember</c> plays them
/// (<c>Limb.cs:91-99</c> — <c>"gore"</c> at the limb, then <c>Body.DoGoreSound</c>
/// → <c>"gore{1..5}"</c> at the body, <c>Body.cs:2443-2446</c>), and the
/// amputation minigame reaches that call from its own STEP — its <c>Update</c>
/// dismembers the limb once <c>cutProgress</c> settles the unit
/// (<c>AmputationMinigame.cs:69-94</c>). That step runs frames after the limb
/// action that STARTED the minigame returned, so the clips stayed on the acting
/// client alone — on a REMOTE amputation, on the operator alone: the operator's
/// minigame dismembers the limb of the displayed body, while the dismemberment
/// itself is applied on the patient's client through the kernel projection,
/// whose game therefore never calls <c>Dismember</c> for it.
/// <para>
/// The scope is entered per STEP around <c>Update</c>, the shape the bandage
/// minigame's own step uses, and never kept open across frames: it bounds the
/// window to that call, so the rest of the scene update stays outside it. It
/// opens only for a plain local action on this client — the minigame runs on the
/// acting client, both when the operating player treats their OWN limb (the
/// local path) and when they operate on another player's (the remote path), and
/// the two are the sides a peer would otherwise never hear. On the remote path
/// the window also moves the displayed body off its parked off-world position
/// onto the patient's own clone (<see cref="RemoteMedicalDisplayCapture"/>),
/// because the native call reports the LIMB's and the BODY's own transform and
/// a peer cannot hear a clip played at (0, -10000).
/// </para>
/// </summary>
internal static class AmputationMinigameSoundPatch
{
	[HarmonyPatch(typeof(AmputationMinigame), "Update")]
	internal static class UpdateSoundPatch
	{
		private static void Prefix(AmputationMinigame __instance, out IDisposable? __state)
		{
			if (!CaptureScopeGuard.IsLocalAction())
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
