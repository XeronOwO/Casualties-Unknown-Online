using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The character-action event (CharacterSoundMsg): a player's attack/throw/
/// exert/gunfire/footstep/landing plays locally and travels as one dedicated
/// reliable message — guest reports reach the host (whose adapter replays it on
/// the guest's clone) and relay to the other guests; the host's own event
/// broadcasts to every guest. One event = one message; there is no snapshot
/// fallback to assert (the event has no persistent state).
/// </summary>
public class CharacterSoundSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static CharacterSoundMsg Sound(ulong owner = GuestId, CharacterSoundKind kind = CharacterSoundKind.AttackSwing, float recoilDegrees = 0f) => new()
	{
		OwnerSteamId = owner,
		Kind = kind,
		Clip = kind switch
		{
			CharacterSoundKind.Exert => "exert2",
			CharacterSoundKind.GunFire => "rifleshot",
			CharacterSoundKind.Footstep => "footstep/Rock/RockStep1",
			CharacterSoundKind.LandingImpact => "bodyFall1",
			_ => "BSSwing3",
		},
		Position = new NetVector2Msg { X = 10f, Y = 20f },
		Volume = 0.7f,
		FollowOwner = kind != CharacterSoundKind.ThrowSwing && kind != CharacterSoundKind.GunFire,
		TwoDimensional = kind == CharacterSoundKind.Exert || kind == CharacterSoundKind.GunFire,
		RecoilDegrees = recoilDegrees,
	};

	[Fact]
	public void CharacterSound_RoundTripsEveryField()
	{
		var source = Sound(recoilDegrees: 5.5f);

		var decoded = NetPacket.DecodePayload<CharacterSoundMsg>(NetPacket.Encode(NetMsg.CharacterSound, source));

		Assert.Equal(source.OwnerSteamId, decoded.OwnerSteamId);
		Assert.Equal(source.Kind, decoded.Kind);
		Assert.Equal(source.Clip, decoded.Clip);
		Assert.Equal(source.Position.X, decoded.Position.X);
		Assert.Equal(source.Position.Y, decoded.Position.Y);
		Assert.Equal(source.Volume, decoded.Volume);
		Assert.Equal(source.FollowOwner, decoded.FollowOwner);
		Assert.Equal(source.TwoDimensional, decoded.TwoDimensional);
		Assert.Equal(source.RecoilDegrees, decoded.RecoilDegrees);
	}

	[Fact]
	public void GunFire_RoundTripsRecoilDegrees()
	{
		var source = Sound(kind: CharacterSoundKind.GunFire, recoilDegrees: 12f);

		var decoded = NetPacket.DecodePayload<CharacterSoundMsg>(NetPacket.Encode(NetMsg.CharacterSound, source));

		Assert.Equal(CharacterSoundKind.GunFire, decoded.Kind);
		Assert.Equal("rifleshot", decoded.Clip);
		Assert.Equal(12f, decoded.RecoilDegrees);
		Assert.False(decoded.FollowOwner);
		Assert.True(decoded.TwoDimensional);
	}

	[Fact]
	public void FootstepAndLandingImpact_RoundTripTheirKindsAndClips()
	{
		var footstep = NetPacket.DecodePayload<CharacterSoundMsg>(
			NetPacket.Encode(NetMsg.CharacterSound, Sound(kind: CharacterSoundKind.Footstep)));
		Assert.Equal(CharacterSoundKind.Footstep, footstep.Kind);
		Assert.Equal("footstep/Rock/RockStep1", footstep.Clip);

		var landing = NetPacket.DecodePayload<CharacterSoundMsg>(
			NetPacket.Encode(NetMsg.CharacterSound, Sound(kind: CharacterSoundKind.LandingImpact)));
		Assert.Equal(CharacterSoundKind.LandingImpact, landing.Kind);
		Assert.Equal("bodyFall1", landing.Clip);
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
