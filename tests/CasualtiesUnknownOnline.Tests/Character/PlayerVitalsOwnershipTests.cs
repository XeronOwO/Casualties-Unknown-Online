using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
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
		store.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, bleed: 5f));

		// The ordinary 1 Hz report: fresh inputs, but the two owned values are the host's.
		store.SaveReportedCharacterData(GuestId, Snapshot(bloodVolume: 10f, bleed: 0f));

		var stored = store.GetSavedCharacter(GuestId)!;
		Assert.Equal(80f, stored.Health!.BloodVolume, 3);
		Assert.Equal(5f, stored.Limbs[0].BleedAmount, 3);
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

	private static CharacterDataMsg Snapshot(float bloodVolume, float bleed) => new()
	{
		Health = new CharacterHealthMsg { BloodVolume = bloodVolume },
		Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = bleed, SkinHealth = 50f }],
	};
}
