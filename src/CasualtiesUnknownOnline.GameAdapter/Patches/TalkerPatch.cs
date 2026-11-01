using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The Talker speech hooks (phase B): a remote clone's bubble must come from
/// the report, never a local auto-talk — the clone's Update condition-polls
/// pain/hunger/bleeding exactly like the real body (Talker.cs:139-423), so
/// without the suppression every clone would speak its own rolls (random line
/// + distortion) and the bubbles would diverge. The suppressed clones only
/// ever DISPLAY what the SpeechMsg replay writes. The report postfix fires on
/// a currentString change — the game's own talk conditions (conscious,
/// brainHealth, mindWipe, happiness/trauma; Talker.cs:500-504) already decided
/// whether the talk landed; a same-string repeat is not re-reported (the peers
/// already show that exact bubble — no visible gap, recorded).
/// </summary>
internal static class TalkerPatch
{
	[HarmonyPatch(typeof(Talker), "Talk", [typeof(List<string>), typeof(Limb), typeof(bool), typeof(bool)])]
	internal static class TalkPatch
	{
		private static bool Prefix(Talker __instance, out string __state)
		{
			__state = Traverse.Create(__instance).Field("currentString").GetValue<string>();
			if (PatchBridge.Impl is not { } bridge || !bridge.IsSessionActive)
			{
				return true; // solo: vanilla
			}

			// A remote clone talks only what the SpeechMsg replay writes.
			if (__instance.body != null && __instance.body.GetComponentInParent<RemoteBodyDriver>() != null) // Unity objects — ==
			{
				return false;
			}

			// A guest-side trader's bubble is host-broadcast (the host's trader
			// is authoritative) — the guest's own rolls would diverge.
			if (__instance.trader != null && !bridge.IsHostMode) // Unity object — ==
			{
				return false;
			}

			return true;
		}

		private static void Postfix(Talker __instance, string __state)
		{
			var current = Traverse.Create(__instance).Field("currentString").GetValue<string>();
			if (current == __state)
			{
				return; // suppressed (the original was skipped) or the talk conditions failed — nothing new
			}

			PatchBridge.Impl?.OnSpeechReported(__instance, current);
		}
	}

	/// <summary>
	/// A remote clone must not make the LOCAL trader answer. `Talker.PromptTraderResponse` forwards a body's own
	/// condition line to `PlayerCamera.main.lastTrader.PromptResponse`, which starts a `DelayedTalk` coroutine per
	/// call (TraderScript.cs: PromptResponse → DelayedTalk → `talker.Talk`), so a clone whose condition poll fires
	/// repeatedly makes the trader speak over and over: measured 2026-11-02, a dead player's body next to a trader
	/// produced 20-65 trader bubbles per second and the speech clip played continuously on both clients, while moving
	/// that body away dropped it to 2 in five seconds.
	///
	/// <para>
	/// The rule is the same one the talk patch already applies: a clone's speech is REPORTED, never performed — and
	/// asking the local trader for an answer is performing it. The owning client drives its own trader interaction.
	/// </para>
	/// </summary>
	[HarmonyPatch(typeof(Talker), nameof(Talker.PromptTraderResponse))]
	internal static class PromptTraderResponsePatch
	{
		private static bool Prefix(Talker __instance) =>
			__instance.body == null // Unity object — ==
			|| __instance.body.GetComponentInParent<RemoteBodyDriver>() == null; // Unity object — ==
	}
}
