namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The player-character action sounds that travel as dedicated one-shot
/// events. The block hit/break sounds are NOT here: every side applies a
/// block mutation through the game's own <c>WorldGeneration.DamageBlock</c>,
/// which already plays the block hit/break sounds natively.
/// </summary>
public enum CharacterSoundKind : byte
{
	/// <summary><c>Body.Attack</c> played its weapon swing sound (Body.cs:1912).</summary>
	AttackSwing = 1,

	/// <summary><c>Body.ThrowItem</c> played its swing sound (Body.cs:1668).</summary>
	ThrowSwing = 2,

	/// <summary><c>Body.TryExertSound</c> played an exertion sound (Body.cs:2103-2109).</summary>
	Exert = 3,

	/// <summary><c>GunScript.Fire</c> fired a gun (the fire sound + recoil presentation on the owner's clone).</summary>
	GunFire = 4,

	/// <summary><c>Body.FootStep</c> played a step sound (Body.cs:1169-1184) — the fallback
	/// <c>BSFootstepN</c> string or a material/water clip under <c>Sounds/footstep/…</c>.</summary>
	Footstep = 5,

	/// <summary><c>Body.HandleGroundedState</c> played a landing impact (Body.cs:2729-2737,
	/// the <c>impactSmall/Medium/Large</c> <c>bodyFallN</c> clips).</summary>
	LandingImpact = 6,

	/// <summary><c>PantSound.Update</c> played a one-shot pain vocalization
	/// (<c>PantSound.cs:55-67</c>) — the pain sound fires only when
	/// <c>averagePain &gt; 20</c> and the 30-50 s pain timer reaches zero; the
	/// receiver replays it without the continuous pant loop.</summary>
	Pain = 7,

	/// <summary><c>PantSound.Bark</c> played the B-key bark
	/// (<c>PlayerCamera.cs:980-982</c>, <c>PantSound.cs:22-30</c>).</summary>
	Bark = 8,

	/// <summary><c>PantSound.TryGrowl</c> played a low-happiness growl
	/// (<c>Body.cs:3434</c>, <c>PantSound.cs:33-39</c>).</summary>
	Growl = 9,

	/// <summary><c>PantSound.Update</c> played a low-energy yawn
	/// (<c>PantSound.cs:72-80</c>).</summary>
	Yawn = 10,

	/// <summary>A direct placeable-item use played its placement sound
	/// (<c>scrapmetal</c> / <c>climbingrope</c> / <c>scaffoldingpack</c>,
	/// Item.cs:2203/2246/2287 — the exact one-shot <c>"scrapmetal"</c> /
	/// <c>"ropeplace"</c> clip at the placement point).</summary>
	ItemPlacement = 11,

	/// <summary>The local body ingested something or finished a meal: an edible
	/// <c>ItemInfo.useAction</c> played <c>"eatCrunch"</c> / <c>"eatFlesh"</c> /
	/// <c>"glass"</c> / <c>"crystalenemylaugh"</c> at the eater's body
	/// (Item.cs:2387/1789/2463/3588-3589 …), a container use action drank and
	/// played <c>"drink"</c> / <c>"pills"</c> (<c>WaterContainerItem.Drink</c>,
	/// WaterContainerItem.cs:214, reached from the container use actions in
	/// Item.cs), or the meal-end burp timer played <c>"burp"</c> in
	/// <c>Body.HandleVisuals</c> (Body.cs:3142). They all run
	/// locally only, so without this event the other players never hear the
	/// reported meal (user report 2026-09-21).</summary>
	Consume = 12,

	/// <summary>The local camera's <c>PlayerCamera.ApplyWoundItem</c> treated a limb with a
	/// medical item — the item's own <c>useLimbAction</c> delegate
	/// (PlayerCamera.cs:754) or the container's <c>WaterContainerItem.ApplyToLimb</c>
	/// (:760). The clip (<c>syringe</c> / <c>splint</c> / <c>goo</c> /
	/// <c>boneweld</c> / <c>drainuse</c> / <c>tweezeruse</c> / <c>spray</c> /
	/// <c>laser</c> / <c>wrenchhit</c> / <c>cream</c>) plays at the TREATED LIMB's
	/// body position, which may be another player's body — the native call passes
	/// no follow transform, so the position travels and every side replays it
	/// where the treatment happened.</summary>
	Medical = 13,

	/// <summary>The local body drank from a world liquid:
	/// <c>FluidManager.DrinkLiquid</c>'s own water-branch <c>"drink"</c>
	/// (FluidManager.cs:314) and the liquid registry's <c>onDrink</c> clip
	/// (Liquids.cs:1501). The container-drink half of "drinking" rides
	/// <see cref="Consume"/>.</summary>
	Drink = 14,

	/// <summary>A local item use played its own device/utility feedback clip —
	/// <c>flashlighttoggle</c> / <c>error</c> / <c>centrifuge</c> /
	/// <c>combine</c> / <c>drop</c>, all inside the item's use action. The state
	/// they accompany already syncs through its own domain; they are carried
	/// because the world should SOUND the same on every side.</summary>
	Utility = 15,

	/// <summary>An inventory-internal gesture played its clip — SwitchHands /
	/// SwapSlots (<c>switch</c>, Body.cs:1131/1427), CombineItems
	/// (<c>combine</c>, :1284) and CombineLiquids (<c>waterpour</c>, :1250).</summary>
	Gesture = 16,

	/// <summary>The local body's own one-shot outside the item path: the Vomiter
	/// vomit routines (<c>vomit1</c> / <c>vomit2</c>, Vomiter.cs:86/125), the nap
	/// <c>stretch</c> (Body.cs:2510) and the water <c>dogshake</c> (:2553). The
	/// 2D <c>vomitwarning</c> / <c>bloodvomitwarning</c> screen feedback is NOT
	/// here by decision — it is the acting player's own HUD prompt.</summary>
	BodySound = 17,
}
