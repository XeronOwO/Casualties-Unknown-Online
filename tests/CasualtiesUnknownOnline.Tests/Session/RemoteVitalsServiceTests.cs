using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The Online-UI remote-vitals cache: it projects the already-received
/// character-data stream (host reports, host snapshot, cross-guest relays) into
/// a read-only per-SteamID view and clears on session end. No protocol change:
/// the 1 Hz character snapshots already carry every field the UI needs.
/// </summary>
public class RemoteVitalsServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong OtherGuestId = 2002;
	private const ulong LobbyId = 9001;

	private static CharacterDataMsg Snapshot(
		ulong owner,
		float brainHealth = 70f,
		float hunger = 40f,
		float thirst = 50f,
		float stamina = 80f) => new()
		{
			OwnerSteamId = owner,
			Health = new CharacterHealthMsg
			{
				BrainHealth = brainHealth,
				Hunger = hunger,
				Thirst = thirst,
				Stamina = stamina,
				Alive = brainHealth > 0f,
				Conscious = brainHealth > 0f,
			},
		};

	[Fact]
	public void Host_CachesGuestReportBySender()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var vitals = host.Services.GetRequiredService<RemoteVitalsService>();

		host.Services.GetRequiredService<ICharacterDataControl>()
			.FireCharacterDataReceived(GuestId, Snapshot(0, brainHealth: 42f));

		Assert.True(vitals.TryGet(GuestId, out var snapshot));
		Assert.Equal(42f, snapshot.BrainHealth);
		Assert.Equal(40f, snapshot.Hunger);
		Assert.Equal(80f, snapshot.Stamina);
	}

	[Fact]
	public void Guest_CachesHostBroadcastByHostSteamId()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var vitals = guest.Services.GetRequiredService<RemoteVitalsService>();

			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(Snapshot(0, brainHealth: 88f));

			Assert.True(vitals.TryGet(HostId, out var snapshot));
			Assert.Equal(88f, snapshot.BrainHealth);
		}
	}

	[Fact]
	public void Guest_CachesCrossGuestRelayByOwnerSteamId()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var vitals = guest.Services.GetRequiredService<RemoteVitalsService>();

			// The host relays another guest's report; the transport sender is the
			// host, but the payload carries the actual owner.
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireCharacterDataReceived(HostId, Snapshot(OtherGuestId, brainHealth: 55f));

			Assert.True(vitals.TryGet(OtherGuestId, out var snapshot));
			Assert.Equal(55f, snapshot.BrainHealth);
			Assert.False(vitals.TryGet(HostId, out _));
		}
	}

	[Fact]
	public void Guest_IgnoresOwnRestoreOwnerZero()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var vitals = guest.Services.GetRequiredService<RemoteVitalsService>();

			// The host's reconnect restore of the LOCAL player arrives with
			// OwnerSteamId = 0; it is not a remote display target.
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireCharacterDataReceived(HostId, Snapshot(0, brainHealth: 99f));

			Assert.False(vitals.TryGet(HostId, out _));
			Assert.Equal(0, vitals.Count);
		}
	}

	[Fact]
	public void RemoteLeavingWorld_ClearsThatPlayersVitals()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var vitals = guest.Services.GetRequiredService<RemoteVitalsService>();
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(Snapshot(0, brainHealth: 88f));
			Assert.Equal(1, vitals.Count);

			((ISessionControl)guest.Session).FireRemoteSceneChanged(HostId, false);

			Assert.Equal(0, vitals.Count);
			Assert.False(vitals.TryGet(HostId, out _));
		}
	}

	[Fact]
	public void SessionEnd_ClearsTheCache()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var vitals = guest.Services.GetRequiredService<RemoteVitalsService>();
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(Snapshot(0, brainHealth: 88f));
			Assert.Equal(1, vitals.Count);

			((ISessionControl)guest.Session).EndSession();

			Assert.Equal(0, vitals.Count);
			Assert.False(vitals.TryGet(HostId, out _));
		}
	}

	[Fact]
	public void Snapshot_ProjectsOnlyNonNullHealth()
	{
		Assert.Null(RemoteVitalsSnapshot.From(null));

		var snapshot = RemoteVitalsSnapshot.From(new CharacterHealthMsg
		{
			BrainHealth = 64.4f,
			Hunger = -3.5f,
			Thirst = 120.6f,
			Stamina = 45.4f,
			Alive = true,
			Conscious = false,
		})!;

		Assert.Equal(64.4f, snapshot.BrainHealth);
		Assert.Equal(-3.5f, snapshot.Hunger);
		Assert.Equal(120.6f, snapshot.Thirst);
		Assert.Equal(45.4f, snapshot.Stamina);
		Assert.Equal("HP 64 H -4 T 121 St 45", snapshot.ToShortString());
	}
}
