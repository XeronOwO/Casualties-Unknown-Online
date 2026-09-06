using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Maps a logical adaptive stream to the wire observation that
/// <see cref="NetworkTraffic.NetworkTrafficWindow"/> already records.
/// High-frequency kernel streams appear as <see cref="WirePayloadType"/>;
/// a few direct streams such as the tutorial claw appear as a plain
/// <see cref="NetMsg"/>. The mapper keeps the adaptive layer decoupled from
/// generic traffic storage: the traffic monitor never needs to know about
/// adaptive stream ids.
///
/// All Stage 2 callers drive the sender side of a stream, so the send
/// direction in <see cref="NetworkTraffic.NetworkTrafficWindow"/> is the
/// relevant observation; the same payload type is intentionally shared by
/// <see cref="AdaptiveStreamId.PlayerStateBroadcast"/> and
/// <see cref="AdaptiveStreamId.PlayerStateReport"/> because they are the same
/// wire family on opposite roles.
/// </summary>
internal static class AdaptiveStreamWireMapper
{
	internal static bool TryGet(AdaptiveStreamId streamId, out WirePayloadType? payloadType, out NetMsg? message)
	{
		switch (streamId)
		{
			case AdaptiveStreamId.PlayerStateBroadcast:
			case AdaptiveStreamId.PlayerStateReport:
				payloadType = WirePayloadType.PlayerStateStream;
				message = null;
				return true;
			case AdaptiveStreamId.EnemyStateBroadcast:
				payloadType = WirePayloadType.EnemyStateStream;
				message = null;
				return true;
			case AdaptiveStreamId.TutorialClawBroadcast:
				payloadType = null;
				message = NetMsg.TutorialClawState;
				return true;
			default:
				payloadType = null;
				message = null;
				return false;
		}
	}
}
