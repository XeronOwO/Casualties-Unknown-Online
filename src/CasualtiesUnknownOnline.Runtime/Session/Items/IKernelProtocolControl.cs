using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Control surface for the Phase C four-envelope kernel protocol. The packet
/// handler and the item domain use this narrow interface; the ownership and
/// wiring live in <see cref="KernelProtocolService"/>.
/// </summary>
public interface IKernelProtocolControl
{
	void HandleFrame(ulong sender, ProtocolFrame frame);

	void SendCommand(WireCommand command, WirePayloadType payloadType);

	void BroadcastCommittedBatch(CommittedBatch batch);

	void SendCheckpoint(ulong targetSteamId);

	void ResetForSessionEnd();
}
