using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Pure classification for the player-character sound capture (no Unity):
/// maps a call-identity scope + the clip the game just played to a
/// <see cref="CharacterSoundKind"/>. The GameAdapter's <c>Sound.Play</c>
/// patches run this inside the call-identity scopes opened around
/// <c>Body.Attack</c> / <c>Body.ThrowItem</c> / <c>Body.TryExertSound</c> /
/// <c>Body.FootStep</c> / <c>Body.HandleGroundedState</c> /
/// <c>PantSound.Update</c> / <c>PantSound.Bark</c> / <c>PantSound.TryGrowl</c> /
/// <c>LockpingMinigame.Update</c> / direct placeable-item uses
/// (<c>Body.UseItem</c> / <c>Body.UseItemInHand</c>) / the local body's
/// <c>Body.HandleVisuals</c> / the local camera's <c>PlayerCamera.ApplyWoundItem</c>
/// (the choke point every limb treatment enters) / <c>FluidManager.DrinkLiquid</c> /
/// <c>Body.CombineLiquids</c> / a local body's own one-shot coroutines (the
/// Vomiter vomit routines, <c>Body.NapCoroutine</c>, <c>Body.WaterShake</c>),
/// plus the two inventory scopes that already exist for other reasons
/// (<c>InternalReorder</c> around SwitchHands / SwapSlots and <c>Craft</c>
/// around CombineItems);
/// any block hit sound that fires during an attack is excluded before this
/// policy sees it, because <c>WorldGeneration.DamageBlock</c> opens its own
/// innermost <c>DamageBlockOrigin</c> scope.
/// </summary>
public static class CharacterSoundPolicy
{
	/// <summary>The call-identity scopes the sound capture understands (mirrors the GameAdapter's CallContext origins).</summary>
	public enum Origin
	{
		None = 0,
		Attack = 1,
		Throw = 2,
		Exert = 3,
		Footstep = 4,
		LandingImpact = 5,
		Pain = 6,
		Bark = 7,
		Growl = 8,
		Yawn = 9,
		LockpickPain = 10,
		ItemPlacement = 11,

		/// <summary>Inside a local body's item-use action (<c>Body.UseItem</c> /
		/// <c>Body.UseItemInHand</c>) — only the INGEST clips are classified from
		/// this scope (the eat clips, and the container drink's <c>"drink"</c> /
		/// <c>"pills"</c>, which is what "drinking/pouring" reduces to on the
		/// item-use path). It also wraps every other item use (medical, tools,
		/// gestures); those sounds are not classified here, and the ones that
		/// need their own carrier are recorded in
		/// <c>docs/backlog/review/host-eating-sound-not-heard-on-guest.md</c>.</summary>
		ItemUse = 12,

		/// <summary>Inside <c>Body.HandleVisuals</c>, whose meal-end burp timer
		/// plays <c>"burp"</c> (Body.cs:3137-3142) — the scope exists for that
		/// one clip, the way <see cref="LockpickPain"/> exists for <c>"gore2"</c>.</summary>
		Burp = 13,

		/// <summary>Inside <c>PlayerCamera.ApplyWoundItem</c> — the limb-treatment
		/// clip, played at the treated limb (the operator's own or another
		/// player's body).</summary>
		Medical = 14,

		/// <summary>Inside <c>FluidManager.DrinkLiquid</c> — the world-liquid drink.</summary>
		WorldDrink = 15,

		/// <summary>Inside an inventory-internal gesture: SwitchHands / SwapSlots
		/// (<c>"switch"</c>), CombineItems (<c>"combine"</c>) and CombineLiquids
		/// (<c>"waterpour"</c>).</summary>
		InventoryGesture = 16,

		/// <summary>Inside a local body's own one-shot coroutine — vomit, nap
		/// stretch, water shake.</summary>
		BodySound = 17,
	}

	/// <summary>
	/// The kind to report, or null when the call is not a reportable character
	/// sound. An empty clip is never reportable (a null <c>AudioClip</c> load
	/// plays nothing). Inside <c>Body.Attack</c>, every non-empty string sound
	/// that reaches this policy (block sounds excluded by the innermost damage
	/// scope) is either the swing sound or the exertion sound — the exertion
	/// prefix is the discriminator. Inside the footstep scope every non-empty
	/// clip is a step (the fallback <c>BSFootstepN</c> or a material path under
	/// <c>Sounds/footstep/…</c>); inside the landing-impact scope every non-empty
	/// clip is a landing impact (the <c>bodyFallN</c> clips).
	/// </summary>
	public static CharacterSoundKind? Classify(Origin origin, string clip)
	{
		if (string.IsNullOrEmpty(clip))
		{
			return null;
		}

		return origin switch
		{
			Origin.Exert => CharacterSoundKind.Exert,
			Origin.Throw => CharacterSoundKind.ThrowSwing,
			Origin.Attack => IsExertClip(clip) ? CharacterSoundKind.Exert : CharacterSoundKind.AttackSwing,
			Origin.Footstep => CharacterSoundKind.Footstep,
			Origin.LandingImpact => CharacterSoundKind.LandingImpact,
			Origin.Pain => CharacterSoundKind.Pain,
			Origin.Bark => CharacterSoundKind.Bark,
			Origin.Growl => CharacterSoundKind.Growl,
			Origin.Yawn => CharacterSoundKind.Yawn,
			Origin.LockpickPain => clip == "gore2" ? CharacterSoundKind.Pain : null,
			Origin.ItemPlacement => clip is "scrapmetal" or "ropeplace" ? CharacterSoundKind.ItemPlacement : null,
			Origin.ItemUse => IsIngestClip(clip) ? CharacterSoundKind.Consume
				: IsMedicalClip(clip) ? CharacterSoundKind.Medical
				: IsItemUseFeedbackClip(clip) ? CharacterSoundKind.Utility
				: null,
			Origin.Burp => clip == "burp" ? CharacterSoundKind.Consume : null,
			Origin.Medical => IsMedicalClip(clip) ? CharacterSoundKind.Medical : null,
			Origin.WorldDrink => clip is "drink" or "pills" ? CharacterSoundKind.Drink : null,
			Origin.InventoryGesture => clip is "switch" or "waterpour" or "combine" ? CharacterSoundKind.Gesture : null,
			Origin.BodySound => clip is "stretch" or "dogshake" or "vomit1" or "vomit2" ? CharacterSoundKind.BodySound : null,
			_ => null,
		};
	}

	/// <summary>The clips an item-use action plays for INGESTION (the edible use
	/// actions and the container's own drink) — the family the consume cycle
	/// carried.</summary>
	private static bool IsIngestClip(string clip) =>
		clip is "eatCrunch" or "eatFlesh" or "glass" or "crystalenemylaugh" or "drink" or "pills";

	/// <summary>The item-use action's own device/utility feedback — a world sound
	/// at the item, whose state already syncs through its own domain. Carried
	/// because the native call is a 3D one-shot every side should hear, not
	/// because the state needs it.</summary>
	private static bool IsItemUseFeedbackClip(string clip) =>
		clip is "flashlighttoggle" or "error" or "centrifuge" or "combine" or "drop";

	/// <summary>Every medical clip of the family. The limb-treatment half plays
	/// inside the two <c>ApplyWoundItem</c> branches (the item's own
	/// <c>useLimbAction</c> delegates and the liquid registry's
	/// <c>onHealthUse</c> clips reached through the container); four censused
	/// sites instead play from the item's WORLD <c>useAction</c> (the rag's
	/// <c>"splint"</c> at Item.cs:515, the rosepod's <c>"goo"</c> at :1443, the
	/// drainer's <c>"drainuse"</c> at :1658, and <c>Item.DrawBlood</c>'s
	/// <c>"syringe"</c> at :7123 reached from the two liquid-container use
	/// actions), which runs under <c>CharacterItemUse</c>. A medical clip is
	/// therefore reportable from EITHER scope — the independent review of that
	/// cycle found the four sites silently uncarried while only the limb-action
	/// scope classified them.
	/// <para>
	/// <c>"bandage"</c> joins the set with its own producer: the native
	/// <c>BandageMinigame.PhysicsUpdate</c> plays it when a wrap completes
	/// (BandageMinigame.cs:112), frames after the limb action that STARTED the
	/// minigame returned — so the local treatment left it on the acting client
	/// alone, and the remote treatment (which CUO drives through the same native
	/// minigame) did too.
	/// </para>
	/// <para>
	/// The gore family joins it the same way, from two producers that both run
	/// per STEP of a native minigame: the amputation minigame's own completion
	/// plays <c>"gore"</c> and then the body's roll (<c>Limb.Dismember</c>,
	/// Limb.cs:91-99 → <c>Body.DoGoreSound</c>, Body.cs:2443-2446), and the
	/// shrapnel minigame's broken grasp plays the same roll
	/// (ShrapnelMinigame.cs:61-71). Those steps are where a REMOTE amputation is
	/// heard: the operator's minigame runs on the displayed body while the
	/// dismemberment is applied on the patient's client by the kernel projection,
	/// so the patient's game never calls <c>Dismember</c> and the peers heard
	/// nothing. <c>"gore2"</c> keeps its separate meaning under
	/// <c>Origin.LockpickPain</c> (the lockpick-failure pain); the two origins
	/// are distinct scopes, never nested, and the relay drops the source's own
	/// echo — so a clip classified under both can never be reported twice for one
	/// play.
	/// </para></summary>
	private static bool IsMedicalClip(string clip) =>
		clip is "bandage" or "syringe" or "splint" or "goo" or "boneweld" or "drainuse" or "tweezeruse" or "spray" or "laser" or "wrenchhit" or "cream"
			or "gore" or "gore1" or "gore2" or "gore3" or "gore4" or "gore5";

	private static bool IsExertClip(string clip) =>
		clip.StartsWith("exert", StringComparison.Ordinal);
}
