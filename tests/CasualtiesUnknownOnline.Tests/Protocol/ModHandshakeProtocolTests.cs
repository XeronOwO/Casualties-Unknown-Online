using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

/// <summary>
/// The Phase 4 Mod API wire additions: the handshake's mod list round-trips
/// exactly (id/version/NetworkMode), an old client's frame WITHOUT the field
/// decodes to null (the host treats null as an empty list — the protocol
/// version gate refuses cross-version sessions anyway), and the mod frame
/// round-trips its opaque payload byte-for-byte.
/// </summary>
public class ModHandshakeProtocolTests
{
	[Fact]
	public void HandshakeWithMods_RoundTripsExactly()
	{
		var msg = new HandshakeMsg
		{
			Protocol = ProtocolVersion.Current,
			Mods =
			[
				new ModInfoMsg { Id = "mod.a", Version = "1.2.3", NetworkMode = NetworkMode.RequiresAllPlayers },
				new ModInfoMsg { Id = "mod.b", Version = "0.9.0", NetworkMode = NetworkMode.ClientOnly },
			],
		};

		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, msg));

		Assert.NotNull(decoded.Mods);
		Assert.Equal(2, decoded.Mods!.Count);
		Assert.Equal("mod.a", decoded.Mods[0].Id);
		Assert.Equal("1.2.3", decoded.Mods[0].Version);
		Assert.Equal(NetworkMode.RequiresAllPlayers, decoded.Mods[0].NetworkMode);
		Assert.Equal(NetworkMode.ClientOnly, decoded.Mods[1].NetworkMode);
	}

	[Fact]
	public void HandshakeWithoutModsField_DecodesToNull()
	{
		// An old client's frame predates the field — protobuf leaves it null.
		var msg = new HandshakeMsg { Protocol = ProtocolVersion.Current };

		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, msg));

		Assert.Null(decoded.Mods);
	}

	[Fact]
	public void ModMessageFrame_RoundTripsOpaquePayload()
	{
		var payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray(); // includes zero bytes — the payload is opaque
		var msg = new ModMessageMsg { ModId = "mod.a", Payload = payload };

		var decoded = NetPacket.DecodePayload<ModMessageMsg>(NetPacket.Encode(NetMsg.ModMessage, msg));

		Assert.Equal("mod.a", decoded.ModId);
		Assert.Equal(payload, decoded.Payload);
	}
}
