using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
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
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			Assert.True(received.Count == 1, $"the saved snapshot must reach the reconnecting guest, got {received.Count}");
			Assert.Equal("flashlight", received[0].Items[0].ItemId);
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
			host.Services.GetRequiredService<ICharacterDataControl>().SendSavedCharacter(GuestId);

			Assert.True(received.Count == 1, $"the saved snapshot must reach the reconnecting guest, got {received.Count}");
			Assert.Null(received[0].Position);
		}
	}
}
