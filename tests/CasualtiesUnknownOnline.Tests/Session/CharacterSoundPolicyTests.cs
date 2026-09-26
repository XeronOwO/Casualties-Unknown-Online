using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure capture classification behind the Sound.Play patches: an attack
/// scope reports its swing/exert sound, a throw scope reports the throw swing,
/// an exert scope reports the exertion, a footstep scope reports the step clip
/// and a landing-impact scope reports the bodyFall clip. Empty clips and
/// unknown scopes are never reportable; inside the attack scope any non-empty
/// non-exert string sound is the swing clip (block hit sounds never reach the
/// policy — the innermost DamageBlockOrigin scope excludes them in the adapter).
/// The item-use and burp scopes are clip WHITELISTS instead: they wrap whole
/// native methods (a local item use, Body.HandleVisuals), so only the ingest
/// clips are Consume and every other sound in them stays unreported.
/// </summary>
public class CharacterSoundPolicyTests
{
	[Fact]
	public void AttackSwingClips_ClassifyAsAttackSwing()
	{
		Assert.Equal(CharacterSoundKind.AttackSwing,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Attack, "BSSwing3"));
		Assert.Equal(CharacterSoundKind.AttackSwing,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Attack, "laser"));
	}

	[Fact]
	public void AttackScope_ExertClip_ClassifiesAsExert()
	{
		Assert.Equal(CharacterSoundKind.Exert,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Attack, "exert4"));
	}

	[Fact]
	public void ThrowScope_ClassifiesAsThrowSwing()
	{
		Assert.Equal(CharacterSoundKind.ThrowSwing,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Throw, "BSSwing1"));
	}

	[Fact]
	public void ExertScope_ClassifiesAsExert()
	{
		Assert.Equal(CharacterSoundKind.Exert,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Exert, "exert2"));
	}

	[Fact]
	public void FootstepScope_ClassifiesFallbackAndMaterialClipsAsFootstep()
	{
		Assert.Equal(CharacterSoundKind.Footstep,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Footstep, "BSFootstep2"));
		Assert.Equal(CharacterSoundKind.Footstep,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Footstep, "footstep/Rock/RockStep1"));
		Assert.Equal(CharacterSoundKind.Footstep,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Footstep, "footstep/Water/WaterStep1"));
	}

	[Fact]
	public void LandingImpactScope_ClassifiesAsLandingImpact()
	{
		Assert.Equal(CharacterSoundKind.LandingImpact,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.LandingImpact, "bodyFall1"));
	}

	[Fact]
	public void VocalizationScopes_ClassifyAsTheirKinds()
	{
		Assert.Equal(CharacterSoundKind.Pain,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Pain, "pain1"));
		Assert.Equal(CharacterSoundKind.Bark,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Bark, "bark2"));
		Assert.Equal(CharacterSoundKind.Growl,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Growl, "growl7"));
		Assert.Equal(CharacterSoundKind.Yawn,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Yawn, "yawn1"));
	}

	[Fact]
	public void VocalizationKinds_AreDefinedInTheWireEnum()
	{
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)7),
			"CharacterSoundKind.Pain must be defined for the pain-vocalization event.");
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)8),
			"CharacterSoundKind.Bark must be defined for the B-key bark event.");
	}

	[Fact]
	public void VocalizationOrigins_ExistInThePolicyOriginEnum()
	{
		var origin = typeof(CharacterSoundPolicy).GetNestedType("Origin")
			?? throw new InvalidOperationException("CharacterSoundPolicy.Origin not found.");
		Assert.NotNull(origin.GetField("Pain"));
		Assert.NotNull(origin.GetField("Bark"));
	}

	[Fact]
	public void LockpickPainOrigin_ExistsAndClassifiesOnlyGore2AsPain()
	{
		var origin = typeof(CharacterSoundPolicy).GetNestedType("Origin")
			?? throw new InvalidOperationException("CharacterSoundPolicy.Origin not found.");
		var field = origin.GetField("LockpickPain");
		Assert.NotNull(field);

		var lockpickPain = field!.GetValue(null)!;
		var classify = typeof(CharacterSoundPolicy).GetMethod("Classify", BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("CharacterSoundPolicy.Classify not found.");

		var painResult = classify.Invoke(null, [lockpickPain, "gore2"]);
		Assert.Equal(CharacterSoundKind.Pain, painResult);

		var unlockResult = classify.Invoke(null, [lockpickPain, "unlock"]);
		Assert.Null(unlockResult);
	}

	[Fact]
	public void ItemPlacementOrigin_ExistsAndClassifiesPlacementClips()
	{
		var originType = typeof(CharacterSoundPolicy).GetNestedType("Origin")
			?? throw new InvalidOperationException("CharacterSoundPolicy.Origin not found.");
		var placementField = originType.GetField("ItemPlacement");
		Assert.NotNull(placementField);

		var kindType = typeof(CharacterSoundKind);
		var placementKindField = kindType.GetField("ItemPlacement");
		Assert.NotNull(placementKindField);

		var placementOrigin = placementField!.GetValue(null)!;
		var placementKind = placementKindField!.GetValue(null)!;
		Assert.Equal(placementKind, CharacterSoundPolicy.Classify((CharacterSoundPolicy.Origin)placementOrigin, "scrapmetal"));
		Assert.Equal(placementKind, CharacterSoundPolicy.Classify((CharacterSoundPolicy.Origin)placementOrigin, "ropeplace"));
		Assert.Null(CharacterSoundPolicy.Classify((CharacterSoundPolicy.Origin)placementOrigin, "BSSwing3"));
		Assert.Null(CharacterSoundPolicy.Classify((CharacterSoundPolicy.Origin)placementOrigin, "unlock"));
	}

	[Fact]
	public void ItemUseScope_ClassifiesTheIngestFamilyAsConsume()
	{
		// The container drink is the same ingest family: WaterContainerItem.Drink
		// plays its clip at the drinker's body from inside the container use
		// action (WaterContainerItem.cs:214), with "drink" or "pills" as the
		// only two clip arguments the item table passes.
		foreach (var clip in new[] { "eatCrunch", "eatFlesh", "glass", "crystalenemylaugh", "drink", "pills" })
		{
			Assert.Equal(CharacterSoundKind.Consume,
				CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.ItemUse, clip));
		}
	}

	[Fact]
	public void ItemUseScope_ClassifiesTheItemUseFeedbackAsUtility()
	{
		// The item's own device/utility feedback rides the SAME use-action scope
		// as the ingest clips: the native call is a 3D one-shot at the item, and
		// the decision is that the world should sound the same on every side.
		foreach (var clip in new[] { "flashlighttoggle", "error", "centrifuge", "combine", "drop" })
		{
			Assert.Equal(CharacterSoundKind.Utility,
				CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.ItemUse, clip));
		}
	}

	[Fact]
	public void MedicalClips_AreCarriedFromBOTHTheLimbActionAndTheItemUseScope()
	{
		// Four censused medical sites play their clip from the item's WORLD use
		// action, not from the limb-action choke point (Item.cs:515 "splint",
		// :1443 "goo", :1658 "drainuse" and :7123 "syringe" through
		// Item.DrawBlood) — those run under CharacterItemUse. The medical set is
		// therefore consulted from BOTH scopes; listing it under the limb scope
		// alone left exactly those four sites silent (this cycle's independent
		// review found it).
		foreach (var clip in new[] { "syringe", "splint", "goo", "boneweld", "drainuse", "tweezeruse", "spray", "laser", "wrenchhit", "cream" })
		{
			Assert.Equal(CharacterSoundKind.Medical,
				CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.ItemUse, clip));
		}
	}

	[Fact]
	public void ItemUseScope_LeavesTheOtherItemSoundsSilent()
	{
		// The scope wraps EVERY local item use (medical, tools, gestures): the
		// ingest clips, the medical set and the item's own feedback report from
		// it; everything else has its own carrier (the gesture scopes) or is not
		// a character sound at all.
		foreach (var clip in new[] { "waterpour", "switch", "scrapmetal", "BSSwing3", "waterflow1" })
		{
			Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.ItemUse, clip));
		}
	}

	[Fact]
	public void MedicalScope_ClassifiesTheLimbTreatmentClipsOnly()
	{
		foreach (var clip in new[] { "syringe", "splint", "goo", "boneweld", "drainuse", "tweezeruse", "spray", "laser", "wrenchhit", "cream" })
		{
			Assert.Equal(CharacterSoundKind.Medical,
				CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Medical, clip));
		}

		foreach (var clip in new[] { "eatCrunch", "combine", "scrapmetal", "BSSwing3", "" })
		{
			Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Medical, clip));
		}
	}

	[Fact]
	public void BandageMinigameClip_IsCarriedFromTheLimbTreatmentScope()
	{
		// "bandage" is played by the native BandageMinigame's own physics step
		// (BandageMinigame.cs:112, 3D, at the carried item) — frames after the limb
		// action that started the minigame returned, so the scope that carries it is
		// the minigame's PER-STEP one. The clip has to be inside the medical set, or
		// that scope would capture a call the policy refuses to classify.
		Assert.Equal(CharacterSoundKind.Medical,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Medical, "bandage"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Medical, "bandagex"));
	}

	[Fact]
	public void WorldDrinkScope_ClassifiesTheWorldDrinkClipsOnly()
	{
		Assert.Equal(CharacterSoundKind.Drink,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.WorldDrink, "drink"));
		Assert.Equal(CharacterSoundKind.Drink,
			CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.WorldDrink, "pills"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.WorldDrink, "eatFlesh"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.WorldDrink, "waterflow1"));
	}

	[Fact]
	public void GestureScope_ClassifiesTheInventoryGesturesOnly()
	{
		foreach (var clip in new[] { "switch", "waterpour", "combine" })
		{
			Assert.Equal(CharacterSoundKind.Gesture,
				CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.InventoryGesture, clip));
		}

		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.InventoryGesture, "drop"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.InventoryGesture, "flashlighttoggle"));
	}

	[Fact]
	public void BodySoundScope_ClassifiesTheBodyOneShots_AndLeavesTheTwoDimensionalPrompts()
	{
		foreach (var clip in new[] { "vomit1", "vomit2", "stretch", "dogshake" })
		{
			Assert.Equal(CharacterSoundKind.BodySound,
				CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.BodySound, clip));
		}

		// The 2D screen prompts are the acting player's own HUD feedback: they
		// play in Vomiter.Vomit / VomitBlood (Sound.Play at Vector2.zero with the
		// 2D flag), outside every wrapped routine, and stay local by decision.
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.BodySound, "vomitwarning"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.BodySound, "bloodvomitwarning"));
	}

	[Fact]
	public void BodyFamilyKinds_AreDefinedInTheWireEnum()
	{
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)13), "CharacterSoundKind.Medical must be defined.");
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)14), "CharacterSoundKind.Drink must be defined.");
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)15), "CharacterSoundKind.Utility must be defined.");
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)16), "CharacterSoundKind.Gesture must be defined.");
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)17), "CharacterSoundKind.BodySound must be defined.");
	}

	[Fact]
	public void BurpScope_ClassifiesTheMealEndOnly()
	{
		Assert.Equal(CharacterSoundKind.Consume, CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Burp, "burp"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Burp, "eatCrunch"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Burp, ""));
	}

	[Fact]
	public void ConsumeKind_IsDefinedInTheWireEnum() =>
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), (CharacterSoundKind)12),
			"CharacterSoundKind.Consume must be defined for the ingest/meal one-shot event.");

	[Fact]
	public void EmptyOrUnknownCalls_AreNotReportable()
	{
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Attack, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Throw, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.None, "BSSwing1"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.None, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Footstep, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.LandingImpact, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.ItemPlacement, ""));
	}
}
