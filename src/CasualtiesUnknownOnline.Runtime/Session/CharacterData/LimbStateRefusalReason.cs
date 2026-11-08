namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>Why a reported limb transition is not something the game itself can produce.</summary>
internal enum LimbStateRefusalReason
{
	None,
	DismemberedLimbReturned,
	SkinJumped,
	MuscleJumped,
	InfectionCleared,
}
