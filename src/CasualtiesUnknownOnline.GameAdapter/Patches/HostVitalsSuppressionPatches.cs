using System.Collections;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The owner-local side effects of running the game's own vital stages on a host clone. Every
/// prefix here is scoped by <see cref="HostVitalsPass.IsActive"/>, so a local body never sees it:
/// inside the pass the clone's native calls are the model's arithmetic and must not reach the
/// HOST's camera, its audio, its items or a terminal transition; outside the pass the game's own
/// behaviour is untouched. The one exception is the clone's <c>Start</c> droppings coroutine,
/// which starts at creation rather than inside the pass and is therefore gated on the clone
/// itself.
///
/// <para>
/// The sound half is <see cref="SoundPlayPatch"/> (the same scope, before the play and before the
/// character-sound capture); the speech half is the existing <see cref="TalkerPatch"/> clone
/// refusal; the camera's <c>threatMusicTime</c> field has no patch point and is preserved by
/// <see cref="HostVitalsPass.Run"/>; the RNG stream is preserved there too. The contract pin is
/// <c>HostVitalsSuppressionPinTests</c>.
/// </para>
/// </summary>
internal static class HostVitalsSuppressionPatches
{
	/// <summary>
	/// <c>Ragdoll()</c> (Body.cs:1713) is the physics re-enable: it sets every limb rigidbody
	/// simulated, the body's mass to 0.01, disables the collider and clears <c>standing</c>. On a
	/// clone whose pose the entity stream owns, that is the "limb simulating under a teleported
	/// root" family (and the host would ragdoll a body its owner has not collapsed). The model's
	/// <c>standing</c> input stays what the stream says.
	/// </summary>
	[HarmonyPatch(typeof(Body), nameof(Body.Ragdoll))]
	internal static class BodyRagdollPatch
	{
		private static bool Prefix() => !HostVitalsPass.IsActive;
	}

	/// <summary>
	/// <c>TryStartFibrillation</c> (Body.cs:654) is the native circulation's terminal transition.
	/// The host never produces a terminal fact for another player's body (ruling 2): the owner's
	/// report seeds the fibrillation state, and the model only advances what it was given.
	/// </summary>
	[HarmonyPatch(typeof(Body), nameof(Body.TryStartFibrillation))]
	internal static class BodyTryStartFibrillationPatch
	{
		private static bool Prefix() => !HostVitalsPass.IsActive;
	}

	/// <summary>
	/// <c>TryLastStand</c> (Body.cs:955) is a random terminal rescue — the host must not roll it
	/// for another body (ruling 4). The owner reports the outcome; the next snapshot seeds it.
	/// </summary>
	[HarmonyPatch(typeof(Body), nameof(Body.TryLastStand))]
	internal static class BodyTryLastStandPatch
	{
		private static bool Prefix() => !HostVitalsPass.IsActive;
	}

	/// <summary>
	/// <c>WakeUp</c> (Body.cs:3029) writes the sleep terminal state (bad-sleep amount, caffeinated,
	/// sleeping-bag flag, forced quality). Sleep is an input the entity stream carries; the host
	/// model must not wake a body its owner is still sleeping in.
	/// </summary>
	[HarmonyPatch(typeof(Body), nameof(Body.WakeUp))]
	internal static class BodyWakeUpPatch
	{
		private static bool Prefix() => !HostVitalsPass.IsActive;
	}

	/// <summary>
	/// <c>DropItem(int)</c> (Body.cs:1431) is called from the brain-damage periodic branch. The
	/// clone's slots hold display proxies, never the owner's real items; a drop would move a
	/// display item into the world. The owner's own drop is the event.
	/// </summary>
	[HarmonyPatch(typeof(Body), "DropItem", [typeof(int)])]
	internal static class BodyDropItemPatch
	{
		private static bool Prefix() => !HostVitalsPass.IsActive;
	}

	/// <summary><c>HandlePeriodicChecks</c> starts <c>Cry</c> (Body.cs:3724) on low happiness.</summary>
	[HarmonyPatch(typeof(Body), nameof(Body.Cry))]
	internal static class BodyCryPatch
	{
		private static bool Prefix(ref IEnumerator __result)
		{
			if (!HostVitalsPass.IsActive)
			{
				return true;
			}

			__result = HostVitalsPass.EmptyRoutine();
			return false;
		}
	}

	/// <summary><c>HandlePeriodicChecks</c> starts <c>BrainControlReverse</c> (Body.cs:3708) on brain damage.</summary>
	[HarmonyPatch(typeof(Body), nameof(Body.BrainControlReverse))]
	internal static class BodyBrainControlReversePatch
	{
		private static bool Prefix(ref IEnumerator __result)
		{
			if (!HostVitalsPass.IsActive)
			{
				return true;
			}

			__result = HostVitalsPass.EmptyRoutine();
			return false;
		}
	}

	/// <summary><c>HandlePeriodicChecks</c> starts <c>BrainDamageRagdoll</c> (Body.cs:3659), which ragdolls after five seconds.</summary>
	[HarmonyPatch(typeof(Body), "BrainDamageRagdoll")]
	internal static class BodyBrainDamageRagdollPatch
	{
		private static bool Prefix(ref IEnumerator __result)
		{
			if (!HostVitalsPass.IsActive)
			{
				return true;
			}

			__result = HostVitalsPass.EmptyRoutine();
			return false;
		}
	}

	/// <summary>
	/// <c>HandlePeriodicChecks</c> starts <c>PlayerCamera.FlashBrain</c> (PlayerCamera.cs:1197) on
	/// the HOST's own camera. The camera is not the simulated body's; the host's screen must not
	/// flash for another player's brain damage.
	/// </summary>
	[HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.FlashBrain))]
	internal static class PlayerCameraFlashBrainPatch
	{
		private static bool Prefix(ref IEnumerator __result)
		{
			if (!HostVitalsPass.IsActive)
			{
				return true;
			}

			__result = HostVitalsPass.EmptyRoutine();
			return false;
		}
	}

	/// <summary>
	/// <c>Vomit()</c> (Vomiter.cs:189) starts the five-second <c>DoVomit</c> coroutine: it changes
	/// the HOST's time scale, drops an item, spawns the particle at the clone's head, drains blood
	/// and talks. The factory disables the component's own Update, but the native pass calls this
	/// method directly, so the entry itself must refuse inside the pass.
	/// </summary>
	[HarmonyPatch(typeof(Vomiter), nameof(Vomiter.Vomit))]
	internal static class VomiterVomitPatch
	{
		private static bool Prefix() => !HostVitalsPass.IsActive;
	}

	/// <summary>The blood-vomit twin (<c>Vomiter.VomitBlood</c>, Vomiter.cs:204).</summary>
	[HarmonyPatch(typeof(Vomiter), nameof(Vomiter.VomitBlood))]
	internal static class VomiterVomitBloodPatch
	{
		private static bool Prefix() => !HostVitalsPass.IsActive;
	}

	/// <summary>
	/// <c>Body.Start</c> starts this coroutine on EVERY body, including a render clone; every
	/// 1000 s while unconscious it creates a <c>droppings</c> world item at the clone's feet — a
	/// world mutation from a body the host only displays. It is not pass-scoped (it starts at
	/// creation), so the gate is the clone marker itself.
	/// </summary>
	[HarmonyPatch(typeof(Body), nameof(Body.TheCoroutineThatMakesYouShitYourselfWhenUnconscious))]
	internal static class CloneDroppingsCoroutinePatch
	{
		private static bool Prefix(Body __instance, ref IEnumerator __result)
		{
			if (__instance.GetComponentInParent<RemoteBodyDriver>() == null) // Unity object — ==
			{
				return true;
			}

			__result = HostVitalsPass.EmptyRoutine();
			return false;
		}
	}

	/// <summary>
	/// <c>LiquidAffect</c> is added by <c>Body.Start</c>/<c>Limb.Start</c> AFTER
	/// <see cref="RemoteBodyFactory"/> has run its disable pass, so the factory cannot cover it. On
	/// a render clone its <c>FixedUpdate</c> writes model fields the host pass reads
	/// (<c>wetness</c>, <c>dirtyness</c>, <c>temperature</c>, limb <c>pain</c>/<c>skinHealth</c>/
	/// <c>muscleHealth</c>) and can call <c>Body.Scream</c> and <c>FluidManager.SetLiquid</c> — the
	/// owner's water effects, outside the pass scope. The marker decides; an item's own
	/// <c>LiquidAffect</c> (no body/limb) is left to the game.
	/// </summary>
	[HarmonyPatch(typeof(LiquidAffect), "FixedUpdate")]
	internal static class LiquidAffectFixedUpdatePatch
	{
		private static bool Prefix(LiquidAffect __instance)
		{
			var body = __instance.GetComponent<Body>();
			if (body != null) // Unity object — ==
			{
				return body.GetComponentInParent<RemoteBodyDriver>() == null; // Unity object — ==
			}

			var limb = __instance.GetComponent<Limb>();
			return limb == null || limb.body == null || limb.body.GetComponentInParent<RemoteBodyDriver>() == null; // Unity objects — ==
		}
	}
}
