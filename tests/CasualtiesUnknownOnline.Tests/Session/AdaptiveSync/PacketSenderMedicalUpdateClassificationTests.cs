using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class PacketSenderMedicalUpdateClassificationTests
{
	private const ulong HostId = 1001;
	private const ulong PeerId = 2001;

	[Fact]
	public void MedicalUpdateMessages_ClassifyBySubStream()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var transport = new FakeTransport(HostId, network);
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		var sender = new PacketSender(transport, monitor);

		sender.Send(PeerId, NetMsg.MedicalOperationUpdate, new MedicalOperationUpdateMsg
		{
			OperationId = 1,
			PieceIndex = -1,
			DeltaMl = 5f,
		}, reliable: true);

		sender.Send(PeerId, NetMsg.MedicalOperationUpdate, new MedicalOperationUpdateMsg
		{
			OperationId = 2,
			PieceIndex = 0,
			X = 1f,
			Y = 2f,
			Grabbed = true,
		}, reliable: false);

		sender.Send(PeerId, NetMsg.MedicalOperationUpdate, new MedicalOperationUpdateMsg
		{
			OperationId = 2,
			PieceIndex = 0,
			X = 3f,
			Y = 4f,
			Grabbed = true,
			OwnershipChange = true,
		}, reliable: true);

		var window = monitor.CurrentWindow;
		Assert.True(window.SendByPeerPayloadType.TryGetValue((PeerId, WirePayloadType.MedicalInjectionUpdate), out var injection));
		Assert.Equal(1, injection.Count);
		Assert.True(window.SendByPeerPayloadType.TryGetValue((PeerId, WirePayloadType.MedicalShrapnelPositionUpdate), out var shrapnel));
		Assert.Equal(1, shrapnel.Count);
		Assert.True(window.SendByPeerPayloadType.TryGetValue((PeerId, WirePayloadType.MedicalOperationOtherUpdate), out var other));
		Assert.Equal(1, other.Count);
	}
}
