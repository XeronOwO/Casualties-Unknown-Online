using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The character ragdoll-toggle event (CharacterRagdollMsg): a player's local
/// Body.Ragdoll collapses the body and travels as one dedicated reliable
/// message — guest reports reach the host (whose adapter replays the lying pose
/// on the guest's clone) and relay to the other guests; the host's own collapse
/// broadcasts to every guest. One collapse = one message; the 20 Hz entity
/// stream remains the fallback for the continuous standing flag.
/// </summary>
public class CharacterRagdollSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

	private static CharacterRagdollMsg Collapse(ulong owner = GuestId) => new()
	{
		OwnerSteamId = owner,
		Position = new NetVector2Msg { X = 11f, Y = -4f },
	};

	[Fact]
	public void CharacterRagdoll_RoundTripsEveryField()
	{
		var source = Collapse();

		var decoded = NetPacket.DecodePayload<CharacterRagdollMsg>(NetPacket.Encode(NetMsg.CharacterRagdoll, source));

		Assert.Equal(source.OwnerSteamId, decoded.OwnerSteamId);
		Assert.Equal(source.Position.X, decoded.Position.X);
		Assert.Equal(source.Position.Y, decoded.Position.Y);
	}

	[Fact]
	public void GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostStore = w.Host.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		hostStore.CharacterRagdollReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterRagdoll(Collapse(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must fire the received event (the adapter replays it)");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterRagdoll) == 1, "the host must relay the ragdoll to the other guest");
	}

	[Fact]
	public void HostOwnCollapse_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<CharacterDataStore>().SendCharacterRagdoll(Collapse(HostId));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.CharacterRagdoll) == 1, "the host's own collapse must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterRagdoll) == 1, "the host's own collapse must reach G2");
	}

	[Fact]
	public void GuestRelay_FiresTheEventOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2Store = w.G2.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		g2Store.CharacterRagdollReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterRagdoll(Collapse(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed ragdoll must fire the received event on the other guest");
	}
}
