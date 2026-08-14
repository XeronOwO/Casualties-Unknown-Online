using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-bite event (EnemyBiteMsg): a player bitten by an enemy reports the
/// post-bite limb/body state as a dedicated event (never the 1 Hz snapshot).
/// The host adopts it (accept-first) and relays to the other members; a
/// guest's report and the host's own bite both reach the peers.
/// </summary>
public class EnemyBiteSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static EnemyBiteMsg Bite(ulong victim = GuestId, int limbIndex = 0) => new()
	{
		VictimSteamId = victim,
		Limb = new CharacterLimbMsg
		{
			Index = limbIndex,
			SkinHealth = 80f,
			MuscleHealth = 90f,
			BleedAmount = 4f,
			Pain = 12f,
			Infected = true,
			InfectionAmount = 1f,
		},
		VenomTotal = 3f,
		Adrenaline = 75f,
		Happiness = -0.75f,
	};

	[Fact]
	public void EnemyBite_RoundTripsThePostBiteState()
	{
		var source = Bite();

		var decoded = NetPacket.DecodePayload<EnemyBiteMsg>(NetPacket.Encode(NetMsg.EnemyBite, source));

		Assert.Equal(source.VictimSteamId, decoded.VictimSteamId);
		Assert.Equal(source.Limb.Index, decoded.Limb.Index);
		Assert.Equal(source.Limb.SkinHealth, decoded.Limb.SkinHealth);
		Assert.Equal(source.Limb.MuscleHealth, decoded.Limb.MuscleHealth);
		Assert.Equal(source.Limb.BleedAmount, decoded.Limb.BleedAmount);
		Assert.Equal(source.Limb.Pain, decoded.Limb.Pain);
		Assert.True(decoded.Limb.Infected);
		Assert.Equal(source.Limb.InfectionAmount, decoded.Limb.InfectionAmount);
		Assert.Equal(source.VenomTotal, decoded.VenomTotal);
		Assert.Equal(source.Adrenaline, decoded.Adrenaline);
		Assert.Equal(source.Happiness, decoded.Happiness);
	}

	[Fact]
	public void LimbIndexZero_IsAValidFirstLimb_AndRoundTrips()
	{
		// Limb index 0 (the first limb) is valid — protobuf omits zero, and the
		// omission must decode back to 0 (the same discipline as RecipeUnlockMsg).
		var decoded = NetPacket.DecodePayload<EnemyBiteMsg>(
			NetPacket.Encode(NetMsg.EnemyBite, new EnemyBiteMsg { Limb = new CharacterLimbMsg { Index = 0 } }));

		Assert.Equal(0, decoded.Limb.Index);
	}

	[Fact]
	public void GuestReport_HostAppliesAndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostBites = w.Host.Services.GetRequiredService<EnemySyncService>();
		var applied = 0;
		hostBites.EnemyBiteReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyBite(Bite(victim: w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must apply the victim's bite report");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EnemyBite) == 1, "the host must relay the bite to the other guest");
	}

	[Fact]
	public void HostOwnBite_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();
		var hostBites = w.Host.Services.GetRequiredService<EnemySyncService>();

		hostBites.SendEnemyBite(Bite(victim: HostId));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.EnemyBite) == 1, "the host's own bite must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EnemyBite) == 1, "the host's own bite must reach G2");
	}

	[Fact]
	public void GuestRelay_AppliesOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2Bites = w.G2.Services.GetRequiredService<EnemySyncService>();
		var applied = 0;
		g2Bites.EnemyBiteReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyBite(Bite(victim: w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed bite must fire the received event on the other guest");
	}
}
