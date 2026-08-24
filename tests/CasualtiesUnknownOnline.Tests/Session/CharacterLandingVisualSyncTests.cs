using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The character landing-visual event (CharacterLandingVisualMsg): a player's
/// Body.HandleGroundedState already played the Grounded clip and spawned the
/// native landing dust locally; the exact cloud size/anchor/velocity travels as
/// one dedicated reliable message — guest reports reach the host (whose
/// adapter replays it on the guest's clone) and relay to the other guests; the
/// host's own landing broadcasts to every guest. One landing = one message;
/// there is no snapshot fallback to assert (the visual has no persistent state).
/// </summary>
public class CharacterLandingVisualSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

	private static CharacterLandingVisualMsg BigLanding(ulong owner = GuestId) => new()
	{
		OwnerSteamId = owner,
		CloudSize = CharacterLandingVisualMsg.CloudBig,
		Position = new NetVector2Msg { X = 10f, Y = 20f },
		VelocityX = 2.5f,
	};

	[Fact]
	public void CharacterLandingVisual_RoundTripsEveryField()
	{
		var source = BigLanding();

		var decoded = NetPacket.DecodePayload<CharacterLandingVisualMsg>(NetPacket.Encode(NetMsg.CharacterLandingVisual, source));

		Assert.Equal(source.OwnerSteamId, decoded.OwnerSteamId);
		Assert.Equal(source.CloudSize, decoded.CloudSize);
		Assert.Equal(source.Position.X, decoded.Position.X);
		Assert.Equal(source.Position.Y, decoded.Position.Y);
		Assert.Equal(source.VelocityX, decoded.VelocityX);
	}

	[Fact]
	public void GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostStore = w.Host.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		hostStore.CharacterLandingVisualReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterLandingVisual(BigLanding(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must fire the received event (the adapter replays it)");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterLandingVisual) == 1, "the host must relay the landing visual to the other guest");
	}

	[Fact]
	public void HostOwnLanding_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<CharacterDataStore>().SendCharacterLandingVisual(BigLanding(HostId));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.CharacterLandingVisual) == 1, "the host's own landing must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterLandingVisual) == 1, "the host's own landing must reach G2");
	}

	[Fact]
	public void GuestRelay_FiresTheEventOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2Store = w.G2.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		g2Store.CharacterLandingVisualReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterLandingVisual(BigLanding(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed landing must fire the received event on the other guest");
	}
}
