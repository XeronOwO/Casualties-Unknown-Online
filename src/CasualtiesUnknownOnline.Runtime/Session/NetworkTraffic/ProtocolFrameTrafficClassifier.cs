using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

/// <summary>
/// Reads the <see cref="WirePayloadType"/> from an already-decoded
/// <see cref="ProtocolFrame"/> without touching the data plane. The classifier
/// exists because one transport <see cref="NetMsg.KernelEnvelope"/> carries many
/// semantic payload families; send and receive paths both already have the
/// decoded frame at the points where they report semantic traffic.
/// </summary>
internal static class ProtocolFrameTrafficClassifier
{
	internal static WirePayloadType? TryGetPayloadType(ProtocolFrame? frame) => frame?.Kind switch
	{
		EnvelopeKind.Command => frame.Command?.Header?.PayloadType,
		EnvelopeKind.CommittedBatch => frame.CommittedBatch?.Header?.PayloadType,
		EnvelopeKind.Checkpoint => frame.Checkpoint?.Header?.PayloadType,
		EnvelopeKind.StateStream => frame.StateStream?.Header?.PayloadType,
		_ => null,
	};
}
