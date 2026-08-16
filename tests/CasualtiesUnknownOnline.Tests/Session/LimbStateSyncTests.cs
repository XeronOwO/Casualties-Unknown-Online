using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The limb-latch event (LimbStateEventMsg): a player's break/mend/dislocate/
/// dismember applies locally and reports the FULL post-event limb + body
/// state as a dedicated event (never the 1 Hz snapshot). The host adopts it
/// (accept-first) and relays to the other members; a guest's report and the
/// host's own latch both reach the peers.
/// </summary>
public class LimbStateSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static LimbStateEventMsg LimbEvent(ulong owner = GuestId) => new()
	{
		OwnerSteamId = owner,
		Limbs =
		[
			new CharacterLimbMsg
			{
				Index = 0,
				SkinHealth = 80f,
				MuscleHealth = 90f,
				BleedAmount = 4f,
				FurBloodAmount = 0.97f,
				Pain = 12f,
				Broken = true,
			},
			new CharacterLimbMsg
			{
				Index = 1,
				SkinHealth = 100f,
				MuscleHealth = 100f,
			},
		],
		Health = new CharacterHealthMsg
		{
			Adrenaline = 75f,
			InternalBleeding = 10f,
			Happiness = 40f,
		},
	};

	[Fact]
	public void LimbStateEvent_RoundTripsTheFullPostEventState()
	{
		var source = LimbEvent();

		var decoded = NetPacket.DecodePayload<LimbStateEventMsg>(NetPacket.Encode(NetMsg.LimbStateEvent, source));

		Assert.Equal(source.OwnerSteamId, decoded.OwnerSteamId);
		Assert.Equal(2, decoded.Limbs.Count);
		Assert.Equal(source.Limbs[0].Index, decoded.Limbs[0].Index);
		Assert.Equal(source.Limbs[0].Broken, decoded.Limbs[0].Broken);
		Assert.Equal(source.Limbs[0].BleedAmount, decoded.Limbs[0].BleedAmount);
		Assert.Equal(source.Limbs[0].FurBloodAmount, decoded.Limbs[0].FurBloodAmount);
		Assert.Equal(source.Health!.Adrenaline, decoded.Health!.Adrenaline);
		Assert.Equal(source.Health!.InternalBleeding, decoded.Health!.InternalBleeding);
		Assert.Equal(source.Health!.Happiness, decoded.Health!.Happiness);
	}

	[Fact]
	public void LimbIndexZero_IsAValidFirstLimb_AndRoundTrips()
	{
		// Limb index 0 (the head) is valid — protobuf omits zero, and the
		// omission must decode back to 0 (the same discipline as EnemyBiteMsg).
		var decoded = NetPacket.DecodePayload<LimbStateEventMsg>(
			NetPacket.Encode(NetMsg.LimbStateEvent, new LimbStateEventMsg
			{
				Limbs = [new CharacterLimbMsg { Index = 0, Broken = true }],
			}));

		Assert.Single(decoded.Limbs);
		Assert.Equal(0, decoded.Limbs[0].Index);
	}

	[Fact]
	public void GuestReport_HostAppliesAndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostStore = w.Host.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		hostStore.LimbStateEventReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendLimbStateEvent(LimbEvent(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must apply the guest's limb-latch report");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.LimbStateEvent) == 1, "the host must relay the latch to the other guest");
	}

	[Fact]
	public void HostOwnLimbLatch_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<CharacterDataStore>().SendLimbStateEvent(LimbEvent(HostId));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.LimbStateEvent) == 1, "the host's own latch must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.LimbStateEvent) == 1, "the host's own latch must reach G2");
	}

	[Fact]
	public void GuestRelay_AppliesOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2Store = w.G2.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		g2Store.LimbStateEventReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendLimbStateEvent(LimbEvent(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed latch must fire the received event on the other guest");
	}

	[Fact]
	public void HostReport_MergesTheFullStateIntoTheSavedCharacterImmediately()
	{
		using var w = ItemSimWorld.Create();
		var hostStore = w.Host.Services.GetRequiredService<CharacterDataStore>();
		hostStore.SaveCharacterData(w.G1.SteamId, new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { Happiness = 1f },
			Limbs =
			[
				new CharacterLimbMsg { Index = 0, Pain = 1f },
				new CharacterLimbMsg { Index = 3, Broken = true },
			],
		});

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendLimbStateEvent(LimbEvent(w.G1.SteamId));
		w.Driver.Tick(33);

		var saved = hostStore.GetSavedCharacter(w.G1.SteamId);
		Assert.True(saved != null, "the host must keep the saved character");
		Assert.True(saved!.Health!.Happiness == 40f, "the body terminal state must merge immediately");
		Assert.True(saved.Health.Adrenaline == 75f, "the post-event adrenaline must merge immediately");
		Assert.Equal(2, saved.Limbs.Count);
		Assert.True(saved.Limbs[0].Broken, "the event's full limb set replaces the saved limb set");
		Assert.False(saved.Limbs[1].Broken);
	}
}
