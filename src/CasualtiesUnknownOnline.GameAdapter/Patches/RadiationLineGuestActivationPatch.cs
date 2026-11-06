using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// A member never decides when the radiation line comes down: the host's broadcast does.
///
/// <para>
/// The native <c>WorldGeneration.Update</c> arms the line from the LOCAL layer clock
/// (<c>WorldGeneration.cs:860-863</c>). That clock is host state — the host's
/// <c>RadiationLineSync</c> publishes its own clock and limit with every line state and
/// <c>RadiationLineTable.Apply</c> writes both onto this client — but the comparison inside the
/// Update body still runs here, and a member whose copy briefly sat over the limit started the
/// line alone (2026-11-06: member active at 3600 s of 3600, host inactive at 3808 s of 10800).
/// Capping the clock only moved the crossing one frame, because the Update body adds its
/// deltaTime before the test, so this patch removes the member's decision entirely.
/// </para>
///
/// <para>
/// Only the game's own clock path is suppressed (<see cref="WorldGenerationUpdatePatch.InUpdate"/>).
/// The host's broadcast sets the line's flag directly in <c>RadiationLineTable.Apply</c>, so the
/// legitimate path — the host deciding — keeps working; and the host itself is never touched.
/// </para>
/// </summary>
[HarmonyPatch(typeof(RadiationLine), "Activate")]
internal static class RadiationLineGuestActivationPatch
{
	private static bool Prefix()
	{
		if (PatchBridge.Impl is not { IsSessionActive: true } bridge || bridge.IsHostMode)
		{
			return true;
		}

		return !WorldGenerationUpdatePatch.InUpdate;
	}
}
