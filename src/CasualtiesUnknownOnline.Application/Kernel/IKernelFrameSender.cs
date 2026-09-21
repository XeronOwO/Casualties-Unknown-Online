using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The transport the kernel replication surface writes through. It speaks
/// protocol frames, never the transport's own message vocabulary: every kernel
/// frame rides the same envelope kind, so the port carries the frame and lets
/// the Runtime side choose the wire message that carries it.
/// </summary>
public interface IKernelFrameSender
{
	/// <summary>Send one frame to a peer.</summary>
	void Send(ulong targetSteamId, ProtocolFrame frame, bool reliable = true);

	/// <summary>Send one frame to a peer when a transport is available; false when it was not sent.</summary>
	bool TrySend(ulong targetSteamId, ProtocolFrame frame, bool reliable = true);

	/// <summary>Send one frame to every target.</summary>
	void SendToAll(IEnumerable<ulong> targetSteamIds, ProtocolFrame frame, bool reliable = true);
}
