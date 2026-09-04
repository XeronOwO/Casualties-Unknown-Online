using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-bite result now rides the kernel journal: a local bite is
/// recorded as a journal-only Entities domain event and the
/// <see cref="EnemyCombatKernelProjection"/> restores the post-bite
/// presentation event on every peer except the source victim. No legacy direct
/// result wire remains.
/// </summary>
public class EnemyBiteSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

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
	public void HostOwnBite_ProjectsToBothGuests_AndNotOnHost()
	{
		using var w = ItemSimWorld.Create();
		var hostBites = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Bites = w.G1.Services.GetRequiredService<EnemySyncService>();
		var g2Bites = w.G2.Services.GetRequiredService<EnemySyncService>();
		var hostApplied = 0;
		var g1Applied = 0;
		var g2Applied = 0;
		hostBites.EnemyBiteReceived += (_, _) => hostApplied++;
		g1Bites.EnemyBiteReceived += (_, _) => g1Applied++;
		g2Bites.EnemyBiteReceived += (_, _) => g2Applied++;

		hostBites.SendEnemyBite(Bite(victim: HostId));
		w.Driver.Tick(33);

		Assert.Equal(0, hostApplied);
		Assert.Equal(1, g1Applied);
		Assert.Equal(1, g2Applied);
	}

	[Fact]
	public void GuestReport_HostApplies_OtherGuestProjects_SourceGuestSkips()
	{
		using var w = ItemSimWorld.Create();
		var hostBites = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Bites = w.G1.Services.GetRequiredService<EnemySyncService>();
		var g2Bites = w.G2.Services.GetRequiredService<EnemySyncService>();
		var hostApplied = 0;
		var g1Applied = 0;
		var g2Applied = 0;
		hostBites.EnemyBiteReceived += (_, _) => hostApplied++;
		g1Bites.EnemyBiteReceived += (_, _) => g1Applied++;
		g2Bites.EnemyBiteReceived += (_, _) => g2Applied++;

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyBite(Bite(victim: w.G1.SteamId));
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
			Health = new CharacterHealthMsg { VenomTotal = 0f, Adrenaline = 0f, Happiness = 0f },
			Limbs = [new CharacterLimbMsg { Index = 0, SkinHealth = 100f }],
		});

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyBite(Bite(victim: w.G1.SteamId));
		w.Driver.Tick(33);

		var saved = hostData.GetSavedCharacter(w.G1.SteamId);
		Assert.NotNull(saved);
		Assert.Equal(3f, saved.Health!.VenomTotal);
		Assert.Equal(75f, saved.Health.Adrenaline);
		Assert.Equal(-0.75f, saved.Health.Happiness);
		Assert.Equal(80f, saved.Limbs[0].SkinHealth);
	}
}
