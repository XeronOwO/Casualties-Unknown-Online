using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Character-data disk persistence over the production composition root: the
/// same host file path across two full stacks models a host restart, the
/// session-end reset models a same-process lobby switch, and a new-run clear
/// must void both memory and disk.
/// </summary>
public class CharacterDataPersistenceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static string NewPath() =>
		Path.Combine(Path.GetTempPath(), "cuo-tests", "character-data", Guid.NewGuid().ToString("N"), "characters.bin");

	private static CharacterDataMsg Snapshot(ulong owner) => new()
	{
		OwnerSteamId = owner,
		Items = [new CharacterItemMsg { ItemId = "flashlight", Condition = 1f }],
		Position = new NetVector2Msg(12.5f, 34.75f),
	};

	[Fact]
	public void HostRestart_ReloadsSavedCharacterAndRestoresIt()
	{
		var path = NewPath();
		try
		{
			SaveAndDispose(path, store => store.SaveCharacterData(GuestId, Snapshot(GuestId)));

			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				var store = host.Services.GetRequiredService<CharacterDataStore>();
				var restored = store.GetSavedCharacter(GuestId);
				Assert.NotNull(restored);
				Assert.Equal("flashlight", restored!.Items[0].ItemId);

				// The reconnect surface must also work from the reloaded table.
				var received = new List<CharacterDataMsg>();
				guest.Services.GetRequiredService<CharacterDataStore>().CharacterDataReceived += (_, msg) => received.Add(msg);
				store.SendSavedCharacter(GuestId);
				Assert.True(received.Count == 1, $"the reloaded snapshot must reach the reconnecting guest, got {received.Count}");
				Assert.Equal(12.5f, received[0].Position!.X);
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void NewRunClear_DeletesTheDiskCopy()
	{
		var path = NewPath();
		try
		{
			SaveAndDispose(path, store =>
			{
				store.SaveCharacterData(GuestId, Snapshot(GuestId));
				store.ClearSavedCharacters(); // the host clicked start on a new run
			});

			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				Assert.Null(host.Services.GetRequiredService<CharacterDataStore>().GetSavedCharacter(GuestId));
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void SessionEnd_ClearsMemoryButKeepsTheDiskCopy()
	{
		var path = NewPath();
		try
		{
			SaveAndDispose(path, store =>
			{
				store.SaveCharacterData(GuestId, Snapshot(GuestId));
				store.ResetForSessionEnd();
				Assert.Null(store.GetSavedCharacter(GuestId)); // memory stays session-scoped
			});

			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				Assert.NotNull(host.Services.GetRequiredService<CharacterDataStore>().GetSavedCharacter(GuestId));
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void EnemyBiteMerge_PersistsAcrossRestart()
	{
		var path = NewPath();
		try
		{
			SaveAndDispose(path, store =>
			{
				store.SaveCharacterData(GuestId, Snapshot(GuestId));
				store.ApplyEnemyBite(new EnemyBiteMsg
				{
					VictimSteamId = GuestId,
					Limb = new CharacterLimbMsg { Index = 0, Pain = 12f, SkinHealth = 80f },
					VenomTotal = 3f,
					Adrenaline = 75f,
					Happiness = -0.75f,
				});
			});

			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				var restored = host.Services.GetRequiredService<CharacterDataStore>().GetSavedCharacter(GuestId)!;
				Assert.True(restored.Limbs.Count == 1, "the bitten limb must survive the restart");
				Assert.Equal(12f, restored.Limbs[0].Pain);
				Assert.Equal(3f, restored.Health!.VenomTotal);
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void EnemyLungeMerge_PersistsAcrossRestart()
	{
		var path = NewPath();
		try
		{
			SaveAndDispose(path, store =>
			{
				store.SaveCharacterData(GuestId, Snapshot(GuestId));
				store.ApplyEnemyLunge(new EnemyLungeMsg
				{
					VictimSteamId = GuestId,
					Limb = new CharacterLimbMsg { Index = 1, Pain = 22f, SkinHealth = 60f },
					Adrenaline = 70f,
					Stamina = 100f,
				});
			});

			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				var restored = host.Services.GetRequiredService<CharacterDataStore>().GetSavedCharacter(GuestId)!;
				Assert.Equal(22f, restored.Limbs[0].Pain);
				Assert.Equal(70f, restored.Health!.Adrenaline);
				Assert.Equal(100f, restored.Health.Stamina);
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void EnemyEffectMerge_PersistsAcrossRestart()
	{
		var path = NewPath();
		try
		{
			SaveAndDispose(path, store =>
			{
				store.SaveCharacterData(GuestId, Snapshot(GuestId));
				store.ApplyEnemyEffect(new EnemyEffectMsg
				{
					VictimSteamId = GuestId,
					Kind = EnemyEffectKind.GrabberGrabbed,
					Shock = 20f,
					EyePanicTime = 0.5f,
				});
			});

			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				var restored = host.Services.GetRequiredService<CharacterDataStore>().GetSavedCharacter(GuestId)!;
				Assert.Equal(20f, restored.Health!.Shock);
				Assert.Equal(0.5f, restored.Health.EyePanicTime);
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void CorruptFile_DegradesToEmptyAtStartup()
	{
		var path = NewPath();
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, [0xFF, 0x00, 0xAA]);

			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				Assert.Null(host.Services.GetRequiredService<CharacterDataStore>().GetSavedCharacter(GuestId));
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void FailedClear_DoesNotReloadAStaleFileInProcess()
	{
		var path = NewPath();
		try
		{
			// A directory at the file path makes both the empty-tombstone write
			// and Delete fail. The fresh run must stay empty in this process even
			// after a valid file appears at that path: there is deliberately no
			// lazy disk reload, so the old process cannot leak a previous run's
			// save into a brand-new lobby.
			Directory.CreateDirectory(path);
			var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
			using (host)
			using (guest)
			{
				var store = host.Services.GetRequiredService<CharacterDataStore>();
				store.SaveCharacterData(GuestId, Snapshot(GuestId)); // write fails — memory keeps the data
				store.ClearSavedCharacters(); // tombstone + delete both fail — memory stays clear

				Directory.Delete(path); // empty directory — non-recursive
				var validStore = new CharacterDataFileStore(path, NullLogger<CharacterDataFileStore>.Instance);
				Assert.True(validStore.Save(new Dictionary<ulong, CharacterDataMsg> { [GuestId] = Snapshot(GuestId) }),
					"the stale valid file setup must succeed");

				Assert.Null(store.GetSavedCharacter(GuestId));
			}
		}
		finally
		{
			DeletePath(path);
		}
	}

	private static void SaveAndDispose(string path, Action<CharacterDataStore> mutate)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, characterDataFile: path);
		using (host)
		using (guest)
		{
			mutate(host.Services.GetRequiredService<CharacterDataStore>());
		}
	}

	private static void DeletePath(string path)
	{
		DeleteFileIfExists(path);
		DeleteFileIfExists(path + ".tmp");
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
		{
			Directory.Delete(directory); // only after the two known files are gone — never recursive
		}
	}

	private static void DeleteFileIfExists(string path)
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}
}
