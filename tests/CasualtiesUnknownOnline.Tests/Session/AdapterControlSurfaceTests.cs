using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The adapter-facing control-surface contract: the Runtime's concrete domain
/// services must be consumable through the same narrow interfaces the Game
/// Adapter now composes against. This is the L0 proof for the "concrete service
/// dependency" slice of the GameAdapter testability work — the adapter's deep
/// modules no longer need to reference SessionService/WorldService/ItemService/
/// EntitySyncService/CharacterDataStore/PlayerInteractionService by type.
/// </summary>
public class AdapterControlSurfaceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void RuntimeContainer_ResolvesAllAdapterControlSurfaces()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		using (host)
		using (guest)
		{
			Assert.NotNull(host.Services.GetRequiredService<ISessionControl>());
			Assert.NotNull(host.Services.GetRequiredService<IWorldControl>());
			Assert.NotNull(host.Services.GetRequiredService<IItemControl>());
			Assert.NotNull(host.Services.GetRequiredService<IEntitySyncControl>());
			Assert.NotNull(host.Services.GetRequiredService<ICharacterDataControl>());
			Assert.NotNull(host.Services.GetRequiredService<IPlayerInteractionControl>());
		}
	}

	[Fact]
	public void SessionControl_SurfacesAdapterSceneReadPath()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		using (host)
		using (guest)
		{
			host.Steam.FireLobbyCreated(LobbyId);
			host.Steam.LobbyMembers = [HostId, GuestId];
			guest.Steam.FireLobbyEntered(LobbyId);

			var session = host.Services.GetRequiredService<ISessionControl>();
			session.ReportSceneState(SceneStateType.InWorld, "level1", new NetVector2(3f, 4f));

			Assert.True(session.LocalInWorld);
			Assert.True(guest.Session.IsRemoteInWorld(HostId));
			Assert.Equal(new NetVector2(3f, 4f), guest.Session.GetRemoteSpawnPos(HostId));
		}
	}

	[Fact]
	public void WorldControl_SurfacesAdapterWorldJoinAndParams()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		using (host)
		using (guest)
		{
			host.Steam.FireLobbyCreated(LobbyId);
			host.Steam.LobbyMembers = [HostId, GuestId];
			guest.Steam.FireLobbyEntered(LobbyId);

			var world = host.Services.GetRequiredService<IWorldControl>();
			var parameters = new WorldStartParams
			{
				RandomState = [1, 2, 3],
				BiomeOverride = 1,
			};

			world.PublishWorldParams(parameters);
			world.SendWorldJoin(isTutorial: true);
			world.SendWorldJoinTo(GuestId);

			Assert.Same(parameters, world.WorldParams);
		}
	}

	[Fact]
	public void ItemAndEntityControls_SurfaceAdapterStatePaths()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		using (host)
		using (guest)
		{
			var items = host.Services.GetRequiredService<IItemControl>();
			items.LayerModifierRandomState = [9, 8, 7];
			Assert.Equal([9, 8, 7], items.LayerModifierRandomState);

			var entities = host.Services.GetRequiredService<IEntitySyncControl>();
			Assert.Empty(entities.RemotePlayers);
			Assert.Null(entities.GetRemotePlayer(GuestId));
			entities.MarkLocalAttackSwing();
			entities.PublishLocalState(
				new NetVector2(1f, 2f),
				new NetVector2(3f, 4f),
				new NetVector2(0f, 0f),
				isRight: true,
				standing: true,
				alive: true,
				conscious: true,
				crouching: false);
		}
	}

	[Fact]
	public void CharacterDataControl_SurfacesAdapterSaveAndEventPaths()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		using (host)
		using (guest)
		{
			var characters = host.Services.GetRequiredService<ICharacterDataControl>();
			characters.ClearSavedCharacters();

			var received = 0;
			characters.CharacterDataReceived += (_, _) => received++;
			characters.FireCharacterDataReceived(GuestId, new Runtime.Protocol.Messages.CharacterDataMsg());
			Assert.Equal(1, received);
		}
	}
}
