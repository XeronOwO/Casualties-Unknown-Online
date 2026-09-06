namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The category of a remote medical operation. Stage 1 only implements
/// injection; later stages add shaped session payloads without changing the
/// session envelope.
/// </summary>
public enum MedicalOperationKind : int
{
	Injection = 1,
}
