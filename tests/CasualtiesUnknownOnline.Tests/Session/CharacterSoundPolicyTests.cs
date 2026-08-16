using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure capture classification behind the Sound.Play patch: an attack
/// scope reports its swing/exert sound, a throw scope reports the throw swing,
/// an exert scope reports the exertion. Empty clips and unknown scopes are
/// never reportable; inside the attack scope any non-empty non-exert string
/// sound is the swing clip (block hit sounds never reach the policy — the
/// innermost DamageBlockOrigin scope excludes them in the adapter).
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
	public void EmptyOrUnknownCalls_AreNotReportable()
	{
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Attack, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.Throw, ""));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.None, "BSSwing1"));
		Assert.Null(CharacterSoundPolicy.Classify(CharacterSoundPolicy.Origin.None, ""));
	}
}
