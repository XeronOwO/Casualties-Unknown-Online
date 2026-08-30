using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The crystal-lunge result now rides the kernel journal: a local lunge is
/// recorded as a journal-only Entities domain event and the
/// <see cref="EnemyCombatKernelProjection"/> restores the post-lunge
/// presentation event on every peer except the source victim.
/// </summary>
public class EnemyLungeSyncTests
{
	private const ulong HostId = 1001;

	private static EnemyLungeMsg Lunge(ulong victim, int limbIndex = 0) => new()
	{
		VictimSteamId = victim,
		Limb = new CharacterLimbMsg
		{
			Index = limbIndex,
			SkinHealth = 50f,
			MuscleHealth = 65f,
			BleedAmount = 15f,
			Pain = 60f,
		},
		Adrenaline = 70f,
		Stamina = 100f,
	};

	[Fact]
	public void HostOwnLunge_ProjectsToBothGuests_AndNotOnHost()
	{
		using var w = ItemSimWorld.Create();
		var hostLunges = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Lunges = w.G1.Services.GetRequiredService<EnemySyncService>();
		var g2Lunges = w.G2.Services.GetRequiredService<EnemySyncService>();
		var hostApplied = 0;
		var g1Applied = 0;
		var g2Applied = 0;
		hostLunges.EnemyLungeReceived += (_, _) => hostApplied++;
		g1Lunges.EnemyLungeReceived += (_, _) => g1Applied++;
		g2Lunges.EnemyLungeReceived += (_, _) => g2Applied++;

		hostLunges.SendEnemyLunge(Lunge(HostId));
		w.Driver.Tick(33);

		Assert.Equal(0, hostApplied);
		Assert.Equal(1, g1Applied);
		Assert.Equal(1, g2Applied);
	}

	[Fact]
	public void GuestReport_HostApplies_OtherGuestProjects_SourceGuestSkips()
	{
		using var w = ItemSimWorld.Create();
		var hostLunges = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Lunges = w.G1.Services.GetRequiredService<EnemySyncService>();
		var g2Lunges = w.G2.Services.GetRequiredService<EnemySyncService>();
		var hostApplied = 0;
		var g1Applied = 0;
		var g2Applied = 0;
		hostLunges.EnemyLungeReceived += (_, _) => hostApplied++;
		g1Lunges.EnemyLungeReceived += (_, _) => g1Applied++;
		g2Lunges.EnemyLungeReceived += (_, _) => g2Applied++;

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyLunge(Lunge(w.G1.SteamId));
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
			Health = new CharacterHealthMsg { Adrenaline = 0f, Stamina = 0f },
			Limbs = [new CharacterLimbMsg { Index = 0, SkinHealth = 100f }],
		});

		w.G1.Services.GetRequiredService<EnemySyncService>().SendEnemyLunge(Lunge(w.G1.SteamId));
		w.Driver.Tick(33);

		var saved = hostData.GetSavedCharacter(w.G1.SteamId);
		Assert.NotNull(saved);
		Assert.Equal(70f, saved.Health!.Adrenaline);
		Assert.Equal(100f, saved.Health.Stamina);
		Assert.Equal(50f, saved.Limbs[0].SkinHealth);
	}
}
