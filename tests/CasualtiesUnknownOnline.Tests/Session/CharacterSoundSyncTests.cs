using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The character-sound event (CharacterSoundMsg): a player's attack/throw/
/// exert sound plays locally and travels as one dedicated reliable message —
/// guest reports reach the host (whose adapter replays it on the guest's
/// clone) and relay to the other guests; the host's own sound broadcasts to
/// every guest. One sound = one message; there is no snapshot fallback to
/// assert (the sound has no persistent state).
/// </summary>
public class CharacterSoundSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static CharacterSoundMsg Sound(ulong owner = GuestId, CharacterSoundKind kind = CharacterSoundKind.AttackSwing) => new()
	{
		OwnerSteamId = owner,
		Kind = kind,
		Clip = kind == CharacterSoundKind.Exert ? "exert2" : "BSSwing3",
		Position = new NetVector2Msg { X = 10f, Y = 20f },
		Volume = 0.7f,
		FollowOwner = kind != CharacterSoundKind.ThrowSwing,
		TwoDimensional = kind == CharacterSoundKind.Exert,
	};

	[Fact]
	public void CharacterSound_RoundTripsEveryField()
	{
		var source = Sound();

		var decoded = NetPacket.DecodePayload<CharacterSoundMsg>(NetPacket.Encode(NetMsg.CharacterSound, source));

		Assert.Equal(source.OwnerSteamId, decoded.OwnerSteamId);
		Assert.Equal(source.Kind, decoded.Kind);
		Assert.Equal(source.Clip, decoded.Clip);
		Assert.Equal(source.Position.X, decoded.Position.X);
		Assert.Equal(source.Position.Y, decoded.Position.Y);
		Assert.Equal(source.Volume, decoded.Volume);
		Assert.Equal(source.FollowOwner, decoded.FollowOwner);
		Assert.Equal(source.TwoDimensional, decoded.TwoDimensional);
	}

	[Fact]
	public void GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostStore = w.Host.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		hostStore.CharacterSoundReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterSound(Sound(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must fire the received event (the adapter replays it)");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterSound) == 1, "the host must relay the sound to the other guest");
	}

	[Fact]
	public void HostOwnSound_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<CharacterDataStore>().SendCharacterSound(Sound(HostId));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.CharacterSound) == 1, "the host's own sound must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterSound) == 1, "the host's own sound must reach G2");
	}

	[Fact]
	public void GuestRelay_FiresTheEventOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2Store = w.G2.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		g2Store.CharacterSoundReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterSound(Sound(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed sound must fire the received event on the other guest");
	}
}
