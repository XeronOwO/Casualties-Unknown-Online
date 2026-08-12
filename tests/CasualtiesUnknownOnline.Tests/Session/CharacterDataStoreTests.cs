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
}
