namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The category of a remote medical operation. Stage 1 implements injection;
/// Stage 2 adds the shared shrapnel session. The session envelope and the
/// terminal/state messages are shared; only the payload shape differs.
/// </summary>
public enum MedicalOperationKind : int
{
	Injection = 1,
	Shrapnel = 2,
}
