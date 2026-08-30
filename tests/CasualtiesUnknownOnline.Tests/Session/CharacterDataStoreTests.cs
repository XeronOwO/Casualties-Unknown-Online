using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The character-data save lifecycle (a745fc3 — "started paradise, got the
/// previous run's emergency light"): the saved data belongs to the run that
/// produced it. A SAME-RUN re-entry (death → menu → re-enter) finds the save
/// and restores normally; a NEW run (the host clicked start) clears the table
/// so the fresh run's starting supplies survive. The restore travels the wire
/// (the reconnecting guest receives the saved snapshot).
/// </summary>
public class CharacterDataStoreTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static CharacterDataMsg Snapshot(ulong owner) => new()
	{
		OwnerSteamId = owner,
		Items = [new CharacterItemMsg { ItemId = "flashlight", Condition = 1f }],
		Position = new NetVector2Msg(12.5f, 34.75f),
	};

	/// <summary>The restore is only legal while the host has a live world — a menu handshake must not stage a previous run's save.</summary>
	private static void MarkHostInWorld(TestNode host) =>
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	[Fact]
	public void SavedData_SurvivesReentry_SameRunRestores()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();

		store.SaveCharacterData(GuestId, Snapshot(GuestId));

		Assert.NotNull(store.GetSavedCharacter(GuestId));
		Assert.Equal("flashlight", store.GetSavedCharacter(GuestId)!.Items[0].ItemId);
	}

	[Fact]
	public void ClearSavedCharacters_NewRunStartsFresh()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();

		store.SaveCharacterData(GuestId, Snapshot(GuestId));
		store.ClearSavedCharacters(); // the host clicked start on a new run

		Assert.Null(store.GetSavedCharacter(GuestId));
	}

	[Fact]
	public void SendSavedCharacter_ReachesTheReconnectingGuest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			// The guest's reconnect surface: it receives the saved snapshot back.
			var received = new List<CharacterDataMsg>();
			guest.Services.GetRequiredService<CharacterDataStore>().CharacterDataReceived += (_, msg) => received.Add(msg);

			host.Services.GetRequiredService<CharacterDataStore>().SaveCharacterData(GuestId, Snapshot(GuestId));
			MarkHostInWorld(host);
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			Assert.True(received.Count == 1, $"the saved snapshot must reach the reconnecting guest, got {received.Count}");
			Assert.Equal("flashlight", received[0].Items[0].ItemId);
		}
	}

	[Fact]
	public void SendSavedCharacter_WhileHostIsInMenu_DoesNotSend()
	{
		// A menu handshake must never stage a previous run's save for the next
		// run: the host clears the save only when it clicks "new run", which
		// happens AFTER a guest may have already handshaken in the menu.
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var received = new List<CharacterDataMsg>();
			guest.Services.GetRequiredService<CharacterDataStore>().CharacterDataReceived += (_, msg) => received.Add(msg);

			host.Services.GetRequiredService<CharacterDataStore>().SaveCharacterData(GuestId, Snapshot(GuestId));
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			Assert.True(received.Count == 0, $"a menu restore must not be sent, got {received.Count}");
		}
	}

	[Fact]
	public void SendSavedCharacter_PositionSurvivesTheSaveRoundTrip()
	{
		// The reconnect restore returns the character to its LEAVE spot — the
		// position must survive the save table untouched (the host's table is
		// the only copy between the disconnect and the reconnect).
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var received = new List<CharacterDataMsg>();
			guest.Services.GetRequiredService<CharacterDataStore>().CharacterDataReceived += (_, msg) => received.Add(msg);

			host.Services.GetRequiredService<CharacterDataStore>().SaveCharacterData(GuestId, Snapshot(GuestId));
			MarkHostInWorld(host);
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			Assert.True(received.Count == 1, $"the saved snapshot must reach the reconnecting guest, got {received.Count}");
			var position = received[0].Position;
			Assert.NotNull(position);
			Assert.Equal(12.5f, position.X);
			Assert.Equal(34.75f, position.Y);
		}
	}

	[Fact]
	public void SendSavedCharacter_NullPositionPassesThrough()
	{
		// An old sender (pre-Position protocol) saved no position — the restore
		// claims none and the game spawns at the fresh world's landing spot.
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var received = new List<CharacterDataMsg>();
			guest.Services.GetRequiredService<CharacterDataStore>().CharacterDataReceived += (_, msg) => received.Add(msg);

			var snapshot = Snapshot(GuestId);
			snapshot.Position = null;
			host.Services.GetRequiredService<CharacterDataStore>().SaveCharacterData(GuestId, snapshot);
			MarkHostInWorld(host);
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			Assert.True(received.Count == 1, $"the saved snapshot must reach the reconnecting guest, got {received.Count}");
			Assert.Null(received[0].Position);
		}
	}

	[Fact]
	public void ApplyEnemyBite_MergesTheTerminalStateIntoTheSavedSnapshot()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(GuestId));

		store.ApplyEnemyBite(new EnemyBiteMsg
		{
			VictimSteamId = GuestId,
			Limb = new CharacterLimbMsg { Index = 0, Pain = 12f, SkinHealth = 80f },
			VenomTotal = 3f,
			Adrenaline = 75f,
			Happiness = -0.75f,
		});

		var saved = store.GetSavedCharacter(GuestId)!;
		Assert.True(saved.Limbs.Count == 1, "the event must add the bitten limb to the saved snapshot");
		Assert.Equal(12f, saved.Limbs[0].Pain);
		Assert.Equal(3f, saved.Health!.VenomTotal);
		Assert.Equal(-0.75f, saved.Health!.Happiness);
	}

	[Fact]
	public void ApplyEnemyEffect_MergesOnlyTheKindFieldsIntoTheSavedSnapshot()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, Snapshot(GuestId));

		store.ApplyEnemyEffect(new EnemyEffectMsg
		{
			VictimSteamId = GuestId,
			Kind = EnemyEffectKind.GrabberGrabbed,
			Shock = 20f,
			EyePanicTime = 0.5f,
		});

		var saved = store.GetSavedCharacter(GuestId)!;
		Assert.Equal(20f, saved.Health!.Shock);
		Assert.Equal(0.5f, saved.Health!.EyePanicTime);
		Assert.Equal(0f, saved.Health!.SepticShock); // untouched — this kind carries no septic state
	}

	[Fact]
	public void SendSavedCharacter_ProjectsKernelTerminalFactsOverSnapshot()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var received = new List<CharacterDataMsg>();
			guest.Services.GetRequiredService<CharacterDataStore>().CharacterDataReceived += (_, msg) => received.Add(msg);

			var store = host.Services.GetRequiredService<CharacterDataStore>();
			var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

			var snapshot = Snapshot(GuestId);
			snapshot.Health = new CharacterHealthMsg
			{
				Alive = true,
				Conscious = true,
				BrainHealth = 80f,
				Disfigured = false,
				EyeGone = false,
				BothEyesGone = false,
				HasPulmonaryEmbolism = false,
				TriedRollingLastStand = false,
				SuccesfullyRolledLastStand = false,
				UsedNeuralBooster = false,
				FibrillationForced = false,
				MindwipeScriptPresent = false,
				MindwipeScriptActive = false,
			};
			snapshot.Limbs.Add(new CharacterLimbMsg
			{
				Index = 0,
				SkinHealth = 55f,
				MuscleHealth = 45f,
				Broken = false,
				IsHead = true,
			});
			store.SaveCharacterData(GuestId, snapshot);

			var body = new PlayerBodyTerminalState(
				Disfigured: true,
				EyeGone: true,
				BothEyesGone: true,
				HasPulmonaryEmbolism: true,
				TriedRollingLastStand: true,
				SuccesfullyRolledLastStand: true,
				UsedNeuralBooster: true,
				FibrillationForced: true,
				MindwipeScriptPresent: true,
				MindwipeScriptActive: true);
			Assert.True(authority.TryUpdatePlayerStatus(
				HostId,
				new PlayerState(GuestId, false, false, Limbs:
				[
					new PlayerLimbState(0, false, true, true, true, true, true, true, false),
				], Body: body),
				out _,
				out var rejection), rejection?.Message);

			MarkHostInWorld(host);
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			var restored = Assert.Single(received);
			Assert.False(restored.Health!.Alive);
			Assert.False(restored.Health.Conscious);
			Assert.True(restored.Health.Disfigured);
			Assert.True(restored.Health.EyeGone);
			Assert.True(restored.Health.BothEyesGone);
			Assert.True(restored.Health.HasPulmonaryEmbolism);
			Assert.True(restored.Health.TriedRollingLastStand);
			Assert.True(restored.Health.SuccesfullyRolledLastStand);
			Assert.True(restored.Health.UsedNeuralBooster);
			Assert.True(restored.Health.FibrillationForced);
			Assert.True(restored.Health.MindwipeScriptPresent);
			Assert.True(restored.Health.MindwipeScriptActive);

			var limb = Assert.Single(restored.Limbs);
			Assert.Equal(55f, limb.SkinHealth);
			Assert.Equal(45f, limb.MuscleHealth);
			Assert.False(limb.Broken);
			Assert.True(limb.Dismembered);
			Assert.True(limb.Dislocated);
			Assert.True(limb.Splinted);
			Assert.True(limb.Infected);
			Assert.True(limb.BlockedBleeding);
			Assert.True(limb.IsHead);
			Assert.False(limb.IsVital);
		}
	}

	[Fact]
	public void SendSavedCharacter_AddsKernelLimbFactMissingFromSnapshot()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var received = new List<CharacterDataMsg>();
			guest.Services.GetRequiredService<CharacterDataStore>().CharacterDataReceived += (_, msg) => received.Add(msg);

			var store = host.Services.GetRequiredService<CharacterDataStore>();
			var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

			var snapshot = Snapshot(GuestId);
			snapshot.Health = new CharacterHealthMsg { Alive = true, Conscious = true };
			store.SaveCharacterData(GuestId, snapshot);

			Assert.True(authority.TryUpdatePlayerStatus(
				HostId,
				new PlayerState(GuestId, true, true, Limbs:
				[
					new PlayerLimbState(2, false, true, false, true, false, false, false, true),
				]),
				out _,
				out var rejection), rejection?.Message);

			MarkHostInWorld(host);
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			var restored = Assert.Single(received);
			var limb = Assert.Single(restored.Limbs, l => l.Index == 2);
			Assert.True(limb.Dismembered);
			Assert.True(limb.Splinted);
			Assert.True(limb.IsVital);
			Assert.Equal(0f, limb.SkinHealth);
		}
	}
}
