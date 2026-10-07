using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: a member reached the end of the layer and chose to continue.
/// The choice itself is the member's own — the native end-of-layer panel appears
/// on the client whose own body is at the layer's bottom, and that client's
/// "Continue" is the judgment that the layer is finished — while the layer is
/// the host's to generate: the generation baseline (RNG state, world-defining
/// fields, rarity multipliers) is captured on the host's boundary.
/// <para>
/// The request therefore carries the requester's kernel generation stamp, the
/// same (run epoch, layer index) identity every layer-relative world report
/// carries, so the host arbitrates it against the identity it stamps its own
/// reports with: a request naming the layer the host is still in is the
/// session's next step, and one naming any other layer — or none at all, when
/// the requester has no committed run baseline — is refused rather than guessed
/// at. That stamp is what makes "the layer advances once" checkable when two
/// members choose at the same moment.
/// </para>
/// </summary>
[ProtoContract]
public sealed class LayerAdvanceRequestMsg
{
	/// <summary>The requester's kernel generation when it chose; null when this peer holds no committed run baseline.</summary>
	[ProtoMember(1)]
	public WorldGenerationMsg? Generation { get; set; }
}
