using System;
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
	public void EmptyOrUnknownCalls_AreNotReportable()
	{
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Attack, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Throw, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.None, "BSSwing1"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.None, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Footstep, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.LandingImpact, ""));
	}
}
