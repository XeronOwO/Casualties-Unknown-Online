using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-proximity effect event (EnemyEffectMsg): a player affected by an
/// ElderThornback / Xaloris / GrabberPlant side effect reports the post-effect
/// body state as a dedicated event (never the 1 Hz snapshot). The host adopts
/// it (accept-first) and relays to the other members; a guest's report and the
/// host's own effect both reach the peers.
/// </summary>
public class EnemyEffectSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static EnemyEffectMsg Effect(EnemyEffectKind kind = EnemyEffectKind.ElderHorrorTick, ulong victim = GuestId) => new()
	{
		VictimSteamId = victim,
		Kind = kind,
		HorrifiedLevel = 100f,
		FocusedLevel = 100f,
		Adrenaline = 50f,
		Energy = 15f,
		Stamina = 61f,
		Happiness = 40f,
		Caffeinated = 600f,
		SepticShock = 12.074f,
		Shock = 20f,
		EyePanicTime = 0.5f,
	};

	[Fact]
	public void EnemyEffect_RoundTripsEveryKindAndItsFields()
	{
		foreach (var kind in new[] { EnemyEffectKind.ElderHorrorTick, EnemyEffectKind.ElderHorrorDefeat, EnemyEffectKind.XalorisSepticTick, EnemyEffectKind.GrabberGrabbed })
		{
			var source = Effect(kind);
			var decoded = NetPacket.DecodePayload<EnemyEffectMsg>(NetPacket.Encode(NetMsg.EnemyEffect, source));

			Assert.Equal(source.VictimSteamId, decoded.VictimSteamId);
			Assert.Equal(source.Kind, decoded.Kind);
			Assert.Equal(source.HorrifiedLevel, decoded.HorrifiedLevel);
			Assert.Equal(source.FocusedLevel, decoded.FocusedLevel);
			Assert.Equal(source.Adrenaline, decoded.Adrenaline);
			Assert.Equal(source.Energy, decoded.Energy);
			Assert.Equal(source.Stamina, decoded.Stamina);
			Assert.Equal(source.Happiness, decoded.Happiness);
			Assert.Equal(source.Caffeinated, decoded.Caffeinated);
			Assert.Equal(source.SepticShock, decoded.SepticShock);
			Assert.Equal(source.Shock, decoded.Shock);
			Assert.Equal(source.EyePanicTime, decoded.EyePanicTime);
		}
	}

	[Fact]
	public void EnemyEffect_ZeroValuedFields_DecodeToZero()
	{
		// Every kind carries only its own fields — the omitted (zero) fields must
		// decode back to zero, never to another kind's payload.
		var decoded = NetPacket.DecodePayload<EnemyEffectMsg>(
			NetPacket.Encode(NetMsg.EnemyEffect, new EnemyEffectMsg
			{
				VictimSteamId = GuestId,
				Kind = EnemyEffectKind.XalorisSepticTick,
				SepticShock = 0.074f,
			}));

		Assert.Equal(EnemyEffectKind.XalorisSepticTick, decoded.Kind);
		Assert.Equal(0.074f, decoded.SepticShock);
		Assert.Equal(0f, decoded.Shock);
		Assert.Equal(0f, decoded.HorrifiedLevel);
	}

	[Fact]
	public void GuestReport_HostAppliesAndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostEffects = w.Host.Services.GetRequiredService<EnemySyncService>();
		var applied = 0;
		hostEffects.EnemyEffectReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyEffect(Effect(victim: w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must apply the victim's effect report");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EnemyEffect) == 1, "the host must relay the effect to the other guest");
	}

	[Fact]
	public void HostOwnEffect_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();
		var hostEffects = w.Host.Services.GetRequiredService<EnemySyncService>();

		hostEffects.SendEnemyEffect(Effect(victim: HostId));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.EnemyEffect) == 1, "the host's own effect must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EnemyEffect) == 1, "the host's own effect must reach G2");
	}

	[Fact]
	public void GuestRelay_AppliesOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2Effects = w.G2.Services.GetRequiredService<EnemySyncService>();
		var applied = 0;
		g2Effects.EnemyEffectReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyEffect(Effect(victim: w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed effect must fire the received event on the other guest");
	}

	[Fact]
	public void GuestReport_MergesIntoTheHostSavedCharacter()
	{
		using var w = ItemSimWorld.Create();
		var hostData = w.Host.Services.GetRequiredService<CharacterDataStore>();
		hostData.SaveCharacterData(w.G1.SteamId, new CharacterDataMsg
		{
			OwnerSteamId = w.G1.SteamId,
			Health = new CharacterHealthMsg { SepticShock = 0f, Shock = 0f, HorrifiedLevel = 0f },
		});

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyEffect(Effect(
			EnemyEffectKind.XalorisSepticTick, victim: w.G1.SteamId));
		w.Driver.Tick(33);

		var saved = hostData.GetSavedCharacter(w.G1.SteamId);
		Assert.NotNull(saved);
		Assert.Equal(12.074f, saved.Health!.SepticShock);
		Assert.Equal(0f, saved.Health!.Shock);
	}
}
