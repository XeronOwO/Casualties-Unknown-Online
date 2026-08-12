using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

/// <summary>
/// The frame layer: [msgId:1][protobuf payload]. These tests are deliberately
/// transport-free — they lock the wire format contract (the first layer the
/// FakeTransport pipeline builds on) without touching any seam.
/// </summary>
public class NetPacketTests
{
	[Fact]
	public void Encode_WritesMsgIdThenPayload()
	{
		var frame = NetPacket.Encode(NetMsg.Handshake, new HandshakeMsg { Protocol = ProtocolVersion.Current });

		Assert.Equal((byte)NetMsg.Handshake, frame[0]);
		Assert.True(frame.Length > 1, "payload must follow the msgId byte");
	}

	[Fact]
	public void EncodeDecode_RoundTripsMessage()
	{
		var msg = new HandshakeMsg { Protocol = ProtocolVersion.Current };

		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, msg));

		Assert.Equal(ProtocolVersion.Current, decoded.Protocol);
	}

	[Fact]
	public void Encode_WithoutPayload_IsSingleByteFrame()
	{
		var frame = NetPacket.Encode(NetMsg.HandshakeAckAck);

		Assert.Equal(new byte[] { (byte)NetMsg.HandshakeAckAck }, frame);
	}

	[Fact]
	public void DecodePayload_ZeroValueOmission_RoundTripsToDefault()
	{
		// protobuf-net omits zero values on the wire — a zero field
		// deserializes back to its default. Round-trip of zero must be stable.
		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, new HandshakeMsg()));

		Assert.Equal(0, decoded.Protocol);
		Assert.NotNull(decoded.Scene);
	}
}
