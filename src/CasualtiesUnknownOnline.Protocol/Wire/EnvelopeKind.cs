namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// The four production envelope families in the Phase C protocol. A frame
/// carries exactly one envelope; the kind is explicit so receivers can reject
/// unknown/unsupported envelopes before touching the payload.
/// </summary>
public enum EnvelopeKind
{
	Command = 1,
	CommittedBatch = 2,
	Checkpoint = 3,
	StateStream = 4,
}
