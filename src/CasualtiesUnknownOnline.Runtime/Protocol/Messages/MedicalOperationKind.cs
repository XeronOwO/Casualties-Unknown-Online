namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The category of a remote medical operation. Stage 1 implements injection;
/// Stage 2 adds the shared shrapnel session; Stage 3 adds the remaining native
/// medical minigames/actions. The session envelope and the terminal/state
/// messages are shared; only the payload shape differs.
/// </summary>
public enum MedicalOperationKind : int
{
	Injection = 1,
	Shrapnel = 2,
	Bandage = 3,
	SplintRemoval = 4,
	TourniquetRemoval = 5,
	Dislocation = 6,
	Aed = 7,
	ManualDefib = 8,
	Amputation = 9,
}
