namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The semantic action carried by one generic medical operation update.
/// Meaning depends on <see cref="MedicalOperationKind"/>: wraps are bandage
/// progress, hits are dislocation hits, cuts are amputation cut-progress,
/// stages/Shock are defibrillator transitions.
/// </summary>
public enum MedicalOperationUpdateAction : int
{
	None = 0,
	Wrap = 1,
	Hit = 2,
	Cut = 3,
	Stage = 4,
	Shock = 5,
}
