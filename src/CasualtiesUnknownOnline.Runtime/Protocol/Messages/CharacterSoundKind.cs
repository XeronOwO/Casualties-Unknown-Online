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
}
