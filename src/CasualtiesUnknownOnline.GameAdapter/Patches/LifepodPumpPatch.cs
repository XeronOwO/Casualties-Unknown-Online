using System.Collections;
using System.Linq;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Life-pod pump (guest session): the pump's grid writes are the host's — the
/// coroutine is replaced with a no-op (an IEnumerator Prefix that returns
/// false hands back an EMPTY enumerator; StartCoroutine must not receive null).
/// Host/solo: original behaviour.
/// </summary>
[HarmonyPatch(typeof(LifepodPump), "PumpLoop")]
internal static class LifepodPumpPatch
{
	private static bool Prefix(out IEnumerator __result)
	{
		if (PatchBridge.Impl is { } bridge && bridge.IsSessionActive && !bridge.IsHostMode)
		{
			__result = Enumerable.Empty<object>().GetEnumerator(); // a no-op coroutine — the writes are the host's
			return false;
		}

		__result = null!;
		return true;
	}
}
