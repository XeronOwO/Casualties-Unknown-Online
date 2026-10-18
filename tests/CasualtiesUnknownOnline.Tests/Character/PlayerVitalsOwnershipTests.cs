using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The ownership seam between a member's 1 Hz report and the host's HOST-ARBITRATED writes to
/// the same saved character snapshot (the 2026-10-18 ruling): a report may refresh the model's
/// inputs but never the continuous values the host owns, while an event the host judged — a
/// revive, an amputation, a shrapnel wound — writes them and keeps them. These tests use the
/// REAL store and the real applier policies, because the fake store has no filter to get wrong.
/// </summary>
[Trait("Category", "Integration")]
public class PlayerVitalsOwnershipTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void AReviveSave_KeepsTheRevivedBloodVolume()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		var dead = Snapshot(bloodVolume: 0f, bleed: 6f);
		dead.Health = new CharacterHealthMsg { BloodVolume = 0f, Alive = false, Conscious = false };
		store.SaveCharacterData(GuestId, dead);

		// The real revive path: the policy builds a fresh health block with blood volume 100 and
		// the coordinator saves it over the corpse's snapshot (TraderRecruitCoordinator).
		var revived = TraderRecruitPolicy.PrepareRevive(dead);
		store.SaveCharacterData(GuestId, revived);

		Assert.Equal(100f, store.GetSavedCharacter(GuestId)!.Health!.BloodVolume, 3);
	}

	[Fact]
	public void AnAmputationSave_KeepsTheZeroedLimbBleed()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 4f));

		// The real amputation path clones the stored snapshot and writes the stump's bleed.
		var amputated = PlayerCharacterAccess.CloneCharacter(store.GetSavedCharacter(GuestId)!);
		amputated.Limbs[0].BleedAmount = 0f;
		store.SaveCharacterData(GuestId, amputated);

		Assert.Equal(0f, store.GetSavedCharacter(GuestId)!.Limbs[0].BleedAmount, 3);
	}

	[Fact]
	public void APlainReport_CannotStateTheOwnedVitals()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f));

		// The ordinary 1 Hz report: fresh inputs, but the three owned values are the host's.
		store.SaveReportedCharacterData(GuestId, Snapshot(bloodVolume: 10f, bleed: 0f, totalBleedSpeed: 9f));

		var stored = store.GetSavedCharacter(GuestId)!;
		Assert.Equal(80f, stored.Health!.BloodVolume, 3);
		Assert.Equal(0.5f, stored.Health.TotalBleedSpeed, 3);
		Assert.Equal(5f, stored.Limbs[0].BleedAmount, 3);
	}

	[Fact]
	public void ANonFiniteReport_IsRefusedWholeAndThePreviousSnapshotStays()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		var previous = Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f);
		store.SaveCharacterData(GuestId, previous);

		// One corrupt input would otherwise poison the host clone's native rules; the old
		// snapshot (and every value in it) must stay exactly as it was.
		var corrupt = Snapshot(bloodVolume: 10f, bleed: 0f, totalBleedSpeed: 9f);
		corrupt.Health!.BloodViscosity = float.NaN;

		Assert.False(store.SaveReportedCharacterData(GuestId, corrupt));
		Assert.Same(previous, store.GetSavedCharacter(GuestId));

		// A finite report afterwards is stored again, and the owned values stay the host's.
		Assert.True(store.SaveReportedCharacterData(GuestId, Snapshot(bloodVolume: 10f, bleed: 0f, totalBleedSpeed: 9f)));
		var stored = store.GetSavedCharacter(GuestId)!;
		Assert.Equal(80f, stored.Health!.BloodVolume, 3);
		Assert.Equal(0.5f, stored.Health.TotalBleedSpeed, 3);
	}

	[Fact]
	public void ClearingTheSavedCharacters_RaisesTheResetSignal()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		var raised = 0;
		store.SavedCharactersCleared += () => raised++;
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 5f));

		store.ClearSavedCharacters();

		Assert.Equal(1, raised);
	}

	[Fact]
	public void TheFirstReport_SeedsTheOwnedValues_AndLaterReportsCannotChangeThem()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();

		// A fresh join has no prior snapshot: the report IS the only baseline source, and the census
		// says so. What must hold is that every LATER report is filtered.
		Assert.True(store.SaveReportedCharacterData(GuestId, Snapshot(bloodVolume: 60f, bleed: 3f, totalBleedSpeed: 0.3f)));
		Assert.True(store.SaveReportedCharacterData(GuestId, Snapshot(bloodVolume: 10f, bleed: 0f, totalBleedSpeed: 9f)));

		var stored = store.GetSavedCharacter(GuestId)!;
		Assert.Equal(60f, stored.Health!.BloodVolume, 3);
		Assert.Equal(0.3f, stored.Health.TotalBleedSpeed, 3);
		Assert.Equal(3f, stored.Limbs[0].BleedAmount, 3);
	}

	[Fact]
	public void AnOwnerStateEvent_KeepsTheHostBloodAndTakesTheInjuryBleed()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f));

		// The injury event is the owner's post-injury limb state plus its body health. The limb
		// bleed (the injury) must be adopted; the two continuous blood values are the host's.
		store.ApplyLimbStateEvent(new LimbStateEventMsg
		{
			OwnerSteamId = GuestId,
			Limbs =
			[
				new CharacterLimbMsg { Index = 0, BleedAmount = 12f, SkinHealth = 30f, Broken = true },
				new CharacterLimbMsg { Index = 1, BleedAmount = 0f, SkinHealth = 100f },
			],
			Health = new CharacterHealthMsg
			{
				BloodVolume = 1f,
				TotalBleedSpeed = 99f,
				BrainHealth = 50f,
				Consciousness = 40f,
			},
		});

		var stored = store.GetSavedCharacter(GuestId)!;
		Assert.Equal(80f, stored.Health!.BloodVolume, 3);
		Assert.Equal(0.5f, stored.Health.TotalBleedSpeed, 3);
		Assert.Equal(50f, stored.Health.BrainHealth, 3);
		Assert.Equal(12f, stored.Limbs[0].BleedAmount, 3);
		Assert.True(stored.Limbs[0].Broken);
	}

	[Fact]
	public void ADivergentReport_IsLoggedWithBothValues_AndTheHostValueStands()
	{
		var log = new RecordingLogger<CharacterDataStore>();
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<ILogger<CharacterDataStore>>(log))).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f));
		store.MarkHostModelAdvancing(GuestId, Environment.TickCount);

		Assert.True(store.SaveReportedCharacterData(GuestId, Snapshot(bloodVolume: 70f, bleed: 2f, totalBleedSpeed: 0.2f)));

		Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning
			&& entry.Message.Contains("divergence", StringComparison.Ordinal)
			&& entry.Message.Contains("blood volume", StringComparison.Ordinal)
			&& entry.Message.Contains("model 80.00", StringComparison.Ordinal)
			&& entry.Message.Contains("reported 70.00", StringComparison.Ordinal));
		Assert.Equal(80f, store.GetSavedCharacter(GuestId)!.Health!.BloodVolume, 3);
	}

	[Fact]
	public void ANonFiniteAuthorityWrite_IsRefusedAndThePreviousSnapshotStays()
	{
		var log = new RecordingLogger<CharacterDataStore>();
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<ILogger<CharacterDataStore>>(log))).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		var previous = Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f);
		store.SaveCharacterData(GuestId, previous);

		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: float.NaN, bleed: 5f, totalBleedSpeed: 0.5f));

		Assert.Same(previous, store.GetSavedCharacter(GuestId));
		Assert.True(log.HasError("non-finite"));
	}

	[Fact]
	public void ANonFiniteOwnerStateEvent_IsRefusedWhole()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		var previous = Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f);
		store.SaveCharacterData(GuestId, previous);

		Assert.False(store.ApplyLimbStateEvent(new LimbStateEventMsg
		{
			OwnerSteamId = GuestId,
			Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = float.NaN, SkinHealth = 30f }],
			Health = new CharacterHealthMsg { BloodVolume = 1f, TotalBleedSpeed = 1f },
		}));

		Assert.Same(previous, store.GetSavedCharacter(GuestId));
	}

	[Fact]
	public void AnOwnerStateEvent_CannotLowerTheHostLimbBleed()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f));

		// The event travels both ways for the limb bleed; the host holds the last written value, so a
		// LOWER value is not adopted (the model decays it on its own). Otherwise a member could state
		// the output downward and Preserve would keep it low.
		Assert.True(store.ApplyLimbStateEvent(new LimbStateEventMsg
		{
			OwnerSteamId = GuestId,
			Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = 1f, SkinHealth = 90f }],
		}));

		Assert.Equal(5f, store.GetSavedCharacter(GuestId)!.Limbs[0].BleedAmount, 3);
	}

	[Fact]
	public void ADismemberedOwnerStateEvent_ZeroesTheLimbBleed()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f));

		// The amputation is the one downward write: a dismembered limb carries no bleed.
		Assert.True(store.ApplyLimbStateEvent(new LimbStateEventMsg
		{
			OwnerSteamId = GuestId,
			Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = 9f, Dismembered = true }],
		}));

		Assert.Equal(0f, store.GetSavedCharacter(GuestId)!.Limbs[0].BleedAmount, 3);
	}

	[Fact]
	public void ANonFiniteEnemyEvent_IsRefusedWithoutTouchingTheSnapshot()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		var previous = Snapshot(bloodVolume: 80f, bleed: 5f, totalBleedSpeed: 0.5f);
		store.SaveCharacterData(GuestId, previous);

		store.ApplyEnemyBite(new EnemyBiteMsg
		{
			VictimSteamId = GuestId,
			Limb = new CharacterLimbMsg { Index = 0, BleedAmount = 40f },
			VenomTotal = float.NaN,
		});

		Assert.Same(previous, store.GetSavedCharacter(GuestId));
		Assert.Equal(0, previous.LocalRevision);
	}

	private static CharacterDataMsg Snapshot(float bloodVolume, float bleed, float totalBleedSpeed = 0f) => new()
	{
		Health = new CharacterHealthMsg { BloodVolume = bloodVolume, TotalBleedSpeed = totalBleedSpeed },
		Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = bleed, SkinHealth = 50f }],
	};
}
