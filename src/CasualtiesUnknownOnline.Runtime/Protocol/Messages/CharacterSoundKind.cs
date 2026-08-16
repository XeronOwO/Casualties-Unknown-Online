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
}
