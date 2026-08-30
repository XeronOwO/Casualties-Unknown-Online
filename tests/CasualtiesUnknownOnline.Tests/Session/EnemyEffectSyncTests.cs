using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-proximity side-effect result now rides the kernel journal: the
/// affected player records the post-effect body facts as a journal-only
/// Entities domain event and the projection restores the presentation event on
/// every peer except the source victim. No legacy direct result wire remains.
/// </summary>
public class EnemyEffectSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

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
	public void HostOwnEffect_ProjectsToBothGuests_AndNotOnHost()
	{
		using var w = ItemSimWorld.Create();
		var hostEffects = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Effects = w.G1.Services.GetRequiredService<EnemySyncService>();
		var g2Effects = w.G2.Services.GetRequiredService<EnemySyncService>();
		var hostApplied = 0;
		var g1Applied = 0;
		var g2Applied = 0;
		hostEffects.EnemyEffectReceived += (_, _) => hostApplied++;
		g1Effects.EnemyEffectReceived += (_, _) => g1Applied++;
		g2Effects.EnemyEffectReceived += (_, _) => g2Applied++;

		hostEffects.SendEnemyEffect(Effect(victim: HostId));
		w.Driver.Tick(33);

		Assert.Equal(0, hostApplied);
		Assert.Equal(1, g1Applied);
		Assert.Equal(1, g2Applied);
	}

	[Fact]
	public void GuestReport_HostApplies_OtherGuestProjects_SourceGuestSkips()
	{
		using var w = ItemSimWorld.Create();
		var hostEffects = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Effects = w.G1.Services.GetRequiredService<EnemySyncService>();
		var g2Effects = w.G2.Services.GetRequiredService<EnemySyncService>();
		var hostApplied = 0;
		var g1Applied = 0;
		var g2Applied = 0;
		hostEffects.EnemyEffectReceived += (_, _) => hostApplied++;
		g1Effects.EnemyEffectReceived += (_, _) => g1Applied++;
		g2Effects.EnemyEffectReceived += (_, _) => g2Applied++;

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyEffect(Effect(victim: w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.Equal(1, hostApplied);
		Assert.Equal(0, g1Applied);
		Assert.Equal(1, g2Applied);
	}

	[Fact]
	public void GuestReport_MergesIntoHostSavedCharacter()
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
		Assert.Equal(0f, saved.Health.Shock);
	}
}
